; Instalador Capital Aco â€” gerado por publicar.ps1
; Compilar com: Inno Setup 6  https://jrsoftware.org/isinfo.php

[Setup]
AppName=Capital Aco
AppVersion=1.2.4
AppPublisher=Capital Aco
DefaultDirName={localappdata}\CapitalAco
DefaultGroupName=Capital Aco
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=CapitalAco_Instalar_v1.2.4
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableDirPage=yes
DisableProgramGroupPage=yes
MinVersion=10.0
UninstallDisplayName=Capital Aco
UninstallDisplayIcon={app}\CapitalAco.DrawingMacro.App.exe
SetupIconFile=C:\Users\leona\Documents\desenho_macro\desenho_c#\CapitalAco.DrawingMacro.App\Assets\logo.ico

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; Todos os arquivos do publish + data/chapas.csv
; biblioteca_pecas.json NAO esta no staging â€” o app cria automaticamente na primeira vez
Source: "staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userdesktop}\Capital Aco";              Filename: "{app}\CapitalAco.DrawingMacro.App.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Capital Aco\Capital Aco"; Filename: "{app}\CapitalAco.DrawingMacro.App.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Capital Aco\Desinstalar"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\CapitalAco.DrawingMacro.App.exe"; Description: "Abrir o Capital Aco agora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove pastas criadas pelo app (logs, relatorios) â€” data\ preservada para guardar a biblioteca
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\files"