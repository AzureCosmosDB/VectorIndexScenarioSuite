# Main script
param (
    [Parameter(Mandatory=$true)]
    [string]$destinationFolder,
    
    [Parameter(Mandatory=$false)]
    [bool]$skipDownload = $false
)

if (-not $destinationFolder) {
    Write-Error "The destination folder is required."
    exit 1
}

$destinationFolder = Resolve-Path -Path $destinationFolder
if (-not $skipDownload)
{
    # Ground Truth files
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/GT.public.1M.bin" -OutFile $destinationFolder\ground_truth_1000000
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/GT.public.5M.bin" -OutFile $destinationFolder\ground_truth_5000000

    # Query file
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/query.public.100k.u8bin" -OutFile $destinationFolder\query.u8bin

    # Base Dataset files
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/base.1M.u8bin" -OutFile $destinationFolder\base_1000000.u8bin
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/base.5M.u8bin" -OutFile $destinationFolder\base_5000000.u8bin
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/base.10M.u8bin" -OutFile $destinationFolder\base_10000000.u8bin

    # Label files (download and rename with .label suffix)
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/base.1M.label.txt" -OutFile $destinationFolder\base_1000000.u8bin.label
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/base.5M.label.txt" -OutFile $destinationFolder\base_5000000.u8bin.label
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/base.10M.label.txt" -OutFile $destinationFolder\base_10000000.u8bin.label
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/yfcc/query.label.txt" -OutFile $destinationFolder\query.u8bin.label
}