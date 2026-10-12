const fs=require('node:fs');const assert=require('node:assert/strict');const {manifest,testPassword}=require('./acceptance_stack.cjs');const base='http://127.0.0.1:3002/api/v1';
manifest();
(async()=>{const results=[];
for(const [role,username,centerCode,routes] of [
 ['Student','student01','EDUTWIN_A',['/auth/me','/students/me/dashboard','/students/me/workspace-summary','/students/me/academic-context','/students/me/twin','/students/me/assignments','/notifications','/notifications/summary','/platform/operations','/platform/centers']],
 ['Teacher','teacher.math','EDUTWIN_A',['/auth/me','/classes','/questions','/assignments','/curriculums','/notifications','/notifications/summary','/platform/operations','/platform/centers']],
 ['CenterManager','manager','EDUTWIN_A',['/auth/me','/students','/teachers','/classes','/subjects','/notifications','/notifications/summary','/platform/operations']],
 ['PlatformAdmin','platform.admin','PLATFORM',['/auth/me','/platform/centers','/platform/operations','/platform/ai-usage','/platform/storage','/notifications','/notifications/summary','/students/me/dashboard','/questions','/assignments','/curriculums','/classes']]
]){const l=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({username,centerCode,password:testPassword})});const token=(await l.json()).data?.accessToken;if(!token)throw new Error(`Login ${role}: ${l.status}`);
for(const route of routes){const r=await fetch(base+route,{headers:{authorization:`Bearer ${token}`},signal:AbortSignal.timeout(20000)});const body=await r.text();results.push({role,route,status:r.status,json:body.trim().startsWith('{')});}
}
fs.writeFileSync('scratch/functional-fixes-smoke-results.json',JSON.stringify({results},null,2));
assert.equal(results.length,39);
for(const result of results){
 const denied=(result.role!=='PlatformAdmin'&&result.route.startsWith('/platform/')) ||
  (result.role==='PlatformAdmin'&&['/students/me/dashboard','/questions','/assignments','/curriculums','/classes'].includes(result.route));
 const expected=denied?403:result.route==='/students/me/twin'?400:200;
 assert.equal(result.status,expected,`${result.role} ${result.route}`);assert.ok(result.json);
}
console.log('PASS 39 role smoke routes with expected HTTP status and JSON.');
})().catch(e=>{console.error(e.message);process.exitCode=1;});
