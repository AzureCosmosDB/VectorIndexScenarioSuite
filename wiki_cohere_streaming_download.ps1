# Main script
param (
    [Parameter(Mandatory=$true)]
    [string]$destinationFolder,

    [Parameter(Mandatory=$true)]
    [string]$azcopyPath,
    
    [Parameter(Mandatory=$false)]
    [bool]$skipDownload = $false
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

if (-not $skipDownload)
{
    azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-35M_expirationtime_runbook.yaml" $destinationFolder --from-to BlobLocal --check-md5 NoCheck        

    # loop from 1 to 1001 for all steps defined in runbook here : https://github.com/harsha-simhadri/big-ann-benchmarks/blob/main/neurips23/streaming/wikipedia-35M_expirationtime_runbook.yaml
    # Note that Ground truth files are not available for all steps. Only for 'Search' steps, we iterate over all steps anyways and fail silently if the file is not found.
    for ($i=1; $i -le 1001; $i++)
    {
        # handle 404 error with azcopy (do not log error)
        $outputIgnore = azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-35M_expirationtime_runbook.yaml/step$i.gt100" $destinationFolder --from-to BlobLocal --check-md5 NoCheck 2>&1            
    }
}