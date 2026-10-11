#ifndef PayloadRoot
  #error PayloadRoot is required
#endif
#ifndef ReleaseVersion
  #error ReleaseVersion is required
#endif

[Setup]
AppId={{24B01232-D2FD-4DBF-B1E9-7DD0D4CF8B13}
AppName=SI Local AI Launcher
AppVersion={#ReleaseVersion}
AppVerName=SI Local AI Launcher
AppPublisher=SI Local AI
VersionInfoVersion={#ReleaseVersion}.0
VersionInfoDescription=SI Local AI Offline Installer
UninstallDisplayName=SI Local AI Launcher
DefaultDirName={localappdata}\Programs\SI Local AI Launcher
DefaultGroupName=SI Local AI
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputRoot}
OutputBaseFilename=SI_Local_AI_Offline_Setup
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
WelcomeLabel1=SI Local AI 설치
WelcomeLabel2=모델과 실행기가 모두 포함된 오프라인 설치입니다.%n%n설치 중 GitHub에 접속하거나 파일을 다운로드하지 않습니다.%n%n기존 Launcher와 트레이 앱을 종료한 뒤 진행해 주세요.
FinishedLabel=설치가 완료되었습니다.%n%n시작 메뉴의 SI Local AI Launcher에서 모델을 선택하고 서버를 시작하세요.%n인터넷 연결이나 LM Studio는 필요하지 않습니다.

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; Flags: unchecked

[Files]
Source: "{#PayloadRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "state\*,*.partial,__pycache__\*"

[Icons]
Name: "{group}\SI Local AI Launcher"; Filename: "{app}\SI.LocalAI.Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\installer\assets\satreci.ico"
Name: "{group}\설치 및 사용 안내"; Filename: "{app}\README_INSTALL_KO.txt"
Name: "{autodesktop}\SI Local AI Launcher"; Filename: "{app}\SI.LocalAI.Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\installer\assets\satreci.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\SI.LocalAI.Launcher.exe"; Description: "SI Local AI Launcher 열기"; Flags: postinstall nowait unchecked skipifsilent

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
    SuppressibleMsgBox('.NET Framework 4.8 이상이 필요합니다. 회사 담당자를 통해 승인된 오프라인 설치본을 먼저 설치해 주세요. 이 인스톨러는 인터넷에서 런타임을 다운로드하지 않습니다.', mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if LauncherRunning(ExpandConstant('{app}')) then
    Result := '기존 Launcher에서 서버를 중지하고 트레이 앱까지 종료한 뒤 다시 진행해 주세요.';
end;

function InitializeUninstall: Boolean;
begin
  Result := not LauncherRunning(ExpandConstant('{app}'));
  if not Result then
    SuppressibleMsgBox('Launcher에서 서버를 중지하고 트레이 앱까지 종료한 뒤 제거해 주세요.', mbError, MB_OK, IDOK);
end;
