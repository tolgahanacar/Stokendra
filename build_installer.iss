; Inno Setup Script for Stokendra
; This script will pack the published .NET 8 app into a single setup.exe

#define MyAppName "Stokendra"
#define MyAppVersion "3.9.4"
#define MyAppPublisher "Tolgahan Acar"
#define MyAppURL "https://github.com/tolgahanacar/Stokendra"
#define MyAppExeName "Stokendra.exe"
#define MyAppIcon "StokTakip\\StokTakip.ico"
#define MyAppPublishDir "StokTakip\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
; App Information
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Installer Settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename={#MyAppName}_Setup_v{#MyAppVersion}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#MyAppIcon}
; Adjust this pointing to where you will publish the app
OutputDir=.\Installer

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; IMPORTANT: Run 'dotnet publish -c Release -r win-x64 --self-contained true -o .\publish' first! (100% Offline Compatible)
; This points to the compiled and published output directory
Source: "{#MyAppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// If you want to check for .NET 8 Runtime, you can add Pascal Scripting here
// But for now, using a framework-dependent publish will just prompt the user to download .NET if missing natively.
