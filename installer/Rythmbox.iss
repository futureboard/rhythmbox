#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\out\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\out\installer"
#endif

#ifndef Runtime
  #define Runtime "win-x64"
#endif

#define MyAppUserDir "{localappdata}\Programs\Futureboard Studio\Rythmbox"
#define MyAppMachineDir "{commonpf64}\Futureboard Studio\Rythmbox"

[Setup]
AppId={{A4E9F2C1-8B3D-4F6A-9C2E-1D5B7A8E4F30}
AppName=Rythmbox
AppVersion={#AppVersion}
AppPublisher=Futureboard Studio
AppPublisherURL=https://github.com/futureboardstudio/rythmbox
DefaultDirName={code:GetDefaultDir}
DefaultGroupName=Futureboard Studio\Rythmbox
DisableProgramGroupPage=yes
LicenseFile=
OutputDir={#OutputDir}
OutputBaseFilename=Rythmbox-{#AppVersion}-{#Runtime}-setup
SetupLogging=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
UninstallDisplayIcon={app}\Rythmbox.App.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Rythmbox"; Filename: "{app}\Rythmbox.App.exe"; WorkingDir: "{app}"
Name: "{group}\Rythmbox Editor"; Filename: "{app}\Rythmbox.Editor.exe"; WorkingDir: "{app}"
Name: "{group}\Sample Creator"; Filename: "{app}\Rythmbox.SampleCreator.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,Rythmbox}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Rythmbox"; Filename: "{app}\Rythmbox.App.exe"; Tasks: desktopicon; WorkingDir: "{app}"

[Run]
Filename: "{app}\Rythmbox.App.exe"; Description: "{cm:LaunchProgram,Rythmbox}"; Flags: nowait postinstall skipifsilent

[Code]
function GetDefaultDir(Param: string): string;
begin
  if IsAdminInstallMode then
    Result := ExpandConstant('{#MyAppMachineDir}')
  else
    Result := ExpandConstant('{#MyAppUserDir}');
end;
