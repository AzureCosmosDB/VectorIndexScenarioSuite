#requires -Version 7.0
<#
.SYNOPSIS
  Spherical vs Product quantizer comparison harness for VectorIndexScenarioSuite.

  Uses the REAL BigANN Wikipedia-Cohere dataset (35M x 768, inner-product). For each
  requested base slice size and each quantizer type (product = default, spherical), this:
    1. Downloads a real cropped wiki-cohere slice + real queries and computes exact
       ground truth locally via fetch_wikicohere.py (HTTP range GET; ~15 MB per slice).
    2. Runs the suite against a SEPARATE collection (<label>-<quantizer>), creating it
       fresh via the Cosmos SQL SDK with the chosen quantizerType.
    3. Captures the RESULT_JSON summary line (insert time, lazy catch-up time, query time,
       recall, and DiskANN-usage validation).
  Finally it prints a comparison table and writes results.csv / results.json.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$NQuery = 50,
    [int]$RUInitial = 8000,
    [int]$RUFinal = 10000,
    [int]$CatchupTimeoutMin = 30,
    [string[]]$Quantizers = @("product", "spherical"),
    # Real wiki-cohere base-slice sizes to sweep. Each size is one "dataset".
    [int[]]$BaseSizes = @(5000),
    # Optional container-name label override (default "wikicohere<NBase>"). Lets runs at
    # different RU/accounts use distinct collections instead of clobbering each other.
    [string]$Label = "",
    # Optional Cosmos account endpoint override (AAD). Empty = use appSettings.json.
    [string]$AccountEndpoint = "",
    # Reuse already-downloaded base file (deterministic crop) instead of re-fetching.
    [switch]$ReuseBase,
    # Skip fetching entirely (data files already present). Needed when running multiple
    # instances concurrently so they don't race writing the same data files.
    [switch]$SkipFetch
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $RepoRoot "bin\Release\net8.0\win-x64\VectorIndexScenarioSuite.exe"
$dataDir = Join-Path $RepoRoot "data"
$fetch = Join-Path $RepoRoot "perf\fetch_wikicohere.py"
$outDir = Join-Path $RepoRoot "perf\out"
New-Item -ItemType Directory -Force -Path $dataDir, $outDir | Out-Null

if (-not (Test-Path $exe)) { throw "Executable not found: $exe (build first)" }

$results = New-Object System.Collections.Generic.List[object]

foreach ($NBase in $BaseSizes) {
    $label = if ($Label) { $Label } else { "wikicohere$NBase" }

    Write-Host "`n=== Fetching REAL wiki-cohere slice '$label' (nbase=$NBase) ===" -ForegroundColor Cyan
    if ($SkipFetch) {
        Write-Host "  -SkipFetch set; using existing data files on disk." -ForegroundColor DarkYellow
    }
    else {
        $fetchArgs = @("--outdir", $dataDir, "--nbase", $NBase, "--nquery", $NQuery, "--gtk", "100")
        if ($ReuseBase) { $fetchArgs += "--reuse-base" }
        python $fetch @fetchArgs
        if ($LASTEXITCODE -ne 0) { throw "real wiki-cohere fetch failed for $label" }
    }

    foreach ($q in $Quantizers) {
        $container = "$label-$q"
        Write-Host "`n--- Run: dataset=$label quantizer=$q container=$container ---" -ForegroundColor Yellow

        $args = @(
            "--AppSettings:scenario:name=wiki-cohere-english-embedding-only",
            "--AppSettings:cosmosContainerId=$container",
            "--AppSettings:scenario:quantizerType=$q",
            "--AppSettings:scenario:datasetLabel=$label",
            "--AppSettings:scenario:sliceCount=$NBase",
            "--AppSettings:scenario:startVectorId=0",
            "--AppSettings:scenario:endVectorId=$NBase",
            "--AppSettings:scenario:numQueries=$NQuery",
            "--AppSettings:scenario:runIngestion=true",
            "--AppSettings:scenario:runQuery=true",
            "--AppSettings:scenario:computeRecall=true",
            "--AppSettings:scenario:computeLatencyAndRUStats=true",
            "--AppSettings:scenario:ingestWithBulkExecution=true",
            "--AppSettings:cosmosContainerRUInitial=$RUInitial",
            "--AppSettings:cosmosContainerRUFinal=$RUFinal",
            "--AppSettings:scenario:catchupTimeoutMinutes=$CatchupTimeoutMin",
            "--AppSettings:deleteContainerOnStart=true",
            "--AppSettings:waitForUserInputBeforeExit=false"
        )
        if ($AccountEndpoint) {
            $args += "--AppSettings:accountEndpoint=$AccountEndpoint"
            $args += "--AppSettings:useAADAuth=true"
        }

        $logFile = Join-Path $outDir "$container.log"
        # Pipe a newline for the final Console.ReadLine(); tee full output to a per-run log.
        "" | & $exe @args 2>&1 | Tee-Object -FilePath $logFile | Out-Null

        $resultLine = Select-String -Path $logFile -Pattern '^RESULT_JSON: ' | Select-Object -Last 1
        if ($null -eq $resultLine) {
            Write-Host "  WARNING: no RESULT_JSON produced for $container (see $logFile)" -ForegroundColor Red
            continue
        }
        $json = $resultLine.Line -replace '^RESULT_JSON: ', ''
        $obj = $json | ConvertFrom-Json
        $results.Add($obj)
        Write-Host ("  insert={0:N1}s catchup={1:N1}s query={2:N1}s recall@10={3} diskAnnUsed={4} effQuantizer={5}" -f `
            ($obj.ingestionDurationMs/1000), ($obj.indexCatchupDurationMs/1000), `
            ($obj.queryPhaseDurationMs/1000), $obj.recall.'10', $obj.diskAnnUsed, $obj.effectiveQuantizerType) -ForegroundColor Green
    }
}

if ($results.Count -eq 0) { throw "No results collected." }

# ---- Report ----
$rows = $results | ForEach-Object {
    [pscustomobject]@{
        Dataset          = $_.datasetLabel
        Quantizer        = $_.quantizerType
        EffQuantizer     = $_.effectiveQuantizerType
        Container        = $_.container
        Vectors          = $_.ingestedVectors
        InsertSec        = [math]::Round($_.ingestionDurationMs/1000, 1)
        CatchupSec       = [math]::Round($_.indexCatchupDurationMs/1000, 1)
        QuerySec         = [math]::Round($_.queryPhaseDurationMs/1000, 1)
        QueryLatencyMs   = [math]::Round($_.avgQueryClientLatencyMs, 1)
        QueryRU          = [math]::Round($_.avgQueryRU, 2)
        'Recall@10'      = $_.recall.'10'
        DiskAnnUsed      = $_.diskAnnUsed
        ApproxDocs       = $_.diskAnnApproxRetrievedDocs
        ExactDocs        = $_.diskAnnExactRetrievedDocs
        'Overlap%'       = $_.diskAnnApproxVsExactOverlapPct
    }
}

Write-Host "`n================ COMPARISON: Spherical vs Product Quantizer ================" -ForegroundColor Cyan
$rows | Sort-Object Dataset, Quantizer | Format-Table -AutoSize

$csvPath = Join-Path $outDir ("results{0}.csv" -f ($(if ($Label) { "-$Label" } else { "" })))
$jsonPath = Join-Path $outDir ("results{0}.json" -f ($(if ($Label) { "-$Label" } else { "" })))
$rows | Export-Csv -Path $csvPath -NoTypeInformation
$results | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath
Write-Host "Wrote $csvPath and $jsonPath"
