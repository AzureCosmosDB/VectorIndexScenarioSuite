#requires -Version 7.0
<#
.SYNOPSIS
  Query-only run against EXISTING wiki-cohere collections (no ingestion, no delete).
  For each collection it runs warmup + numQueries measured queries and captures RESULT_JSON
  (query latency/RU, recall, DiskANN-usage validation).

  SAFETY: deleteContainerOnStart=false and runIngestion=false so existing data is untouched.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$NBase = 100000,
    [int]$NQuery = 500,
    [int]$NWarmup = 500,
    # Collection;quantizerType pairs to query.
    [string[]]$Targets = @("wikicohere100000-product;product", "wikicohere100000-spherical;spherical"),
    # Optional Cosmos account endpoint override (AAD). Empty = use appSettings.json.
    [string]$AccountEndpoint = ""
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $RepoRoot "bin\Release\net8.0\win-x64\VectorIndexScenarioSuite.exe"
$outDir = Join-Path $RepoRoot "perf\out"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (-not (Test-Path $exe)) { throw "Executable not found: $exe (build first)" }

$results = New-Object System.Collections.Generic.List[object]

foreach ($t in $Targets) {
    $parts = $t.Split(";")
    $container = $parts[0]; $q = $parts[1]
    Write-Host "`n--- Query-only: container=$container quantizer=$q warmup=$NWarmup queries=$NQuery ---" -ForegroundColor Yellow

    $args = @(
        "--AppSettings:scenario:name=wiki-cohere-english-embedding-only",
        "--AppSettings:cosmosContainerId=$container",
        "--AppSettings:scenario:quantizerType=$q",
        "--AppSettings:scenario:datasetLabel=wikicohere$NBase",
        "--AppSettings:scenario:sliceCount=$NBase",
        "--AppSettings:scenario:startVectorId=0",
        "--AppSettings:scenario:endVectorId=$NBase",
        "--AppSettings:scenario:numQueries=$NQuery",
        "--AppSettings:scenario:runIngestion=false",
        "--AppSettings:scenario:runQuery=true",
        "--AppSettings:scenario:computeRecall=true",
        "--AppSettings:scenario:computeLatencyAndRUStats=true",
        "--AppSettings:scenario:warmup:enabled=true",
        "--AppSettings:scenario:warmup:numWarmupQueries=$NWarmup",
        # Do NOT touch throughput or delete data.
        "--AppSettings:cosmosContainerRUInitial=0",
        "--AppSettings:cosmosContainerRUFinal=0",
        "--AppSettings:deleteContainerOnStart=false",
        "--AppSettings:waitForUserInputBeforeExit=false"
    )
    if ($AccountEndpoint) {
        $args += "--AppSettings:accountEndpoint=$AccountEndpoint"
        $args += "--AppSettings:useAADAuth=true"
    }

    $logFile = Join-Path $outDir "queryonly-$container.log"
    "" | & $exe @args 2>&1 | Tee-Object -FilePath $logFile | Out-Null

    $resultLine = Select-String -Path $logFile -Pattern '^RESULT_JSON: ' | Select-Object -Last 1
    if ($null -eq $resultLine) {
        Write-Host "  WARNING: no RESULT_JSON for $container (see $logFile)" -ForegroundColor Red
        continue
    }
    $obj = ($resultLine.Line -replace '^RESULT_JSON: ', '') | ConvertFrom-Json
    $results.Add($obj)
    Write-Host ("  queryLatMs={0:N1} queryRU={1:N2} recall@10={2} diskAnnUsed={3} effQuantizer={4}" -f `
        $obj.avgQueryClientLatencyMs, $obj.avgQueryRU, $obj.recall.'10', $obj.diskAnnUsed, $obj.effectiveQuantizerType) `
        -ForegroundColor Green
}

if ($results.Count -eq 0) { throw "No results collected." }

$rows = $results | ForEach-Object {
    [pscustomobject]@{
        Container      = $_.container
        EffQuantizer   = $_.effectiveQuantizerType
        QueriesRun     = $NQuery
        QueryLatencyMs = [math]::Round($_.avgQueryClientLatencyMs, 2)
        QueryRU        = [math]::Round($_.avgQueryRU, 2)
        'Recall@10'    = $_.recall.'10'
        DiskAnnUsed    = $_.diskAnnUsed
        ApproxDocs     = $_.diskAnnApproxRetrievedDocs
        ExactDocs      = $_.diskAnnExactRetrievedDocs
        'Overlap%'     = $_.diskAnnApproxVsExactOverlapPct
    }
}
Write-Host "`n======== QUERY-ONLY: Spherical vs Product ($NQuery queries, warmup on) ========" -ForegroundColor Cyan
$rows | Format-Table -AutoSize
$rows | Export-Csv -Path (Join-Path $outDir "queryonly-results.csv") -NoTypeInformation
$results | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $outDir "queryonly-results.json")
Write-Host "Wrote queryonly-results.csv / .json"
