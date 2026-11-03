; Inno Setup Script for DETECTive
; Builds standalone Windows installer for x64 architecture

#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#define AppName "DETECTive"
#define AppPublisher "Igor Kurilov"
#define AppURL "https://github.com/kurilovigor/detective"
#define AppExeName "Detective.exe"

#ifndef SourceDir
#define SourceDir "..\publish"
#endif

#ifndef OutputDir
#define OutputDir "..\dist"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "Detective-Setup-" + AppVersion + "-x64"
#endif

[Setup]
; Unique AppId generated for DETECTive
AppId={{C60FD158-E319-4F03-A785-DB67FF924847}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\Detective\Resources\Images\DetectiveLogo.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequiredOverridesAllowed=dialog commandline
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#AppExeName}"

[Registry]
; Associate .detective extension with DETECTive application
Root: HKA; Subkey: "Software\Classes\.detective"; ValueType: string; ValueName: ""; ValueData: "DETECTiveProject"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.detective"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-detective"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\DETECTiveProject"; ValueType: string; ValueName: ""; ValueData: "DETECTive Project File"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\DETECTiveProject\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKA; Subkey: "Software\Classes\DETECTiveProject\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

