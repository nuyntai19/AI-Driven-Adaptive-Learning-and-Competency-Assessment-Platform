const fs=require('node:fs');
const assert=require('node:assert/strict');
const {createAssignmentFixture,execSql,STUDENT02_A_ID}=require('./fixture_manager.cjs');
const {testPassword}=require('./acceptance_stack.cjs');
const file='scratch/functional-retest-browser-fixture.json';
const base='http://127.0.0.1:3002/api/v1';
(async()=>{
 const login=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({username:'student02',centerCode:'EDUTWIN_A',password:testPassword})});
 assert.equal(login.status,200);const token=(await login.json()).data.accessToken;
 async function req(route,method='GET',body){const response=await fetch(base+route,{method,headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)}),signal:AbortSignal.timeout(30000)});return {status:response.status,body:await response.json()};}
 let fixture;
 if(process.argv[2]==='create'){
  fixture=createAssignmentFixture({tag:'RETEST_BROWSER',title:'Retest edit reload submit',timeLimitMinutes:1440,studentIds:[STUDENT02_A_ID]});
  fixture.answers=[{questionId:'10000',finalAnswer:execSql('SELECT option_id FROM question_options WHERE question_id=10000 AND is_correct=1 LIMIT 1;').trim(),reasoningText:'RETEST_FIRST_REASONING: With x=3, y=7.',confidence:80,timeSpentSeconds:12,answerChanges:1},
   {questionId:'10001',finalAnswer:execSql('SELECT correct_answer FROM questions WHERE question_id=10001;').trim(),reasoningText:'RETEST_SECOND_REASONING: The denominator cannot equal zero.',confidence:80,timeSpentSeconds:12,answerChanges:1}];
  assert.equal((await req(`/students/me/assignments/${fixture.assignmentId}/start`,'POST',{})).status,200);
  assert.equal((await req(`/students/me/assignments/${fixture.assignmentId}/draft`,'PUT',{draftVersion:1,answers:fixture.answers})).status,200);
  fs.writeFileSync(file,JSON.stringify(fixture,null,2));
 } else fixture=JSON.parse(fs.readFileSync(file));
 const detail=await req(`/students/me/assignments/${fixture.assignmentId}`);assert.equal(detail.status,200);
 const draft=detail.body.data.draftAnswers;
 if(process.argv[2]==='after-edit'){
  for(const expected of fixture.answers){const saved=draft.find(a=>String(a.questionId)===expected.questionId);assert.equal(saved.finalAnswer,expected.finalAnswer);assert.equal(saved.reasoningText,expected.questionId==='10000'?'RETEST_UPDATED_REASONING: y=2*3+1=7.':expected.reasoningText);}
 }
 const record={assignmentId:fixture.assignmentId,status:detail.status,draft,draftVersion:detail.body.data.draftVersion,
   url:`http://127.0.0.1:3002/hoc-tap/luyen-tap?assignmentId=${fixture.assignmentId}`,
   data:process.argv[2]==='after-submit'?detail.body.data:undefined};
 fs.writeFileSync(`scratch/functional-retest-browser-${process.argv[2]}.json`,JSON.stringify(record,null,2));
 console.log(JSON.stringify({assignmentId:fixture.assignmentId,url:record.url,status:detail.status,draftVersion:record.draftVersion,mode:process.argv[2]}));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
