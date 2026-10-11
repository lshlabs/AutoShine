export type Binding = { id: string; name: string };
export function fingerprintMatches(expected: string, entered: string): boolean {
  const normalize = (s:string)=>s.replace(/[:\s]/g,'').toUpperCase();
  const pin = normalize(expected);
  return /^[A-F0-9]{64}$/.test(pin) && pin===normalize(entered);
}
export type Rule = { device: Binding; start: string; startEnabled?:boolean|null; startMuted?:boolean|null; end: string; optional: boolean; enableOnEnd: boolean; endEnabled?: boolean | null };
export type Backend = {
  schemaVersion: number;
  sunshine: { configPath: string; basePort: number };
  display: { sunshineDeviceId: string; policy: string; useClientResolution: boolean; useClientRefreshRate: boolean; holdOnDisconnect: boolean; windowsDevice?: Binding | null };
  audio: { controlToolPath: string; remoteOutput: Binding | null; restore: string; rules: Rule[]; localOutputPriority: Binding[]; reapplyOnLocalReconnect: boolean };
  hotkey: { enabled: boolean; control: boolean; alt: boolean; shift: boolean; key: string };
};
export type Device = { id: string; name: string; kind: string; connected: boolean; state?: string };
export const visibleByDefault = (d: Device) => d.connected && d.state !== 'disabled';
export type EndRule = { mode: 'restore' | 'keep' | 'custom'; enable: boolean|null; mute: boolean; selectedMute?: boolean; legacyEnable?: boolean; legacyActivation?: boolean };
export type Config = {
  blockScope: 'all' | 'selected' | 'none'; blockSet: string[]; blockAction: Record<string, 'mute' | 'disable'>;
  startRule?:Record<string,{enable:boolean|null;mute:boolean|null}>;
  chain: string[]; endRule: Record<string, EndRule>; hotkeyEnabled: boolean; hotkey: string;
  backend: Backend;
};
export function remoteBinding(audio: {id:string;name:string}[], saved: Binding | null): Binding | null {
  const matches = audio.filter(d => /^(?:Steam Streaming Speakers|.*\(Steam Streaming Speakers\))$/i.test(d.name));
  if (saved && /Steam Streaming Speakers/i.test(saved.name)) return matches.find(d => d.id === saved.id) ?? saved;
  return matches.length === 1 ? matches[0] : null;
}
export function hotkeyValue(text: string, enabled: boolean) {
  const parts = text.split('+').map(s => s.trim());
  const key = parts.at(-1) || '';
  return { enabled, control: parts.includes('Ctrl'), alt: parts.includes('Alt'), shift: parts.includes('Shift'), key };
}
export function fromBackend(b: Backend, devices: Device[]): Config {
  const local = devices.filter(d => d.id !== b.audio.remoteOutput?.id && d.name !== 'Steam Streaming Speakers');
  const targets = b.audio.rules.filter(r => r.start !== 'unchanged').map(r => r.device.id);
  return {
    backend: structuredClone(b),
    startRule:Object.fromEntries(b.audio.rules.filter(r=>r.startEnabled!=null||r.startMuted!=null).map(r=>[r.device.id,{enable:r.startEnabled??(r.start==='disable'?false:null),mute:r.startMuted??(r.start!=='unchanged'?true:null)}])),
    blockScope: targets.length === 0 ? 'none' : local.length > 0 && local.every(d => targets.includes(d.id)) ? 'all' : 'selected',
    blockSet: targets, blockAction: Object.fromEntries(b.audio.rules.map(r => [r.device.id, r.start === 'disable' ? 'disable' : 'mute'])),
    chain: b.audio.localOutputPriority.map(d => d.id),
    endRule: Object.fromEntries(b.audio.rules.map(r => [r.device.id, {
      mode: r.end === 'previousState' ? 'restore' : r.end === 'unchanged' && r.endEnabled == null ? 'keep' : 'custom',
      enable: r.endEnabled ?? null, mute: r.end !== 'unmuted',
      selectedMute: r.end === 'selectedOutputMutedElseUnmuted', legacyEnable: r.enableOnEnd, legacyActivation: r.endEnabled == null,
    }])), hotkeyEnabled: b.hotkey.enabled,
    hotkey: [b.hotkey.control && 'Ctrl', b.hotkey.alt && 'Alt', b.hotkey.shift && 'Shift', b.hotkey.key].filter(Boolean).join(' + '),
  };
}
export function toBackend(c: Config, devices: Device[], remote: Binding | null): Backend {
  const b = structuredClone(c.backend);
  const binding = (id: string) => ({ id, name: (id === remote?.id ? remote.name : undefined) ?? devices.find(d => d.id === id)?.name ?? b.audio.rules.find(r => r.device.id === id)?.device.name ?? id });
  const ids = new Set([...devices.filter(d => d.id !== remote?.id).map(d => d.id), ...b.audio.rules.map(r => r.device.id), ...c.chain]);
  b.audio.restore = c.chain.some(id => id !== remote?.id) ? 'fixedProfile' : 'previousState';
  b.audio.remoteOutput = remote;
  b.audio.localOutputPriority = c.chain.filter(id => id !== remote?.id).map(binding);
  if (remote && c.endRule[remote.id]) ids.add(remote.id);
  b.audio.rules = [...ids].filter(id => id !== remote?.id || c.endRule[id]).map(id => {
    const previous = b.audio.rules.find(r => r.device.id === id);
    const end = c.endRule[id] ?? { mode: 'restore', enable: true, mute: false };
    const target = id !== remote?.id && (c.blockScope === 'all' || c.blockScope === 'selected' && c.blockSet.includes(id));
    return { device: binding(id), optional: previous?.optional ?? true,
      start: c.startRule?.[id] ? c.startRule[id].enable===false ? 'disable' : c.startRule[id].mute===true ? 'mute' : 'unchanged' : target ? c.blockAction[id] ?? 'mute' : 'unchanged',
      startEnabled: c.startRule?.[id] ? c.startRule[id].enable : previous?.startEnabled ?? null,
      startMuted: c.startRule?.[id] ? c.startRule[id].mute : previous?.startMuted ?? null,
      end: end.mode === 'restore' ? 'previousState' : end.mode === 'keep' ? 'unchanged' : end.selectedMute ? 'selectedOutputMutedElseUnmuted' : end.mute ? 'muted' : 'unmuted',
      enableOnEnd: end.legacyActivation || end.mode === 'restore' ? end.legacyEnable ?? false : false,
      endEnabled: end.legacyActivation===false || end.mode==='custom' && end.legacyActivation!==true ? end.enable : previous?.endEnabled ?? null,
    };
  });
  b.hotkey = hotkeyValue(c.hotkey, c.hotkeyEnabled);
  return b;
}
