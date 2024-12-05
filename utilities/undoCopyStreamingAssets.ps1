# Define the destination directory (directory where the script is being run)
$destinationDir = Get-Location

# Define the source directory (one level above the destination directory)
$sourceDir = Split-Path -Path $destinationDir -Parent

# Define the folder to copy (replace 'YourFolder' with your folder name)
$folderToCopy = "$sourceDir\StreamingAssets"
$folderToCopyMeta = "$sourceDir\StreamingAssets.meta"

# Define the destination path in the parent directory
$destinationPath = "$destinationDir\StreamingAssets"
$destinationPathMeta = "$destinationDir\StreamingAssets.meta"

# Check if the folder exists
if (Test-Path $folderToCopy) {
    # Copy the folder to the parent directory
    Copy-Item -Path $folderToCopy -Destination $destinationPath -Recurse -Force
    Copy-Item -Path $folderToCopyMeta -Destination $destinationPathMeta -Recurse -Force
    Write-Host "Folder copied successfully to: $destinationPath"

    # Delete the original folder from the current directory
    Remove-Item -Path $folderToCopy -Recurse -Force
    Remove-Item -Path $folderToCopyMeta -Recurse -Force
    Write-Host "Original folder deleted from: $folderToCopy"
} else {
    Write-Host "The folder $folderToCopy does not exist."
}