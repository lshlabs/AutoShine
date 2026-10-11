const assert=require('node:assert/strict');
const fs=require('node:fs');
const {translate,setLanguage}=require('../artifacts/i18n-check/translate.js');
setLanguage('en');
const files=['ui/src/App.tsx','ui/src/model.ts','ui/src/simulation.ts','ui/src/api.mjs','src/AutoShine.Web/Program.cs','src/AutoShine.Web/FilePicker.cs','src/AutoShine.Sunshine/SunshineConfiguration.cs','src/AutoShine.Sunshine/SunshineInspector.cs','src/AutoShine.Windows/SunshineQuit.cs','src/AutoShine.Windows/CredentialVault.cs','src/AutoShine.Core/Policy.cs','src/AutoShine.Windows/HotkeyProbe.cs','src/AutoShine.Windows/AudioControl.cs'];
const missing=new Set();
for(const file of files){
 const source=fs.readFileSync(file,'utf8');
 for(const pattern of [/"([^"\r\n]*)"/g,/'([^'\r\n]*)'/g,/>\s*([^<>{}\r\n]+)\s*</g]){
  for(const match of source.matchAll(pattern)){
   const value=match[1].trim();
   if(value==='한국어')continue;
   if(/[가-힣]/.test(value)&&/[가-힣]/.test(translate(value)))missing.add(value);
  }
 }
}
assert.deepEqual([...missing],[], 'Untranslated UI/API messages');
for(const value of ['서버 응답을 해석할 수 없습니다. 오류 코드: 500','요청에 실패했습니다. 오류 코드: 403','중복 Sunshine 설정: output_name','지원하지 않는 설정 버전입니다. 오디오 목록이 없습니다.',' · 12:00:00 저장됨 · 자동화 프로그램 반영 확인 필요'])assert.ok(!/[가-힣]/.test(translate(value)),value);
assert.equal(translate(' VDD 설치 '),' VDD installation ');
assert.equal(translate('내 모니터 · 123'),'내 모니터 · 123');
setLanguage('ko');assert.equal(translate('Sunshine 연동'),'Sunshine 연동');
console.log('4 i18n checks passed (source coverage, dynamic messages, whitespace/device names, Korean mode)');
