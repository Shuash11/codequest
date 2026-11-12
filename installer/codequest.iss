; =============================================================================
; CodeQuest — Inno Setup script (per-user installer)
; - PrivilegesRequired=lowest: installs into the user's own Programs folder,
;   no administrator rights, and the folder stays WRITABLE — the SQLite
;   database lives next to the exe and must accept writes.
; - codequest.db and config.local.json use onlyifdoesntexist: an upgrade
;   replaces the app but NEVER overwrites the user's attempts or settings.
; =============================================================================

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

[Setup]
AppId={{7E9F5A62-4C3D-4B8E-9A11-C0DEQUEST01}}
AppName=CodeQuest
AppVersion={#AppVersion}
AppPublisher=CodeQuest
DefaultDirName={userpf}\CodeQuest
DirExistsWarning=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupIconFile=..\src\CodeQuest\Assets\app.ico
OutputDir=.
OutputBaseFilename=CodeQuest-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\CodeQuest.exe

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
; paths are relative to THIS script's folder (installer\) — the publish output is its sibling
Source: "..\publish\CodeQuest.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\codequest.db"; DestDir: "{app}"; Flags: onlyifdoesntexist ignoreversion
Source: "..\publish\config\*"; DestDir: "{app}\config"; Flags: onlyifdoesntexist ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\CodeQuest"; Filename: "{app}\CodeQuest.exe"
Name: "{autodesktop}\CodeQuest"; Filename: "{app}\CodeQuest.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CodeQuest.exe"; Description: "&Launch CodeQuest"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; keep user data (codequest.db) on uninstall — nothing to clean
