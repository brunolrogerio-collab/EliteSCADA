#if Ver != EncodeVer(6, 7, 3)
  #error This recipe was qualified with Inno Setup 6.7.3.
#endif

[Setup]
AppId={{5E77D761-B55A-42C2-B674-5D981500D9A1}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} {#Channel} {#ProductVersion} ({cm:LabName})
AppPublisher={#ProductName}
AppPublisherURL=https://scadivo.com/
VersionInfoVersion={#ProductVersion}
VersionInfoProductName={#ProductName}
VersionInfoProductVersion={#ProductVersion}
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
PrivilegesRequired=admin
; Only the payload pilot can be exercised in a disposable non-admin directory.
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\{#ProductName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
WizardStyle=modern
WizardImageFile={#BrandDir}\welcome.png
WizardSmallImageFile={#BrandDir}\mark.png
SetupIconFile={#BrandDir}\scadivo.ico
UninstallDisplayIcon={app}\releases\{#ProductVersion}\scadivo.ico
OutputBaseFilename={#ProductName}-{#Channel}-{#ProductVersion}-win-x64-lab
Compression=lzma2/fast
SolidCompression=yes
SetupLogging=yes
CloseApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
brazilianportuguese.LabName=Bancada
english.LabName=Lab
spanish.LabName=Laboratorio

[Messages]
brazilianportuguese.WelcomeLabel2=Esta edição de bancada instala os arquivos da API e da interface Web. A configuração do serviço e do banco será entregue na próxima etapa.
english.WelcomeLabel2=This lab build installs the API and web interface files. Service and database setup will follow in the next stage.
spanish.WelcomeLabel2=Esta edición de laboratorio instala los archivos de la API y de la interfaz web. La configuración del servicio y de la base de datos se entregará en la próxima etapa.

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}\releases\{#ProductVersion}"; Flags: recursesubdirs ignoreversion
