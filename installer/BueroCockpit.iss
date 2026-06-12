; BüroCockpit Windows installer.
; Build on Windows with Inno Setup 6:
;   ISCC.exe installer\BueroCockpit.iss

#define MyAppName "BüroCockpit"
#define MyAppVersion "0.2.1"
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
ArchitecturesAllowed=x64compatible arm64
ArchitecturesInstallIn64BitMode=x64compatible arm64
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknuepfung erstellen"; GroupDescription: "Zusaetzliche Verknuepfungen:"; Flags: unchecked

[Files]
Source: "..\publish\windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64Install
Source: "..\publish\windows-arm64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsArm64Install

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} starten"; Flags: nowait postinstall skipifsilent

[Code]
function IsArm64Install: Boolean;
begin
  Result := IsARM64;
end;

function IsX64Install: Boolean;
begin
  Result := IsX64Compatible and not IsARM64;
end;

