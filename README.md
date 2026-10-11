# AutoShine

Windows 11 x64에서 Sunshine 스트리밍의 오디오 전환과 세션 종료 편의 기능을 관리하는 도구입니다. 작은 실행 버튼과 트레이에서 localhost 웹 설정 화면을 엽니다. 화면 전환과 VDD 관리는 Sunshine이 담당합니다.

**현재 버전: 1.0.0.** Windows 11 x64용 릴리즈입니다.

## 설치

GitHub Releases에서 설치 파일을 다운로드하세요. 소스에서는 아래 빌드 절차로 생성할 수 있습니다.

1. Sunshine과 사용할 가상 디스플레이 환경을 먼저 준비합니다.
2. `AutoShine-1.0.0-Setup.exe`를 실행합니다. 기본 설치 경로는 `C:\Program Files\AutoShine`이며 설치에는 관리자 권한이 필요합니다. 설치 후 앱은 원래 사용자 권한으로 실행합니다. 개인 설정과 인증정보는 사용자별로 보관합니다.
3. 시작 메뉴의 AutoShine을 실행합니다. 전체 사용자용 바탕화면 바로가기와 로그인 자동 실행은 설치 때 선택할 수 있으며 기본적으로 선택하지 않습니다. 자동 실행을 선택하면 로그인한 사용자 권한으로 실행합니다.
4. 설정 → Sunshine 연동에서 Sunshine 설정 파일·포트 및 직접 준비한 `SoundVolumeView.exe` 경로를 지정합니다. SoundVolumeView는 설치 파일에 포함되지 않습니다.
5. 사운드에서 시작·종료 오디오 상태와 종료 후 출력 우선순위를 설정하고 저장합니다.
6. 설정 → Sunshine 연동에서 연동 버튼으로 등록합니다. 필요한 경우 Windows 관리자 승인이 표시됩니다. 기존 설정의 백업을 만든 후 파일을 변경합니다.
7. 세션이 없는 상태에서 Sunshine을 다시 시작하고 실제 스트리밍 동작을 확인합니다. 서비스 재시작은 자동으로 수행하지 않습니다.

기존 `SunshineAutomation.exe` 준비 명령이 있으면 새 연동의 적용을 거부합니다. 기존 Agent를 정리하고 안전하게 이전하는 작업은 아직 남아 있습니다. 두 Agent를 동시에 운영하지 마세요.

## 사용

- **사운드:** 스트리밍 출력, 종료 후 출력 우선순위, 장치별 시작/종료 동작을 설정합니다.
- **시뮬레이션:** 시작·종료·연결 끊김·재접속의 예상 오디오·디스플레이 상태를 표시합니다. 실제 장치는 변경하지 않습니다.
- **디스플레이:** 스트리밍 디스플레이 선택과 VDD 해상도 프리셋을 설정합니다.
- **설정:** SoundVolumeView 경로, Sunshine 연동·로그인, 종료 단축키와 설정 백업을 관리합니다. 테마·언어는 헤더에서 변경합니다.
- **트레이:** 설정 열기, 세션 종료, 상태 확인을 제공합니다. 진행 중인 세션이 있으면 Agent 종료를 거부합니다.

기본적인 세션 종료는 Moonlight 등 클라이언트의 **세션 종료 버튼**을 사용합니다. PC 종료 단축키는 선택형 편의 기능이며 기본값은 꺼짐입니다. 단축키나 트레이 종료를 사용하려면 Sunshine 인증을 연결해야 합니다.

### 오디오 복구

스트리밍 출력은 시작 시 활성화하고 음소거를 해제해 기본 출력으로 선택합니다. 차단 대상은 먼저 음소거한 뒤 필요하면 비활성화합니다.

종료 후 기본 출력은 다음 중 하나로 설정합니다.

- 스트리밍 시작 전 출력으로 복구
- 연결된 장치 중 설정한 우선순위에 따라 선택

장치별 `이 장치로 돌아올 때만 음소거` 정책은 해당 장치가 기본 출력으로 선택되면 음소거하고, 다른 후보가 선택되면 음소거를 해제합니다. 모든 후보가 없으면 기본 출력 선택을 유지하고 조건부 음소거 변경을 생략합니다. 실제 미연결 장치는 선택형 규칙이면 건너뛰고, 필수 규칙이면 오류를 보고합니다.

연결 해제만으로는 로컬 상태로 돌아가지 않습니다. Sunshine의 세션이 끝날 때까지 원격 상태를 유지합니다. 종료 전환 중에는 Windows가 기본 출력을 다시 바꾸는 상황에 대비해 음소거 상태로 출력을 안정화합니다. 최소 5초, 최대 12초의 제한된 전환 검사이며, 복구 이후에는 로컬 출력과 볼륨을 주기적으로 강제하지 않습니다.

로컬 장치 재연결 복구는 기본적으로 꺼져 있습니다. 마지막 복구 기록을 보존하여 Agent 재시작 후에도 이후 재연결에 반응합니다. 재시작 자체를 재연결로 처리하지 않습니다. 실기 검증은 남아 있습니다.

### 설정과 인증정보

| 항목 | 보관 위치 |
| --- | --- |
| 설정 | `%LOCALAPPDATA%\AutoShine\settings.json` |
| 직전 설정 백업 | 같은 위치의 `settings.json.bak` |
| 진행 중인 오디오 세션 기록 | 같은 위치의 `audio-session.json` |
| 마지막 복구·진단 이벤트 | 같은 위치의 `last-recovery.json`, `agent.log` |
| Sunshine 계정과 인증서 핀 | Windows 자격 증명 관리자 |
| Sunshine 설정 적용 백업 | 원래 설정 파일 옆의 `.autoshine-…bak` |

내보내기에 Sunshine 계정과 비밀번호는 포함되지 않습니다. 장치 선택은 이름이 아닌 endpoint ID를 사용합니다. PC 변경·포맷 후 가져온 설정은 장치 ID를 다시 확인해야 합니다. 인증서가 바뀌면 종료 기능의 인증 연결을 다시 해야 합니다.

웹 서버는 `http://127.0.0.1:48191/`에서만 수신합니다. 변경 API는 동일 Origin과 프로세스별 토큰을 요구합니다. 외부 CDN이나 폰트 서버를 사용하지 않습니다.

## 소스에서 빌드

필수 도구는 Windows, **.NET SDK 10.0.401**, Node.js/npm, 설치 EXE 생성 시 Inno Setup 6.7 이상입니다. SDK 버전은 `global.json`, npm 의존성 버전은 `package-lock.json`으로 고정합니다.

```powershell
Set-Location AutoShine
.\build.ps1
.\package.ps1 -Iscc 'C:\path\to\ISCC.exe'
```

도구가 PATH에 없으면 경로를 지정할 수 있습니다.

```powershell
.\build.ps1 -Dotnet 'C:\path\to\dotnet.exe'
.\package.ps1 -Dotnet 'C:\path\to\dotnet.exe' -Iscc 'C:\path\to\ISCC.exe'
```

`build.ps1`은 UI 생성, 솔루션 빌드, Core·Windows 통합 테스트 및 두 실행 파일 게시를 수행합니다. `package.ps1`은 같은 빌드 과정을 거친 후 Inno Setup 설치 파일을 생성합니다.

| 출력 | 용도 |
| --- | --- |
| `artifacts/package/` | Agent와 웹 서버의 self-contained 실행 폴더 |
| `artifacts/installer/` | 새로 빌드한 설치 EXE |
| `releases/` | 보관한 최신 설치 파일; Git에는 포함하지 않음 |

실행은 `artifacts/package/AutoShine.exe`입니다. `--background`를 지정하면 브라우저를 열지 않고 트레이 Agent를 실행합니다. 웹 UI만 확인할 때는 `AutoShine.Web.exe --no-browser`를 사용할 수 있습니다. 다른 경로에서 같은 포트의 서버가 실행 중이면 먼저 정확한 실행 경로를 확인해 종료해야 합니다.

## 검증 범위와 남은 작업

GUI는 사용자 Lovable 프로토타입에서 가져온 React 화면으로 교체했습니다. 소스는 `ui/src/App.tsx`, 스타일은 `ui/src/styles.css`이며 `npm run build:ui`가 localhost 서버의 정적 파일을 생성합니다. 기존 renew.js GUI는 제거했습니다.

자동 검사 107개(Core 59, Windows 통합 17, UI 모델 22, API 5, i18n 4)를 통과했습니다. 사용자는 실제 Moonlight 동작 테스트 완료를 보고했습니다. 모든 PC와 장치 조합을 보장하는 것은 아닙니다. 설치 파일은 코드 서명되지 않았습니다.

## 라이선스

React·React DOM·Scheduler·Tailwind CSS·Lucide·애니메이션 CSS·로컬 폰트의 라이선스는 `ui/public/licenses/`에 보관하고 빌드 시 `wwwroot/licenses/`에 포함합니다. 프로젝트 자체의 공개 배포 라이선스는 아직 결정하지 않았습니다. 포함되지 않은 SoundVolumeView의 설치·사용·재배포 조건은 별도로 확인해야 합니다.
