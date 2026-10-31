#define MyAppName "FileMorph"
#define MyAppVersion "0.1.0-alpha"
#define MyAppPublisher "FileMorph"
#define MyAppExeName "FileMorph.exe"

[Setup]
AppId={{6B3C72F1-8A6D-4E2C-9A55-7E89D1C0A111}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\FileMorph
DefaultGroupName=FileMorph
OutputDir=..\artifacts\installer
OutputBaseFilename=FileMorph-Setup
Compression=lzma2
; VC++ runtime files are bundled app-local by the packaging workflow.
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FileMorph"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FileMorph"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch FileMorph"; Flags: postinstall nowait skipifsilent
