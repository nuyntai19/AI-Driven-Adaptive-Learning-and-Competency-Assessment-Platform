const fs=require('node:fs');
const assert=require('node:assert/strict');
const {testPassword}=require('./acceptance_stack.cjs');
const subjectId='30000000-0000-0000-0000-000000000003',classId='51000000-0000-0000-0000-000000000010';
(async()=>{
 const base='http://127.0.0.1:3002/api/v1';
 const login=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({centerCode:'EDUTWIN_A',username:'student02',password:testPassword})});
 assert.equal(login.status,200);const token=(await login.json()).data.accessToken;
 const response=await fetch(base+'/students/me/learning-path/generate',{method:'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},body:JSON.stringify({subjectId,classId,targetWeeks:1,daysPerWeek:3,minutesPerDay:10,targetMastery:80,forceRegenerate:true})});
 assert.equal(response.status,200);const data=(await response.json()).data;
 const result={pathId:data.learningPathId,session:data.phases[0].weeks[0].sessions[0],url:`http://127.0.0.1:3002/hoc-tap/lo-trinh-hoc-tap?subjectId=${subjectId}&classId=${classId}`};
 fs.writeFileSync('scratch/functional-retest-path-ui.json',JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
