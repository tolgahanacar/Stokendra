; ══════════════════════════════════════════════════════════════════════
; Stokendra Installer — Inno Setup 6 Script
; Author   : Tolgahan Acar
; ══════════════════════════════════════════════════════════════════════
;
; KULLANIM:
;   1) Projeyi publish edin:
;
;      dotnet publish Stokendra/Stokendra.csproj ^
;          -c Release -r win-x64 ^
;          --self-contained true ^
;          -p:PublishSingleFile=false ^
;          -p:PublishTrimmed=false ^
;          -o Stokendra\bin\Release\net8.0\win-x64\publish
;
;   2) Bu dosyayı Inno Setup Compiler ile derleyin:
;
;      "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" build_installer.iss
;
;   Çıktı: Installer\Stokendra_Setup_v<versiyon>.exe
;
; ══════════════════════════════════════════════════════════════════════

; ── Sabitler ──────────────────────────────────────────────────────────
#define MyAppName      "Stokendra"
#define MyAppPublisher "Tolgahan Acar"
#define MyAppURL       "https://github.com/tolgahanacar/Stokendra"
#define MyAppExeName   "Stokendra.exe"
#define MyAppIcon      "Stokendra\Stokendra.ico"
#define MyAppPublishDir SourcePath + "\Stokendra\bin\Release\net8.0\win-x64\publish"

; Versiyon bilgisini publish edilmiş exe'den otomatik çek (tek kaynak: .csproj)
#define MyAppFullVer   GetVersionNumbersString(MyAppPublishDir + "\" + MyAppExeName)
#define MyAppVersion   Copy(MyAppFullVer, 1, RPos(".", MyAppFullVer) - 1)


; ── Kurulum Ayarları ──────────────────────────────────────────────────
[Setup]
AppId={{E5F3A1B2-7C4D-4E8F-9A6B-2D3C5E7F8A1B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Kurulum dizini
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; Çıktı
OutputBaseFilename={#MyAppName}_Setup_v{#MyAppVersion}
OutputDir=.\Installer

; İkon
SetupIconFile={#MyAppIcon}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Sıkıştırma
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4

; Mimari — sadece 64-bit Windows
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Kontrol paneli sürüm bilgisi
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} Stok Takip Programı

; Modern sihirbaz (Inno Setup 6+)
WizardStyle=modern
WizardSizePercent=110

; Minimum Windows 10
MinVersion=10.0

; Çalışan uygulamayı kapat, yeniden başlatma isteme
CloseApplications=yes
RestartApplications=no

; Modern profesyonel kurulum seçenekleri
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
UsedUserAreasWarning=no

; ── Diller ────────────────────────────────────────────────────────────
[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Görevler ──────────────────────────────────────────────────────────
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

; ── Dosyalar ──────────────────────────────────────────────────────────
[Files]
Source: "{#MyAppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── Kısayollar ────────────────────────────────────────────────────────
[Icons]
Name: "{group}\{#MyAppName}";                          Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}";    Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";                    Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; ── Kurulum Sonrası ───────────────────────────────────────────────────
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

; ── Kaldırma Temizliği ────────────────────────────────────────────────
[UninstallDelete]
Type: files;            Name: "{app}\*.*"
Type: filesandordirs;   Name: "{app}"

; ── Pascal Script: Önceki Sürüm Tespiti ve Otomatik Kaldırma ─────────
[Code]
function GetUninstallString(): string;
var
  sUnInstPath: string;
  sUnInstallString: string;
begin
  Result := '';
  sUnInstPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1';
  if RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    Result := sUnInstallString
  else if RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString) then
    Result := sUnInstallString;
end;

function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

procedure UnInstallOldVersion();
var
  sUnInstallString: string;
  iResultCode: Integer;
begin
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then
  begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    Exec(sUnInstallString, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and IsUpgrade() then
    UnInstallOldVersion();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Stok verilerinizi, ayarlarınızı ve günlüklerinizi (stok.db, ayarlar.json, logs) bilgisayarınızdan silmek istiyor musunuz?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\Stokendra'), True, True, True);
    end;
  end;
end;
