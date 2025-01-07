# Define the source directory (directory where the script is being run)
$sourceDir = Get-Location

# Define the destination directory (one level above the source directory)
$destinationDir = Split-Path -Path $sourceDir -Parent

# Define the folder containing the files to delete (replace 'YourFolder' with your folder name)
$folderToModify = "$sourceDir\StreamingAssets"

# Define the files to delete
$filesToDelete = @(
    "HRTF_ILD_48000.3dti-ild",
    "Kemar_HRTF_ITD_48000Hz.3dti-hrtf",
    "NearFieldCompensation_ILD_48000.3dti-ild",
    "HRTF_ILD_48000.3dti-ild.meta",
    "Kemar_HRTF_ITD_48000Hz.3dti-hrtf.meta",
    "NearFieldCompensation_ILD_48000.3dti-ild.meta"
)

# Check if the folder exists
if (Test-Path $folderToModify) {
    foreach ($file in $filesToDelete) {
        $filePath = "$folderToModify\$file"
	$destinationPath = "$destinationDir\StreamingAssets\$file"

        # Check if the file exists and delete it
        if (Test-Path $filePath) {
            Copy-Item -Path $filePath -Destination $destinationPath -Force
	    Remove-Item -Path $filePath -Force
            Write-Host "Deleted file: $filePath"
        } else {
            Write-Host "File not found: $filePath"
        }
    }
} else {
    Write-Host "The folder $folderToModify does not exist."
}