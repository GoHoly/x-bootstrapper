#define MyAppName "X Bootstrapper"
#define MyAppVersion "2.2.1"
#define MyAppPublisher "X Bootstrapper"
#define MyAppExeName "X Bootstrapper.exe"
#define MyAppURL "https://octane.wtf"

[Setup]
AppId={{E8C4A91B-2D7F-4B3A-9E15-6F0C8D4A2B11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Caelus\Assets\x-bootstrapper.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=X Bootstrapper Setup
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "protocols"; Description: "Handle Play on octane.wtf"; GroupDescription: "Protocols:"; Flags: checkedonce

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "-menu"; Comment: "{#MyAppName}"
Name: "{group}\Octane"; Filename: "{app}\{#MyAppExeName}"; Parameters: "-player"; Comment: "Launch Octane"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "-menu"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\octane-player"; ValueType: string; ValueName: ""; ValueData: "URL:octane-player"; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-player"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-player\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-player\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-studio"; ValueType: string; ValueName: ""; ValueData: "URL:octane-studio"; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-studio"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-studio\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: protocols
Root: HKCU; Subkey: "Software\Classes\octane-studio\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: protocols

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "-menu"; Description: "Open {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  if ExpandConstant('{param:fromapp|0}') = '1' then
  begin
    Result := True;
    exit;
  end;

  Result := False;
  Exec(ExpandConstant('{app}\{#MyAppExeName}'), '-uninstall', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;
