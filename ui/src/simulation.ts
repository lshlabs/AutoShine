import type { Backend } from './model';
export type VirtualDevice = {id:string;name:string;connected:boolean;enabled:boolean;muted:boolean};
export type SimulationStep = {text:string;enabled?:boolean;muted?:boolean;defaultOutput?:boolean;kind:'switch'|'block'|'info'|'skip'|'keep'|'error'};
// Show each visible connected device once, including unchanged final states.
export function simulationSteps(before: VirtualDevice[], after: VirtualDevice[], defaults: (string|null)[], rules: Backend['audio']['rules'], event: string, visibleIds?: ReadonlySet<string>): SimulationStep[] {
  const steps: SimulationStep[] = [];
  const output = after.find(d=>d.id===defaults[0]);
  const validOutput = !!output?.connected && !!output.enabled;
  for (const d of after) {
    const isOutput = validOutput && d.id === output?.id;
    if (visibleIds && !visibleIds.has(d.id) && !isOutput) continue;
    if (!d.connected) continue;
    const previous = before.find(x=>x.id===d.id);
    const blocking = previous && (previous.enabled!==d.enabled && !d.enabled || previous.muted!==d.muted && d.muted);
    steps.push({kind: isOutput ? 'switch' : blocking ? 'block' : 'info', text:d.name,enabled:d.enabled,muted:d.muted,defaultOutput:isOutput});
  }
  if (!validOutput) steps.push({kind:'error',text:'사용 가능한 가상 기본 출력이 없습니다.'});
  return steps;
}

export type SimulatedDisplay={id:string;name:string;enabled:boolean|undefined};
export function displayStates(before:SimulatedDisplay[],target:string,policy:string,restore:boolean):SimulatedDisplay[] {
  if(restore||policy==='verifyOnly'||!before.some(d=>d.id===target))return before.map(d=>({...d}));
  return before.map(d=>({...d,enabled:d.id===target?true:policy==='ensureOnlyDisplay'?false:d.enabled}));
}
