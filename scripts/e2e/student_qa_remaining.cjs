// Remaining student QA fixtures: disposable empty student and scheduled/withheld exams.
const fs=require('node:fs'), assert=require('node:assert/strict');
const {manifest,testPassword}=require('./acceptance_stack.cjs');
const {createAssignmentFixture,execSql}=require('./fixture_manager.cjs');
const base='http://127.0.0.1:3002/api/v1';
const results=[];
async function req(token,route,method='GET',body){const r=await fetch(base+route,{method,headers:{'content-type':'application/json',...(token?{authorization:`Bearer ${token}`}:{})},...(body===undefined?{}:{body:JSON.stringify(body)}),signal:AbortSignal.timeout(20000)});const t=await r.text();return{status:r.status,data:t?JSON.parse(t):null};}
async function login(username){const r=await req(null,'/auth/login','POST',{centerCode:'EDUTWIN_A',username,password:testPassword});assert.equal(r.status,200);return r.data.data.accessToken;}
async function check(name,fn){try{await fn();results.push({name,status:'PASS'});}catch(e){results.push({name,status:'FAIL',error:e.message});}}
async function main(){
 manifest();const manager=await login('manager'),student=await login('student01');
 const username='qa.empty.'+Date.now();
 await check('Create disposable student without classes',async()=>{const r=await req(manager,'/students','POST',{username,temporaryPassword:testPassword,fullName:'Học sinh QA chưa có lớp',gradeLevel:10,classIds:[]});assert.equal(r.status,201);});
 const future=createAssignmentFixture({tag:'QA_FUTURE',title:'QA kỳ thi chưa đến giờ',questionConfigs:[{questionId:10000,points:1}]});
 execSql(`UPDATE assignments SET assignment_kind='Midterm',exam_at=DATE_ADD(UTC_TIMESTAMP(),INTERVAL 1 HOUR) WHERE assignment_id='${future.assignmentId}';`);
 await check('Future exam rejects start before its schedule',async()=>assert.equal((await req(student,`/students/me/assignments/${future.assignmentId}/start`,'POST',{})).status,409));
 await check('Future exam hides question content',async()=>{const r=await req(student,`/students/me/assignments/${future.assignmentId}`);assert.equal(r.status,200);assert.ok(r.data.data.questions.every(q=>!q.questionText));});
 const withheld=createAssignmentFixture({tag:'QA_WITHHELD',title:'QA bài thi chờ công bố điểm',questionConfigs:[{questionId:10000,points:1}]});
 execSql(`UPDATE assignments SET assignment_kind='Midterm',exam_at=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 1 HOUR) WHERE assignment_id='${withheld.assignmentId}';`);
 await check('Current term exam accepts a valid submission',async()=>{
  assert.equal((await req(student,`/students/me/assignments/${withheld.assignmentId}/start`,'POST',{})).status,200);
  assert.equal((await req(student,`/students/me/assignments/${withheld.assignmentId}/submit`,'POST',{answers:[{questionId:'10000',finalAnswer:'10000',reasoningText:'Thay x = 3 vào y = 2x + 1, được 7.',confidence:80,timeSpentSeconds:12,answerChanges:0}]})).status,200);
 });
 await check('Submitted term exam keeps results hidden before publication',async()=>{const r=await req(student,`/students/me/assignments/${withheld.assignmentId}`);assert.equal(r.status,200);assert.equal(r.data.data.resultsPublishedAt,null);assert.ok(r.data.data.questions.every(q=>q.effectiveIsCorrect==null));});
 const dir='docs/verification/evidence/student-deep-qa-2026-11-20';fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(dir+'/remaining-fixtures.json',JSON.stringify({emptyUsername:username,future,withheld,results},null,2));console.log(JSON.stringify({emptyUsername:username,future:future.assignmentId,withheld:withheld.assignmentId,results}));
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
