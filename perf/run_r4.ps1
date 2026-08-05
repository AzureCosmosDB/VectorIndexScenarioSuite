<#
  run_r4.ps1 - Round-4 "index-AFTER-ingest" vector-index benchmark orchestrator.

  Same matrix/params as r3 (3 Product + 3 Spherical per account, 12 total;
  1 partition @ 10,000 RU; 1,000,000 wiki-cohere vectors; 500 queries + 100 warmup;
  Product byteSize=192; Spherical service default) EXCEPT the index timing:

    r3: container created WITH the DiskANN vector index -> index builds incrementally
        DURING ingest.
    r4: container created WITHOUT any vector index (embedding policy only, which is
        immutable/required) -> ingest all 1M docs -> THEN add the DiskANN vector index
        via ReplaceContainer -> the graph builds as a FULL build over existing data.

  Phases: create | ingest | addindex | query | report | all
    create   : idxctl create-noindex @400 -> az scale 10000 -> pcheck feedRanges==1.
    ingest   : suite ingest 1M (all 12 concurrent). catchupTimeoutMinutes=1 (no index yet;
               the suite's catch-up probe can't engage, so keep it short).
    addindex : idxctl add-index (ReplaceContainer, Product->192 / Spherical->default),
               then WAIT for the DiskANN graph to build over existing data by polling a
               tiny suite query until diskAnnUsed==true (records post-ingest build wall-clock).
    query    : suite query 500 + 100 warmup + recall + latency/RU (serial per account,
               accounts parallel) -- identical to r3.
    report   : parse RESULT_JSON -> CSV + txt.

  Smoke-tested end-to-end (see findings id 43): the backend DOES allow adding a DiskANN
  vector index via ReplaceContainer on a populated container.
#>
param(
  [ValidateSet("all","create","ingest","addindex","query","report")]
  [string]$Phase = "all",
  [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
  [Parameter(Mandatory = $true)]
  [string]$AccountConfigPath,
  [int[]]$Runs = @(1,2,3),
  [string[]]$Quantizers = @("product","spherical"),
  [string[]]$Accounts = @("account1","account2"),
  [int]$SliceCount = 1000000,
  [int]$NumQueries = 500,
  [int]$NumWarmupQueries = 100,
  [int]$ProductByteSize = 192,
  [int]$BuildWaitMaxMinutes = 180
)

$ErrorActionPreference = "Stop"
$root   = (Resolve-Path $RepoRoot).Path
$exe    = "$root\bin\Release\net8.0\win-x64\VectorIndexScenarioSuite.exe"
$pcheck = "$root\perf\pcheck\bin\Release\net8.0\pcheck.dll"
$idxctl = "$root\perf\idxctl\bin\Release\net8.0\idxctl.dll"
$db     = "vector-benchmarking"
$outDir = "$root\perf\r4"
$logDir = "$outDir\logs"
$track  = "$outDir\run_tracking_r4.txt"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

if (-not (Test-Path $AccountConfigPath)) {
  throw "Account config not found: $AccountConfigPath"
}

$AccountCfg = @{}
$accountConfig = Get-Content $AccountConfigPath -Raw | ConvertFrom-Json
foreach ($property in $accountConfig.PSObject.Properties) {
  $AccountCfg[$property.Name] = @{
    endpoint = $property.Value.endpoint
    account  = $property.Value.account
    rg       = $property.Value.resourceGroup
  }
}
foreach ($accountAlias in $Accounts) {
  if (-not $AccountCfg.ContainsKey($accountAlias)) {
    throw "Account alias '$accountAlias' is missing from $AccountConfigPath"
  }
}

function Log($msg) {
  $line = "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $msg
  Write-Host $line
  Add-Content -Path $track -Value $line
}

function Container-Name($acct,$q,$n) { "r4-$acct-$q-run$n" }

function Get-Matrix {
  $items = @()
  foreach ($acct in $Accounts) {
    foreach ($q in $Quantizers) {
      foreach ($n in $Runs) {
        $items += [pscustomobject]@{
          Account = $acct; Quantizer = $q; Run = $n
          Container = (Container-Name $acct $q $n)
          ByteSize  = ($(if ($q -eq "product") { "$ProductByteSize" } else { "" }))
          Endpoint  = $AccountCfg[$acct].endpoint
          AccountName = $AccountCfg[$acct].account
          Rg        = $AccountCfg[$acct].rg
        }
      }
    }
  }
  return $items
}

# ---------------- CREATE (no vector index) + scale + verify ----------------
function Do-Create {
  Log "=== CREATE PHASE (no vector index) ==="
  foreach ($acct in $Accounts) {
    $c = $AccountCfg[$acct]
    Log "Ensuring DB '$db' on $acct ..."
    az cosmosdb sql database create --account-name $c.account --resource-group $c.rg --name $db -o none 2>$null
  }
  foreach ($it in (Get-Matrix)) {
    $cn = $it.Container
    # Clean start: delete any pre-existing container (idxctl create-noindex is create-if-not-exists).
    Log "DELETE (if exists) $cn"
    az cosmosdb sql container delete --account-name $it.AccountName --resource-group $it.Rg --database-name $db --name $cn --yes -o none 2>$null

    Log "CREATE-NOINDEX $cn (acct=$($it.Account) q=$($it.Quantizer))"
    dotnet $idxctl create-noindex $it.Endpoint $db $cn 400 2>&1 | ForEach-Object { if ($_ -match "IDXCTL_JSON") { Log "  $_" } }

    Log "SCALE $cn -> 10000 (control-plane)"
    az cosmosdb sql container throughput update --account-name $it.AccountName --resource-group $it.Rg --database-name $db --name $cn --throughput 10000 -o none 2>$null

    Start-Sleep 3
    $pj = (dotnet $pcheck $it.Endpoint $db $cn 2>&1 | Select-String "PCHECK_JSON").ToString()
    $tp = (az cosmosdb sql container throughput show --account-name $it.AccountName --resource-group $it.Rg --database-name $db --name $cn --query "resource.throughput" -o tsv 2>$null)
    if ($pj) {
      $fr = ([regex]'"feedRanges":(\d+)').Match($pj).Groups[1].Value
      Log "VERIFY $cn feedRanges=$fr throughput=$tp $(if($fr -ne '1'){'*** MISMATCH: not 1 partition (ingesting anyway) ***'})"
    } else {
      Log "VERIFY $cn pcheck FAILED (throughput=$tp) - ingesting anyway"
    }
  }
  Log "=== CREATE PHASE DONE ==="
}

# ---------------- INGEST (all containers concurrent, no index present) ----------------
function Do-Ingest {
  Log "=== INGEST PHASE (all containers concurrent, no index) ==="
  $jobs = @()
  foreach ($it in (Get-Matrix)) {
    $cn = $it.Container
    Log "INGEST START $cn"
    $jobs += Start-Job -Name "ingest-$cn" -ScriptBlock {
      param($it,$exe,$db,$logDir,$track,$SliceCount)
      function JLog($m,$track){ $l="[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date),$m; Add-Content -Path $track -Value $l }
      $cn = $it.Container
      $args = @(
        "--AppSettings:accountEndpoint=$($it.Endpoint)",
        "--AppSettings:useAADAuth=true",
        "--AppSettings:cosmosDatabaseId=$db",
        "--AppSettings:cosmosContainerId=$cn",
        "--AppSettings:deleteContainerOnStart=false",
        "--AppSettings:cosmosContainerRUInitial=0",
        "--AppSettings:cosmosContainerRUFinal=0",
        "--AppSettings:scenario:sliceCount=$SliceCount",
        "--AppSettings:scenario:runIngestion=true",
        "--AppSettings:scenario:runQuery=false",
        "--AppSettings:scenario:startVectorId=0",
        "--AppSettings:scenario:endVectorId=0",
        "--AppSettings:scenario:quantizerType=$($it.Quantizer)",
        "--AppSettings:scenario:quantizationByteSize=$($it.ByteSize)",
        "--AppSettings:scenario:catchupTimeoutMinutes=1"
      )
      "" | & $exe @args *>&1 | Tee-Object -FilePath "$logDir\ingest-$cn.log" | Out-Null
      $perf = Select-String -Path "$logDir\ingest-$cn.log" -Pattern "\[PERF\]|Ingestion of" | ForEach-Object { $_.Line }
      JLog "INGEST DONE $cn :: $($perf -join ' | ')" $track
    } -ArgumentList $it,$exe,$db,$logDir,$track,$SliceCount
  }
  Log "All $($jobs.Count) ingest jobs started. Waiting..."
  $jobs | Wait-Job | Out-Null
  $jobs | Receive-Job
  $jobs | Remove-Job
  Log "=== INGEST PHASE DONE ==="
}

# ---------------- ADD-INDEX (post-ingest) + wait for full graph build ----------------
function Do-AddIndex {
  Log "=== ADDINDEX PHASE (add DiskANN via ReplaceContainer, wait for build) ==="
  $jobs = @()
  foreach ($it in (Get-Matrix)) {
    $cn = $it.Container
    Log "ADDINDEX START $cn (q=$($it.Quantizer) byteSize='$($it.ByteSize)')"
    $jobs += Start-Job -Name "addidx-$cn" -ScriptBlock {
      param($it,$exe,$idxctl,$db,$logDir,$track,$SliceCount,$BuildWaitMaxMinutes)
      function JLog($m,$track){ $l="[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date),$m; Add-Content -Path $track -Value $l }
      $cn = $it.Container
      $t0 = Get-Date
      # 1) Add the DiskANN vector index over existing data.
      $addOut = dotnet $idxctl add-index $it.Endpoint $db $cn $it.Quantizer $it.ByteSize 2>&1
      $addOut | Set-Content "$logDir\addindex-$cn.log"
      $addJson = ($addOut | Select-String "IDXCTL_JSON").ToString()
      JLog "ADDINDEX REPLACED $cn :: $addJson" $track

      # 2) Poll a tiny suite query until DiskANN engages (graph built over existing docs).
      $deadline = $t0.AddMinutes($BuildWaitMaxMinutes)
      $engaged = $false
      while ((Get-Date) -lt $deadline) {
        Start-Sleep 60
        $qargs = @(
          "--AppSettings:accountEndpoint=$($it.Endpoint)","--AppSettings:useAADAuth=true",
          "--AppSettings:cosmosDatabaseId=$db","--AppSettings:cosmosContainerId=$cn",
          "--AppSettings:deleteContainerOnStart=false",
          "--AppSettings:cosmosContainerRUInitial=0","--AppSettings:cosmosContainerRUFinal=0",
          "--AppSettings:scenario:sliceCount=$SliceCount",
          "--AppSettings:scenario:runIngestion=false","--AppSettings:scenario:runQuery=true",
          "--AppSettings:scenario:numQueries=3",
          "--AppSettings:scenario:warmup:enabled=false",
          "--AppSettings:scenario:computeRecall=false","--AppSettings:scenario:computeLatencyAndRUStats=true",
          "--AppSettings:scenario:quantizerType=$($it.Quantizer)","--AppSettings:scenario:quantizationByteSize=$($it.ByteSize)",
          "--AppSettings:scenario:datasetLabel=$cn-probe"
        )
        "" | & $exe @qargs *>&1 | Tee-Object -FilePath "$logDir\addindex-probe-$cn.log" | Out-Null
        $rj = Select-String -Path "$logDir\addindex-probe-$cn.log" -Pattern "RESULT_JSON:" | Select-Object -Last 1 | ForEach-Object { $_.Line }
        $used = $false
        if ($rj -match '"diskAnnUsed":(true|false)') { $used = ($Matches[1] -eq "true") }
        $elapsed = [int]((Get-Date) - $t0).TotalSeconds
        JLog "ADDINDEX PROBE $cn t=${elapsed}s diskAnnUsed=$used" $track
        if ($used) { $engaged = $true; JLog "ADDINDEX BUILT $cn :: buildWaitSec=$elapsed" $track; break }
      }
      if (-not $engaged) { JLog "ADDINDEX TIMEOUT $cn :: DiskANN not engaged within $BuildWaitMaxMinutes min" $track }
    } -ArgumentList $it,$exe,$idxctl,$db,$logDir,$track,$SliceCount,$BuildWaitMaxMinutes
  }
  Log "All $($jobs.Count) addindex jobs started. Waiting..."
  $jobs | Wait-Job | Out-Null
  $jobs | Receive-Job
  $jobs | Remove-Job
  Log "=== ADDINDEX PHASE DONE ==="
}

# ---------------- QUERY (serial within account, accounts parallel) ----------------
function Do-Query {
  Log "=== QUERY PHASE (serial within account, accounts parallel) ==="
  $jobs = @()
  foreach ($acct in $Accounts) {
    $items = (Get-Matrix) | Where-Object { $_.Account -eq $acct }
    $jobs += Start-Job -Name "query-$acct" -ScriptBlock {
      param($items,$exe,$db,$logDir,$track,$SliceCount,$NumQueries,$NumWarmupQueries)
      function JLog($m,$track){ $l="[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date),$m; Add-Content -Path $track -Value $l; Write-Host $l }
      foreach ($it in $items) {
        $cn = $it.Container
        JLog "QUERY START $cn" $track
        $args = @(
          "--AppSettings:accountEndpoint=$($it.Endpoint)",
          "--AppSettings:useAADAuth=true",
          "--AppSettings:cosmosDatabaseId=$db",
          "--AppSettings:cosmosContainerId=$cn",
          "--AppSettings:deleteContainerOnStart=false",
          "--AppSettings:cosmosContainerRUInitial=0",
          "--AppSettings:cosmosContainerRUFinal=0",
          "--AppSettings:scenario:sliceCount=$SliceCount",
          "--AppSettings:scenario:runIngestion=false",
          "--AppSettings:scenario:runQuery=true",
          "--AppSettings:scenario:numQueries=$NumQueries",
          "--AppSettings:scenario:warmup:enabled=true",
          "--AppSettings:scenario:warmup:numWarmupQueries=$NumWarmupQueries",
          "--AppSettings:scenario:computeRecall=true",
          "--AppSettings:scenario:computeLatencyAndRUStats=true",
          "--AppSettings:scenario:quantizerType=$($it.Quantizer)",
          "--AppSettings:scenario:quantizationByteSize=$($it.ByteSize)",
          "--AppSettings:scenario:datasetLabel=$cn"
        )
        "" | & $exe @args *>&1 | Tee-Object -FilePath "$logDir\query-$cn.log" | Out-Null
        $rj = Select-String -Path "$logDir\query-$cn.log" -Pattern "RESULT_JSON:" | Select-Object -Last 1 | ForEach-Object { $_.Line }
        $qc = Select-String -Path "$logDir\query-$cn.log" -Pattern "QUANTIZER-CHECK" | ForEach-Object { $_.Line }
        JLog "QUERY DONE $cn :: $rj" $track
        if ($qc) { JLog "QUERY QCHECK $cn :: $($qc -join ' || ')" $track }
      }
    } -ArgumentList $items,$exe,$db,$logDir,$track,$SliceCount,$NumQueries,$NumWarmupQueries
  }
  Log "Query jobs started: $($jobs.Name -join ', '). Waiting..."
  $jobs | Wait-Job | Out-Null
  $jobs | Receive-Job
  $jobs | Remove-Job
  Log "=== QUERY PHASE DONE ==="
}

# ---------------- REPORT ----------------
function Do-Report {
  Log "=== REPORT ==="
  $rows = @()
  foreach ($it in (Get-Matrix)) {
    $cn = $it.Container
    $lp = "$logDir\query-$cn.log"
    if (-not (Test-Path $lp)) { continue }
    $rj = Select-String -Path $lp -Pattern "RESULT_JSON:" | Select-Object -Last 1 | ForEach-Object { $_.Line -replace '^.*RESULT_JSON:\s*','' }
    if ($rj) {
      try { $rows += ($rj | ConvertFrom-Json) } catch { Log "REPORT parse failed for $cn" }
    }
  }
  $reportPath = "$outDir\report_r4.csv"
  $rows | Select-Object container, quantizerType, effectiveQuantizerType, ingestedVectors,
      @{n='recall10';e={$_.recall.'10'}}, avgQueryServerLatencyMs, p50QueryServerLatencyMs,
      p99QueryServerLatencyMs, avgQueryClientLatencyMs, avgQueryRU, diskAnnUsed |
    Sort-Object container | Format-Table -AutoSize | Out-String -Width 400 | Tee-Object -FilePath "$outDir\report_r4.txt"
  $rows | Export-Csv -Path $reportPath -NoTypeInformation
  Log "Report written: $reportPath"
}

Log "############ run_r4 START phase=$Phase accounts=$($Accounts -join ',') quantizers=$($Quantizers -join ',') runs=$($Runs -join ',') ############"
switch ($Phase) {
  "create"   { Do-Create }
  "ingest"   { Do-Ingest }
  "addindex" { Do-AddIndex }
  "query"    { Do-Query }
  "report"   { Do-Report }
  "all"      { Do-Create; Do-Ingest; Do-AddIndex; Do-Query; Do-Report }
}
Log "############ run_r4 END phase=$Phase ############"
