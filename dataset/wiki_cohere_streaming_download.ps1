# Main script
# Please use 'wiki_cohere_download.ps1' to download the dataset and query file. This script is for downloading the additional ground truth files for each 'search' step in the streaming runbook. 
param (
    [Parameter(Mandatory=$true)]
    [string]$destinationFolder,
)

if (-not $destinationFolder) {
    Write-Error "The destination folder is required."
    exit 1
}

# Set the InformationPreference to Continue to ensure Write-Information logs to the console
$InformationPreference = 'Continue'

Write-Information "Starting download for 1M Runbook..."

# loop from 1 to 260 for all steps defined in runbook.
# Note that Ground truth files are not available for all steps but only for 'Search' steps. To simplify the script, we iterate over all steps anyways and fail silently if the file is not found.
$destinationFolder = $destinationFolder + "\wikipedia-1M_expirationtime_runbook_data"
$destinationFolder = Resolve-Path -Path $destinationFolder
for ($i=1; $i -le 260; $i++)
{
    # handle 404 error (do not log error)    
    $outputIgnore = Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-1M_expirationtime_runbook.yaml/step$i.gt100" -OutFile $destinationFolder 2>&1       

    Write-Information "Done with step $i."
}

Write-Information "Starting download for 35M Runbook..."

# loop from 1 to 1001 for all steps defined in runbook here : https://github.com/harsha-simhadri/big-ann-benchmarks/blob/main/neurips23/streaming/wikipedia-35M_expirationtime_runbook.yaml
# Note that Ground truth files are not available for all steps but only for 'Search' steps. To simplify the script, we iterate over all steps anyways and fail silently if the file is not found.
$destinationFolder = $destinationFolder + "\wikipedia-35M_expirationtime_runbook_data"
for ($i=1; $i -le 1001; $i++)
{
    # handle 404 error with azcopy (do not log error)
    $outputIgnore = Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-35M_expirationtime_runbook.yaml/step$i.gt100" -OutFile $destinationFolder 2>&1          
    Write-Information "Done with step $i."
}