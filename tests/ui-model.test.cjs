const assert = require('node:assert/strict');
const {fromBackend,toBackend,remoteBinding,hotkeyValue} = require('../artifacts/ui-model-tests/model.js');
const devices = [{id:'local',name:'Speaker',kind:'output',connected:true},{id:'remote',name:'Steam Streaming Speakers',kind:'virtual',connected:true}];
const original = {schemaVersion:1,sunshine:{basePort:47989,configPath:''},display:{sunshineDeviceId:'',policy:'verifyOnly',useClientResolution:false,useClientRefreshRate:false,holdOnDisconnect:true},audio:{controlToolPath:'example.exe',remoteOutput:{id:'remote',name:'Steam Streaming Speakers'},restore:'fixedProfile',localOutputPriority:[{id:'local',name:'Speaker'}],reapplyOnLocalReconnect:false,rules:[{device:{id:'local',name:'Speaker'},start:'disable',end:'selectedOutputMutedElseUnmuted',optional:false,enableOnEnd:true}]},hotkey:{enabled:false,control:true,alt:true,shift:true,key:'F10'}};
let count=0;function check(name,fn){fn();count++;console.log('PASS '+name);}
check('remote end settings are saved without allowing start blocking or local priority',()=>{
  const c=fromBackend(original,devices);c.blockScope='all';c.chain.push('remote');
  c.endRule.remote={mode:'custom',enable:false,mute:true};
  const b=toBackend(c,devices,original.audio.remoteOutput);
  const r=b.audio.rules.find(r=>r.device.id==='remote');
  assert.equal(r.start,'unchanged');assert.equal(r.end,'muted');assert.equal(r.endEnabled,false);
  assert.equal(r.device.name,'Steam Streaming Speakers');assert.ok(!b.audio.localOutputPriority.some(d=>d.id==='remote'));
  assert.deepEqual(fromBackend(b,devices).endRule.remote,{mode:'custom',enable:false,mute:true,selectedMute:false,legacyEnable:false,legacyActivation:false});
});
check('legacy conditional mute and activation survive UI roundtrip',()=>{
  const b=toBackend(fromBackend(original,devices),devices,original.audio.remoteOutput);
  assert.equal(b.audio.rules[0].end,'selectedOutputMutedElseUnmuted');assert.equal(b.audio.rules[0].enableOnEnd,true);assert.equal(b.audio.rules[0].endEnabled,null);assert.equal(b.audio.rules[0].optional,false);
});
check('unrelated display and reconnect policy are preserved',()=>{const b=toBackend(fromBackend(original,devices),devices,original.audio.remoteOutput);assert.deepEqual(b.display,original.display);assert.equal(b.audio.reapplyOnLocalReconnect,false);});
check('explicit end disable maps without changing start policy',()=>{const c=fromBackend(original,devices);c.endRule.local={mode:'custom',enable:false,mute:true};const r=toBackend(c,devices,original.audio.remoteOutput).audio.rules[0];assert.equal(r.endEnabled,false);assert.equal(r.start,'disable');});
check('absent configured device binding remains available',()=>{const c=fromBackend(original,[]);const b=toBackend(c,[],original.audio.remoteOutput);assert.equal(b.audio.rules[0].device.id,'local');assert.equal(b.audio.localOutputPriority[0].id,'local');});
check('duplicate remote names cannot auto select',()=>assert.equal(remoteBinding([{id:'a',name:'Steam Streaming Speakers'},{id:'b',name:'Steam Streaming Speakers'}],null),null));
check('saved endpoint is not silently rebound by friendly name',()=>assert.equal(remoteBinding([{id:'new',name:'Steam Streaming Speakers'}],{id:'old',name:'Steam Streaming Speakers'}).id,'old'));
check('Windows friendly remote name can be detected',()=>assert.equal(remoteBinding([{id:'a',name:'Speakers (Steam Streaming Speakers)'}],null).id,'a'));
check('hotkey chord maps to Windows modifiers',()=>assert.deepEqual(hotkeyValue('Ctrl + Shift + K',true),{enabled:true,control:true,alt:false,shift:true,key:'K'}));
const {simulationSteps}=require('../artifacts/ui-model-tests/simulation.js');
const inactive={id:'inactive',name:'Inactive output',connected:true,enabled:false,muted:false};
const active={id:'active',name:'Active output',connected:true,enabled:true,muted:false};
check('changed final output appears once with both states and default marker',()=>{
  const steps=simulationSteps([active],[{...active,muted:true}],['active','active','active'],[],'end');
  assert.equal(steps.length,1);assert.equal(steps[0].defaultOutput,true);
  assert.equal(steps[0].enabled,true);assert.equal(steps[0].muted,true);
});
check('hidden devices do not appear in simulation changes even with saved policies',()=>{
  const steps=simulationSteps([inactive,active],[{...inactive,muted:true},active],['active','active','active'],[],'start',new Set(['active']));
  assert.ok(!steps.some(s=>s.text.includes(inactive.name)));
  assert.ok(steps.some(s=>s.text.includes(active.name)));
});
check('unchanged devices remain visible with both final states',()=>{
  const steps=simulationSteps([inactive,active],[inactive,active],['active','active','active'],[],'start');
  assert.equal(steps.length,2);const unchanged=steps.find(s=>s.text===inactive.name);assert.equal(unchanged.enabled,false);assert.equal(unchanged.muted,false);assert.equal(steps.filter(s=>s.defaultOutput).length,1);
});
check('disabled device activation shows the end policy reason',()=>{
  const steps=simulationSteps([inactive],[{...inactive,enabled:true}],['inactive','inactive','inactive'],[{device:{id:'inactive',name:inactive.name},end:'previousState'}],'end');
  assert.equal(steps[0].text,'Inactive output');assert.equal(steps[0].enabled,true);assert.equal(steps[0].muted,false);
});
check('disabled final device is not reported as a usable output',()=>{
  assert.equal(simulationSteps([inactive],[inactive],['inactive','inactive','inactive'],[],'end').at(-1).kind,'error');
});
check('disconnect and reconnect show the same device state rows as start',()=>{
  const before=[active,inactive];const defaults=['active','active','active'];
  const start=simulationSteps(before,before,defaults,[],'start');
  for(const event of ['drop','reconnect']){const steps=simulationSteps(before,before,defaults,[],event);assert.deepEqual(steps,start);assert.ok(!steps.some(s=>s.kind==='keep'));}
});
check('disabled and disconnected outputs are hidden by default',()=>{
  const {visibleByDefault}=require('../artifacts/ui-model-tests/model.js');
  assert.equal(visibleByDefault({...devices[0],state:'active'}),true);
  assert.equal(visibleByDefault({...devices[0],state:'disabled'}),false);
  assert.equal(visibleByDefault({...devices[0],connected:false,state:'unplugged'}),false);
});
check('certificate comparison requires a complete matching SHA-256 fingerprint',()=>{
  const {fingerprintMatches}=require('../artifacts/ui-model-tests/model.js');
  const pin='A1'.repeat(32);
  assert.equal(fingerprintMatches(pin,pin.toLowerCase().match(/../g).join(': ')),true);
  assert.equal(fingerprintMatches(pin,'B2'.repeat(32)),false);
  assert.equal(fingerprintMatches('',''),false);
  assert.equal(fingerprintMatches(pin,pin.slice(0,62)),false);
});
check('empty output priority preserves valid previous-state recovery',()=>{
  const saved=structuredClone(original);saved.audio.restore='previousState';saved.audio.localOutputPriority=[];
  const config=fromBackend(saved,devices);
  assert.deepEqual(config.backend,saved);
  const result=toBackend(config,devices,saved.audio.remoteOutput);
  assert.equal(result.audio.restore,'previousState');assert.deepEqual(result.audio.localOutputPriority,[]);
  config.chain=['local'];assert.equal(toBackend(config,devices,saved.audio.remoteOutput).audio.restore,'fixedProfile');
});
check('display simulation includes disabled screens and restores original states',()=>{
  const {displayStates}=require('../artifacts/ui-model-tests/simulation.js');
  const before=[{id:'physical',name:'Monitor',enabled:true},{id:'vdd',name:'VDD',enabled:false}];
  const start=displayStates(before,'vdd','ensureOnlyDisplay',false);
  assert.deepEqual(start.map(d=>d.enabled),[false,true]);
  assert.deepEqual(displayStates(before,'vdd','ensureOnlyDisplay',true),before);
  assert.deepEqual(displayStates(before,'vdd','ensureActive',false).map(d=>d.enabled),[true,true]);
  assert.deepEqual(displayStates(before,'vdd','verifyOnly',false),before);
});
check('editing sound alone preserves unspecified start activation',()=>{
  const c=fromBackend(original,devices);c.startRule={local:{enable:null,mute:true}};
  const b=toBackend(c,devices,original.audio.remoteOutput);const r=b.audio.rules.find(r=>r.device.id==='local');
  assert.equal(r.start,'mute');assert.equal(r.startEnabled,null);assert.equal(r.startMuted,true);
});
check('editing legacy end sound does not silently enable the device',()=>{
  const c=fromBackend(original,devices);const id=original.audio.rules[0].device.id;
  c.endRule[id]={...c.endRule[id],mode:'custom',mute:false,selectedMute:false,legacyActivation:false};
  const r=toBackend(c,devices,original.audio.remoteOutput).audio.rules.find(r=>r.device.id===id);
  assert.equal(r.end,'unmuted');assert.equal(r.endEnabled,null);
});
check('new inventory devices do not acquire an implicit end activation policy',()=>{
 const b=toBackend(fromBackend(original,devices),[...devices,{id:'new',name:'New output',kind:'output',connected:true}],original.audio.remoteOutput);
 assert.equal(b.audio.rules.find(r=>r.device.id==='new').endEnabled,null);
});
console.log(`${count} UI model checks passed`);
