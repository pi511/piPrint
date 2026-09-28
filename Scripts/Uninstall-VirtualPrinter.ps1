#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls and cleans up the "PiPrint" virtual printer device in Windows.
#>

[CmdletBinding()]
param(
    [string]$PrinterName = "PiPrint"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PiPrint - Virtual Printer Cleanup" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify Administrative Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Administrator rights required to remove Windows printer devices."
    Write-Host "Re-launching with elevation..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

# 2. Stop running process
Get-Process -Name "PiPrint" -ErrorAction SilentlyContinue | Stop-Process -Force

# 3. Remove Startup Entry
try {
    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Remove-ItemProperty -Path $runKey -Name "PiPrint" -ErrorAction SilentlyContinue
    Write-Host "[+] Removed PiPrint from Windows Startup." -ForegroundColor Green
} catch {}

# 4. Remove Desktop Shortcut
try {
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "PiPrint.lnk"
    if (Test-Path $desktopShortcut) {
        Remove-Item $desktopShortcut -Force
        Write-Host "[+] Removed desktop shortcut." -ForegroundColor Green
    }
} catch {}

# 5. Remove Printer
$existingPrinter = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
if ($existingPrinter) {
    Write-Host "[+] Removing printer '$PrinterName'..." -ForegroundColor Yellow
    Remove-Printer -Name $PrinterName
    Write-Host "[+] Printer removed." -ForegroundColor Green
} else {
    Write-Host "[*] Printer '$PrinterName' not found." -ForegroundColor Gray
}

# 6. Remove Port
$spoolDir = Join-Path $env:ProgramData "PiPrint\Spool"
$portName = Join-Path $spoolDir "printjob.xps"

$existingPort = Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue
if ($existingPort) {
    Write-Host "[+] Removing port '$portName'..." -ForegroundColor Yellow
    Remove-PrinterPort -Name $portName
    Write-Host "[+] Port removed." -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Uninstallation Complete." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
