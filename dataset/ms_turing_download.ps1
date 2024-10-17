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
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/msturing-gt-1M" -OutFile $destinationFolder\ground_truth_1000000    

    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/msturing-gt-10M" -OutFile $destinationFolder\ground_truth_10000000       
   
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/msturing-gt-100M" -OutFile $destinationFolder\ground_truth_100000000      

    # Query file
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/query100K.fbin" -OutFile $destinationFolder\query.fbin      

    # Base Dataset
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/comp21/MSFT-TURING-ANNS/base1b.fbin" -OutFile $destinationFolder\base_1000000000.fbin     
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
        Write-Error "The base file should have 100M vectors."
        exit 1
    }  

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
$basePath = Resolve-Path -Path $destinationFolder\base_1000000000.fbin
$new1MSlicePath = Join-Path $destinationFolder "base_1000000.fbin"
CreateSlice -basePath $basePath -newSliceBasePath $new1MSlicePath -numVectors 1000000

# Generate 10M Slice
$new10MSlicePath = Join-Path $destinationFolder "base_10000000.fbin"
CreateSlice -basePath $basePath -newSliceBasePath $new10MSlicePath -numVectors 10000000

# Generate 100M Slice
$new100MSlicePath = Join-Path $destinationFolder "base_100000000.fbin"
CreateSlice -basePath $basePath -newSliceBasePath $new100MSlicePath -numVectors 100000000