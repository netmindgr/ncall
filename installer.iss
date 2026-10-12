; nCall per-user installer. GPL-3.0-or-later.
#ifndef AppVersion
  #define AppVersion "0.2.2"
#endif
#define AppExeName "ncall.exe"
#ifdef LifecycleTest
  #define AppName "nCall Lifecycle Test"
  #define ProductId "D1D4EAC8-2071-4872-9A22-A61C34E3C608"
  #define UiDataDir "netmind\nCall Lifecycle Test"
  #define OutputFile "ncall-lifecycle-test"
#else
  #define AppName "nCall"
  #define ProductId "0464EE33-F560-4BCA-A66D-CC55A0AF1A47"
  #define UiDataDir "netmind\nCall"
  #define OutputFile "ncall-" + AppVersion + "-setup"
#endif
[Setup]
AppId={{{#ProductId}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=netmind
AppPublisherURL=https://www.netmind.gr/
AppSupportURL=https://www.netmind.gr/
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=installer
OutputBaseFilename={#OutputFile}
SetupIconFile=source\res\netmind_softphone.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
MinVersion=10.0.19041
InfoBeforeFile=branding\installer-info.txt
AppCopyright=© 2011-2026 MicroSIP, © Teluu Inc., © 2026 netmind. GPL-3.0-or-later
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=netmind
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Greek customers: the installer starts in Greek without asking (English stays available via /LANG=english).
ShowLanguageDialog=no
LanguageDetectionMethod=none
WizardImageFile=branding\installer-side-100.bmp,branding\installer-side-150.bmp,branding\installer-side-200.bmp
WizardSmallImageFile=branding\installer-small-100.bmp,branding\installer-small-150.bmp,branding\installer-small-200.bmp
CloseApplications=yes
RestartApplications=no
AppMutex=netmind.nCall.AppMutex,Global\netmind.nCall.AppMutex
[Languages]
Name: "greek"; MessagesFile: "branding\Greek.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
[Files]
Source: "build\Win32\Release\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "build\Win32\Release\netmind.ui.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "build\Win32\Release\fonts\*.ttf"; DestDir: "{app}\fonts"; Flags: ignoreversion
Source: "langpack_greek.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "source\res\sounds\*.wav"; DestDir: "{app}"; Flags: ignoreversion
Source: "source\res\sounds\ringtones\*.wav"; DestDir: "{app}\ringtones"; Flags: ignoreversion
Source: "COPYING"; DestDir: "{app}"; Flags: ignoreversion
Source: "COPYING-GPL-3"; DestDir: "{app}"; Flags: ignoreversion
Source: "NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "dependency-versions.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "third_party\netmind\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
[Registry]
Root: HKCU; Subkey: "Software\{#AppName}"; ValueType: string; ValueName: ""; ValueData: "{app}"; Flags: uninsdeletevalue uninsdeletekeyifempty
; tel:, sip:, callto: links. nCall is offered in Settings > Default apps; it does not take over existing handlers.
Root: HKCU; Subkey: "Software\Classes\nCall.URL"; ValueType: string; ValueName: ""; ValueData: "URL:nCall"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\nCall.URL"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\nCall.URL\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKCU; Subkey: "Software\Classes\nCall.URL\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\{#AppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#AppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "nCall by netmind"
Root: HKCU; Subkey: "Software\{#AppName}\Capabilities\URLAssociations"; ValueType: string; ValueName: "tel"; ValueData: "nCall.URL"
Root: HKCU; Subkey: "Software\{#AppName}\Capabilities\URLAssociations"; ValueType: string; ValueName: "sip"; ValueData: "nCall.URL"
Root: HKCU; Subkey: "Software\{#AppName}\Capabilities\URLAssociations"; ValueType: string; ValueName: "callto"; ValueData: "nCall.URL"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "Software\{#AppName}\Capabilities"; Flags: uninsdeletevalue

[UninstallDelete]
; Files the program may create next to itself; the user profile in %APPDATA%\nCall is kept for a reinstall.
Type: files; Name: "{app}\ncall.ini"
Type: files; Name: "{app}\ncall_log*.txt"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; AppUserModelID: "netmind.nCall"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; AppUserModelID: "netmind.nCall"; Tasks: desktopicon
[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Automatic updates install silently: start nCall again afterwards.
Filename: "{app}\{#AppExeName}"; Flags: nowait; Check: WizardSilent
[Code]
var RemovePersonalData: Boolean;
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32,'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','Release',Release) and (Release >= 528040);
  if not Result then MsgBox('Το nCall χρειάζεται .NET Framework 4.8. Εγκαταστήστε το από τη Microsoft και εκτελέστε ξανά την εγκατάσταση.',mbError,MB_OK);
end;
function InitializeUninstall(): Boolean;
begin
  Result := True;
  RemovePersonalData := ExpandConstant('{param:REMOVEUSERDATA|0}') = '1';
  if not UninstallSilent then
    // Task dialog with our own button labels (MsgBox buttons follow the Windows language, e.g. "Yes/No").
    RemovePersonalData := TaskDialogMsgBox('Να διαγραφούν και τα προσωπικά δεδομένα;',
      'Ρυθμίσεις, λογαριασμοί, επαφές, ιστορικό κλήσεων και SMS. Με «Διατήρηση» θα είναι διαθέσιμα σε επόμενη εγκατάσταση.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2, ['Διαγραφή', 'Διατήρηση'], 0) = IDYES;
end;
procedure CurUninstallStepChanged(Step: TUninstallStep);
var StartupPath: String;
begin
  if Step = usPostUninstall then begin
    // Remove only an autostart entry referring to this installed copy.
    if RegQueryStringValue(HKCU,'Software\Microsoft\Windows\CurrentVersion\Run','{#AppName}',StartupPath) then
      if Pos(Lowercase(ExpandConstant('{app}\{#AppExeName}')),Lowercase(StartupPath)) > 0 then
        RegDeleteValue(HKCU,'Software\Microsoft\Windows\CurrentVersion\Run','{#AppName}');
    if RemovePersonalData then begin
      DelTree(ExpandConstant('{userappdata}\{#AppName}'),True,True,True);
      DelTree(ExpandConstant('{localappdata}\{#AppName}'),True,True,True);
      DelTree(ExpandConstant('{localappdata}\{#UiDataDir}'),True,True,True);
    end;
  end;
end;
