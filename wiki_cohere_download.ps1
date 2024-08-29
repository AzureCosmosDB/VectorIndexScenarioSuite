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
    #Pre-computed ground truth file for 100K, 1M and 35M vectors.
    azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-100K" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp100KPath = Join-Path $destinationFolder "wikipedia-100K"
    $new100KPath = Join-Path $destinationFolder "wikipedia_truth_100000"
    Rename-Item -Path $temp100KPath -NewName $new100KPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-1M" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp1MPath = Join-Path $destinationFolder "wikipedia-1M"
    $new1MPath = Join-Path $destinationFolder "wikipedia_truth_1000000"
    Rename-Item -Path $temp1MPath -NewName $new1MPath

    azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-35M" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp35MPath = Join-Path $destinationFolder "wikipedia-35M"
    $new35MPath = Join-Path $destinationFolder "wikipedia_truth_35000000"
    Rename-Item -Path $temp35MPath -NewName $new35MPath

    # Query file
    azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_query.bin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $tempQueryPath = Join-Path $destinationFolder "wikipedia_query.bin"
    $newQueryPath = Join-Path $destinationFolder "wikipedia_query.fbin"
    Rename-Item -Path $tempQueryPath -NewName $newQueryPath

    # Base Dataset
    azcopy copy "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_base.bin" $destinationFolder --from-to BlobLocal --check-md5 NoCheck
    $temp35MPath = Join-Path $destinationFolder "wikipedia_base.bin"
    $new35MPath = Join-Path $destinationFolder "wikipedia_base_35000000.fbin"
    Rename-Item -Path $temp35MPath -NewName $new35MPath
}

# Generate 100K and 1M slices from base file. Do this in streaming mode to avoid loading the entire file into memory.
# Slices follow the same format as the base file.
# Base format:
# First 4 bytes are the number of vectors, next 4 bytes are the dimensions
# Rest of the file is the vectors
function CreateSlice {
    param (
        [string]$new35MPath,
        [string]$newSliceBasePath,
        [int]$numVectors
    )

    $stream = [System.IO.File]::OpenRead($new35MPath)
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

# Generate 100K Slice
$new100KSlicePath = Join-Path $destinationFolder "wikipedia_base_100000.fbin"
CreateSlice -new35MPath $new35MPath -newSliceBasePath $new100KSlicePath -numVectors 100000

# Generate 1M Slice
$new1MSlicePath = Join-Path $destinationFolder "wikipedia_base_1000000.fbin"
CreateSlice -new35MPath $new35MPath -newSliceBasePath $new1MSlicePath -numVectors 1000000