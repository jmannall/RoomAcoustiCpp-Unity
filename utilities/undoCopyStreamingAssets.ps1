# Define the destination directory (directory where the script is being run)
$destinationDir = Get-Location

# Define the destination folder
$destinationFolder = "$destinationDir\StreamingAssets"

# Define the Asset directory (one level above the destination directory)
$assetDir = Split-Path -Path $destinationDir -Parent

# Define the folder containing the files to move
$sourceFolder = "$assetDir\StreamingAssets"

# Define the files to move
$filesToMove = @(
    "HRTF_ILD_48000.3dti-ild",
    "Kemar_HRTF_ITD_48000Hz.3dti-hrtf",
    "NearFieldCompensation_ILD_48000.3dti-ild",
    "HRTF_ILD_48000.3dti-ild.meta",
    "Kemar_HRTF_ITD_48000Hz.3dti-hrtf.meta",
    "NearFieldCompensation_ILD_48000.3dti-ild.meta"
)

# Check if destination folder exists
if (Test-Path $destinationFolder) {
    # Check if the source folder exists
    if (Test-Path $sourceFolder) {
        foreach ($file in $filesToMove) {
            $sourcePath = "$sourceFolder\$file"
	    $destinationPath = "$destinationFolder\$file"

            # Check if the file exists and delete it
            if (Test-Path $sourcePath) {
                Move-Item -Path $sourcePath -Destination $destinationPath -Force
                Write-Host "Moved file: $sourcePath"
            } else {
                Write-Host "File not found: $sourcePath"
            }
        }
    } else {
        Write-Host "The folder $sourceFolder does not exist."
    }
} else {
	Write-Host "The folder $destinationFolder does not exist."
}