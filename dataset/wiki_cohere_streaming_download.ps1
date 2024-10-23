# Main script
# Please use 'wiki_cohere_download.ps1' to download the dataset and query file. This script is for downloading the additional ground truth files for each 'search' step in the streaming runbook. 
param (
    [Parameter(Mandatory=$true)]
    [string]$destinationFolder
)

if (-not $destinationFolder) {
    Write-Error "The destination folder is required."
    exit 1
}

$destinationFolder = Resolve-Path -Path $destinationFolder

# Set the InformationPreference to Continue to ensure Write-Information logs to the console
$InformationPreference = 'Continue'

function Download-GroundTruthFiles {
    param (
        [string]$runbookName,
        [string]$folderName,
        [int]$maxSteps
    )

    Write-Information "Starting download for $runbookName..."
    $destinationFolderGT = New-Item -Path $destinationFolder -Name $folderName -ItemType "directory"

    for ($i=1; $i -le $maxSteps; $i++) {
        try 
        {
            $outputIgnore = Invoke-WebRequest "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/$runbookName/step$i.gt100" -OutFile $destinationFolderGT
        } 
        catch
        {
            Write-Information "Ground truth file not found for step $i."
        }

        Write-Information "Done with step $i."
    }
}

# GT for Delete only runbook.
Download-GroundTruthFiles -runbookName "wikipedia-1M_expirationtime_runbook.yaml" -folderName "wikipedia-1M_expirationtime_runbook_data" -maxSteps 260
Download-GroundTruthFiles -runbookName "wikipedia-35M_expirationtime_runbook.yaml" -folderName "wikipedia-35M_expirationtime_runbook_data" -maxSteps 1001

# GT for Replace only runbook. Upload needed for 1M.
#Download-GroundTruthFiles -runbookName "wikipedia-1M_expiration_time_replace_only_runbook.yaml" -folderName "wikipedia-1M_expirationtime_runbook_replace_only_data" -maxSteps 278
Download-GroundTruthFiles -runbookName "wikipedia-35M_expiration_time_replace_only_runbook.yaml" -folderName "wikipedia-35M_expirationtime_replace_only_runbook_data" -maxSteps 222

# GT for Replace and Delete runbook.
Download-GroundTruthFiles -runbookName "wikipedia-1M_expiration_time_replace_delete_runbook.yaml" -folderName "wikipedia-1M_expirationtime_replace_delete_runbook_data" -maxSteps 316
Download-GroundTruthFiles -runbookName "wikipedia-35M_expiration_time_replace_delete_runbook.yaml" -folderName "wikipedia-35M_expirationtime_replace_delete_runbook_data" -maxSteps 1150