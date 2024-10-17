# Main script
# Please use 'wiki_cohere_download.ps1' to download the dataset and query file. This script is for downloading the additional ground truth files for each 'search' step in the streaming runbook. 
param (
    [Parameter(Mandatory=$true)]
    [string]$destinationFolder,

    [Parameter(Mandatory=$true)]
    [string]$azcopyPath
)

if (-not $destinationFolder) {
    Write-Error "The destination folder is required."
    exit 1
}

if (-not $azcopyPath) {
    Write-Error "The azcopy tool is required."
    exit 1
}

$destinationFolder = Resolve-Path -Path $destinationFolder
$azcopyPath = Resolve-Path -Path $azcopyPath
$env:PATH += ";$azcopyPath"

# Set the InformationPreference to Continue to ensure Write-Information logs to the console
$InformationPreference = 'Continue'

Write-Information "Starting download..."

# loop from 1 to 1001 for all steps defined in runbook here : https://github.com/harsha-simhadri/big-ann-benchmarks/blob/main/neurips23/streaming/wikipedia-35M_expirationtime_runbook.yaml
# Note that Ground truth files are not available for all steps but only for 'Search' steps. To simplify the script, we iterate over all steps anyways and fail silently if the file is not found.
for ($i=1; $i -le 1001; $i++)
{
    # handle 404 error with azcopy (do not log error)
    $outputIgnore = azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-35M_expirationtime_runbook.yaml/step$i.gt100" $destinationFolder --from-to BlobLocal  2>&1     
    
    Write-Information "Done with step $i."
}