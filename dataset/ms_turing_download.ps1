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
$newBasePath = Join-Path $destinationFolder "base_1000000000.fbin"
write-host "Destination folder: $newBasePath"

if (-not $skipDownload)
{
    # Ground Truth files
    azcopy copy "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/msturing-gt-1M" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp1MPath = Join-Path $destinationFolder "msturing-gt-1M"
    $new1MPath = Join-Path $destinationFolder "ground_truth_1000000"
    Rename-Item -Path $temp1MPath -NewName $new1MPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/msturing-gt-10M" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp10MPath = Join-Path $destinationFolder "msturing-gt-10M"
    $new10MPath = Join-Path $destinationFolder "ground_truth_10000000"
    Rename-Item -Path $temp10MPath -NewName $new10MPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/msturing-gt-100M" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp100MPath = Join-Path $destinationFolder "msturing-gt-100M"
    $new100MPath = Join-Path $destinationFolder "ground_truth_100000000"
    Rename-Item -Path $temp100MPath -NewName $new100MPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/query_gt100.bin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp1000MPath = Join-Path $destinationFolder "msturing-gt-1000M"
    $new1000MPath = Join-Path $destinationFolder "ground_truth_1000000000"
    Rename-Item -Path $temp100MPath -NewName $new100MPath

    # Query file
    azcopy copy "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/query100K.fbin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $tempQueryPath = Join-Path $destinationFolder "query100K.fbin"
    $newQueryPath = Join-Path $destinationFolder "query.fbin"
    Rename-Item -Path $tempQueryPath -NewName $newQueryPath

    # Base Dataset
    azcopy copy "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/base1b.fbin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $tempBasePath = Join-Path $destinationFolder "base1b.fbin"
    $newBasePath = Join-Path $destinationFolder "base_1000000000.fbin"
    Rename-Item -Path $tempBasePath -NewName $newBasePath
}

# Slices follow the same format as the base file.
# Base format:
# First 4 bytes are the number of vectors, next 4 bytes are the dimensions
# Rest of the file is the vectors
function CreateSlice {
    param (
        [string]$basePath,
        [string]$newSliceBasePath,
        [int]$numVectors
    )

    $stream = [System.IO.File]::OpenRead($basePath)
    $reader = New-Object System.IO.BinaryReader($stream)
    $writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create($newSliceBasePath))

    $reader.BaseStream.Seek(4, [System.IO.SeekOrigin]::Begin) # Skip number of vectors
    $writer.Write($numVectors) # Number of vectors
    $dim = $reader.ReadInt32()
    $writer.Write($dim) # Dimensions

    for ($i = 0; $i -lt $numVectors; $i++) {
        $writer.Write($reader.ReadBytes($dim * 4)) # Each vector elementr is a float (4 bytes)
    }

    $reader.Close()
    $writer.Close()
}


# Generate 1M Slice
$new1MSlicePath = Join-Path $destinationFolder "base_1000000.fbin"
CreateSlice -basePath $newBasePath -newSliceBasePath $new1MSlicePath -numVectors 1000000

# Generate 10M Slice
$new10MSlicePath = Join-Path $destinationFolder "base_10000000.fbin"
CreateSlice -basePath $newBasePath -newSliceBasePath $new10MSlicePath -numVectors 10000000
# Generate 100M Slice
$new100MSlicePath = Join-Path $destinationFolder "base_100000000.fbin"
CreateSlice -basePath $newBasePath -newSliceBasePath $new100MSlicePath -numVectors 100000000