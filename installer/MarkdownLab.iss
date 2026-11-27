#define AppVersion "1.7.0"
[Setup]
AppId=Sealsworn.MarkdownLab
AppName=MarkdownLab
AppVersion={#AppVersion}
AppPublisher=Farga Studios
AppPublisherURL=https://github.com/Farga-Studios/MarkdownLab
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
SetupIconFile=..\MarkdownLab\Assets\MarkdownLab.ico
UninstallDisplayIcon={app}\MarkdownLab.exe
LicenseFile=..\LICENSE
ChangesAssociations=yes
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "associate"; Description: "{cm:AssociateMarkdown}"; Flags: unchecked
[CustomMessages]
english.AssociateMarkdown=Register MarkdownLab in Open with for Markdown files
spanish.AssociateMarkdown=Registrar MarkdownLab en Abrir con para archivos Markdown
[Files]
Source: "installed.marker"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MarkdownLab"; Filename: "{app}\MarkdownLab.exe"
Name: "{autodesktop}\MarkdownLab"; Filename: "{app}\MarkdownLab.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Classes\MarkdownLab.Document\DefaultIcon"; ValueType: string; ValueData: "{app}\MarkdownLab.exe,0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\MarkdownLab.Document"; ValueType: string; ValueData: "Documento Markdown"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\MarkdownLab.Document\shell\open\command"; ValueType: string; ValueData: """{app}\MarkdownLab.exe"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "MarkdownLab.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "MarkdownLab.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
[Run]
Filename: "{app}\MarkdownLab.exe"; Description: "{cm:LaunchProgram,MarkdownLab}"; Flags: nowait postinstall skipifsilent
