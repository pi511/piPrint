<#
.SYNOPSIS
    Builds, publishes, and compiles PiPrint into a single-file setup installer
    (PiPrintSetup.exe) ready for Microsoft Store (Win32) or web distribution.
#>

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stagingDir = Join-Path $root "dist\staging\PiPrint"
$appDir = Join-Path $stagingDir "app"
$issPath = Join-Path $root "Installer\PiPrint.iss"
$outputDir = Join-Path $root "dist\Setup"
$outputExe = Join-Path $outputDir "PiPrintSetup.exe"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PiPrint - Build Single-File Setup Installer" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Publish Release App Binaries
Write-Host "`n[1/4] Publishing Release binaries..." -ForegroundColor Yellow
if (Test-Path (Join-Path $root "dist\staging")) {
    Remove-Item (Join-Path $root "dist\staging") -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $appDir -Force | Out-Null

dotnet publish (Join-Path $root "PiPrint.App\PiPrint.App.csproj") -c Release -o $appDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

# 2. Locate or Install Inno Setup Compiler (iscc.exe)
Write-Host "`n[2/4] Detecting Inno Setup compiler (iscc.exe)..." -ForegroundColor Yellow
$potentialIsccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\iscc.exe")
)

$isccPath = $null
foreach ($p in $potentialIsccPaths) {
    if (Test-Path $p) {
        $isccPath = $p
        break
    }
}

if (-not $isccPath) {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) {
        $isccPath = $cmd.Source
    }
}

if (-not $isccPath) {
    Write-Host "[*] Inno Setup not found. Downloading official Inno Setup 6 installer..." -ForegroundColor Gray
    $tempInstaller = Join-Path $env:TEMP "innosetup-installer.exe"
    $targetDir = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe" -OutFile $tempInstaller -UseBasicParsing
        Write-Host "[*] Installing Inno Setup silently..." -ForegroundColor Gray
        Start-Process $tempInstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=`"$targetDir`"" -Wait
    } catch {
        Write-Warning "Could not automatically download Inno Setup: $_"
    }

    foreach ($p in $potentialIsccPaths) {
        if (Test-Path $p) {
            $isccPath = $p
            break
        }
    }
}

if (-not $isccPath) {
    Write-Error "Inno Setup compiler (iscc.exe) could not be located. Please install Inno Setup 6 from https://jrsoftware.org/isdl.php"
    exit 1
}

Write-Host "[+] Found Inno Setup Compiler: $isccPath" -ForegroundColor Green

# 3. Create Output Directory
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 4. Compile Installer
Write-Host "`n[3/4] Compiling $issPath ..." -ForegroundColor Yellow
& $isccPath $issPath

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $outputExe)) {
    Write-Error "Inno Setup compilation failed."
    exit 1
}

# 5. Summary
$fileSizeMb = [math]::Round(((Get-Item $outputExe).Length / 1MB), 2)
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Installer Built Successfully!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Executable : $outputExe" -ForegroundColor Cyan
Write-Host "  Size       : $fileSizeMb MB" -ForegroundColor Gray
Write-Host "`nHow to test:" -ForegroundColor Yellow
Write-Host "  Run 'dist\Setup\PiPrintSetup.exe' to install PiPrint on this or any Windows PC." -ForegroundColor Gray
Write-Host "`nMicrosoft Store Submission:" -ForegroundColor Yellow
Write-Host "  1. Sign in to Partner Center: https://partner.microsoft.com/" -ForegroundColor Gray
Write-Host "  2. Create a new app -> choose 'Windows Desktop Application (EXE/MSI)'" -ForegroundColor Gray
Write-Host "  3. Upload 'PiPrintSetup.exe' and provide installer silent arguments: /VERYSILENT" -ForegroundColor Gray
