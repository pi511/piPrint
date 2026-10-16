#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs and configures the "PiPrint" virtual printer device in Windows.
.DESCRIPTION
    Creates a dedicated virtual printer using the built-in Microsoft XPS Document Writer v4
    driver and configures a local spool port. Also sets up background startup so PiPrint
    automatically intercepts print jobs without needing to be manually launched.
#>

[CmdletBinding()]
param(
    [string]$PrinterName = "PiPrint",
    [string]$AppExecutablePath = "",
    [switch]$SetAsDefault
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PiPrint - Virtual Printer Setup" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify Administrative Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Administrator rights required to install Windows printer devices."
    Write-Host "Re-launching with elevation..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

# 2. Setup Spool Directory
$spoolDir = Join-Path $env:ProgramData "PiPrint\Spool"
$archiveDir = Join-Path $spoolDir "Archive"

if (-not (Test-Path $spoolDir)) {
    New-Item -ItemType Directory -Path $spoolDir -Force | Out-Null
}
if (-not (Test-Path $archiveDir)) {
    New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
}

# Grant full control to Users so any application (Chrome, Word, Notepad, etc.) can write print spools
try {
    $acl = Get-Acl $spoolDir
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.AddAccessRule($rule)
    Set-Acl -Path $spoolDir -AclObject $acl
    Write-Host "[+] Configured spool permissions: $spoolDir" -ForegroundColor Green
} catch {
    Write-Warning "Could not explicitly set ACL on spool folder: $_"
}

# 3. Create Local Port for Print Interception
$portName = Join-Path $spoolDir "printjob.xps"

$existingPort = Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue
if (-not $existingPort) {
    Write-Host "[+] Creating printer port: $portName" -ForegroundColor Yellow
    Add-PrinterPort -Name $portName
} else {
    Write-Host "[*] Printer port already exists: $portName" -ForegroundColor Gray
}

# 4. Determine XPS Driver
$driverName = ""
$installedDrivers = Get-PrinterDriver | Select-Object -ExpandProperty Name

if ($installedDrivers -contains "Microsoft XPS Document Writer v4") {
    $driverName = "Microsoft XPS Document Writer v4"
} elseif ($installedDrivers -contains "Microsoft XPS Document Writer") {
    $driverName = "Microsoft XPS Document Writer"
} else {
    Write-Host "[*] XPS driver not found. Attempting to install Windows XPS feature..." -ForegroundColor Yellow
    try {
        Start-Process -FilePath "dism.exe" -ArgumentList "/Online", "/NoRestart", "/Enable-Feature", "/FeatureName:Printing-XPSServices-Features", "/All" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
    } catch {}

    try {
        Start-Process -FilePath "rundll32.exe" -ArgumentList "printui.dll,PrintUIEntry", "/ia", "/m", "`"Microsoft XPS Document Writer v4`"", "/f", "`"$env:windir\inf\ntprint.inf`"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
    } catch {}

    $installedDrivers = Get-PrinterDriver | Select-Object -ExpandProperty Name
    if ($installedDrivers -contains "Microsoft XPS Document Writer v4") {
        $driverName = "Microsoft XPS Document Writer v4"
    } elseif ($installedDrivers -contains "Microsoft XPS Document Writer") {
        $driverName = "Microsoft XPS Document Writer"
    }
}

if (-not [string]::IsNullOrEmpty($driverName)) {
    Write-Host "[+] Using driver: $driverName" -ForegroundColor Green

    # 5. Create or Update the Virtual Printer Device
    $existingPrinter = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
    if ($existingPrinter) {
        Write-Host "[*] Printer '$PrinterName' already exists. Updating port..." -ForegroundColor Yellow
        Set-Printer -Name $PrinterName -PortName $portName
    } else {
        Write-Host "[+] Creating printer device: '$PrinterName'..." -ForegroundColor Green
        Add-Printer -Name $PrinterName -DriverName $driverName -PortName $portName
    }
} else {
    Write-Warning "Microsoft XPS Document Writer driver is not currently installed on this Windows 11 edition."
    Write-Host "Tip: You can enable the XPS feature anytime in Windows Settings -> Optional Features." -ForegroundColor Yellow
}

# 6. Setup Windows Startup & Shortcut if App path is known
if ([string]::IsNullOrEmpty($AppExecutablePath)) {
    $potentialPaths = @(
        (Join-Path $PSScriptRoot "..\dist\PiPrint\app\PiPrint.exe"),
        (Join-Path $PSScriptRoot "..\PiPrint.App\bin\Debug\net8.0-windows\PiPrint.exe"),
        (Join-Path $PSScriptRoot "app\PiPrint.exe")
    )
    foreach ($p in $potentialPaths) {
        if (Test-Path $p) {
            $AppExecutablePath = (Resolve-Path $p).Path
            break
        }
    }
}

if (-not [string]::IsNullOrEmpty($AppExecutablePath) -and (Test-Path $AppExecutablePath)) {
    Write-Host "[+] Found PiPrint executable: $AppExecutablePath" -ForegroundColor Green

    # Add to HKCU Startup so it runs silently in the system tray on Windows boot
    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $runKey -Name "PiPrint" -Value "`"$AppExecutablePath`" --background" -Force
    Write-Host "[+] Registered PiPrint to Windows Startup (background tray mode)" -ForegroundColor Green

    # Create Desktop Shortcut
    try {
        $wshShell = New-Object -ComObject WScript.Shell
        $desktopPath = [Environment]::GetFolderPath("Desktop")
        $shortcutPath = Join-Path $desktopPath "PiPrint.lnk"
        $shortcut = $wshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $AppExecutablePath
        $shortcut.Description = "PiPrint - Print Preview & Page Organizer"
        $shortcut.WorkingDirectory = [System.IO.Path]::GetDirectoryName($AppExecutablePath)
        $shortcut.Save()
        Write-Host "[+] Created Desktop Shortcut: PiPrint.lnk" -ForegroundColor Green
    } catch {
        Write-Warning "Could not create desktop shortcut: $_"
    }

    # Launch PiPrint in background immediately if not running
    $isRunning = Get-Process -Name "PiPrint" -ErrorAction SilentlyContinue
    if (-not $isRunning) {
        Start-Process -FilePath $AppExecutablePath -ArgumentList "--background"
        Write-Host "[+] PiPrint background listener launched." -ForegroundColor Green
    }
}

if ($SetAsDefault) {
    (Get-WmiObject -Class Win32_Printer -Filter "Name='$PrinterName'").SetDefaultPrinter() | Out-Null
    Write-Host "[+] Set '$PrinterName' as Windows default printer." -ForegroundColor Cyan
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Installation Successful!" -ForegroundColor Green
Write-Host " Printer '$PrinterName' is ready to use in any application." -ForegroundColor Green
Write-Host " PiPrint is running in the background and will pop up" -ForegroundColor Gray
Write-Host " automatically whenever you print from any program!" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Green
