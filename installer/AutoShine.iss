#define AppVersion "1.0.0"
[Setup]
AppId={{029728BE-30EE-4C8C-9115-AD2B29E17931}
AppName=AutoShine
AppVersion={#AppVersion}
DefaultDirName={autopf}\AutoShine
UsePreviousAppDir=no
DefaultGroupName=AutoShine
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=AutoShine-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=AutoShine.Installer
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\AutoShine.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop launch button"; Flags: unchecked
Name: "startup"; Description: "Start the tray Agent when a Windows user signs in"; Flags: unchecked

[Files]
Source: "..\artifacts\package\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AutoShine"; Filename: "{app}\AutoShine.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\AutoShine"; Filename: "{app}\AutoShine.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AutoShine"; ValueData: """{app}\AutoShine.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\AutoShine.exe"; Description: "Open AutoShine"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function InitializeUninstall(): Boolean;
var ExitCode: Integer;
begin
  Result := True;
  if not Exec(ExpandConstant('{app}\AutoShine.exe'), '--check-uninstall', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then begin
    MsgBox('End the session and unlink AutoShine under Sunshine connection before uninstalling. Verify the configured Sunshine path is accessible.', mbError, MB_OK);
    Result := False;
  end;
end;
