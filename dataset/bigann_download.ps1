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

    Invoke-WebRequest "https://dl.fbaipublicfiles.com/billion-scale-ann-benchmarks/GT_10M/bigann-10M" -OutFile $destinationFolder\ground_truth_10000000
   
    Invoke-WebRequest "https://dl.fbaipublicfiles.com/billion-scale-ann-benchmarks/GT_100M/bigann-100M" -OutFile $destinationFolder\ground_truth_100000000

    Invoke-WebRequest "https://dl.fbaipublicfiles.com/billion-scale-ann-benchmarks/bigann/GT.public.1B.ibin" -OutFile $destinationFolder\ground_truth_1000000000

    # Query file
    Invoke-WebRequest "https://dl.fbaipublicfiles.com/billion-scale-ann-benchmarks/bigann/query.public.10K.u8bin" -OutFile $destinationFolder\query.fbin

    # Base Dataset
    Invoke-WebRequest "https://dl.fbaipublicfiles.com/billion-scale-ann-benchmarks/bigann/base.1B.u8bin" -OutFile $destinationFolder\base_1000000000.u8bin
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

    $totalVectorsInBaseFile = $reader.ReadInt32()  
    if ($totalVectorsInBaseFile -ne 1000000000) {
        Write-Error "The base file should have 1b vectors."
        exit 1
    }  

    $writer.Write($numVectors) # Number of vectors
    $dim = $reader.ReadInt32()
    $writer.Write($dim) # Dimensions

    for ($i = 0; $i -lt $numVectors; $i++) {
        $writer.Write($reader.ReadBytes($dim * 1)) # Each vector elementr is a uint8 (1 byte)
    }

    $reader.Close()
    $writer.Close()
}

#$$destinationFolder = "C:\src\big-ann-benchmarks\data\bigann"
$basePath = Resolve-Path -Path $destinationFolder\base_1000000000.u8bin

# Generate 10M Slice
$new10MSlicePath = Join-Path $destinationFolder "base_10000000.u8bin"
#CreateSlice -basePath $basePath -newSliceBasePath $new10MSlicePath -numVectors 10000000

# Generate 100M Slice
$new100MSlicePath = Join-Path $destinationFolder "base_100000000.u8bin"
#CreateSlice -basePath $basePath -newSliceBasePath $new100MSlicePath -numVectors 100000000