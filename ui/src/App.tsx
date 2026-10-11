/** @jsxImportSource ./i18n */
import { api, setToken } from "./api.mjs";
import { setLanguage, translate, type Language } from './i18n/translate';
import { simulationSteps,displayStates,type SimulatedDisplay } from './simulation';
import { fromBackend, toBackend, hotkeyValue, remoteBinding, fingerprintMatches, visibleByDefault, type Config, type Device, type EndRule, type Backend } from "./model";
import { createContext, useContext, useEffect, useRef, useState, useImperativeHandle, type Ref, type ReactNode } from "react";
import {
  Sun, Plug, Cpu, Radio, ArrowUp, ArrowDown, X, Plus, Play, Square, RotateCcw,
  ChevronRight, CheckCircle2, AlertTriangle, XCircle, HelpCircle, Headphones,
  Settings2, Eye, EyeOff, LogIn, LogOut, Keyboard, ShieldCheck, FlaskConical, Unplug,
  Save, Info, Download, Upload, FileText, KeyRound, Trash2,
  Power, PowerOff, Volume2, VolumeX, Monitor, FolderOpen, Moon, Globe, Menu,
} from "lucide-react";

/* ---------- types & data ---------- */
type Health = "unknown" | "ok" | "warn" | "error";
type Session = "unknown" | "idle" | "streaming" | "disconnected";
type BlockAction = "mute" | "disable";
type EndMode = "restore" | "keep" | "custom";
let REMOTE = { id: "", name: "Steam Streaming Speakers" };
let LOCAL: Device[] = [];
let REMOTE_DEVICE: Device | null = null;
const DeviceVisibility = createContext<{added: string[]; add: (id: string) => void; hide: (id: string)=>void}>({added: [], add: () => {}, hide: () => {}});
function useVisibleDevices() {
  const {added} = useContext(DeviceVisibility);
  return LOCAL.filter(d => visibleByDefault(d) || added.includes(d.id));
}
const dn = (id: string) => (id === REMOTE.id ? REMOTE.name : LOCAL.find((d) => d.id === id)?.name ?? id);
const SESSION_LABEL: Record<Session, string> = { unknown: "확인 안 됨", idle: "스트리밍 세션 없음", streaming: "스트리밍 중", disconnected: "연결 끊김" };
const ACTION_LABEL: Record<BlockAction, string> = { mute: "소리 끔", disable: "비활성화" };
const defRule: EndRule = { mode: "restore", enable: true, mute: false };

const NAV = [
  { id: "start", label: "스트리밍 시작", icon: LogIn },
  { id: "end", label: "스트리밍 종료", icon: LogOut },
  { id: "display", label: "디스플레이", icon: Monitor },
  { id: "sim", label: "시뮬레이션", icon: Eye },
  { id: "settings", label: "설정", icon: Settings2 },
] as const;
type Tab = (typeof NAV)[number]["id"];

type SaveState = { kind: "idle" } | { kind: "saving" } | { kind: "ok"; at: string; notice?:string } | { kind: "error"; cause: string; fix: string };
function saveErrorFix(message:string) {
  if(/changed|변경되/i.test(message))return '파일이 변경되었습니다. 편집 내용을 확인하고 페이지를 새로고침하세요.';
  if(/streaming|스트리밍|세션/i.test(message))return '스트리밍을 종료한 뒤 다시 저장하세요.';
  if(/denied|권한|취소/i.test(message))return '파일 권한을 확인하고 관리자 권한 요청을 승인하세요.';
  if(/프리셋|주사율|해상도/i.test(message))return '디스플레이에서 표시된 입력 오류를 수정하세요.';
  if(/파일|경로|file|path/i.test(message))return '설정에서 파일 경로를 확인하세요.';
  return '표시된 오류 내용을 확인하고 다시 저장하세요.';
}
const stamp = () => new Date().toLocaleTimeString("ko-KR", { hour12: false });
const eq = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);
function download(name: string, text: string, type = "application/json") {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const a = document.createElement("a"); a.href = url; a.download = name; a.click(); URL.revokeObjectURL(url);
}

type Environment = { token: string; settings: Backend; settingsError?: string; integrated: boolean; paired: boolean; isolatedPreview?: boolean;
  sunshineConfigExists?:boolean; vddInstalled?:boolean; soundVolumeViewInstalled?:boolean;
  displays?:{name:string;enabled:boolean;virtual:boolean}[];
  sunshineDisplays: {id:string; name:string; virtual:boolean;displayName?:string}[];
  audio: {id: string; name: string; state: string}[]; sunshine: {reachable: boolean; appId: number | null; basePort: number; message: string; configPath: string | null; deviceId?: string} };
type AgentState = { reachable: boolean; message?: string; state?: {activeSession: boolean; hotkey: Backend['hotkey'] | null; error: string} };
function updateDevices(e: Environment) {
  const remote = remoteBinding(e.audio, e.settings.audio.remoteOutput);
  REMOTE = { id: remote?.id ?? '', name: remote?.name ?? 'Steam Streaming Speakers' };
  const endpoint = e.audio.find(d => d.id === REMOTE.id);
  REMOTE_DEVICE = REMOTE.id ? {...REMOTE, kind:'출력', state:endpoint?.state ?? 'absent', connected:endpoint?.state === 'active' || endpoint?.state === 'disabled'} : null;
  const known = [...e.settings.audio.rules.map(r => r.device), ...e.settings.audio.localOutputPriority, ...LOCAL];
  LOCAL = e.audio.filter(d => d.id !== REMOTE.id && d.name !== REMOTE.name).map(d => ({...d, kind: d.state === 'disabled' ? '비활성' : '출력', connected: d.state === 'active' || d.state === 'disabled'}));
  for (const d of known) if (d.id !== REMOTE.id && !LOCAL.some(x => x.id === d.id)) LOCAL.push({...d, kind: '출력', connected: false, state: 'absent'});
}
export default function App() {
  const [preferences, setPreferences] = useState(() => {
    try { const saved = localStorage.getItem('autoshine-ui') ?? localStorage.getItem('companion-ui'); if (saved && !localStorage.getItem('autoshine-ui')) { localStorage.setItem('autoshine-ui', saved); localStorage.removeItem('companion-ui'); } const p = JSON.parse(saved || '{}'); return {theme: p.theme === 'light' ? 'light' : 'dark', language: p.language === 'en' ? 'en' : 'ko' as Language, added: Array.isArray(p.added) ? p.added.filter((id: unknown) => typeof id === 'string') as string[] : []}; }
    catch { return {theme:'dark', language:'ko' as Language, added:[] as string[]}; }
  });
  setLanguage(preferences.language as Language);
  useEffect(() => {
    document.documentElement.dataset.theme = preferences.theme;
    document.documentElement.lang = preferences.language;
    try {localStorage.setItem('autoshine-ui', JSON.stringify(preferences));} catch { /* Local storage can be denied by the browser. */ }
  }, [preferences]);
  const [tab, setTab] = useState<Tab>('start');
  const [soundOpen,setSoundOpen]=useState(()=>matchMedia('(min-width:768px)').matches);
  const [menuOpen,setMenuOpen]=useState(false);
  const menuRef=useRef<HTMLElement>(null);
  const menuButtonRef=useRef<HTMLButtonElement>(null);
  useEffect(()=>{
    if(!menuOpen)return;
    const outside=(e:PointerEvent)=>{if(!menuRef.current?.contains(e.target as Node)&&!menuButtonRef.current?.contains(e.target as Node))setMenuOpen(false);};
    const escape=(e:KeyboardEvent)=>{if(e.key==='Escape'){setMenuOpen(false);menuButtonRef.current?.focus();}};
    const wide=matchMedia('(min-width:768px)');
    const resize=()=>{if(wide.matches)setMenuOpen(false);};
    document.addEventListener('pointerdown',outside);document.addEventListener('keydown',escape);wide.addEventListener('change',resize);
    return()=>{document.removeEventListener('pointerdown',outside);document.removeEventListener('keydown',escape);wide.removeEventListener('change',resize);};
  },[menuOpen]);
  const selectTab=(next:Tab)=>{setTab(next);setMenuOpen(false);if(menuOpen)menuButtonRef.current?.focus();};
  const mainRef = useRef<HTMLElement>(null);
  useEffect(()=>{mainRef.current?.scrollTo({top:0});},[tab]);
  const [draft, setDraft] = useState<Config | null>(null);
  const [applied, setApplied] = useState<Config | null>(null);
  const [env, setEnv] = useState<Environment | null>(null);
  const [agentState, setAgentState] = useState<AgentState>({reachable: false});
  const vddEditor=useRef<VddEditorHandle>(null);
  const [vddChanged,setVddChanged]=useState(false);
  const [save, setSave] = useState<SaveState>({kind: 'idle'});
  const [error, setError] = useState('');
  const refresh = async () => {
    const [e, agent]: [Environment, AgentState] = await Promise.all([api('environment'), api('agent/status').catch(()=>({reachable:false}))]); setToken(e.token); updateDevices(e); setEnv(e);
    if (!e.settingsError) {
      const loaded=fromBackend(e.settings,LOCAL);
      setApplied(previous=>previous??loaded);
      setDraft(previous=>previous??loaded);
    }
    setAgentState(agent);setError('');
    return e;
  };
  useEffect(() => {
    let active = true;
    refresh().then(e => { if (!active) return; if (e.settingsError) throw Error(e.settingsError); const c = fromBackend(e.settings, LOCAL); setApplied(c); setDraft(c); }).catch(e => {if(active) setError(e.message);});
    const timer = setInterval(() => { refresh().catch(() => {if(active) {setError('상태 갱신 실패 · 마지막 조회 결과 표시 중 · 자동 재시도');}}); }, 5000);
    return () => { active = false; clearInterval(timer); };
  }, []);
  const set = <K extends keyof Config>(k: K, v: Config[K]) => setDraft(d => d && ({...d, [k]: v}));
  const settingsChanged = draft && applied ? Object.keys(draft).filter(k => !eq(draft[k as keyof Config], applied[k as keyof Config])).length : 0;
  const changed=settingsChanged+(vddChanged?1:0);
  useEffect(() => {
    if (save.kind !== 'ok') return;
    const timer = setTimeout(() => setSave({kind:'idle'}), 3000);
    return () => clearTimeout(timer);
  }, [save]);
  const doSave = async () => {
    if (!draft) return; setSave({kind:'saving'});
    try {
      let notice='';
      const b = toBackend(draft, LOCAL, REMOTE.id ? REMOTE : null);
      if(settingsChanged) await api('settings/validate', b);
      if(draft.backend.display.sunshineDeviceId && draft.backend.display.sunshineDeviceId!==applied?.backend.display.sunshineDeviceId && draft.backend.display.sunshineDeviceId.toLowerCase()!==env?.sunshine.deviceId?.toLowerCase()) {const plan=await api('sunshine/display-plan',b);await api('sunshine/apply',{id:plan.id});notice='Sunshine을 다시 시작하면 화면 설정이 반영됩니다.';}
      if(vddChanged) {await vddEditor.current?.save();notice=notice?notice+' '+translate('VDD 파일을 저장했습니다. 드라이버 반영을 확인하세요.'):'VDD 파일을 저장했습니다. 드라이버 반영을 확인하세요.';}
      if(settingsChanged) await api('settings', b);
      const next = {...draft, backend:b}; setDraft(current=>eq(current,draft)?next:current); setApplied(next);
      setSave({kind:'ok', at:stamp(),notice}); await refresh().catch(()=>{});
    } catch(e) { setSave({kind:'error', cause:(e as Error).message, fix:saveErrorFix((e as Error).message)}); }
  };
  const sunshine: Health = error ? 'warn' : !env ? 'unknown' : !env.sunshine.reachable ? 'error' : env.integrated ? 'ok' : 'warn';
  const agent: Health = agentState.state?.error ? 'warn' : agentState.reachable ? 'ok' : 'unknown';
  const device = env?.audio.find(d => d.id === REMOTE.id);
  const remote: Health = !env ? 'unknown' : !device ? 'error' : device.state === 'disabled' ? (agentState.state?.activeSession || (env.sunshine.reachable && (env.sunshine.appId ?? 0) > 0) ? 'warn' : 'ok') : device.state === 'active' ? 'ok' : 'error';
  // Sunshine currentgame indicates a session, not whether a Moonlight client is connected.
  const session: Session = env?.sunshine.reachable ? env.sunshine.appId === 0 && !agentState.state?.activeSession ? 'idle' : 'streaming' : 'unknown';
  return <DeviceVisibility.Provider value={{added: preferences.added, add: id => setPreferences(p => ({...p,added:[...new Set([...p.added,id])]})), hide: id=>setPreferences(p=>({...p,added:p.added.filter(x=>x!==id)}))}}><div className="flex h-dvh flex-col overflow-hidden">
    <StatusBar menuOpen={menuOpen} menuButtonRef={menuButtonRef} onMenu={()=>setMenuOpen(v=>!v)} {...{sunshine,agent,remote,session}} notice={error} agentNotice={agentState.state?.error || agentState.message} theme={preferences.theme} language={preferences.language as Language} onTheme={theme => setPreferences(p=>({...p,theme}))} onLanguage={language => setPreferences(p=>({...p,language}))} />
    <div className="flex min-h-0 flex-1 flex-col md:flex-row">
      <nav ref={menuRef} id="main-navigation" aria-label="주 메뉴" className={`navigation-panel ${menuOpen?'mobile-open':''} shrink-0 flex-col border-border p-3 md:w-56 md:border-r`}>
        <div className="main-navigation flex items-start gap-1 overflow-x-auto md:flex-col md:items-stretch">
          <div className="shrink-0">
            <button aria-expanded={soundOpen} onClick={()=>setSoundOpen(v=>!v)} className={`flex w-full items-center gap-2 whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors ${tab==='start'||tab==='end'?'text-primary':'text-muted-foreground'} hover:bg-secondary`}><Volume2 className="h-4 w-4"/>사운드<ChevronRight className={`ml-auto h-4 w-4 transition-transform ${soundOpen?'rotate-90':''}`}/></button>
            {soundOpen && <div className="sound-submenu ml-4 mt-1 flex flex-col gap-1 border-l border-border pl-2">{NAV.filter(n=>n.id==='start'||n.id==='end').map(n=><button key={n.id} aria-current={tab===n.id?'page':undefined} onClick={()=>selectTab(n.id)} className={`flex items-center gap-2 whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors ${tab===n.id?'bg-accent text-primary':'text-muted-foreground hover:bg-secondary hover:text-foreground'}`}><n.icon className="h-4 w-4"/>{n.label}</button>)}</div>}
          </div>
          {NAV.filter(n=>n.id!=='start'&&n.id!=='end').map(n=><button key={n.id} aria-current={tab===n.id?'page':undefined} onClick={()=>selectTab(n.id)} className={`flex items-center gap-2 whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors ${tab===n.id?'bg-accent text-primary':'text-muted-foreground hover:bg-secondary hover:text-foreground'}`}><n.icon className="h-4 w-4"/>{n.label}</button>)}
        </div>
        <div className="sidebar-status mt-auto pt-6"><StatusIndicators {...{sunshine,agent,remote}} vdd={!env?'unknown':env.vddInstalled?'ok':'error'} tool={!env?'unknown':env.soundVolumeViewInstalled?'ok':'error'} notice={error} agentNotice={agentState.state?.error || agentState.message}/></div>
      </nav>
      <main ref={mainRef} className={"min-h-0 min-w-0 flex-1 overflow-y-auto overscroll-contain p-5 md:p-8 "+(changed > 0 || save.kind !== 'idle' ? 'has-save-bar' : '')}><div className="content-container mx-auto max-w-4xl"><fieldset disabled={save.kind==='saving'} className="min-w-0">
        {(!draft || !applied || !env) && <div className="animate-pulse rounded-lg border border-border bg-card p-5"><h1 className="text-xl font-semibold">{NAV.find(n=>n.id===tab)?.label}</h1><div className="mt-5 h-20 rounded bg-muted" /></div>}
        {draft && applied && env && <>
        {tab === 'start' && <StartPage c={draft} a={applied} set={set} remote={remote} />}
        {tab === 'end' && <EndPage c={draft} a={applied} set={set} />}
        {tab === 'sim' && <Simulator draft={draft} applied={applied} env={env} />}
        {tab === 'display' && <DisplayPage c={draft} a={applied} set={set} env={env} />}
        <div hidden={tab!=='display'}><VddPresetEditor isolated={!!env.isolatedPreview} editorRef={vddEditor} onDirty={setVddChanged}/></div>
        {tab === 'settings' && <SettingsPage c={draft} a={applied} set={set} setDraft={setDraft} env={env} refresh={refresh} agentState={agentState} />}
        </>}
      </fieldset></div></main>
    </div>
    {draft && applied && (changed > 0 || save.kind !== 'idle') && <SaveBar changed={changed} save={save} onSave={doSave} onRevert={() => {setDraft(applied);vddEditor.current?.revert();setSave({kind:'idle'});}} />} 
  </div></DeviceVisibility.Provider>;
}

/* ---------- status ---------- */
function LoginStartupSection() {
  const [enabled,setEnabled]=useState<boolean|null>(null);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  useEffect(()=>{let active=true;api('startup').then(r=>{if(active)setEnabled(r.enabled);}).catch(e=>{if(active)setError(e.message);});return()=>{active=false;};},[]);
  const toggle=async()=>{setBusy(true);setError('');try{const r=await api('startup',{enabled:!enabled});setEnabled(r.enabled);}catch(e){setError((e as Error).message);}finally{setBusy(false);}};
  return <Section title="시작프로그램" desc="Windows 로그인 시 AutoShine을 실행합니다. 변경 즉시 적용됩니다.">
    <div className="flex items-center justify-between gap-3 rounded-md border border-border p-4"><span className="text-sm">로그인 시 자동 실행</span><button type="button" role="switch" aria-label="로그인 시 자동 실행" aria-checked={enabled===true} disabled={busy||enabled===null} onClick={toggle} className={`relative h-6 w-11 shrink-0 rounded-full disabled:opacity-50 ${enabled?'bg-primary':'bg-muted'}`}><span className={`absolute top-0.5 h-5 w-5 rounded-full bg-foreground ${enabled?'left-5.5':'left-0.5'}`}/></button></div>
    {error&&<p role="alert" className="mt-3 text-sm text-destructive">{error}</p>}
  </Section>;
}
function HealthIcon({ h, className = "h-4 w-4" }: { h: Health; className?: string }) {
  if (h === "ok") return <CheckCircle2 className={`${className} text-success`} />;
  if (h === "warn") return <AlertTriangle className={`${className} text-warning`} />;
  if (h === "error") return <XCircle className={`${className} text-destructive`} />;
  return <HelpCircle className={`${className} text-muted-foreground`} />;
}
const STATUS_TEXT = {
  sunshine: { unknown: "확인되지 않음", ok: "연동됨", warn: "연동 안 됨", error: "연결 실패" },
  agent: { unknown: "확인되지 않음", ok: "실행 중", warn: "응답 지연", error: "실행 안 됨" },
  remote: { unknown: "확인되지 않음", ok: "스트리밍 오디오 장치 확인됨", warn: "장치 비활성화됨", error: "찾을 수 없음" },
} as const;

function StatusIndicators(p: {notice?:string; agentNotice?:string; sunshine:Health; agent:Health; remote:Health; vdd:Health; tool:Health}) {
  const items = [
    {key:'sunshine',icon:Plug,label:'Sunshine',h:p.sunshine,text:STATUS_TEXT.sunshine[p.sunshine]},
    {key:'agent',icon:Cpu,label:'AutoShine',h:p.agent,text:STATUS_TEXT.agent[p.agent]},
    {key:'remote',icon:Radio,label:'스트리밍 오디오',h:p.remote,text:STATUS_TEXT.remote[p.remote]},
    {key:'vdd',icon:Monitor,label:'VDD',h:p.vdd,text:p.vdd==='unknown'?'확인되지 않음':p.vdd==='ok'?'장치 확인됨':'찾을 수 없음'},
    {key:'tool',icon:Volume2,label:'SoundVolumeView',h:p.tool,text:p.tool==='unknown'?'확인되지 않음':p.tool==='ok'?'파일 확인됨':'찾을 수 없음'},
  ];
  return <div className="flex flex-col gap-2">{items.map(i=><div key={i.key} title={i.key==='agent'?p.agentNotice:p.notice||undefined} className="rounded-md border border-border bg-secondary px-2.5 py-2 text-xs">
    <div className="status-card-row text-muted-foreground"><i.icon className="h-3.5 w-3.5"/><span>{i.label}</span></div>
    <div className="status-card-row mt-1"><HealthIcon h={i.h} className="h-3.5 w-3.5"/><span>{i.text}</span></div>
  </div>)}</div>;
}

function StatusBar(p: { menuOpen:boolean; menuButtonRef:Ref<HTMLButtonElement>; onMenu:()=>void; notice?:string; agentNotice?:string; sunshine: Health; agent: Health; remote: Health; session: Session; theme: string; language: Language; onTheme: (v: string)=>void; onLanguage: (v: Language)=>void }) {
  const [languageOpen,setLanguageOpen]=useState(false);
  return (
    <header className="app-header z-20 shrink-0 border-b border-border bg-card/95 backdrop-blur">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-2 px-5 py-3">
        <button ref={p.menuButtonRef} type="button" aria-label={p.menuOpen?"메뉴 닫기":"메뉴 열기"} aria-expanded={p.menuOpen} aria-controls="main-navigation" onClick={p.onMenu} className="rounded-md p-2 text-muted-foreground hover:bg-secondary md:hidden">{p.menuOpen?<X className="h-5 w-5"/>:<Menu className="h-5 w-5"/>}</button><div className="flex items-center gap-2 font-semibold"><Sun className="h-5 w-5 text-primary" /> AutoShine</div>
        <div className="ml-auto flex items-center gap-2">
          <button type="button" aria-label={p.theme==='dark'?'라이트 테마로 변경':'다크 테마로 변경'} title={p.theme==='dark'?'라이트 테마로 변경':'다크 테마로 변경'} onClick={()=>p.onTheme(p.theme==='dark'?'light':'dark')} className="rounded-md p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground">{p.theme==='dark'?<Sun className="h-4 w-4"/>:<Moon className="h-4 w-4"/>}</button>
          <div className="relative" onBlur={e=>{if(!e.currentTarget.contains(e.relatedTarget))setLanguageOpen(false);}} onKeyDown={e=>{if(e.key==='Escape')setLanguageOpen(false);}}>
            <button type="button" aria-label="언어 선택" title="언어 선택" aria-expanded={languageOpen} onClick={()=>setLanguageOpen(!languageOpen)} className="flex items-center gap-1.5 rounded-md px-2 py-2 text-xs text-muted-foreground hover:bg-secondary hover:text-foreground"><Globe className="h-4 w-4"/><span>{p.language==='ko'?'KO':'EN'}</span></button>
            {languageOpen && <div className="absolute right-0 top-full z-50 mt-2 min-w-32 rounded-md border border-border bg-card p-1 shadow-lg">{([['ko','한국어'],['en','English']] as const).map(([value,label])=><button key={value} type="button" aria-pressed={p.language===value} onClick={()=>{p.onLanguage(value);setLanguageOpen(false);}} className={`flex w-full items-center justify-between rounded px-3 py-2 text-sm hover:bg-secondary ${p.language===value?'text-primary':'text-foreground'}`}>{label}{p.language===value&&<CheckCircle2 className="h-3.5 w-3.5"/>}</button>)}</div>}
          </div>
        </div>
      </div>
    </header>
  );
}

function Seg<T extends string>({ label, value, options, onChange }: { label?: string; value: T; options: [T, string][]; onChange: (v: T) => void }) {
  return (
    <div className="flex items-center gap-2 text-xs">
      {label && <span className="text-foreground">{label}</span>}
      <div className="flex flex-wrap rounded-md border border-border bg-background p-0.5">
        {options.map(([v, l]) => (
          <button key={v} aria-pressed={value === v} onClick={() => onChange(v)} className={`rounded px-2 py-1 ${value === v ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground"}`}>{l}</button>
        ))}
      </div>
    </div>
  );
}

function AudioDeviceControls({enabled,muted,onEnable,onMute}:{enabled:boolean|null;muted:boolean|null;onEnable:(v:boolean)=>void;onMute:(v:boolean)=>void}) {
  return <><Seg label="장치" value={enabled===null?'':enabled?'on':'off'} options={[["on","활성화"],["off","비활성화"]]} onChange={v=>onEnable(v==='on')}/><Seg label="소리" value={muted===null?'':muted?'mute':'unmute'} options={[["unmute","소리 켬"],["mute","소리 끔"]]} onChange={v=>onMute(v==='mute')}/>{(enabled===null||muted===null)&&<span className="self-center text-xs text-muted-foreground">미선택 항목은 기존 동작을 유지합니다.</span>}</>;
}

/* ---------- save bar ---------- */
function SaveBar({ changed, save, onSave, onRevert }: { changed: number; save: SaveState; onSave: () => void; onRevert: () => void }) {
  const bar=useRef<HTMLDivElement>(null);
  useEffect(()=>{const element=bar.current;if(!element)return;const observer=new ResizeObserver(()=>document.documentElement.style.setProperty('--save-bar-height',element.offsetHeight+'px'));observer.observe(element);return()=>{observer.disconnect();document.documentElement.style.removeProperty('--save-bar-height');};},[]);
  return (
    <div ref={bar} className="save-bar fixed inset-x-0 bottom-0 z-20 border-t md:left-56 border-border bg-card/95 backdrop-blur">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-3 px-5 py-3">
        <div className="min-w-0 basis-full text-sm sm:basis-auto sm:flex-1">

          {save.kind === "error" ? (
            <div className="text-destructive">
              <div className="flex items-center gap-1.5 font-medium"><XCircle className="h-4 w-4" />저장 실패: {save.cause}</div>
              <div className="mt-0.5 text-foreground">해결 방법: {save.fix}</div>
            </div>
          ) : save.kind === "saving" ? <span className="text-muted-foreground">설정 저장 중…</span>
          : changed > 0 ? <span className="flex items-center gap-1.5 text-warning"><span className="h-2 w-2 rounded-full bg-warning" />저장하지 않은 변경이 있습니다.</span>
          : <span className="flex items-center gap-1.5 text-success"><CheckCircle2 className="h-4 w-4" />저장했습니다.{save.kind==='ok'&&save.notice&&<span className="ml-2 text-xs text-foreground">{save.notice}</span>}</span>}
        </div>
        <button onClick={onRevert} disabled={changed === 0 || save.kind === "saving"} className="rounded-md border border-border px-3 py-2 text-sm hover:bg-secondary disabled:opacity-40">변경 취소</button>
        <button onClick={onSave} disabled={(changed === 0 && save.kind !== "error") || save.kind === "saving"}
          className="flex items-center gap-1.5 rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-40">
          <Save className="h-4 w-4" />{save.kind === "error" ? "다시 시도" : "설정 저장"}
        </button>
      </div>
    </div>
  );
}

/* ---------- shared ui ---------- */
function Section({ title, desc, dirty, children, compact }: { title: ReactNode; desc?: ReactNode; dirty?: boolean; children: ReactNode; compact?: boolean }) {
  return (
    <section className={`mb-6 rounded-lg border bg-card p-5 ${compact ? "compact-section" : ""} ${dirty ? "border-warning/60" : "border-border"}`}>
      <div className="flex items-start justify-between gap-3 ">
        <h2 className="font-semibold">{title}</h2>
        {dirty && <span className="whitespace-nowrap rounded bg-warning/15 px-2 py-0.5 text-xs text-warning">변경됨</span>}
      </div>
      {desc && <p className="mt-1 text-sm leading-relaxed text-muted-foreground ">{desc}</p>}
      <div className={compact ? "compact-actions" : "mt-4"}>{children}</div>
    </section>
  );
}
function PageHead({ title, desc }: { title: string; desc: string }) {
  return (<div className="mb-6"><h1 className="text-2xl font-bold tracking-tight">{title}</h1><p className="mt-1 text-sm leading-relaxed text-muted-foreground ">{desc}</p></div>);
}
function DeviceTag({ d }: { d: Device }) {
  return (
    <span title={d.name} className="flex items-center gap-2 text-xs">
      <span className={`rounded px-2 py-0.5 font-medium ${d.connected ? "bg-success/15 text-success" : "bg-destructive/15 text-destructive"}`}>{d.connected ? "연결됨" : "미연결"}</span>
    </span>
  );
}
function EndStateIcons({ rule: r }: { rule: EndRule }) {
  if (r.mode !== 'custom') {
    const label = r.mode === 'restore' ? '종료 후: 시작 전 상태로 복원' : '종료 후: 상태 유지';
    return <span title={label} aria-label={label} className="inline-flex text-muted-foreground"><RotateCcw className="h-3.5 w-3.5" /></span>;
  }
  const enabled = !r.legacyActivation ? r.enable : null;
  const enableLabel = enabled === null ? '종료 후 활성화: 이전 설정' : enabled ? '종료 후: 활성화' : '종료 후: 비활성화';
  const muteLabel = r.selectedMute ? '종료 후: 기본 출력으로 선택된 경우에만 소리 끔' : r.mute ? '종료 후: 소리 끔' : '종료 후: 소리 켬';
  const EnableIcon = enabled === null ? HelpCircle : enabled ? Power : PowerOff;
  const SoundIcon = r.selectedMute ? HelpCircle : r.mute ? VolumeX : Volume2;
  return <span className="inline-flex items-center gap-1.5">
    <span title={enableLabel} aria-label={enableLabel} className={enabled === false ? 'text-destructive' : 'text-muted-foreground'}><EnableIcon className="h-3.5 w-3.5" /></span>
    <span title={muteLabel} aria-label={muteLabel} className="text-muted-foreground"><SoundIcon className="h-3.5 w-3.5" /></span>
  </span>;
}
function AppliedNote({ show, children }: { show: boolean; children: ReactNode }) {
  if (!show) return null;
  return <p className="mt-3 flex items-center gap-1.5 text-xs text-muted-foreground"><Info className="h-3.5 w-3.5" />저장된 값: <span className="text-foreground">{children}</span></p>;
}
function IconBtn({ onClick, label, children }: { onClick: () => void; label: string; children: ReactNode }) {
  return <button onClick={onClick} aria-label={label} title={label} className="inline-flex min-h-9 min-w-9 items-center justify-center rounded p-1.5 text-muted-foreground hover:bg-secondary hover:text-foreground">{children}</button>;
}
function Btn({ onClick, children, disabled, tone = "default" }: { onClick: () => void; children: ReactNode; disabled?: boolean; tone?: "default" | "primary" | "danger" }) {
  const t = { default: "border border-border hover:bg-secondary", primary: "bg-primary text-primary-foreground hover:bg-primary/90", danger: "border border-destructive/60 text-destructive hover:bg-destructive/10" }[tone];
  return <button onClick={onClick} disabled={disabled} className={`flex items-center gap-1.5 whitespace-nowrap rounded-md px-3 py-2 text-sm disabled:opacity-40 ${t}`}>{children}</button>;
}
type PageProps = { c: Config; a: Config; set: <K extends keyof Config>(k: K, v: Config[K]) => void };
function AddDisconnectedDevice() {
  const {added,add,hide} = useContext(DeviceVisibility);
  const [selected,setSelected] = useState('');
  const hidden = LOCAL.filter(d=>!visibleByDefault(d)&&!added.includes(d.id));
  const chosen = LOCAL.filter(d=>!visibleByDefault(d)&&added.includes(d.id));
  if (!hidden.length && !chosen.length) return null;
  return <details className="mb-4 rounded-md border border-dashed border-border p-3"><summary className="cursor-pointer text-sm">비활성·미연결 장치 표시</summary><p className="mt-2 text-xs text-muted-foreground">목록에서 숨겨도 장치 설정은 유지됩니다.</p><div className="mt-3 flex gap-2"><select aria-label="목록에 표시할 꺼진·미연결 장치" className="min-w-0 flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm" value={selected} onChange={e=>setSelected(e.target.value)}><option value="">장치 선택</option>{hidden.map(d=><option key={d.id} value={d.id}>{d.name}{hidden.filter(x=>x.name===d.name).length>1 ? ` · ${d.id.slice(-8)}` : ''}</option>)}</select><Btn disabled={!hidden.some(d=>d.id===selected)} onClick={()=>{add(selected);setSelected('');}}>추가</Btn></div>{chosen.length>0 && <div className="mt-3 flex flex-wrap gap-2">{chosen.map(d=><span key={d.id} className="flex items-center gap-1 rounded border border-border px-2 py-1 text-xs">{d.name}<IconBtn label="목록에서 숨기기 (정책 유지)" onClick={()=>hide(d.id)}><X className="h-3 w-3"/></IconBtn></span>)}</div>}</details>;
}

/* ---------- start ---------- */
const REMOTE_GUIDE: Record<Health, { title: string; body: string }> = {
  unknown: { title: "아직 확인하지 않았습니다", body: "Agent가 실행되면 Steam Streaming Speakers를 자동으로 찾습니다. 상단 Agent 상태를 확인하세요." },
  ok: { title: "Steam Streaming Speakers 스트리밍 오디오 장치 확인됨", body: "스트리밍이 시작되면 이 장치로 소리를 보냅니다. 별도 선택은 필요 없습니다." },
  warn: { title: "장치가 비활성화되어 있습니다", body: "스트리밍 시작 시 이 장치를 활성화합니다. SoundVolumeView 경로가 유효한지 설정에서 확인하세요." },
  error: { title: "장치를 찾을 수 없습니다", body: "Windows 소리 설정에서 Steam Streaming Speakers의 연결·드라이버 상태를 확인하세요. 이름이 같은 장치가 여러 개이면 자동으로 선택하지 않습니다." },
};

function StartPage({ c, a, set, remote }: PageProps & { remote: Health }) {
  const visible = useVisibleDevices();
  const g = REMOTE_GUIDE[remote];
  const targets = (cfg: Config) => cfg.blockScope === "all" ? LOCAL.map((d) => d.id) : cfg.blockScope === "selected" ? cfg.blockSet : [];
  const tg = targets(c);
  return (
    <>
      <PageHead title="스트리밍 시작" desc="스트리밍 시작 시 오디오 장치의 상태를 설정합니다." />
      <Section title="원격 오디오" desc="원격 오디오 장치는 자동으로 찾아 사용합니다.">
        <div className={`flex gap-3 rounded-md border p-4 ${remote === "ok" ? "border-success/50 bg-success/10" : remote === "unknown" ? "border-border" : remote === "warn" ? "border-warning/50 bg-warning/10" : "border-destructive/50 bg-destructive/10"}`}>
          <HealthIcon h={remote} className="mt-0.5 h-5 w-5 shrink-0" />
          <div><div className="text-sm font-medium">{g.title}</div><p className="mt-1 text-sm leading-relaxed">{g.body}</p></div>
        </div>
      </Section>
      <Section title="오디오 장치 설정" dirty={c.blockScope !== a.blockScope || !eq(c.blockSet, a.blockSet) || !eq(c.blockAction, a.blockAction) || !eq(c.startRule,a.startRule)}>
        <div className="grid gap-3">
          {visible.map(d=>{
            const r=c.startRule?.[d.id]??{enable:tg.includes(d.id)&&c.blockAction[d.id]==='disable'?false:null,mute:tg.includes(d.id)?true:null};
            const ar=a.startRule?.[d.id]??{enable:targets(a).includes(d.id)&&a.blockAction[d.id]==='disable'?false:null,mute:targets(a).includes(d.id)?true:null};
            const up=(patch:Partial<typeof r>)=>set('startRule',{...c.startRule,[d.id]:{...r,...patch}});
            return <div key={d.id} className={`rounded-md border p-3 ${eq(r,ar)?'border-border':'border-warning/60'}`}>
              <div className="mb-2 flex items-center justify-between"><span className="text-sm font-medium">{d.name}</span><DeviceTag d={d}/></div>
              <div className="flex flex-wrap gap-4">
                <AudioDeviceControls enabled={r.enable} muted={r.mute} onEnable={enable=>up({enable})} onMute={mute=>up({mute})}/>
              </div>
            </div>;
          })}
          <AddDisconnectedDevice/>
        </div>
      </Section>
    </>
  );
}

/* ---------- end ---------- */
function EndPage({ c, a, set }: PageProps) {
  const visible = useVisibleDevices();
  const endDevices = REMOTE_DEVICE ? [...visible, REMOTE_DEVICE] : visible;
  const move = (i: number, d: number) => {
    const ch = [...c.chain]; const j = i + d;
    if (j < 0 || j >= ch.length) return;
    [ch[i], ch[j]] = [ch[j]!, ch[i]!]; set("chain", ch);
  };
  const avail = visible.filter((d) => !c.chain.includes(d.id));
  const winner = c.chain.find(id => LOCAL.find(d => d.id === id)?.connected && c.endRule[id]?.enable!==false);
  const rule = (id: string, cfg: Config) => cfg.endRule[id] ?? defRule;
  return (
    <>
      <PageHead title="스트리밍 종료" desc="스트리밍 종료 시 실행할 동작을 설정합니다. 잠시 연결이 끊긴 경우에는 실행하지 않습니다." />
      <Section title="종료 후 출력 우선순위" desc="종료 후 활성화되는 연결 장치 중 위에서부터 오디오 출력을 선택합니다." dirty={!eq(c.chain, a.chain)}>
        <ol className="grid gap-2">
          {c.chain.map((id, i) => {
            if (!visible.some(d=>d.id===id)) return null;
            const d = LOCAL.find((x) => x.id === id) ?? {id, name: id, kind: "출력", connected:false};
            return (
              <li key={id} className={`flex flex-wrap items-center gap-3 rounded-md border px-3 py-2.5 ${winner === id ? "border-success/60 bg-success/10" : "border-border"}`}>
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-primary font-mono text-xs font-bold text-primary-foreground">{i + 1}</span>
                <Headphones className="h-4 w-4 text-muted-foreground" />
                <span className="flex flex-1 items-center gap-2 text-sm"><span>{d.name}</span><EndStateIcons rule={rule(id, c)} /></span>
                <DeviceTag d={d} />
                <div className="flex">
                  <IconBtn onClick={() => move(i, -1)} label="위로"><ArrowUp className="h-4 w-4" /></IconBtn>
                  <IconBtn onClick={() => move(i, 1)} label="아래로"><ArrowDown className="h-4 w-4" /></IconBtn>
                  <IconBtn onClick={() => set("chain", c.chain.filter((x) => x !== id))} label="우선순위에서 제외"><X className="h-4 w-4" /></IconBtn>
                </div>
              </li>
            );
          })}
        </ol>
        {!winner && <p className="mt-3 text-sm text-destructive">출력 우선순위에서 선택할 장치가 없습니다. 시작 전 출력과 장치 설정에 따라 복귀합니다.</p>}
        {avail.length > 0 && (
          <div className="mt-4 flex flex-wrap gap-2">
            {avail.map((d) => (
              <button key={d.id} onClick={() => set("chain", [...c.chain, d.id])} className="flex items-center gap-1 rounded-md border border-dashed border-border px-3 py-1.5 text-xs text-muted-foreground hover:border-primary hover:text-primary"><Plus className="h-3 w-3" />우선순위에 추가: {d.name}</button>
            ))}
          </div>
        )}
        <AppliedNote show={!eq(c.chain, a.chain)}>{a.chain.map(dn).join(" → ")}</AppliedNote>
      </Section>
      <Section title="오디오 장치 설정" dirty={!eq(c.endRule, a.endRule)}>
        <div className="grid gap-3">
          {endDevices.map((d) => {
            const r = rule(d.id, c); const ar = rule(d.id, a);
            const up = (p: Partial<EndRule>) => set("endRule", { ...c.endRule, [d.id]: { ...r, ...p, mode: p.mute===undefined?r.mode:'custom', legacyActivation: false } });
            return (
              <div key={d.id} className={`rounded-md border p-3 ${eq(r, ar) ? "border-border" : "border-warning/60"}`}>
                <div className="mb-2 flex items-center justify-between"><span className="text-sm font-medium">{d.name}</span><DeviceTag d={d} /></div>
                <div className="flex flex-wrap gap-4">
                  <AudioDeviceControls enabled={r.legacyActivation===false?r.enable:null} muted={r.mode==='custom'&&!r.selectedMute?r.mute:null} onEnable={enable=>up({enable})} onMute={mute=>up({mute,selectedMute:false})}/>
                </div>
              </div>
            );
          })}
          <AddDisconnectedDevice />
        </div>
      </Section>
    </>
  );
}

/* ---------- simulator ---------- */
type Step = { text: string; enabled?:boolean; muted?:boolean; defaultOutput?:boolean; kind: "switch" | "block" | "info" | "skip" | "keep" | "error" | "display" };
type Log = { id: number; t: string; event: string; steps: Step[] };

function Simulator({ draft, applied,env }: { draft: Config; applied: Config;env:Environment }) {
  const visible = useVisibleDevices();

  const displaySession=useRef<{target:string;before:SimulatedDisplay[];hold:boolean;policy:string}|null>(null);
  const cfg = draft;
  const [state, setState] = useState<"idle" | "streaming" | "disconnected">("idle");
  const [conn] = useState<Record<string, boolean>>({ [REMOTE.id]: true, ...Object.fromEntries(LOCAL.map((d) => [d.id, d.connected])) });
  const [logs, setLogs] = useState<Log[]>([]);
  const [revealed, setRevealed] = useState(0);
  const logSequence = useRef(0);
  const animating = logs.length>0 && revealed<logs[0].steps.length;
  useEffect(()=>{
    if (!logs.length) return;
    const count = logs[0].steps.length;
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) {setRevealed(count);return;}
    const timer = setInterval(()=>setRevealed(n=>{
      if (n+1>=count) clearInterval(timer);
      return Math.min(n+1,count);
    }),250);
    return ()=>clearInterval(timer);
  },[logs]);
  const [busy,setBusy] = useState(false);
  const [failure,setFailure] = useState<Result>(null);
  const simulated = useRef<{devices:{id:string;name:string;connected:boolean;enabled:boolean;muted:boolean}[];defaults:(string|null)[];session:unknown} | null>(null);
  const fire = async (ev: "start" | "drop" | "reconnect" | "end", label: string, to: typeof state) => {
    setBusy(true);setFailure(null);
    try {
    const initial = simulated.current ?? {devices:[{...REMOTE,connected:!!conn[REMOTE.id],enabled:true,muted:false},...LOCAL.map(d=>({...d,connected:!!conn[d.id],enabled:d.state!=='disabled',muted:false}))],defaults:Array(3).fill(LOCAL.find(d=>d.connected&&d.state!=='disabled')?.id??REMOTE.id),session:null};
    const settings = toBackend(cfg,LOCAL,REMOTE.id?REMOTE:null);
    const r = await api('simulation',{event:ev,settings,devices:initial.devices.map(d=>({...d,connected:!!conn[d.id]})),defaults:initial.defaults,session:initial.session});
    simulated.current=r;
    const policy = (initial.session as {policy?: Backend['audio']} | null)?.policy ?? settings.audio;
    const resultIds = new Set([REMOTE.id, ...visible.map(d => d.id)]);
    const steps:Step[] = simulationSteps(initial.devices,r.devices,r.defaults,policy.rules,ev,resultIds);
    if(ev==='start') {
      const selectedId=settings.display.sunshineDeviceId||env.sunshine.deviceId;
      const local=env.displays??[];
      const matched=new Set<number>();
      const before:SimulatedDisplay[]=env.sunshineDisplays.map(d=>{
        let index=local.findIndex(x=>d.displayName&&x.name.split(' · ').some(part=>part.toLowerCase()===d.displayName!.toLowerCase()));
        if(index<0&&d.virtual&&local.filter(x=>x.virtual).length===1&&env.sunshineDisplays.filter(x=>x.virtual).length===1)index=local.findIndex(x=>x.virtual);
        if(index>=0)matched.add(index);
        return {id:d.id,name:d.name,enabled:index>=0?local[index].enabled:undefined};
      });
      local.forEach((d,i)=>{if(!matched.has(i)&&(d.virtual||d.enabled||d.name.split(' · ').slice(1).some(part=>part.trim())))before.push({id:'windows-'+i,name:d.name,enabled:d.enabled});});
      displaySession.current={target:selectedId??'',before,hold:settings.display.holdOnDisconnect,policy:settings.display.policy};
    }
    const display=displaySession.current;
    if(display) {
      const restored=ev==='end'||ev==='drop'&&!display.hold;
      for(const d of displayStates(display.before,display.target,display.policy,restored))steps.push({kind:'display',text:d.name,enabled:d.enabled});
      if(ev==='end')displaySession.current=null;
    }
    const id = ++logSequence.current;
    setLogs((l) => [{ id, t: stamp(), event: label, steps }, ...l].slice(0,20));
    setRevealed(0);
    setState(to);
    } catch(e) {setFailure({h:'error',text:(e as Error).message});} finally {setBusy(false);}
  };
  const actions = [
    { label: "스트리밍 시작", icon: Play, ev: "start" as const, to: "streaming" as const, on: state === "idle" },
    { label: "연결 끊김", icon: Unplug, ev: "drop" as const, to: "disconnected" as const, on: state === "streaming" },
    { label: "재접속", icon: RotateCcw, ev: "reconnect" as const, to: "streaming" as const, on: state === "disconnected" },
    { label: "스트리밍 종료", icon: Square, ev: "end" as const, to: "idle" as const, on: state !== "idle" },
  ];
  const kindStyle: Record<Step["kind"], string> = {
    display: "border-info/60 text-info", switch: "border-success/60 text-success", block: "border-warning/60 text-warning", info: "border-warning/60 text-warning",
    skip: "border-border text-muted-foreground", keep: "border-info/60 text-info", error: "border-destructive/60 text-destructive",
  };
  const kindLabel: Record<Step["kind"], string> = { display:"디스플레이", switch: "오디오 출력", block: "오디오 장치", info: "오디오 장치", skip: "건너뜀", keep: "유지", error: "경고" };
  return (
    <>
      <PageHead title="시뮬레이션" desc="설정한 시작·종료 동작을 미리 확인합니다. 실제 장치는 변경하지 않습니다." />
      <ResultLine r={failure} />
      <Section title="테스트할 상황">
        <div className="flex flex-wrap gap-2">
          {actions.map((x) => (
            <button key={x.label} disabled={!x.on || busy || animating} onClick={() => fire(x.ev, x.label, x.to)}
              className="flex items-center gap-1.5 rounded-md border border-border bg-secondary px-3 py-2 text-sm hover:border-primary disabled:cursor-not-allowed disabled:opacity-40"><x.icon className="h-4 w-4" /> {x.label}</button>
          ))}
          <button disabled={busy} onClick={() => { simulated.current=null;displaySession.current=null;setLogs([]); setState("idle");setFailure(null); }} className="rounded-md border border-border bg-secondary px-3 py-2 text-sm hover:border-primary disabled:cursor-not-allowed disabled:opacity-40">테스트 초기화</button>
        </div>
      </Section>
      <Section title="예상 결과" desc="오디오 장치와 디스플레이의 예상 상태를 표시합니다.">
        {logs.length === 0 ? <p className="text-sm text-muted-foreground">스트리밍 시작을 누르면 예상 결과가 표시됩니다.</p> : (
          <div className="grid gap-4">
            {logs.map((l, li) => (
              <div key={l.id}>
                <div className="mb-2 flex items-center gap-2 font-mono text-xs text-muted-foreground"><span className="text-primary">▶ {l.event}</span><span>{l.t}</span></div>
                <ol className="grid gap-1.5 border-l border-border pl-4">
                  {l.steps.map((s, i) => (li > 0 || i < revealed) && (
                    <li key={i} className={`simulation-row flex items-center gap-3 rounded border bg-background px-3 py-1.5 text-sm animate-in fade-in slide-in-from-left-2 ${kindStyle[s.kind]}`}>
                      <span className="w-28 shrink-0 whitespace-nowrap text-[11px]">{kindLabel[s.kind]}</span><span className="simulation-name min-w-0 flex-1 break-words">{s.text}</span>
                      {s.kind==='display' && <span className={`ml-auto rounded px-2 py-0.5 text-xs ${s.enabled===undefined?'bg-secondary text-muted-foreground':s.enabled?'bg-success/15 text-success':'bg-destructive/15 text-destructive'}`}>{s.enabled===undefined?'확인되지 않음':s.enabled?'활성화':'비활성화'}</span>}
                      {s.defaultOutput && <span className="rounded bg-success/15 px-2 py-0.5 text-xs text-success">기본 출력</span>}
                      {s.enabled !== undefined && s.muted !== undefined && <span className="ml-auto flex shrink-0 gap-2 text-xs"><span className={`rounded px-2 py-0.5 ${s.enabled ? 'bg-success/15 text-success' : 'bg-destructive/15 text-destructive'}`}>{s.enabled ? '활성화' : '비활성화'}</span><span className={`rounded px-2 py-0.5 ${s.muted ? 'bg-warning/15 text-warning' : 'bg-success/15 text-success'}`}>{s.muted ? '소리 끔' : '소리 켬'}</span></span>}
                    </li>
                  ))}
                </ol>
              </div>
            ))}
          </div>
        )}
      </Section>
    </>
  );
}

/* ---------- settings ---------- */
type Result = { h: Health; text: string } | null;
function ResultLine({ r }: { r: Result }) {
  if (!r) return null;
  const c = { ok: "text-success", warn: "text-warning", error: "text-destructive", unknown: "text-muted-foreground" }[r.h];
  return <p role="status" aria-live="polite" className={`mt-3 flex items-start gap-1.5 text-sm ${c}`}><HealthIcon h={r.h} className="mt-0.5 h-4 w-4 shrink-0" /><span>{r.text}</span></p>;
}

function SettingsPage(p: PageProps & {setDraft: (c: Config) => void; env: Environment; refresh: () => Promise<Environment>; agentState: AgentState}) {
  const {c,a,set} = p;
  const [toolState,setToolState]=useState<'checking'|'ok'|'error'>('checking');
  const latestConfig = useRef(c);
  latestConfig.current = c;
  useEffect(() => {
    let active = true;
    const fill = (toolPath?: string) => {
      if (!active) return;
      const current = latestConfig.current;
      const b = structuredClone(current.backend);
      if (!b.sunshine.configPath && p.env.sunshine.configPath) b.sunshine.configPath = p.env.sunshine.configPath;
      if (!b.display.sunshineDeviceId && p.env.sunshine.deviceId) b.display.sunshineDeviceId = p.env.sunshine.deviceId;
      if (!b.audio.controlToolPath && toolPath) b.audio.controlToolPath = toolPath;
      if (!eq(b,current.backend)) { const next = {...current,backend:b}; latestConfig.current=next; p.setDraft(next); }
    };
    fill();
    if (!c.backend.audio.controlToolPath) api('audio/tool').then(r=>fill(r.path)).catch(()=>{});
    return () => {active=false;};
  }, []);
  const [result, setResult] = useState<Result>(null);
  const [resultFor,setResultFor] = useState('');
  const [busy, setBusy] = useState('');
  const [user,setUser] = useState(''); const [pw,setPw] = useState(''); const [showPassword,setShowPassword] = useState(false);
  const [pin,setPin] = useState(''); const [paired,setPaired] = useState(p.env.paired);
  const [certificateInput,setCertificateInput] = useState('');
  const certificateMatches = fingerprintMatches(pin,certificateInput);
  const [capture,setCapture] = useState(false);
  const [plan,setPlan] = useState<{id:string; path:string; changes:Record<string,string>; message:string} | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const field = 'w-full rounded-md border border-input bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-ring';
  const run = async (name: string, fn: () => Promise<void>) => {
    setBusy(name); setResult(null); setResultFor(name.startsWith('tool') ? 'tool' : ['check','plan','apply'].includes(name) ? 'sunshine' : ['certificate','pair','unpair'].includes(name) ? 'auth' : name);
    try { await fn(); } catch(e) { setResult({h:'error',text:(e as Error).message}); } finally {setBusy('');}
  };
  const updateBackend = (b: Backend) => {set('backend', b);setPlan(null);setPin('');setCertificateInput('');};
  const currentSettings = () => toBackend(c,LOCAL,REMOTE.id ? REMOTE : null);
  const check = () => run('check',async () => {
    const e=await p.refresh(); setPaired(e.paired);
    setResult({h:e.sunshine.reachable ? e.integrated?'ok':'warn':'error',text:`Sunshine 서버 연결: ${e.sunshine.reachable?'연결됨':'연결 실패'} · 자동 실행 등록: ${e.integrated?'등록됨':'등록 안 됨'} · ${e.sunshine.message}`});
  });
  const preview = (unlink: boolean) => run('plan',async () => {
    const b=currentSettings();
    setPlan(await api(unlink ? 'sunshine/unlink-plan':'sunshine/plan',unlink ? b.sunshine:b));
  });
  const applyPlan = () => run('apply',async () => {
    if(!plan) return; const r=await api('sunshine/apply',{id:plan.id}); setPlan(null);
    setResult({h:'ok',text:r.message+' · 백업: '+r.backup+' · Sunshine을 재시작하세요.'}); await p.refresh();
  });
  const pair = (confirmed = false) => run('pair',async () => {
    if (!user || !pw) throw Error('Sunshine 아이디와 비밀번호를 입력하세요.');
    const r = await api('sunshine/pair',{port:c.backend.sunshine.basePort,username:user,password:pw,pin:confirmed ? pin : null});
    if (r.needsConfirmation) {setPin(r.pin);setResult({h:'warn',text:r.message});return;}
    setPw('');setPin('');setPaired(true);setResult({h:'ok',text:'Sunshine 로그인 연결을 완료했습니다.'});
  });
  const unpair = () => run('unpair',async () => {
    const r=await api('sunshine/unpair',c.backend.sunshine);setPaired(false);setResult({h:'ok',text:r.message});
  });
  const importConfig = async (f: File) => run('import',async () => {
    const raw=JSON.parse(await f.text()); const b: Backend=await api('settings/validate',raw);
    const known=[...LOCAL];
    for(const d of [...b.audio.rules.map(r=>r.device),...b.audio.localOutputPriority]) if(!known.some(x=>x.id===d.id)) known.push({...d,kind:'출력',connected:false});
    LOCAL=known; p.setDraft(fromBackend(b,known));setPlan(null);setPin('');
    setResult({h:'warn',text:'설정을 불러왔습니다. 하단에서 저장하세요.'});
  });
  const findTool = () => run('tool',async () => {
    const r=await api('audio/tool');if(!r.path) throw Error('SoundVolumeView를 찾지 못했습니다. 준비 후 실행 파일 경로를 지정하세요.');
    updateBackend({...c.backend,audio:{...c.backend.audio,controlToolPath:r.path}});
    setResult({h:'ok',text:'실행 파일을 찾았습니다. 저장해야 경로가 반영됩니다.'});
  });
  const checkTool = () => run('tool-check',async () => {
    const r=await api('audio/tool/check',{path:c.backend.audio.controlToolPath});setResult({h:'ok',text:r.message});
  });
  useEffect(()=>{const path=c.backend.audio.controlToolPath;setToolState(path?'checking':'error');if(!path)return;let active=true;const timer=setTimeout(()=>{api('audio/tool/check',{path}).then(r=>{if(active){setToolState('ok');}}).catch(()=>{if(active){setToolState('error');}});},400);return()=>{active=false;clearTimeout(timer);};},[c.backend.audio.controlToolPath]);
  const pickFile = (kind:'tool'|'sunshine') => run(kind==='tool'?'tool-pick':'file-pick',async()=>{
    const current=latestConfig.current;
    const r=await api('files/pick/'+kind,{path:kind==='tool'?current.backend.audio.controlToolPath:current.backend.sunshine.configPath});
    if(!r.path)return;
    const latest=latestConfig.current;
    p.setDraft({...latest,backend:kind==='tool'?{...latest.backend,audio:{...latest.backend.audio,controlToolPath:r.path}}:{...latest.backend,sunshine:{...latest.backend.sunshine,configPath:r.path}}});
    setResult(null);
  });
  const record = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    if(!capture) return;
    if(e.key==='Escape'){setCapture(false);return;}
    if(e.key==='Tab'){setCapture(false);return;}
    e.preventDefault();
    if(['Control','Alt','Shift','Meta'].includes(e.key)) return;
    const key=e.key.length===1 ? e.key.toUpperCase():e.key;
    if(e.metaKey || !/^(F([1-9]|1[0-2])|[A-Z0-9]|ScrollLock|Pause)$/.test(key) || (!e.ctrlKey&&!e.altKey&&!e.shiftKey&&!['ScrollLock','Pause'].includes(key))) {
      setResultFor('hotkey');
      setResult({h:'error',text:'Ctrl·Alt·Shift와 문자·숫자·F1~F12 조합 또는 ScrollLock·Pause를 사용하세요.'});return;
    }
    set('hotkey',[e.ctrlKey&&'Ctrl',e.altKey&&'Alt',e.shiftKey&&'Shift',key].filter(Boolean).join(' + '));setCapture(false);setResult(null);
  };
  const registered = p.agentState.state?.hotkey;
  const hotkeyStatus = !p.agentState.reachable ? 'AutoShine에 연결할 수 없어 확인하지 못했습니다.' : !p.agentState.state ? '현재 실행 중인 버전에서 단축키 등록 상태를 제공하지 않습니다.' : registered?.enabled ? eq(registered,hotkeyValue(a.hotkey,a.hotkeyEnabled)) ? 'AutoShine에 등록됨':'실행 중인 조합이 저장값과 다릅니다.':'등록되지 않음';
  return <>
    {resultFor==='file-pick' && <ResultLine r={result}/>}
    <PageHead title="설정" desc="프로그램 연결과 단축키를 설정합니다." />
    <Section title="오디오 제어 도구" desc={<><a href="https://www.nirsoft.net/utils/sound_volume_view.html" target="_blank" rel="noopener noreferrer" className="text-primary underline underline-offset-2 hover:text-primary/80">SoundVolumeView</a>가 필요합니다. 설치된 파일은 자동으로 찾습니다.</>} dirty={c.backend.audio.controlToolPath!==a.backend.audio.controlToolPath}>
      <label className="text-sm" htmlFor="tool-path">실행 파일 경로</label>
      <div className="relative mt-2"><input id="tool-path" value={c.backend.audio.controlToolPath} onChange={e=>updateBackend({...c.backend,audio:{...c.backend.audio,controlToolPath:e.target.value}})} className={field+' pr-20'} placeholder="SoundVolumeView.exe 전체 경로" /><span className="absolute right-12 top-1/2 -translate-y-1/2" title={toolState==='ok'?'파일 확인됨':toolState==='error'?'경로 확인 필요':'경로 확인 중'} aria-label={toolState==='ok'?'파일 확인됨':toolState==='error'?'경로 확인 필요':'경로 확인 중'}>{toolState==='ok'?<CheckCircle2 className="h-4 w-4 text-success"/>:toolState==='error'?<XCircle className="h-4 w-4 text-destructive"/>:<HelpCircle className="h-4 w-4 text-muted-foreground"/>}</span><button type="button" aria-label="SoundVolumeView 파일 선택" title="파일 선택" disabled={!!busy} onClick={()=>pickFile('tool')} className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-1.5 hover:bg-secondary"><FolderOpen className="h-4 w-4"/></button></div>
      {resultFor==='tool' && result?.h==='error' && <ResultLine r={result}/>}
      <p className="mt-2 text-xs text-muted-foreground">찾지 못했다면 다운로드한 ZIP을 풀고 실행 파일 경로를 지정하세요.</p>

    </Section>
    <Section title={<span className="flex items-center gap-2">Sunshine 연동<span className={`rounded px-2 py-0.5 text-xs font-normal ${p.env.integrated?'bg-success/15 text-success':'bg-muted text-muted-foreground'}`}>{p.env.integrated?'연동됨':'연동 안 됨'}</span></span>} desc="스트리밍 시작·종료 시 설정한 오디오 동작을 실행합니다.">
      <div className="grid gap-3">
        <label className="text-sm">설정 파일 경로<div className="relative mt-1"><input className={field+' pr-20'} placeholder={p.env.sunshine.configPath || '자동 탐색'} value={c.backend.sunshine.configPath} onChange={e=>updateBackend({...c.backend,sunshine:{...c.backend.sunshine,configPath:e.target.value}})} /><span className="absolute right-12 top-1/2 -translate-y-1/2" title={(p.env.sunshineConfigExists && c.backend.sunshine.configPath===p.env.sunshine.configPath)?"경로 확인됨":"경로 확인 필요"}>{(p.env.sunshineConfigExists && c.backend.sunshine.configPath===p.env.sunshine.configPath)?<CheckCircle2 className="h-4 w-4 text-success"/>:<HelpCircle className="h-4 w-4 text-muted-foreground"/>}</span><button type="button" aria-label="Sunshine 설정 파일 선택" title="파일 선택" disabled={!!busy} onClick={()=>pickFile('sunshine')} className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-1.5 hover:bg-secondary"><FolderOpen className="h-4 w-4"/></button></div></label>
        <div className="flex items-end gap-2"><label className="min-w-0 flex-1 text-sm">Sunshine 포트<input type="number" min="1024" max="65514" className={field+' mt-1'} value={c.backend.sunshine.basePort} onChange={e=>{setPaired(false);updateBackend({...c.backend,sunshine:{...c.backend.sunshine,basePort:Number(e.target.value)}});}} /></label><Btn onClick={()=>preview(p.env.integrated)} disabled={!!busy} tone={p.env.integrated?"danger":"primary"}>{p.env.integrated?"해제 확인":"연동 설정"}</Btn></div>
      </div>
      <p className="mt-2 text-xs text-muted-foreground">자동으로 찾은 경로와 기본 포트입니다. Sunshine 설치 위치나 포트를 바꾼 경우에만 수정하세요.</p>


      {plan && <div className="mt-4 rounded-md border border-info p-4"><p className="text-sm">{plan.path}</p><details className="mt-2"><summary className="cursor-pointer text-sm">변경 내용</summary><pre className="mt-2 max-h-60 overflow-auto whitespace-pre-wrap break-all text-xs">{JSON.stringify(plan.changes,null,2)}</pre></details><p className="my-3 text-sm">{plan.message}</p><Btn onClick={applyPlan} disabled={!!busy} tone="primary">{plan.changes.global_prep_cmd?.includes('AutoShine.exe')?'연동 적용':'연동 해제'}</Btn></div>}
      {resultFor==='sunshine' && <ResultLine r={result} />}
      <div className="mt-5 border-t border-border pt-4">
      <h3 className="flex items-center gap-2 text-sm font-semibold"><span className="flex items-center gap-2">Sunshine 로그인 <span className={`rounded px-2 py-0.5 text-xs font-normal ${paired?'bg-success/15 text-success':'bg-muted text-muted-foreground'}`}>{paired?'로그인 등록됨':'로그인 미등록'}</span></span></h3>
      <p className="mt-1 text-sm text-muted-foreground">종료 단축키·트레이로 스트리밍을 종료할 때 사용합니다.</p>

      <div className="mt-3 grid gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end"><label className="block text-sm">아이디<input aria-label="Sunshine 아이디" placeholder="Sunshine 아이디" className={field+" mt-1"} value={user} onChange={e=>{setUser(e.target.value);setPin('');}} autoComplete="username"/></label><label className="block text-sm">비밀번호<div className="relative mt-1"><input aria-label="Sunshine 비밀번호" placeholder="Sunshine 비밀번호" type={showPassword?"text":"password"} className={field+" pr-10"} value={pw} onChange={e=>{setPw(e.target.value);setPin('');}} autoComplete="current-password"/><button type="button" aria-label={showPassword?"비밀번호 숨기기":"비밀번호 보기"} aria-pressed={showPassword} onClick={()=>setShowPassword(v=>!v)} className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-1.5 hover:bg-secondary">{showPassword?<EyeOff className="h-4 w-4"/>:<Eye className="h-4 w-4"/>}</button></div></label><Btn onClick={()=>paired?unpair():pair()} disabled={!!busy||(!paired&&(!user||!pw))||p.env.isolatedPreview} tone={paired?"danger":"primary"}>{paired?"로그인 해제":"로그인"}</Btn></div>
      {pin && <div className="mt-3 rounded-md border border-border p-3"><p className="text-sm">이 PC의 Sunshine 서버에 처음 연결합니다.</p><details className="mt-2 text-xs"><summary>서버 인증서 정보</summary><p className="break-all font-mono">SHA-256: {pin}</p></details><div className="mt-3 flex gap-2"><Btn onClick={()=>pair(true)} disabled={!!busy||p.env.isolatedPreview}>이 서버로 연결</Btn><Btn onClick={()=>setPin('')}>취소</Btn></div></div>}
      {resultFor==='auth' && <ResultLine r={result} />}
      </div>
    </Section>
    <LoginStartupSection/>
    <Section title="스트리밍 종료 단축키" desc="이 PC의 키보드로 Moonlight 스트리밍을 종료합니다. 사용하려면 Sunshine 연동 카드에서 로그인하세요." dirty={c.hotkeyEnabled!==a.hotkeyEnabled||c.hotkey!==a.hotkey}>
      <div className="flex items-center justify-between rounded-md border border-border px-4 py-3"><span className="flex items-center gap-2 text-sm"><Keyboard className="h-4 w-4"/>단축키 사용</span><button role="switch" aria-label="단축키 사용" aria-checked={c.hotkeyEnabled} onClick={()=>set('hotkeyEnabled',!c.hotkeyEnabled)} className={`relative h-6 w-11 rounded-full ${c.hotkeyEnabled?'bg-primary':'bg-muted'}`}><span className={`absolute top-0.5 h-5 w-5 rounded-full bg-foreground ${c.hotkeyEnabled?'left-5.5':'left-0.5'}`}/></button></div>
      {c.hotkeyEnabled&&<div className="mt-4"><div className="flex flex-wrap gap-2"><button aria-label="단축키 키 조합 입력" onClick={()=>setCapture(true)} onKeyDown={record} onBlur={()=>setCapture(false)} className={field+' flex-1'}>{capture?'키 조합을 누르세요 · Esc 취소':c.hotkey||'클릭하여 키 조합 지정'}</button><Btn disabled={!!busy} onClick={()=>run('hotkey',async()=>{const r=await api('hotkey/check',hotkeyValue(c.hotkey,true));setResult({h:'ok',text:r.message});})}>단축키 확인</Btn></div></div>}
      {resultFor==='hotkey' && <ResultLine r={result} />}
    </Section>
    <Section compact title="설정 백업" desc="AutoShine 설정을 백업합니다. 로그인 정보와 VDD 프리셋은 제외됩니다.">
      <div className="flex flex-wrap gap-2"><Btn onClick={()=>download('autoshine-settings.json',JSON.stringify(a.backend,null,2))}><Download className="h-4 w-4"/>저장된 설정 백업</Btn><Btn onClick={()=>fileRef.current?.click()}><Upload className="h-4 w-4"/>백업 파일 불러오기</Btn><input ref={fileRef} type="file" accept=".json,application/json" className="hidden" onChange={e=>{const f=e.target.files?.[0];if(f)importConfig(f);e.target.value='';}}/></div>
      {resultFor==='import' && <ResultLine r={result} />}
    </Section>
    <Section compact title="문제 해결" desc="오류 문의에 첨부할 기록입니다. 로그인 정보는 포함하지 않습니다."><a href="/api/diagnostics" download className="inline-flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm"><FileText className="h-4 w-4"/>진단 파일 다운로드</a></Section>
  </>;
}


function DisplayPage({c,set,env}: PageProps & {env:Environment}) {
  const displays=env.sunshineDisplays??[];
  const selected=c.backend.display.sunshineDeviceId || env.sunshine.deviceId || '';
  const choose=(id:string)=>set('backend',{...c.backend,display:{...c.backend.display,sunshineDeviceId:id}});
  return <><PageHead title="디스플레이" desc="스트리밍에 사용할 디스플레이를 선택합니다."/>
    <Section title="스트리밍 디스플레이" desc={<>가상 디스플레이는 <a href="https://github.com/VirtualDrivers/Virtual-Display-Driver" target="_blank" rel="noopener noreferrer" className="text-primary underline underline-offset-2 hover:text-primary/80">VDD 설치</a>가 필요합니다. 화면 전환은 Sunshine이 처리합니다.</>}>
      <div role="radiogroup" aria-label="스트리밍 디스플레이" className="space-y-2">
        {displays.map((d,i)=><label key={d.id} className={'flex cursor-pointer items-center gap-3 rounded-md border px-3 py-3 '+(selected.toLowerCase()===d.id.toLowerCase()?'border-primary bg-primary/5':'border-border')}>
          <input type="radio" name="stream-display" checked={selected.toLowerCase()===d.id.toLowerCase()} onChange={()=>choose(d.id)}/><Monitor size={18} className="text-muted-foreground"/><span className="text-sm">{d.name}{displays.filter(x=>x.name===d.name).length>1?' · '+(i+1):''}</span>{d.virtual && <span className="ml-auto rounded bg-info/15 px-2 py-0.5 text-xs text-info">가상 디스플레이</span>}
        </label>)}
      </div>
      {!displays.length && <p className="text-sm text-muted-foreground">Sunshine에서 디스플레이 목록을 확인하지 못했습니다. Sunshine을 실행한 뒤 다시 확인하세요.</p>}
      {selected!==env.sunshine.deviceId && <p className="mt-3 text-xs text-muted-foreground">하단에서 저장하면 Sunshine에도 적용됩니다.</p>}
    </Section>
  </>;
}


type VddData = {fingerprint:string;globalRates:number[];presets:{name?:string;width:number;height:number;refreshRates:number[]}[]};
type VddEditorHandle={save:()=>Promise<void>;revert:()=>void};
function VddPresetEditor({isolated,editorRef,onDirty}:{isolated:boolean;editorRef:Ref<VddEditorHandle>;onDirty:(dirty:boolean)=>void}) {
  const [data,setData]=useState<VddData|null>(null);
  const [rates,setRates]=useState('');
  const [rows,setRows]=useState<{name:string;width:string;height:string;rates:string}[]>([]);
  const original=useRef<{rates:string;rows:typeof rows}|null>(null);
  const [page,setPage]=useState(0);
  const pageCount=Math.max(1,Math.ceil(rows.length/10));
  const currentPage=Math.min(page,pageCount-1);
  const [busy,setBusy]=useState(false);
  const [result,setResult]=useState<Result>(null);
  const load=(value:VddData)=>{
    const nextRows=value.presets.map((p,i)=>({name:p.name?.trim()?p.name:translate("프리셋")+" "+(i+1),width:String(p.width),height:String(p.height),rates:p.refreshRates.join(', ')}));
    const nextRates=value.globalRates.join(', ');original.current={rates:nextRates,rows:nextRows};setData(value);setRates(nextRates);setRows(nextRows);
  };
  const dirty=!!original.current&&(!eq(rows,original.current.rows)||rates!==original.current.rates);
  useEffect(()=>onDirty(dirty),[dirty,onDirty]);
  useEffect(()=>{let active=true;api('vdd/presets').then(r=>{if(active)load(r);}).catch(e=>{if(active)setResult({h:'error',text:e.message});});return()=>{active=false;};},[]);
  const numbers=(text:string)=>text.trim()?text.split(/[,\s]+/).filter(Boolean).map(Number):[];
  const rowError=(row:typeof rows[number])=>{
    if(!Number.isInteger(Number(row.width))||!Number.isInteger(Number(row.height))||Number(row.width)<1||Number(row.width)>16384||Number(row.height)<1||Number(row.height)>16384)return '해상도는 1~16384px로 입력하세요.';
    if(rows.filter(r=>Number(r.width)===Number(row.width)&&Number(r.height)===Number(row.height)).length>1)return '같은 해상도가 중복되어 있습니다.';
    const values=numbers(row.rates);const common=numbers(rates);
    if(values.some(r=>!Number.isInteger(r)||r<1||r>1000)||new Set(values).size!==values.length||!values.length&&!common.length)return '주사율은 1~1000Hz로 입력하세요.';
    return '';
  };
  useImperativeHandle(editorRef,()=>({
    save:async()=>{if(!data||!dirty)return;if(rows.some(row=>rowError(row))||!rows.length)throw new Error('프리셋의 입력값을 확인하세요.');const common=numbers(rates);if(common.some(r=>!Number.isInteger(r)||r<1||r>1000)||new Set(common).size!==common.length)throw new Error('공통 주사율은 중복 없이 1~1000Hz로 입력하세요.');if(isolated)throw new Error('화면 검증 서버에서는 VDD 설정을 변경하지 않습니다.');setBusy(true);try{const r=await api('vdd/presets',{fingerprint:data.fingerprint,globalRates:numbers(rates),presets:rows.map(p=>({name:p.name,width:Number(p.width),height:Number(p.height),refreshRates:numbers(p.rates)}))});load(r.settings);onDirty(false);}finally{setBusy(false);}},
    revert:()=>{if(original.current){setRates(original.current.rates);setRows(original.current.rows);onDirty(false);}}
  }));
  const input='min-w-0 w-full rounded-md border border-input bg-background px-3 py-2 text-sm';
  return <Section dirty={dirty} title="VDD 해상도 프리셋" desc="가상 디스플레이의 해상도와 주사율을 설정합니다.">
    {data && <><label className="block text-sm">공통 주사율 (Hz)<input className={input+' mt-1'} value={rates} onChange={e=>setRates(e.target.value)} placeholder="30, 60, 120" disabled={busy}/></label>
    <p className="my-3 text-xs text-muted-foreground">공통 주사율은 모든 해상도에 추가됩니다. 개별 주사율은 쉼표로 구분합니다.</p>
    <div className="preset-head mb-2 grid grid-cols-[1.5fr_1fr_1fr_1.5fr_auto] gap-2 text-xs text-muted-foreground"><span>프리셋 이름</span><span>가로 (px)</span><span>세로 (px)</span><span>개별 주사율 (Hz)</span><span className="w-8"/></div><div className="grid gap-2">{rows.slice(currentPage*10,currentPage*10+10).map((p,index)=>{const i=currentPage*10+index;return <div key={i} className="preset-row grid grid-cols-[1.5fr_1fr_1fr_1.5fr_auto] items-center gap-2"><label className="preset-field"><span>프리셋 이름</span><input aria-label="프리셋 이름" placeholder="이름" maxLength={80} className={input} value={p.name} disabled={busy} onChange={e=>setRows(v=>v.map((r,n)=>n===i?{...r,name:e.target.value}:r))}/></label><label className="preset-field"><span>가로 (px)</span><input aria-label="가로 (px)" type="number" min="1" max="16384" className={input} value={p.width} disabled={busy} onChange={e=>setRows(v=>v.map((r,n)=>n===i?{...r,width:e.target.value}:r))}/></label><label className="preset-field"><span>세로 (px)</span><input aria-label="세로 (px)" type="number" min="1" max="16384" className={input} value={p.height} disabled={busy} onChange={e=>setRows(v=>v.map((r,n)=>n===i?{...r,height:e.target.value}:r))}/></label><label className="preset-field"><span>개별 주사율 (Hz)</span><input aria-label="개별 주사율 (Hz)" className={input} value={p.rates} disabled={busy} onChange={e=>setRows(v=>v.map((r,n)=>n===i?{...r,rates:e.target.value}:r))}/></label><IconBtn label="프리셋 삭제" onClick={()=>{if(!busy)setRows(v=>v.filter((_,n)=>n!==i));}}><Trash2 className="h-4 w-4"/></IconBtn>{rowError(p)&&<p role="status" className="col-span-full text-xs text-destructive">{rowError(p)}</p>}</div>;})}</div>
    {rows.length>=128 && <p className="mt-3 text-xs text-muted-foreground">프리셋은 최대 128개까지 추가할 수 있습니다.</p>}
    <button disabled={busy||rows.length>=128} onClick={()=>{let number=rows.length+1;while(rows.some(r=>r.name===translate("프리셋")+" "+number))number++;setRows(v=>[...v,{name:translate("프리셋")+" "+number,width:'1920',height:'1080',rates:''}]);setPage(Math.floor(rows.length/10));}} className="mt-3 flex w-full items-center justify-center gap-2 rounded-md border border-dashed border-border p-3 text-sm text-muted-foreground hover:border-primary hover:text-primary disabled:opacity-40"><Plus className="h-4 w-4"/>프리셋 추가</button>
    {pageCount>1 && <nav aria-label="프리셋 페이지" className="mt-3 flex items-center justify-end gap-2"><Btn disabled={busy||currentPage===0} onClick={()=>setPage(currentPage-1)}>이전</Btn><span className="text-sm tabular-nums">{currentPage+1} / {pageCount}</span><Btn disabled={busy||currentPage===pageCount-1} onClick={()=>setPage(currentPage+1)}>다음</Btn></nav>}</>}
    <ResultLine r={result}/>
  </Section>;
}
