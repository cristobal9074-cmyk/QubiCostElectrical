; Inno Setup 6.3+ — Qubi Cost Electrical
;
; ── Compilación manual ────────────────────────────────────────────────────────
;   1. Publicar la app:
;      dotnet publish src\QubiCostElectrical.Wpf\QubiCostElectrical.Wpf.csproj
;             -c Release -r win-x64 --self-contained true
;             -p:PublishSingleFile=true -o publish\win-x64
;   2. Colocar el ícono en src\QubiCostElectrical.Wpf\Assets\QubiCostElectrical.ico
;      (ya incluido en la distribución)
;   3. Rellenar installer\company.iss con datos de tu empresa
;   4. Abrir este .iss con Inno Setup Compiler → Compile (Ctrl+F9)
;
; ── Compilación en CI (GitHub Actions) ───────────────────────────────────────
;   El workflow .github/workflows/build.yml:
;   - Publica el EXE en publish/win-x64
;   - Llama: iscc /DAppVersion=X.Y.Z installer\QubiCostElectrical.iss
;   La versión viene de Directory.Build.props; no la edites aquí.

; Datos de empresa — editar en installer\company.iss, no aquí.
#ifndef AppPublisher
  #include "company.iss"
#endif

#define AppName    "Qubi Cost Electrical"
#define AppExeName "QubiCostElectrical.exe"
#define PublishDir "..\publish\win-x64"
#define IconFile   "..\src\QubiCostElectrical.Wpf\Assets\QubiCostElectrical.ico"

; AppVersion puede ser inyectada por CI con /DAppVersion=X.Y.Z
#ifndef AppVersion
  #define AppVersion "1.1.1"
#endif

[Setup]
AppId={{A7F3B2C1-D4E5-4F6A-B8C9-0D1E2F3A4B5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=no
OutputDir=..\artifacts
OutputBaseFilename=QubiCostElectrical-Setup-{#AppVersion}
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
ShowLanguageDialog=no
LanguageDetectionMethod=none

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el &escritorio"; GroupDescription: "Accesos directos:"; Flags: checked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";       Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Asociación de archivo .qce — Proyecto de Qubi Cost Electrical
Root: HKCU; Subkey: "Software\Classes\.qce";                                         ValueType: string; ValueName: ""; ValueData: "QubiCostElectrical.Project"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\QubiCostElectrical.Project";                   ValueType: string; ValueName: ""; ValueData: "Proyecto de Qubi Cost Electrical"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\QubiCostElectrical.Project\DefaultIcon";       ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKCU; Subkey: "Software\Classes\QubiCostElectrical.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Los datos de usuario en %LOCALAPPDATA% NO se borran al desinstalar
; para no perder proyectos ni preferencias guardadas localmente.
