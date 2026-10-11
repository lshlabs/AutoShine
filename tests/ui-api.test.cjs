const assert = require('node:assert/strict');
(async () => {
  const {api,setToken} = await import('../ui/src/api.mjs');
  function responses(items) {
    const calls=[]; setToken('old');
    global.fetch=async (path,options)=>{calls.push({path,token:options.headers['X-AutoShine-Token']});const r=items.shift();assert.ok(r,'Unexpected request');return {status:r.status,ok:r.status===200,text:async()=>r.body};};
    return calls;
  }
  responses([{status:500,body:''}]); await assert.rejects(api('hotkey/check',{}),/500/);
  const calls=responses([{status:403,body:''},{status:200,body:'{"token":"new"}'},{status:200,body:'{"message":"available"}'}]);
  assert.equal((await api('hotkey/check',{})).message,'available'); assert.equal(calls[2].token,'new');
  const denied=responses([{status:403,body:''},{status:200,body:'{"token":"new"}'},{status:403,body:''}]);await assert.rejects(api('hotkey/check',{}),/403/);assert.equal(denied.length,3);
  responses([{status:500,body:'<html>Error</html>'}]);await assert.rejects(api('hotkey/check',{}),/서버 응답/);
  responses([{status:500,body:'null'}]);await assert.rejects(api('hotkey/check',{}),/500/);
  console.log('5 UI API checks passed');
})().catch(e=>{console.error(e);process.exitCode=1;});
