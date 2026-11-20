// Isolated acceptance stack only: prepare genuine reviewed results for student UI QA.
const fs=require('node:fs'),assert=require('node:assert/strict');
const {manifest,testPassword}=require('./acceptance_stack.cjs');
const base='http://127.0.0.1:3002/api/v1';
async function req(t,p,m='GET',b){const r=await fetch(base+p,{method:m,headers:{'content-type':'application/json',...(t?{authorization:`Bearer ${t}`}:{})},...(b===undefined?{}:{body:JSON.stringify(b)}),signal:AbortSignal.timeout(20000)});return {status:r.status,data:await r.json()};}
async function login(username){const r=await req(null,'/auth/login','POST',{centerCode:'EDUTWIN_A',username,password:testPassword});assert.equal(r.status,200);return r.data.data.accessToken;}
async function main(){manifest();const id=process.argv[2];assert.match(id??'',/^[0-9a-f-]{36}$/i,'Pass the disposable assignment ID as the first argument');const t=await login('teacher.math');const studentId='d0000000-0000-0000-0001-000000000004';
let q=await req(t,`/teachers/me/review-queue?assignmentId=${id}&studentId=${studentId}&includeAllQuestions=true`);assert.equal(q.status,200);assert.ok(q.data.data.length);
assert.ok(q.data.data.every(a=>a.assignmentTitle?.startsWith('E2E_ACC_')),'Only synthetic acceptance fixtures may be reviewed');
const results=[];
for(const a of q.data.data){const r=await req(t,`/teachers/me/reasoning-analyses/${a.analysisId}/override`,'POST',{reasoningQuality:80,errorType:'None',feedback:'QA: Đã kiểm tra lập luận và đáp án trong dữ liệu kiểm thử.',isCorrect:true,awardedScore:a.maxScore,reason:'QA chuẩn bị luồng yêu cầu xem lại của học sinh.',overrideVersion:a.overrideVersion??0});assert.equal(r.status,200,JSON.stringify(r.data));results.push({name:`Teacher API reviews synthetic question ${a.questionOrderIndex}`,status:'PASS'});}
q=await req(t,`/teachers/me/review-queue?assignmentId=${id}&studentId=${studentId}&includeAllQuestions=true`);const r=await req(t,`/teachers/me/assignments/${id}/approve-result`,'POST',{studentId,finalReviewVersion:q.data.data[0].finalReviewVersion??0,note:'QA chốt bài giả lập để kiểm thử luồng học sinh.'});assert.equal(r.status,200,JSON.stringify(r.data));results.push({name:'Teacher API finalizes own synthetic result',status:'PASS'});
fs.writeFileSync('docs/verification/evidence/student-deep-qa-2026-11-20/review-fixtures.json',JSON.stringify({assignmentId:id,results},null,2));console.log(JSON.stringify(results));}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
