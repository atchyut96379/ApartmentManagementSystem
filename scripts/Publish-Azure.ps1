# Builds a zip package for Azure App Service (Run From Package / ZIP Deploy).
# Usage: .\scripts\Publish-Azure.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$publishDir = Join-Path $root "publish"
$zipPath = Join-Path $root "deploy\apartment-app.zip"

Push-Location $root
try {
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    New-Item -ItemType Directory -Path (Split-Path $zipPath) -Force | Out-Null

    Write-Host "Publishing Release build..."
    dotnet publish ApartmentManagementSystem.csproj -c Release -o $publishDir

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

    Write-Host ""
    Write-Host "Done. Upload this zip in Azure Portal:"
    Write-Host "  $zipPath"
    Write-Host ""
    Write-Host "Or use Azure CLI:"
    Write-Host "  az webapp deployment source config-zip --resource-group RG_NAME --name APP_NAME --src `"$zipPath`""
}
finally {
    Pop-Location
}
