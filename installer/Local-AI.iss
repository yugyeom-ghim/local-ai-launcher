#ifndef PayloadRoot
  #error PayloadRoot is required
#endif
#ifndef ReleaseVersion
  #error ReleaseVersion is required
#endif

[Setup]
AppId={{24B01232-D2FD-4DBF-B1E9-7DD0D4CF8B13}
AppName=Local AI Launcher
AppVersion={#ReleaseVersion}
AppVerName=Local AI Launcher
AppPublisher=Local AI
VersionInfoVersion={#ReleaseVersion}.0
VersionInfoDescription=Local AI Offline Installer
UninstallDisplayName=Local AI Launcher
DefaultDirName={localappdata}\Programs\Local AI Launcher
DefaultGroupName=Local AI Launcher
UsePreviousGroup=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputRoot}
OutputBaseFilename=Local_AI_Offline_Setup
Compression=zip/1
SolidCompression=no
DiskSpanning=yes
DiskSliceSize=1610612736
SlicesPerDisk=1
WizardStyle=modern
WizardSizePercent=115,110
WizardImageFile=assets\wizard-panel.png
WizardSmallImageFile=assets\wizard-mark.png
SetupIconFile=assets\satreci.ico
UninstallDisplayIcon={app}\installer\assets\satreci.ico
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
AllowNoIcons=yes
CloseApplications=no
RestartApplications=no
SetupLogging=yes
InfoBeforeFile=OFFLINE_NOTICE_KO.txt
Uninstallable=yes

[Languages]
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"

[Messages]
WelcomeLabel1=Local AI Launcher 설치
WelcomeLabel2=설치 위치를 선택하고 진행하세요.
FinishedLabel=설치가 완료되었습니다.


[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; Flags: unchecked

[Files]
Source: "{#PayloadRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "state\*,*.partial,__pycache__\*"

[Icons]
Name: "{group}\Local AI Launcher"; Filename: "{app}\LocalAI.Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\installer\assets\satreci.ico"
Name: "{group}\설치 및 사용 안내"; Filename: "{app}\README_INSTALL_KO.txt"
Name: "{autodesktop}\Local AI Launcher"; Filename: "{app}\LocalAI.Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\installer\assets\satreci.ico"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{app}\SI.LocalAI.Launcher.exe"
Type: files; Name: "{app}\SI.LocalAI.Launcher.exe.config"
Type: files; Name: "{userprograms}\SI Local AI\SI Local AI Launcher.lnk"
Type: files; Name: "{userprograms}\SI Local AI\설치 및 사용 안내.lnk"
Type: dirifempty; Name: "{userprograms}\SI Local AI"
Type: files; Name: "{userdesktop}\SI Local AI Launcher.lnk"

[Run]
Filename: "{app}\LocalAI.Launcher.exe"; Description: "Local AI Launcher 열기"; Flags: postinstall nowait unchecked skipifsilent

[Code]
function LauncherRunning(const Folder: String): Boolean;
var
  MutexName: String;
begin
  MutexName := 'Local\SI.LocalAI.' + Copy(Lowercase(GetSHA256OfString(UTF8Encode(Lowercase(AddBackslash(Folder))))), 1, 24);
  Result := CheckForMutexes(MutexName);
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('.NET Framework 4.8 이상을 먼저 설치하세요.', mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if LauncherRunning(ExpandConstant('{app}')) then
    Result := '서버와 Launcher를 종료한 뒤 다시 설치하세요.';
end;

function InitializeUninstall: Boolean;
begin
  Result := not LauncherRunning(ExpandConstant('{app}'));
  if not Result then
    SuppressibleMsgBox('서버와 Launcher를 종료한 뒤 제거하세요.', mbError, MB_OK, IDOK);
end;
