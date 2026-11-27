#define AppVersion "0.2.0"

[Setup]
AppId={{df696067-e8a3-486c-bf67-2a22f4212997}
AppName=RGBControl
AppVersion={#AppVersion}
AppPublisher=RGBControl contributors
AppPublisherURL=https://github.com/a-aerospace/RGBControl
AppSupportURL=https://github.com/a-aerospace/RGBControl/issues
AppUpdatesURL=https://github.com/a-aerospace/RGBControl/releases
DefaultDirName={autopf}\RGBControl
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
LicenseFile=LICENSE
InfoBeforeFile=installer-info.txt
Uninstallable=yes
CreateUninstallRegKey=yes
UninstallDisplayName=RGBControl
UninstallDisplayIcon={app}\RGBControl.exe
CloseApplications=yes
RestartApplications=no
OutputDir=dist
OutputBaseFilename=RGBControl-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: "service"; Description: "Install the background service (start themes with Windows and handle sleep/hibernation)"; GroupDescription: "Lighting mode:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Dirs]
Name: "{commonappdata}\RGBControl"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "RGBControl.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\RGB Control"; Filename: "{app}\RGBControl.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\RGB Control"; Filename: "{app}\RGBControl.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Code]
function ServiceExists: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\RGBControlService');
end;

function RunServiceCommand(const Params: String): Boolean;
var
  ExitCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), Params, '', SW_HIDE,
    ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
end;

function ServiceIsStopped: Boolean;
var
  ExitCode, I: Integer;
  Output: TExecOutput;
begin
  Result := not ServiceExists;
  if Result then Exit;
  if not ExecAndCaptureOutput(ExpandConstant('{sys}\sc.exe'),
    'query RGBControlService', '', SW_SHOWNORMAL, ewWaitUntilTerminated,
    ExitCode, Output) then Exit;
  if ExitCode <> 0 then Exit;
  for I := 0 to GetArrayLength(Output.StdOut) - 1 do
    if (Pos('STATE', Output.StdOut[I]) > 0) and
       (Pos('STOPPED', Output.StdOut[I]) > 0) then
    begin
      Result := True;
      Exit;
    end;
end;

function StopService: Boolean;
var
  I: Integer;
begin
  Result := True;
  if ServiceIsStopped then Exit;
  RunServiceCommand('stop RGBControlService');
  for I := 1 to 40 do
  begin
    if ServiceIsStopped then Exit;
    Sleep(500);
  end;
  Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopService then
    Result := 'RGB Control could not stop its existing background service. ' +
      'Close the app or restart Windows, then run this installer again.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ServiceParams: String;
begin
  if CurStep <> ssPostInstall then Exit;
  if WizardIsTaskSelected('service') then
  begin
    ServiceParams := 'binPath= "\"' + ExpandConstant('{app}\RGBControl.exe') +
      '\" --service" start= auto DisplayName= "RGB Control Lighting"';
    if ServiceExists then
    begin
      if not RunServiceCommand('config RGBControlService ' + ServiceParams) then
        RaiseException('Could not update the RGB Control service.');
    end
    else if not RunServiceCommand('create RGBControlService ' + ServiceParams) then
      RaiseException('Could not install the RGB Control service.');
    if not RunServiceCommand('failure RGBControlService reset= 86400 actions= restart/5000/restart/5000/restart/5000') then
      RaiseException('Could not configure service restart behavior.');
    if not RunServiceCommand('start RGBControlService') then
      RaiseException('Could not start the RGB Control service.');
  end
  else if ServiceExists then
  begin
    if not RunServiceCommand('delete RGBControlService') then
      RaiseException('Could not remove the old RGB Control service.');
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := StopService;
  if not Result then
  begin
    MsgBox('RGB Control could not stop its background service. ' +
      'Restart Windows, then try uninstalling again.', mbError, MB_OK);
    Exit;
  end;
  if ServiceExists then
  begin
    Result := RunServiceCommand('delete RGBControlService');
    if not Result then
      MsgBox('RGB Control could not remove its background service.', mbError, MB_OK);
  end;
end;
