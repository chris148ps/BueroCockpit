; BüroCockpit Windows installer.
; Build on Windows with Inno Setup 6:
;   ISCC.exe installer\BueroCockpit.iss

#define MyAppName "BüroCockpit"
#define MyAppVersion "0.4.23"
#define MyAppPublisher "Christian Stange"
#define MyAppExeName "BueroCockpit.exe"

[Setup]
AppId={{6CB5DD15-9370-4C67-B24F-CB9814E8158F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=BueroCockpitSetup
SetupIconFile=..\Assets\BueroCockpit.ico
Compression=zip
SolidCompression=no
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknuepfung erstellen"; GroupDescription: "Zusaetzliche Verknuepfungen:"; Flags: unchecked

[Files]
Source: "..\publish\windows-x64\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64Install

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} starten"; Flags: nowait postinstall skipifsilent

[Code]
function IsBonjourServiceInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result :=
    Exec(
      ExpandConstant('{sys}\sc.exe'),
      'query mDNSResponder',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode) and
    (ResultCode = 0);
end;

function IsBonjourDnsSdDllAvailable: Boolean;
begin
  Result := FileExists(ExpandConstant('{sys}\dns_sd.dll'));
end;

function IsBonjourAvailable: Boolean;
begin
  Result := IsBonjourServiceInstalled and IsBonjourDnsSdDllAvailable;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsBonjourAvailable then
  begin
    MsgBox(
      'Bonjour/mDNS wurde auf diesem Windows-System nicht vollstaendig erkannt.' + #13#10 + #13#10 +
      'BüroCockpit kann trotzdem installiert werden. Der lokale Testdienst funktioniert ohne Bonjour, ' +
      'aber die automatische Desktop-Suche auf dem iPad benoetigt Bonjour/mDNS. Verwenden Sie bis dahin die manuelle IP-Eingabe.',
      mbInformation,
      MB_OK);
  end;
end;

function IsArm64Install: Boolean;
begin
  Result := IsARM64;
end;

function IsX64Install: Boolean;
begin
  Result := IsX64Compatible and not IsARM64;
end;
