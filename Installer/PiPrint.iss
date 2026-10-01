; =====================================================================
; PiPrint - Windows Print Preview & Page Organizer
; Inno Setup 6 Script (Microsoft Store & Win32 Distribution Ready)
; =====================================================================

#define MyAppName "PiPrint"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PiPrint Software"
#define MyAppURL "https://github.com/pi511/piPrint"
#define MyAppExeName "PiPrint.exe"

[Setup]
AppId={{C74D2E8B-6E35-4A92-9F41-A4D0F8C81234}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\dist\Setup
OutputBaseFilename=PiPrintSetup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\PiPrint.App\Resources\PiPrint.ico
UninstallDisplayIcon={app}\app\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start PiPrint minimized in system tray on Windows startup"; GroupDescription: "Startup Options:"; Flags: checkedonce

[Files]
; Main application binaries (published self-contained/framework-dependent)
Source: "..\dist\staging\PiPrint\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs
; Virtual printer setup and teardown scripts
Source: "..\Scripts\Install-VirtualPrinter.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\Scripts\Uninstall-VirtualPrinter.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\app\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Run on Windows startup if autostart task is selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\app\{#MyAppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; 1. Configure the Windows Virtual Printer Device (runs elevated PowerShell)
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\Install-VirtualPrinter.ps1"" -AppExecutablePath ""{app}\app\{#MyAppExeName}"""; StatusMsg: "Installing PiPrint Virtual Printer device..."; Flags: runhidden
; 2. Launch PiPrint GUI on completion (checked by default)
Filename: "{app}\app\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Cleanly remove virtual printer, port, and spool directory before files are deleted
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\Uninstall-VirtualPrinter.ps1"""; StatusMsg: "Removing PiPrint Virtual Printer device..."; Flags: runhidden

[Code]
// Helper to terminate running PiPrint instances before installation or upgrade
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM ' + '{#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM ' + '{#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
