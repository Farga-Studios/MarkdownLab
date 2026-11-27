#define AppVersion "1.6.0"
[Setup]
AppId=Sealsworn.MarkdownLab
AppName=MarkdownLab
AppVersion={#AppVersion}
AppPublisher=Sealsworn
AppPublisherURL=https://github.com/Sealsworn/MarkdownLab
DefaultDirName={localappdata}\Programs\MarkdownLab
DefaultGroupName=MarkdownLab
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=MarkdownLab-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MarkdownLab.exe
LicenseFile=..\LICENSE
ChangesAssociations=yes
[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; Flags: unchecked
Name: "associate"; Description: "Registrar MarkdownLab en Abrir con para archivos Markdown"; Flags: unchecked
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MarkdownLab"; Filename: "{app}\MarkdownLab.exe"
Name: "{autodesktop}\MarkdownLab"; Filename: "{app}\MarkdownLab.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Classes\MarkdownLab.Document"; ValueType: string; ValueData: "Documento Markdown"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\MarkdownLab.Document\shell\open\command"; ValueType: string; ValueData: """{app}\MarkdownLab.exe"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "MarkdownLab.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
[Run]
Filename: "{app}\MarkdownLab.exe"; Description: "Abrir MarkdownLab"; Flags: nowait postinstall skipifsilent
