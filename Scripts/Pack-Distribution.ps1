<#
.SYNOPSIS
    Builds, publishes, and packages PiPrint into a ready-to-distribute ZIP file.
#>

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$distDir = Join-Path $root "dist\PiPrint"
$stagingDir = Join-Path $root "dist\staging\PiPrint"
$appDir = Join-Path $stagingDir "app"
$zipOutput = Join-Path $root "dist\PiPrint_Setup.zip"

if (Test-Path (Join-Path $root "dist\staging")) {
    Remove-Item (Join-Path $root "dist\staging") -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $appDir -Force | Out-Null

Write-Host "Publishing PiPrint Release build..." -ForegroundColor Cyan
dotnet publish (Join-Path $root "PiPrint.App\PiPrint.App.csproj") -c Release -o $appDir

try {
    Copy-Item "$appDir\*" (Join-Path $distDir "app") -Recurse -Force -ErrorAction SilentlyContinue
} catch {}

Write-Host "Copying installer scripts..." -ForegroundColor Cyan
Copy-Item (Join-Path $root "Scripts\Install-VirtualPrinter.ps1") (Join-Path $stagingDir "Install.ps1") -Force
Copy-Item (Join-Path $root "Scripts\Uninstall-VirtualPrinter.ps1") (Join-Path $stagingDir "Uninstall.ps1") -Force
if (Test-Path (Join-Path $distDir "Setup.cmd")) {
    Copy-Item (Join-Path $distDir "Setup.cmd") (Join-Path $stagingDir "Setup.cmd") -Force
}
if (Test-Path (Join-Path $distDir "Uninstall.cmd")) {
    Copy-Item (Join-Path $distDir "Uninstall.cmd") (Join-Path $stagingDir "Uninstall.cmd") -Force
}

if (Test-Path $zipOutput) {
    Remove-Item $zipOutput -Force
}

Write-Host "Creating portable distribution package: $zipOutput ..." -ForegroundColor Cyan
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipOutput -Force

Write-Host "`nDistribution package created successfully:" -ForegroundColor Green
Write-Host "  $zipOutput" -ForegroundColor Yellow
Write-Host "Simply extract this ZIP on any Windows PC and double-click 'Setup.cmd'!" -ForegroundColor Gray
