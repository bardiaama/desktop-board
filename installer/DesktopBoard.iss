; Desktop Board - Inno Setup script
; Build the app first:
;   dotnet publish src/DesktopBoard.App -c Release -r win-x64 --self-contained true -p:Platform=x64 -o publish
; then compile this script (ISCC.exe installer\DesktopBoard.iss). Output: dist\DesktopBoard-Setup-<version>.exe
;
; The installer is per-user (no administrator rights), creates Start Menu / optional desktop
; shortcuts, and registers a normal "Apps & features" entry. Uninstall runs the app once with
; --uninstall-cleanup so the Windows desktop icons come back and the Run key is removed,
; then optionally deletes the user's data folder.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{9D2A4F1E-6C3B-4B7E-9A55-3F1C2B8E7D60}
AppName=Desktop Board
AppVersion={#AppVersion}
AppVerName=Desktop Board {#AppVersion}
AppPublisher=Desktop Board contributors
AppPublisherURL=https://github.com/bardiaama/desktop-board
AppSupportURL=https://github.com/bardiaama/desktop-board/issues
AppUpdatesURL=https://github.com/bardiaama/desktop-board/releases
DefaultDirName={localappdata}\Programs\DesktopBoard
DefaultGroupName=Desktop Board
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=DesktopBoard-Setup-{#AppVersion}
SetupIconFile=..\src\DesktopBoard.App\Assets\DesktopBoard.ico
UninstallDisplayIcon={app}\DesktopBoard.exe
UninstallDisplayName=Desktop Board
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.17763
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start Desktop Board when I sign in to Windows"; GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Desktop Board"; Filename: "{app}\DesktopBoard.exe"
Name: "{group}\Uninstall Desktop Board"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Desktop Board"; Filename: "{app}\DesktopBoard.exe"; Tasks: desktopicon

[Registry]
; Per-user autostart, the same key the in-app setting manages.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DesktopBoard"; ValueData: """{app}\DesktopBoard.exe"" --autostart"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\DesktopBoard.exe"; Description: "Launch Desktop Board"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 1) restore the Windows desktop icons and drop the Run key, 2) stop the board.
Filename: "{app}\DesktopBoard.exe"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "cleanup"
Filename: "{sys}\taskkill.exe"; Parameters: "/IM DesktopBoard.exe /F"; Flags: runhidden waituntilterminated; RunOnceId: "kill"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\DesktopBoard');
    // Silent uninstalls keep the data; only an interactive user can choose to delete it.
    if DirExists(DataDir) and not UninstallSilent then
    begin
      if MsgBox('Also delete your board data (tasks, notes, settings)?' + #13#10 + DataDir,
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
