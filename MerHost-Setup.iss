; MerHost Installer Script for Inno Setup
; Download Inno Setup: https://jrsoftware.org/isinfo.php

#define MyAppName "MerHost"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Kingofa.com"
#define MyAppURL "https://github.com/ouzhktn/MerHost"
#define MyAppExeName "MerHost.exe"
#define MyAppCopyright "Copyright (C) 2026"

[Setup]
AppId={{8A9C3D2E-5F7B-4A1C-9E2D-6B8F0C1A3D4E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=.
OutputBaseFilename=MerHost-Setup-{#MyAppVersion}
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
ChangesAssociations=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=MerHost Fast Localhost Server with Node.js Support
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
Source: "publish-v1.1.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function NextButtonClick(CurPageID: Integer): Boolean;
var
  SettingsFile: String;
  SettingsContent: String;
begin
  Result := True;
  
  if CurPageID = wpSelectDir then
  begin
    SettingsFile := ExpandConstant('{app}') + '\settings.ini';
    SettingsContent := '; MerHost Settings' + #13#10 +
                     'InstallPath=' + ExpandConstant('{app}') + #13#10 +
                     'WwwPath=' + ExpandConstant('{app}') + '\www' + #13#10;
    SaveStringToFile(SettingsFile, SettingsContent, False);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ForceDirectories(ExpandConstant('{app}\www'));
    ForceDirectories(ExpandConstant('{app}\mysql-data'));
    ForceDirectories(ExpandConstant('{app}\ssl'));
    ForceDirectories(ExpandConstant('{app}\phpmyadmin'));
  end;
end;
