# Main script
param (
    [Parameter(Mandatory=$true)]
    [string]$destinationFolder,
    
    [Parameter(Mandatory=$false)]
    [bool]$skipDownload = $false
)

if (-not $destinationFolder) {
    Write-Error "The destination folder is required."
    exit 
}

$destinationFolder = Resolve-Path -Path $destinationFolder
if (-not $skipDownload)
{
    #Pre-computed ground truth file for 100K, 1M and 35M vectors.   
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-100K" -OutFile $destinationFolder\wikipedia_truth_100000     

    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-1M" -OutFile $destinationFolder\wikipedia_truth_1000000       

    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia-35M" -OutFile $destinationFolder\wikipedia_truth_35000000     

    # Query file
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_query.bin" -OutFile $destinationFolder\wikipedia_query.fbin        

    # Base Dataset
    Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_base.bin" -OutFile $destinationFolder\wikipedia_base_35000000.fbin     
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
    if ($totalVectorsInBaseFile -ne 35000000) {
        Write-Error "The base file should have 35M vectors."
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

# Generate 100K Slice
$basePath = Resolve-Path -Path $destinationFolder\wikipedia_base_35000000.fbin
$new100KSlicePath = Join-Path $destinationFolder "wikipedia_base_100000.fbin"
CreateSlice -basePath $basePath -newSliceBasePath $new100KSlicePath -numVectors 100000

# Generate 1M Slice
$new1MSlicePath = Join-Path $destinationFolder "wikipedia_base_1000000.fbin"
CreateSlice -basePath $basePath -newSliceBasePath $new1MSlicePath -numVectors 1000000