import { clarified } from './clarified';
import { completed } from './completed';
export type Language = 'ko' | 'en';
let language: Language = 'ko';
export function setLanguage(value: Language) {language = value;}
const english: Record<string,string> = {
  '연동':'Integrate','연동 해제':'Unlink','연결 설정':'Connection settings',
  '스트리밍 시작·종료 시 설정한 오디오 동작을 실행합니다.':'Runs your configured audio actions when streaming starts and ends.',
  '종료 단축키·트레이로 스트리밍을 종료할 때 사용합니다.':'Used to end streaming with the keyboard shortcut or tray.',
  '이 PC의 키보드로 Moonlight 스트리밍을 종료합니다. 사용하려면 Sunshine 연동 카드에서 로그인하세요.':'End Moonlight streaming with this PC’s keyboard. Sign in under Sunshine integration to use this feature.',
  '로그인':'Sign in','로그인 해제':'Remove sign-in',
  '진행 중인 스트리밍은 시작 당시 설정을 유지합니다. Sunshine 연동은 설정 화면에서 별도로 반영합니다.':'Active streams keep their initial settings. Apply Sunshine integration separately in Settings.',
  '가 필요합니다. 설치된 파일은 자동으로 찾습니다.':' is required. Installed files are detected automatically.',
  '가상 디스플레이는 ':'Virtual displays require ', 'VDD 설치':'VDD installation', '가 필요합니다. 화면 전환은 Sunshine이 처리합니다.':'. Sunshine handles display switching.',
  '스트리밍에 사용할 디스플레이를 선택합니다.':'Choose the display to use for streaming.',
  '가상 디스플레이는 VDD 설치가 필요합니다. 화면 전환은 Sunshine이 처리합니다.':'Virtual displays require a VDD. Sunshine handles display switching.',
  '가상 디스플레이':'Virtual display',
  '스트리밍 디스플레이':'Streaming display','Sunshine에 적용':'Apply to Sunshine','적용 중':'Applying',
  'Sunshine에서 디스플레이 목록을 확인하지 못했습니다. Sunshine을 실행한 뒤 다시 확인하세요.':'Display list unavailable. Start Sunshine and check again.',
  '상태가 바뀌는 장치와 최종 출력을 표시합니다. 비활성 장치도 정책에 따라 상태가 바뀌면 처리 이유와 함께 표시합니다.':'Shows device state changes and the final output. Disabled devices appear only when their state changes, with the reason for the action.',
  '목록에서 숨기기 (정책 유지)':'Hide from list (keep policy)',
  '적용되지 않은 변경':'Unsaved changes:',
  '건 — 자동 저장되지 않습니다. ‘저장 및 적용’을 눌러 저장합니다. 진행 중인 세션은 시작 당시 정책을 유지합니다.':'— press Save and apply. Active sessions keep their initial policy.',
  '· 서버:':'· Server:','· 관리 주소: https://127.0.0.1:':'· Management URL: https://127.0.0.1:',
  '실행 파일을 확인했습니다. 실제 장치 제어 검증은 스트리밍 실기 검증에서 진행합니다.':'Executable verified. Actual device control must be verified during a real streaming session.',
  '테마':'Theme','언어':'Language','다크':'Dark','라이트':'Light','한국어':'한국어','주 메뉴':'Main menu',
  '스트리밍 시작':'Streaming start','스트리밍 종료':'Streaming end','시뮬레이션':'Simulation','설정':'Settings',
  '원격 오디오':'Remote audio','세션':'Session','세션 유지 중':'Session active','확인 안 됨':'Unknown','확인되지 않음':'Unknown',
  '대기 중':'Idle','스트리밍 중':'Streaming','연결 끊김':'Disconnected','연동 적용됨':'Integrated','연동 안 됨':'Not integrated',
  '연결 실패':'Connection failed','실행 중':'Running','응답 지연':'Delayed response','실행 안 됨':'Not running','준비됨':'Ready',
  '장치 비활성화됨':'Device disabled','찾을 수 없음':'Not found','소리 끔':'Mute','비활성화':'Disable','활성화':'Enable',
  '시작 전 상태 복원':'Restore previous state','현재 상태 유지':'Keep current state','직접 지정':'Custom',
  '세션 종료 시에만 로컬로 복귀합니다.':'Local audio returns only when the session ends.',
  '화면 검증 서버입니다. 설정은 임시 파일에 저장되며 실제 Agent에 반영되지 않습니다. Sunshine 설정과 인증 변경은 차단됩니다.':'UI preview server. Settings are saved to a temporary file and are not applied to the Agent. Sunshine configuration and authentication changes are blocked.',
  'Agent 상태를 확인할 수 없습니다. 저장된 설정은 다음 실행 시 사용됩니다.':'Agent status is unavailable. Saved settings will be used on its next launch.',
  '설정과 상태를 확인하고 있습니다. 계속 표시되면 다시 불러오세요.':'Loading settings and status. Reload if this message persists.',
  '다시 불러오기':'Reload','설정과 장치 상태를 확인한 뒤 다시 시도하세요.':'Check settings and device status, then retry.',
  '저장·적용 실패:':'Save failed:','해결 방법:':'Suggested action:','저장 및 적용 중…':'Saving…','저장된 설정과 일치합니다':'Matches saved settings',
  '수동 저장':'Manual save','되돌리기':'Revert','다시 시도':'Retry','저장 및 적용':'Save and apply','편집됨 · 미적용':'Edited · Not applied',
  '출력':'Output','비활성':'Disabled','연결됨':'Connected','미연결':'Disconnected','적용된 값:':'Saved value:',
  '미연결 장치 추가':'Add a disconnected device','추가할 미연결 장치':'Disconnected device to add','장치 선택':'Select a device','추가':'Add',
  '기존 정책은 숨겨도 유지됩니다. 설정할 장치를 선택해서 목록에 추가하세요.':'Hidden devices retain their policies. Select a device to add it to the list.',
  '아직 확인하지 않았습니다':'Not checked yet',
  'Agent가 실행되면 Steam Streaming Speakers를 자동으로 찾습니다. 상단 Agent 상태를 확인하세요.':'The Agent automatically locates Steam Streaming Speakers. Check the Agent status in the header.',
  'Steam Streaming Speakers 준비됨':'Steam Streaming Speakers is ready',
  '스트리밍이 시작되면 이 장치로 소리를 보냅니다. 별도 선택은 필요 없습니다.':'Audio is sent to this device when streaming starts. No manual selection is needed.',
  '장치가 비활성화되어 있습니다':'Device is disabled',
  '스트리밍 시작 시 이 장치를 활성화합니다. SoundVolumeView 경로가 유효한지 설정에서 확인하세요.':'This device is enabled when streaming starts. Check the SoundVolumeView path in Settings.',
  '장치를 찾을 수 없습니다':'Device not found',
  'Windows 소리 설정에서 Steam Streaming Speakers의 연결·드라이버 상태를 확인하세요. 이름이 같은 장치가 여러 개이면 자동으로 선택하지 않습니다.':'Check Steam Streaming Speakers in Windows sound settings. Devices with duplicate names are not selected automatically.',
  '전체':'All devices','선택 장치':'Selected devices','사용 안 함':'Off','차단 안 함':'Not blocked',
  'Moonlight로 접속해 스트리밍이 시작될 때 원격 오디오를 준비하고 집의 스피커에서 소리가 나지 않게 합니다.':'Prepare remote audio and block local speakers when Moonlight starts streaming.',
  '원격 오디오 장치는 자동으로 찾아 사용합니다.':'The remote audio device is selected automatically.',
  '로컬 소리 차단':'Block local audio',
  '스트리밍 중 차단할 로컬 장치와 장치별 방법을 정합니다. 소리 끔는 장치를 켜둔 채 소리만 끄고, 비활성화는 Windows에서 장치를 끕니다.':'Choose which local devices to block. Mute keeps the device enabled; Disable turns it off in Windows.',
  '로컬 장치를 차단하지 않습니다. 스트리밍 중에도 집의 스피커에서 소리가 날 수 있습니다.':'Local devices are not blocked. Local speakers may play audio during streaming.',
  '세션이 실제로 종료되었을 때만 아래 동작을 실행합니다.':'These actions run only when the session actually ends.',
  '연결 끊김은 종료가 아닙니다.':'Disconnection does not end the session.',
  '연결이 끊겨도 시간이 지났다는 이유로 종료하거나 로컬로 복귀하지 않습니다. 끊긴 동안 원격 설정을 유지하고, Moonlight에서 종료하거나 Sunshine이 세션 종료를 알릴 때만 복귀합니다.':'Elapsed time after a disconnect does not trigger local audio restoration. Remote settings remain until Moonlight or Sunshine ends the session.',
  '복귀 방식':'Restore method','시작 전 기본 출력':'Previous default output','장치 우선순위':'Device priority',
  '비활성화를 직접 지정한 장치는 우선순위에서 제외합니다. 복귀 대상으로 선택한 장치는 활성화합니다.':'Explicitly disabled devices are excluded. The selected return device is enabled.',
  '복귀 출력 장치 우선순위':'Return output priority','위에서부터 연결된 첫 번째 장치를 기본 출력으로 정합니다.':'The first connected device in this list becomes the default output.',
  '복귀 예상 대상':'Expected return output','위로':'Move up','아래로':'Move down','삭제':'Remove',
  '사용 가능한 복귀 후보가 없습니다. 기본 출력은 변경하지 않으며 장치별 종료 정책은 적용됩니다.':'No return output is available. The default output stays unchanged; per-device end policies still apply.',
  '장치별 종료 동작':'Per-device end actions','장치마다 종료 후 활성화·소리 끔 상태를 정합니다. 모든 장치를 일괄 복원하지 않습니다.':'Choose the enabled and muted state for each device after the session ends.',
  '장치':'Device','기존 정책 유지':'Keep legacy policy','소리':'Audio','소리 켬':'Unmute','복귀 출력일 때만 소리 끔':'Mute only when selected as return output',
  '스트리밍 시작 직전의 활성화·소리 끔 상태로 되돌립니다.':'Restore the enabled and muted state from before streaming.',
  '종료 진입 시 상태를 유지합니다. 복귀 대상으로 선택되면 활성화됩니다.':'Keep the state at session end. Enable if selected as the return output.',
  '종료 시 지정한 상태로 설정합니다.':'Apply the specified state when the session ends.',
  '실제 오디오는 바꾸지 않습니다. 실제 Agent와 같은 오디오 정책 엔진을 가상 장치에 실행합니다. 가상 초기 상태는 현재 장치 활성화 상태·모두 소리 켬·첫 활성 로컬 장치를 기본 출력으로 가정합니다. 시작 후에는 시작 당시 설정으로 종료합니다.':'Runs the Agent audio policy engine on virtual devices without changing real audio. Initial state assumes current enabled states, all devices unmuted, and the first enabled local device as default. End uses the policy captured at start.',
  '이벤트':'Events','기준 설정':'Settings source','편집 중 설정':'Draft settings','적용된 설정':'Saved settings','시작':'Start','재접속':'Reconnect','종료':'End','초기화':'Reset',
  '가상 장치 연결 상태':'Virtual device connections','장치를 연결하거나 빼서 결과가 어떻게 달라지는지 확인하세요.':'Toggle virtual connections to compare results.',
  '예상 결과':'Expected results','이벤트를 실행하면 단계가 여기에 표시됩니다.':'Run an event to display its steps here.',
  '정보':'Info','건너뜀':'Skip','유지':'Keep','경고':'Warning',
  '오디오 도구, Sunshine 연동과 종료 단축키를 관리합니다. 설정 값은 저장해야 반영되며 확인·연동·인증 버튼은 각각 실행됩니다.':'Manage the audio tool, Sunshine integration, and end shortcut. Save edited settings; check, integration, and authentication buttons run their own actions.',
  '장치 활성화·비활성화와 기본 출력 변경에 사용하는 별도 도구입니다. 압축을 풀어 준비하세요. 설치 파일에는 포함되지 않습니다.':'External tool for enabling devices and changing default output. Extract it before use. It is not bundled with the installer.',
  '실행 파일 경로':'Executable path','SoundVolumeView.exe 전체 경로':'Full path to SoundVolumeView.exe','자동 탐색':'Auto-detect','사용 가능 확인':'Check availability','공식 다운로드 안내':'Official download',
  'Sunshine 설정에 세션 시작·종료 명령을 추가합니다. 기존 운영 자동화가 있으면 이전 절차를 먼저 수행해야 합니다.':'Add session start and end commands to Sunshine. Migrate any existing automation before applying.',
  '세션 이벤트 연동:':'Session integration:','적용됨':'Applied','서버:':'Server:','설정 파일 경로':'Config file path','기본 포트':'Base port','연동 확인':'Check integration','적용 내용 확인':'Preview changes','연동 해제 내용 확인':'Preview removal','확인한 변경 적용':'Apply reviewed changes',
  '화면 연동':'Display integration','화면 전환은 Sunshine이 수행합니다. 아래 정책은 Sunshine 연동 적용 시 반영됩니다.':'Sunshine handles display switching. These policies take effect when integration is applied.',
  'Sunshine 화면 ID':'Sunshine display ID','확인만':'Verify only','주 화면':'Primary display','지정 화면만':'Selected display only','클라이언트 해상도 사용':'Use client resolution','클라이언트 주사율 사용':'Use client refresh rate',
  '종료 단축키 (선택)':'End shortcut (optional)','호스트 PC에서 세션 종료를 요청합니다. 기본값은 꺼짐입니다.':'Request session end from the host PC. Disabled by default.',
  '단축키 사용':'Enable shortcut','등록 상태:':'Registration:','Agent에 등록됨':'Registered with Agent','Agent 등록 조합이 저장값과 다름':'Agent shortcut differs from saved value','등록되지 않음':'Not registered','단축키 키 조합 입력':'Record shortcut keys','키 조합을 누르세요 · Esc 취소':'Press shortcut keys · Esc to cancel','클릭하여 키 조합 지정':'Click to record shortcut',
  'Ctrl·Alt·Shift와 문자·숫자·F1~F12 조합 또는 ScrollLock·Pause를 사용하세요.':'Use Ctrl, Alt or Shift with a letter, number or F1–F12, or use ScrollLock or Pause.',
  'Sunshine 종료 인증':'Sunshine end authentication','단축키·트레이 종료 요청에만 필요합니다. 세션 이벤트 연동과 별개이며, 인증 정보는 Windows 자격 증명 관리자에 보관합니다.':'Required only for shortcut and tray end requests. Separate from session integration. Credentials are kept in Windows Credential Manager.',
  '이 포트의 인증:':'Authentication for this port:','등록됨':'Registered','등록 안 됨':'Not registered','관리 주소:':'Management URL:','인증서 지문 확인':'Check certificate fingerprint','이 포트의 인증 삭제':'Remove authentication','확인한 지문:':'Verified fingerprint:',
  'Sunshine 사용자 이름':'Sunshine username','사용자 이름':'Username','Sunshine 비밀번호':'Sunshine password','비밀번호':'Password','인증 등록':'Register authentication',
  '설정 가져오기·내보내기':'Import and export settings','저장된 설정을 내보냅니다. 가져온 설정은 내용을 확인하고 저장해야 반영됩니다. 인증 정보는 포함되지 않습니다.':'Export saved settings. Review and save imported settings to apply them. Credentials are excluded.',
  '설정 내보내기':'Export settings','설정 가져오기':'Import settings','진단 정보':'Diagnostics','Agent 이벤트와 복구 기록 유무를 내보냅니다. 인증 정보는 포함되지 않습니다.':'Export Agent events and recovery record status. Credentials are excluded.','진단 정보 내보내기':'Export diagnostics',
  '설정을 불러왔습니다. 아직 저장하지 않았습니다. 내용을 확인한 뒤 저장하세요.':'Settings imported but not saved. Review them, then save.',
  '실행 파일을 찾았습니다. 저장해야 경로가 반영됩니다.':'Executable found. Save to apply the path.',
  'SoundVolumeView를 찾지 못했습니다. 준비 후 실행 파일 경로를 지정하세요.':'SoundVolumeView was not found. Extract it and specify the executable path.',
  '사용자 이름·비밀번호와 확인한 인증서 지문이 필요합니다.':'Username, password and a verified certificate fingerprint are required.',
  '아래 인증서 지문을 확인한 뒤 인증을 등록하세요. 인증은 종료 요청용이며 세션 이벤트 연동과 별개입니다.':'Verify the fingerprint below before registering. Authentication is for end requests and is separate from session integration.',
};
const catalog = Object.fromEntries(Object.entries({...english,...clarified,...completed}).map(([key,value])=>[key.trim(),value]));
const messageParts = Object.entries(completed).filter(([key])=>key.length >= 6 && key.endsWith('.')).sort((a,b)=>b[0].length-a[0].length);
export function translate(value: string): string {
  if (language === 'ko') return value;
  const trimmed = value.trim();
  const translated = catalog[trimmed];
  if (translated) return value.replace(trimmed, translated);
  let result = value;
  for (const [key, text] of messageParts) {
    if (key.length >= 6 && key.endsWith('.')) result = result.split(key).join(text);
  }
  return result
    .replace(/서버 응답을 해석할 수 없습니다\. 오류 코드:/g, catalog['서버 응답을 해석할 수 없습니다. 오류 코드:'])
    .replace(/요청에 실패했습니다\. 오류 코드:/g, catalog['요청에 실패했습니다. 오류 코드:'])
    .replace(/등록할 수 없습니다:/g, 'Cannot register:')
    .replace(/SoundVolumeView 경로를 지정하세요:/g, 'Specify the SoundVolumeView path:')
    .replace(/중복 Sunshine 설정:/g, catalog['중복 Sunshine 설정:'])
    .replace(/ · 백업:/g, ' · Backup:')
    .replace(/ · Sunshine을 재시작하세요\./g, ' · Restart Sunshine.')
    .replace(/저장됨 · 자동화 프로그램 반영 확인 필요/g, 'saved · Agent verification required')
    .replace(/저장·적용 실패:/g, 'Save failed:')
    .replace(/해결 방법:/g, 'Suggested action:')
    .replace(/저장하지 않은 변경/g, 'Unsaved changes:')
    .replace(/건 · 설정 저장을 누르세요\./g, '· press Save settings.')
    .replace(/적용되지 않은 변경 (\d+)건 — 자동 저장되지 않습니다\. ‘저장 및 적용’을 눌러 저장합니다\. 진행 중인 세션은 시작 당시 정책을 유지합니다\./g, '$1 unsaved changes — press Save and apply. Active sessions keep their initial policy.')
    .replace(/저장됨 · Agent 반영 확인 필요/g, 'saved · Agent verification required')
    .replace(/ · 적용된 값:/g, ' · Saved value:')
    .replace(/ · AutoShine 세션 연동 있음/g, ' · AutoShine integration present')
    .replace(/ · AutoShine 세션 연동 없음/g, ' · No AutoShine integration');
}


