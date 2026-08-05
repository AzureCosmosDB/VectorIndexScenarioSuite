<#
  run_r3.ps1 - Round-3 vector-index benchmark orchestrator.

  Goal: 3 Product + 3 Spherical collections per account (12 total for two accounts).
    - Product collections: explicit quantizationByteSize=192.
    - Spherical collections: no byteSize override (feature-gated, service default).
    - Exactly 1 physical partition @ 10,000 RU. Achieved with the PROVEN recipe:
        create @400 RU (suite, data-plane) -> scale to 10000 (control-plane az) -> 1 partition.
    - Verify topology BEFORE ingesting (pcheck feedRanges==1). On mismatch: ingest anyway,
      but record actual topology (user: ingest_anyway).
    - 1,000,000 wiki-cohere vectors + 500 measured queries with warmup.

  Methodology (per user's answers):
    - Both configured accounts run concurrently.
    - queryModel = concurrent_ingest_serial_query : ingest may overlap, but queries run
      one container at a time per account for clean latency (concurrent queries were shown
      to inflate latency ~20x on the same account). We ingest SERIALLY within an account
      (2 concurrent ingests total, one per account) for reliability, and query SERIALLY
      within an account, with the two accounts proceeding in parallel.
    - mismatchHandling = ingest_anyway.

  Throughput mechanics (important):
    - Suite create uses cosmosContainerRUInitial for CreateContainerIfNotExists.
    - WikiCohere ctor unconditionally calls ReplaceFinalThroughput(10000) (data-plane,
      async void). To keep BOTH accounts on the identical proven recipe we pass RUFinal=400
      during CREATE so that call no-ops. Real scaling is done via control-plane az afterwards.

  Phases: create | ingest | query | report | all
#>
param(
  [ValidateSet("all","create","ingest","query","report")]
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
  [int]$ProductByteSize = 192
)

$ErrorActionPreference = "Stop"
$root   = (Resolve-Path $RepoRoot).Path
$exe    = "$root\bin\Release\net8.0\win-x64\VectorIndexScenarioSuite.exe"
$pcheck = "$root\perf\pcheck\bin\Release\net8.0\pcheck.dll"
$db     = "vector-benchmarking"
$outDir = "$root\perf\r3"
$logDir = "$outDir\logs"
$track  = "$outDir\run_tracking_r3.txt"
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

function Container-Name($acct,$q,$n) { "r3-$acct-$q-run$n" }

# Build the full matrix of work items.
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

# Invoke the suite exe with config overrides; pipe empty stdin (unconditional Console.ReadLine).
function Invoke-Suite {
  param([hashtable]$Cfg, [string]$LogPath)
  $args = @()
  foreach ($k in $Cfg.Keys) { $args += "--$k=$($Cfg[$k])" }
  "" | & $exe @args *>&1 | Tee-Object -FilePath $LogPath | Out-Null
  return Get-Content $LogPath
}

# ---------------- CREATE (+ scale + verify) ----------------
function Do-Create {
  Log "=== CREATE PHASE ==="
  # Ensure DB exists on each account (control-plane; idempotent).
  foreach ($acct in $Accounts) {
    $c = $AccountCfg[$acct]
    Log "Ensuring DB '$db' on $acct ..."
    az cosmosdb sql database create --account-name $c.account --resource-group $c.rg --name $db -o none 2>$null
  }
  foreach ($it in (Get-Matrix)) {
    $cn = $it.Container
    Log "CREATE $cn (acct=$($it.Account) q=$($it.Quantizer) byteSize='$($it.ByteSize)')"
    $cfg = @{
      "AppSettings:accountEndpoint"                 = $it.Endpoint
      "AppSettings:useAADAuth"                       = "true"
      "AppSettings:cosmosDatabaseId"                 = $db
      "AppSettings:cosmosContainerId"                = $cn
      "AppSettings:deleteContainerOnStart"           = "true"
      "AppSettings:cosmosContainerRUInitial"         = "400"
      "AppSettings:cosmosContainerRUFinal"           = "400"   # no-op suite scale; we scale via control-plane
      "AppSettings:scenario:sliceCount"              = "$SliceCount"
      "AppSettings:scenario:runIngestion"            = "false"
      "AppSettings:scenario:runQuery"                = "false"
      "AppSettings:scenario:quantizerType"           = $it.Quantizer
      "AppSettings:scenario:quantizationByteSize"    = $it.ByteSize
    }
    Invoke-Suite -Cfg $cfg -LogPath "$logDir\create-$cn.log" | Out-Null

    # Scale to 10000 via control-plane.
    Log "SCALE $cn -> 10000 (control-plane)"
    az cosmosdb sql container throughput update --account-name $it.AccountName --resource-group $it.Rg --database-name $db --name $cn --throughput 10000 -o none 2>$null

    # Verify topology (authoritative feed-range count) BEFORE ingest.
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

# ---------------- INGEST (all containers concurrent) ----------------
# Each container has its own independent 10,000-RU single partition, so ingests do
# not share RU; the client (32 cores, network/RU-bound) is not the bottleneck. Run all
# 12 concurrently for ~2-3h wall-clock instead of ~12h serial.
function Do-Ingest {
  Log "=== INGEST PHASE (all containers concurrent) ==="
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
        "--AppSettings:scenario:catchupTimeoutMinutes=45"
      )
      "" | & $exe @args *>&1 | Tee-Object -FilePath "$logDir\ingest-$cn.log" | Out-Null
      $perf = Select-String -Path "$logDir\ingest-$cn.log" -Pattern "\[PERF\]|Ingestion of|catch-up took" | ForEach-Object { $_.Line }
      JLog "INGEST DONE $cn :: $($perf -join ' | ')" $track
    } -ArgumentList $it,$exe,$db,$logDir,$track,$SliceCount
  }
  Log "All $($jobs.Count) ingest jobs started. Waiting..."
  $jobs | Wait-Job | Out-Null
  $jobs | Receive-Job
  $jobs | Remove-Job
  Log "=== INGEST PHASE DONE ==="
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
  $reportPath = "$outDir\report_r3.csv"
  $rows | Select-Object container, quantizerType, effectiveQuantizerType, ingestedVectors,
      @{n='recall10';e={$_.recall.'10'}}, avgQueryServerLatencyMs, p50QueryServerLatencyMs,
      p99QueryServerLatencyMs, avgQueryClientLatencyMs, avgQueryRU, diskAnnUsed |
    Sort-Object container | Format-Table -AutoSize | Out-String -Width 400 | Tee-Object -FilePath "$outDir\report_r3.txt"
  $rows | Export-Csv -Path $reportPath -NoTypeInformation
  Log "Report written: $reportPath"
}

Log "############ run_r3 START phase=$Phase accounts=$($Accounts -join ',') quantizers=$($Quantizers -join ',') runs=$($Runs -join ',') ############"
switch ($Phase) {
  "create" { Do-Create }
  "ingest" { Do-Ingest }
  "query"  { Do-Query }
  "report" { Do-Report }
  "all"    { Do-Create; Do-Ingest; Do-Query; Do-Report }
}
Log "############ run_r3 END phase=$Phase ############"
