$directories = @{
    "StreamingAssets" = @(
        "HRTF_ILD_48000.3dti-ild",
        "Kemar_HRTF_ITD_48000_3dti-hrtf.3dti-hrtf",
        "Kemar_DTF_ITD_48000_3dti-hrtf.3dti-hrtf"
        "NearFieldCompensation_ILD_48000.3dti-ild"
    );
    "Resources/ProcessedPrefabs" = @(
        "AudioForGames_112_patches.prefab"
    );
    "Resources/PythonExports/AudioForGames_112_patches" = @(
        "materials.csv",
        "mesh.mtl",
        "mesh.obj",
        "MoD-ART.csv",
        "path_indexing.mtx"
    )
}

# Define the destination directory (directory where the script is being run)
$destinationDir = Get-Location

# Define the Asset directory (one level above the destination directory)
$assetDir = Split-Path -Path $destinationDir -Parent

foreach ($directory in $directories.getEnumerator()) {
    $folder = $directory.Name
    $filesToMove = $directory.Value

    # Define the destination folder
    $destinationFolder = "$destinationDir\$folder"
    # Define the folder containing the files to move
    $sourceFolder = "$assetDir\$folder"

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

                $sourcePath = "$sourceFolder\$file.meta"
                $destinationPath = "$destinationFolder\$file.meta"

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
}