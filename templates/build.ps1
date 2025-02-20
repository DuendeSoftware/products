# Copy the .templates.csproj to the artifacts dir
# Copy the templates from bff/templates to artifacts dir
# Copy the templates from identity/templates server to artifacts dir

# Get the directory of the current script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Define the source and destination directories
$sourceCsproj = Join-Path $scriptDir "templates.csproj"
$sourcePaths = @(
    Join-Path $scriptDir "..\bff\templates",
    Join-Path $scriptDir "..\identity-server\templates"
)
$destinationDir = Join-Path $scriptDir ".\artifacts"

# Create the destination directory if it doesn't exist
if (-Not (Test-Path -Path $destinationDir)) {
    New-Item -ItemType Directory -Path $destinationDir
}

# Copy the .templates.csproj to the artifacts dir
Copy-Item -Path $sourceCsproj -Destination $destinationDir -Force

# Copy the templates from each source path to the artifacts dir
foreach ($sourcePath in $sourcePaths) {
    Copy-Item -Path "$sourcePath\*" -Destination $destinationDir -Recurse -Force
}