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
    #Pre-computed ground truth file for 1M, 10M and 100M vectors.
    azcopy copy "https://comp21storage.z5.web.core.windows.net/msmarcowebsearch/msmarco-1M-gt100" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp1MPath = Join-Path $destinationFolder "msmarco-1M-gt100"
    $new1MPath = Join-Path $destinationFolder "ground_truth_1000000"
    Rename-Item -Path $temp1MPath -NewName $new1MPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/msmarcowebsearch/msmarco-10M-gt100" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp10MPath = Join-Path $destinationFolder "msmarco-10M-gt100"
    $new10MPath = Join-Path $destinationFolder "ground_truth_10000000"
    Rename-Item -Path $temp10MPath -NewName $new10MPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/msmarcowebsearch/msmarco-100M-gt100" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp100MPath = Join-Path $destinationFolder "msmarco-100M-gt100"
    $new100MPath = Join-Path $destinationFolder "ground_truth_100000000"
    Rename-Item -Path $temp100MPath -NewName $new100MPath

    # Query file
    azcopy copy "https://msmarco.z22.web.core.windows.net/msmarcowebsearch/vectors/SimANS/query_vectors/vectors.bin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $tempQueryPath = Join-Path $destinationFolder "query.bin"
    $newQueryPath = Join-Path $destinationFolder "query.bin"
    Rename-Item -Path $tempQueryPath -NewName $newQueryPath

    # Base Dataset
    azcopy copy "https://msmarco.z22.web.core.windows.net/msmarcowebsearch/vectors/SimANS/passage_vectors/vectors.bin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $tempBasePath = Join-Path $destinationFolder "vectors.bin"
    $newBasePath = Join-Path $destinationFolder "base.bin"
    Rename-Item -Path $tempBasePath -NewName $newBasePath
}

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
CreateSlice -$basePath $newBasePath -newSliceBasePath $new1MSlicePath -numVectors 1000000

# Generate 10M Slice
$new10MSlicePath = Join-Path $destinationFolder "base_10000000.fbin"
CreateSlice -$basePath $newBasePath -newSliceBasePath $new10MSlicePath -numVectors 10000000

# Generate 100M Slice
$new10MSlicePath = Join-Path $destinationFolder "base_100000000.fbin"
CreateSlice -$basePath $newBasePath -newSliceBasePath $new10MSlicePath -numVectors 100000000