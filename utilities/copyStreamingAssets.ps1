# Get the current directory (this is the directory where the script is being run)
$currentDir = Get-Location

Write-Host "Current Directory: $currentDir"

# Define the source directory (one level above the current directory)
$sourceDir = Split-Path -Path $currentDir -Parent
$sourceDir = $currentDir

Write-Host "Source Directory: $sourceDir"

# Define the folder to copy (replace 'YourFolder' with your folder name)
$folderToCopy = "$sourceDir\StreamingAssets"

# Define the destination directory (one level above the source directory)
$destinationDir = Split-Path -Path $sourceDir -Parent

# Define the destination path in the parent directory
$destinationPath = "$destinationDir\StreamingAssets"

# Check if the folder exists
if (Test-Path $folderToCopy) {
    # Copy the folder to the parent directory
    Copy-Item -Path $folderToCopy -Destination $destinationPath -Recurse -Force
    Write-Host "Folder copied successfully to: $destinationPath"

    # Delete the original folder from the current directory
    Remove-Item -Path $folderToCopy -Recurse -Force
    Write-Host "Original folder deleted from: $folderToCopy"
} else {
    Write-Host "The folder $folderToCopy does not exist."
}