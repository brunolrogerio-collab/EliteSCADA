#if Ver != EncodeVer(6, 7, 3)
  #error This recipe was qualified with Inno Setup 6.7.3.
#endif

[Setup]
AppId={{5E77D761-B55A-42C2-B674-5D981500D9A1}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} {#Channel} {#ProductVersion} ({cm:Evaluation})
AppPublisher={#ProductName}
AppPublisherURL=https://scadivo.com/
VersionInfoVersion={#ProductVersion}
VersionInfoProductName={#ProductName}
VersionInfoProductVersion={#ProductVersion}
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
PrivilegesRequired=admin
DefaultDirName={autopf}\{#ProductName}
DisableDirPage=yes
UsePreviousAppDir=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
WizardStyle=modern dynamic
WizardImageFile={#BrandDir}\welcome.png
WizardSmallImageFile={#BrandDir}\mark.png
SetupIconFile={#BrandDir}\scadivo.ico
UninstallDisplayIcon={app}\releases\{#ProductVersion}\scadivo.ico
OutputBaseFilename={#ProductName}-{#Channel}-{#ProductVersion}-Windows-x64-Setup
Compression=lzma2/fast
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
brazilianportuguese.Evaluation=Avaliação
english.Evaluation=Evaluation
spanish.Evaluation=Evaluación
brazilianportuguese.Ports=Portas locais
english.Ports=Local ports
spanish.Ports=Puertos locales
brazilianportuguese.PortsHelp=Use os valores padrão ou altere as portas que já estão em uso.
english.PortsHelp=Keep the defaults or change ports that are already in use.
spanish.PortsHelp=Mantenga los valores predeterminados o cambie los puertos que ya estén en uso.
brazilianportuguese.InvalidPorts=Informe três portas diferentes entre 1024 e 65535.
english.InvalidPorts=Enter three different ports between 1024 and 65535.
spanish.InvalidPorts=Introduzca tres puertos distintos entre 1024 y 65535.
brazilianportuguese.PortUnavailable=A porta %1 de %2 está indisponível neste computador. Escolha outra porta de %2 e avance novamente.
english.PortUnavailable=Port %1 for %2 is unavailable on this computer. Choose a different %2 port and continue.
spanish.PortUnavailable=El puerto %1 de %2 no está disponible en este equipo. Elija otro puerto de %2 y continúe.
brazilianportuguese.PrepareFailed=O instalador não conseguiu preparar os serviços. Confira as portas e o log em ProgramData\SCADIVO Windows\logs\installer.log.
english.PrepareFailed=Setup could not prepare the services. Check ports and ProgramData\SCADIVO Windows\logs\installer.log.
spanish.PrepareFailed=El instalador no pudo preparar los servicios. Revise los puertos y ProgramData\SCADIVO Windows\logs\installer.log.
brazilianportuguese.InstallFailed=O serviço não ficou pronto. Confira os logs em ProgramData\SCADIVO Windows\logs.
english.InstallFailed=The service did not become ready. Check ProgramData\SCADIVO Windows\logs.
spanish.InstallFailed=El servicio no quedó listo. Revise ProgramData\SCADIVO Windows\logs.
brazilianportuguese.Open=Abrir o SCADIVO
english.Open=Open SCADIVO
spanish.Open=Abrir SCADIVO
brazilianportuguese.Guide=Primeiros passos
english.Guide=Getting started
spanish.Guide=Primeros pasos
brazilianportuguese.Monitor=Monitor do SCADIVO
english.Monitor=SCADIVO Monitor
spanish.Monitor=Monitor de SCADIVO
brazilianportuguese.UninstallTitle=O que deseja fazer com os dados do SCADIVO?
english.UninstallTitle=What should happen to SCADIVO data?
spanish.UninstallTitle=¿Qué desea hacer con los datos de SCADIVO?
brazilianportuguese.KeepData=Desinstalar e manter os dados
english.KeepData=Uninstall and keep data
spanish.KeepData=Desinstalar y conservar los datos
brazilianportuguese.KeepDataNote=Preserva projetos, histórico, usuários e licença para reinstalação.
english.KeepDataNote=Keeps projects, history, users and license for reinstallation.
spanish.KeepDataNote=Conserva proyectos, historial, usuarios y licencia para la reinstalación.
brazilianportuguese.PurgeData=Desinstalar e apagar os dados locais
english.PurgeData=Uninstall and erase local data
spanish.PurgeData=Desinstalar y borrar los datos locales
brazilianportuguese.PurgeDataNote=Apaga o banco local, a configuração e a licença desta instalação.
english.PurgeDataNote=Erases this installation's local database, configuration and license.
spanish.PurgeDataNote=Borra la base local, la configuración y la licencia de esta instalación.
brazilianportuguese.ConfirmPurge=Apagar definitivamente os projetos, o histórico, os usuários e a licença local desta instalação?
english.ConfirmPurge=Permanently erase the projects, history, users and local license of this installation?
spanish.ConfirmPurge=¿Borrar definitivamente los proyectos, el historial, los usuarios y la licencia local de esta instalación?
brazilianportuguese.RemovalFailed=Não foi possível concluir a remoção. Confira o log em ProgramData\SCADIVO Windows\logs\installer.log.
english.RemovalFailed=Uninstall could not complete. Check ProgramData\SCADIVO Windows\logs\installer.log.
spanish.RemovalFailed=No se pudo completar la desinstalación. Revise ProgramData\SCADIVO Windows\logs\installer.log.

[Messages]
brazilianportuguese.WelcomeLabel2=O assistente instala o SCADIVO, o banco local e o monitor de saúde. Ao concluir, abra o SCADIVO para criar o administrador.
english.WelcomeLabel2=Setup installs SCADIVO, the local database, and the health monitor. Open SCADIVO afterwards to create the administrator.
spanish.WelcomeLabel2=El asistente instala SCADIVO, la base de datos local y el monitor de estado. Al terminar, abra SCADIVO para crear el administrador.

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}\releases\{#ProductVersion}"; Flags: recursesubdirs ignoreversion
Source: "{#PayloadDir}\host\Install-Host.ps1"; Flags: dontcopy noencryption

[Icons]
Name: "{commonprograms}\SCADIVO\SCADIVO"; Filename: "{app}\releases\{#ProductVersion}\ScadivoTray.exe"; Parameters: "--open"; IconFilename: "{app}\releases\{#ProductVersion}\scadivo.ico"
Name: "{commonprograms}\SCADIVO\{cm:Monitor}"; Filename: "{app}\releases\{#ProductVersion}\ScadivoTray.exe"
Name: "{commonprograms}\SCADIVO\{cm:Guide}"; Filename: "{app}\releases\{#ProductVersion}\Primeiros passos.html"
Name: "{commonstartup}\SCADIVO"; Filename: "{app}\releases\{#ProductVersion}\ScadivoTray.exe"

[Run]
Filename: "{app}\releases\{#ProductVersion}\ScadivoTray.exe"; Parameters: "--open"; Description: "{cm:Open}"; Flags: postinstall nowait skipifsilent runasoriginaluser

[Code]
var
  PortsPage: TInputQueryWizardPage;
  Existing: Boolean;
  HostScriptExtracted: Boolean;
  HostExitCode: Integer;
  PurgeOnUninstall: Boolean;

function ReleaseDir(): String;
begin
  Result := ExpandConstant('{autopf}\SCADIVO\releases\{#ProductVersion}');
end;

function HostAction(Script, Action, Values: String): Boolean;
begin
  HostExitCode := -1;
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "' + Script +
    '" -Action ' + Action + ' -Release "' + ReleaseDir() + '"' + Values,
    '', SW_HIDE, ewWaitUntilTerminated, HostExitCode) and (HostExitCode = 0);
  Log(Format('SCADIVO %s returned %d', [Action, HostExitCode]));
end;

function PrepareScript(): String;
begin
  if not HostScriptExtracted then begin
    ExtractTemporaryFile('Install-Host.ps1');
    HostScriptExtracted := True;
  end;
  Result := ExpandConstant('{tmp}\Install-Host.ps1');
end;

function HostFailure(DefaultMessage: String): String;
begin
  case HostExitCode of
    21: Result := FmtMessage(CustomMessage('PortUnavailable'), [PortsPage.Values[0], 'Web']);
    22: Result := FmtMessage(CustomMessage('PortUnavailable'), [PortsPage.Values[1], 'API']);
    23: Result := FmtMessage(CustomMessage('PortUnavailable'), [PortsPage.Values[2], 'PostgreSQL']);
  else
    Result := CustomMessage(DefaultMessage);
  end;
end;

procedure InitializeWizard();
begin
  Existing := FileExists(ExpandConstant('{commonappdata}\SCADIVO Windows\instance.json'));
  PortsPage := CreateInputQueryPage(wpWelcome, CustomMessage('Ports'), 'SCADIVO', CustomMessage('PortsHelp'));
  PortsPage.Add('Web', False);
  PortsPage.Add('API', False);
  PortsPage.Add('PostgreSQL', False);
  PortsPage.Values[0] := ExpandConstant('{param:WEBPORT|18080}');
  PortsPage.Values[1] := ExpandConstant('{param:APIPORT|15120}');
  PortsPage.Values[2] := ExpandConstant('{param:DBPORT|15432}');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := Existing and (PageID = PortsPage.ID);
end;

function ValidPorts(): Boolean;
var
  A, B, C: Integer;
begin
  A := StrToIntDef(PortsPage.Values[0], 0);
  B := StrToIntDef(PortsPage.Values[1], 0);
  C := StrToIntDef(PortsPage.Values[2], 0);
  Result := (A >= 1024) and (A <= 65535) and (B >= 1024) and (B <= 65535) and
    (C >= 1024) and (C <= 65535) and (A <> B) and (A <> C) and (B <> C);
end;

function PortArguments(): String;
begin
  Result := Format(' -WebPort %d -ApiPort %d -DbPort %d', [StrToIntDef(PortsPage.Values[0], 0), StrToIntDef(PortsPage.Values[1], 0), StrToIntDef(PortsPage.Values[2], 0)]);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> PortsPage.ID then Exit;
  Result := ValidPorts();
  if not Result then begin
    SuppressibleMsgBox(CustomMessage('InvalidPorts'), mbError, MB_OK, IDOK);
    Exit;
  end;
  if Existing or WizardSilent then Exit;
  Result := HostAction(PrepareScript(), 'CheckPorts', PortArguments());
  if not Result then SuppressibleMsgBox(HostFailure('PrepareFailed'), mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not ValidPorts() then
    Result := CustomMessage('InvalidPorts')
  else if not HostAction(PrepareScript(), 'Prepare', PortArguments()) then
    Result := HostFailure('PrepareFailed');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not HostAction(ReleaseDir() + '\host\Install-Host.ps1', 'Install', PortArguments())) then
    RaiseException(HostFailure('InstallFailed'));
end;

function InitializeUninstall(): Boolean;
var
  Choice: Integer;
begin
  Result := True;
  PurgeOnUninstall := False;
  if UninstallSilent then Exit;
  if not DirExists(ExpandConstant('{commonappdata}\SCADIVO Windows')) then Exit;
  Choice := TaskDialogMsgBox(CustomMessage('UninstallTitle'), '', mbConfirmation, MB_YESNOCANCEL, [CustomMessage('KeepData') + #13#10 + CustomMessage('KeepDataNote'), CustomMessage('PurgeData') + #13#10 + CustomMessage('PurgeDataNote')], 0);
  Result := Choice = IDYES;
  if Choice = IDNO then begin
    PurgeOnUninstall := MsgBox(CustomMessage('ConfirmPurge'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    Result := PurgeOnUninstall;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Options: String;
begin
  if CurUninstallStep <> usUninstall then Exit;
  Options := '';
  if PurgeOnUninstall then Options := ' -PurgeData';
  if not HostAction(ReleaseDir() + '\host\Install-Host.ps1', 'Remove', Options) then
    RaiseException(CustomMessage('RemovalFailed'));
end;
