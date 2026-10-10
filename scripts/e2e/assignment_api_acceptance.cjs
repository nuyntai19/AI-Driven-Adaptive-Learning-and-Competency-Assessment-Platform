// Real HTTP + MySQL assertions. These are API integration cases, NOT Chrome E2E cases.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { root } = require('../ops/mysql_admin.cjs');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const { execSql, createAssignmentFixture, STUDENT01_A_ID, STUDENT02_A_ID } = require('./fixture_manager.cjs');
const base = 'http://localhost:3002/api/v1';
const results = [];
async function request(token, route, method = 'GET', body) {
  const response = await fetch(base + route, { method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(15000) });
  const text = await response.text();
  let responseBody = null;
  if (text) {
    try { responseBody = JSON.parse(text); }
    catch { throw new Error(`HTTP ${response.status}: response is not structured JSON (${route}).`); }
  }
  return { status: response.status, body: responseBody };
}
async function login(username, centerCode = 'EDUTWIN_A') {
  const response = await request(null, '/auth/login', 'POST', { username, centerCode, password: testPassword });
  assert.equal(response.status, 200);
  assert.ok(response.body.data.accessToken);
  return response.body.data.accessToken;
}
async function check(id, name, action) {
  try { await action(); results.push({ id, name, kind: 'real-http-mysql', status: 'PASS' }); console.log(`${id} PASS ${name}`); }
  catch (error) { results.push({ id, name, kind: 'real-http-mysql', status: 'FAIL', error: error.message }); console.error(`${id} FAIL ${error.message}`); }
}
function fixture(tag, questionConfigs, extra = {}) {
  return createAssignmentFixture({ tag, title: 'Kiểm thử bản nháp và nộp bài', questionConfigs, ...extra });
}
const route = f => `/students/me/assignments/${f.assignmentId}`;
const answer = (questionId = 10000, finalAnswer = '10000', reasoningText = 'Với x=3 thì y=2*3+1=7.') =>
  ({ questionId, finalAnswer, reasoningText, confidence: 80, timeSpentSeconds: 12, answerChanges: 1 });
async function run() {
  manifest();
  const student = await login('student01');
  const otherStudent = await login('student02');
  const otherCenter = await login('student01', 'EDUTWIN_B');
  const manager = await login('manager');
  const teacher = await login('teacher.math');
  const f = fixture('CORE', [{ questionId: 10000, points: 1 }, { questionId: 10006, points: 1 }]);
  await check('API01', 'Unauthenticated access denied', async () => assert.equal((await request(null, route(f))).status, 401));
  await check('API02', 'Non-target student denied', async () => assert.ok([403,404].includes((await request(otherStudent, route(f))).status)));
  await check('API03', 'Other center denied', async () => assert.ok([403,404].includes((await request(otherCenter, route(f))).status)));
  await check('API04', 'Manager cannot submit as student', async () => assert.equal((await request(manager, route(f)+'/submit','POST',{})).status,403));
  await check('API05', 'Teacher cannot submit as student', async () => assert.equal((await request(teacher, route(f)+'/submit','POST',{})).status,403));
  await check('API06', 'Submit before start rejected', async () => assert.equal((await request(student,route(f)+'/submit','POST',{answers:[answer()]})).status,409));
  let startedAt;
  await check('API07', 'Start returns authoritative clock', async () => {
    const response = await request(student,route(f)+'/start','POST',{}); assert.equal(response.status,200);
    const data=response.body.data; startedAt=data.startedAt; assert.ok(startedAt); assert.ok(data.effectiveExpiresAt); assert.ok(data.remainingSeconds<=3600 && data.remainingSeconds>3500);
  });
  await check('API08', 'Start twice cannot reset timer', async () => assert.equal((await request(student,route(f)+'/start','POST',{})).body.data.startedAt,startedAt));
  const draft=[answer(),answer(10006,'10/2','Ta tính được 5.')];
  await check('API09', 'Draft persisted with version', async () => {
    const r=await request(student,route(f)+'/draft','PUT',{answers:draft,draftVersion:1}); assert.equal(r.status,200); assert.equal(r.body.draftVersion,1);
    assert.equal((await request(student,route(f))).body.data.draftAnswers.length,2);
  });
  await check('API10', 'Same-version identical retry is idempotent', async () => assert.equal((await request(student,route(f)+'/draft','PUT',{answers:draft,draftVersion:1})).status,200));
  await check('API11', 'Same-version changed payload receives 409', async () => assert.equal((await request(student,route(f)+'/draft','PUT',{answers:[answer(10000,'10001')],draftVersion:1})).status,409));
  await check('API12', 'Old/unversioned snapshots cannot overwrite', async () => {
    for (const version of [0,undefined]) assert.equal((await request(student,route(f)+'/draft','PUT',{answers:[answer(10000,'10001')],draftVersion:version})).status,409);
    assert.equal((await request(student,route(f))).body.data.draftAnswers[0].finalAnswer,'10000');
  });
  await check('API13', 'Competing saves one winner one conflict', async () => {
    const responses=await Promise.all(['10000','10001'].map(value=>request(student,route(f)+'/draft','PUT',{answers:[answer(10000,value)],draftVersion:2})));
    assert.deepEqual(responses.map(r=>r.status).sort(),[200,409]);
  });
  assert.equal((await request(student,route(f)+'/draft','PUT',{answers:draft,draftVersion:3})).status,200);
  await check('API14', 'Required reasoning rejected before deadline', async () => assert.equal((await request(student,route(f)+'/submit','POST',{answers:[answer(10000,'10000','')]})).status,422));
  let submit;
  await check('API15', 'Whole assignment batch submit', async () => {
    const r=await request(student,route(f)+'/submit','POST',{answers:draft}); assert.equal(r.status,200); submit=r.body;
    assert.equal(submit.isCompleted,true); assert.equal(submit.submittedAttemptsCount,2);
    assert.equal(Number(execSql(`SELECT COUNT(*) FROM attempts WHERE assignment_id='${f.assignmentId}';`).trim()),2);
  });
  await check('API16', 'Native question MaxScore and rational equivalence', async () => {
    const rows=execSql(`SELECT question_id,awarded_score FROM attempts WHERE assignment_id='${f.assignmentId}' ORDER BY question_id;`).trim().split('\n').map(l=>l.trim().split('\t'));
    assert.deepEqual(rows.map(r=>[Number(r[0]),Number(r[1])]),[[10000,10],[10006,20]]);
  });
  await check('API17', 'Duplicate submit returns same records', async () => {
    const r=await request(student,route(f)+'/submit','POST',{answers:draft}); assert.equal(r.status,200);
    assert.equal(Number(execSql(`SELECT COUNT(*) FROM attempts WHERE assignment_id='${f.assignmentId}';`).trim()),2);
  });
  await check('API18', 'Completed draft cannot change', async () => assert.equal((await request(student,route(f)+'/draft','PUT',{answers:draft,draftVersion:4})).status,409));
  const timed=fixture('TIMEOUT',[{questionId:10000,points:1}]);
  assert.equal((await request(student,route(timed)+'/start','POST',{})).status,200);
  assert.equal((await request(student,route(timed)+'/draft','PUT',{answers:[answer(10000,'10000','')],draftVersion:1})).status,200);
  // Both timestamps are moved: the draft must precede the authoritative expiry.
  execSql(`UPDATE student_assignment_progress SET started_at=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 2 HOUR), draft_saved_at=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 90 MINUTE) WHERE assignment_id='${timed.assignmentId}';`);
  await check('API19', 'Late draft denied', async () => assert.equal((await request(student,route(timed)+'/draft','PUT',{answers:[answer(10000,'10001')],draftVersion:2})).status,409));
  await check('API20', 'Expired submit uses server draft, not late answers or fake reasoning', async () => {
    const r=await request(student,route(timed)+'/submit','POST',{answers:[answer(10000,'10001','Late injected reasoning')]}); assert.equal(r.status,200);
    const saved=JSON.parse(execSql(`SELECT JSON_OBJECT('answer',final_answer,'reasoning',COALESCE(reasoning_text,'')) FROM attempts WHERE assignment_id='${timed.assignmentId}';`).trim());
    assert.deepEqual(saved,{answer:'10000',reasoning:''});
  });
  const voided=fixture('VOID',[{questionId:10000,points:1,isVoided:true},{questionId:10006,points:1}]);
  assert.equal((await request(student,route(voided)+'/start','POST',{})).status,200);
  await check('API21', 'Voided question gives full credit without synthetic attempt', async () => {
    const r=await request(student,route(voided)+'/submit','POST',{answers:[answer(10006,'5','Giải ra 5.')]});assert.equal(r.status,200);
    assert.equal(Number(execSql(`SELECT COUNT(*) FROM attempts WHERE assignment_id='${voided.assignmentId}' AND question_id=10000;`).trim()),0);
    const data=(await request(student,route(voided))).body.data; assert.equal(data.questions.find(q=>q.questionId==='10000').isVoided,true);
    assert.equal(data.questions.find(q=>q.questionId==='10000').voidedScore,5);
  });
  await check('API22', 'Void does not archive shared bank question', async () => assert.equal(execSql('SELECT status FROM questions WHERE question_id=10000;').trim(),'Active'));
  const foreign=fixture('FOREIGN',[{questionId:10000,points:1}]);
  assert.equal((await request(student,route(foreign)+'/start','POST',{})).status,200);
  await check('API23', 'Foreign/duplicate question IDs never create extra attempts', async () => {
    const r=await request(student,route(foreign)+'/submit','POST',{answers:[answer(),answer(),answer(20000,'20000','Other center')]});
    assert.ok([200,400].includes(r.status));
    assert.equal(Number(execSql(`SELECT COUNT(*) FROM attempts WHERE assignment_id='${foreign.assignmentId}' AND question_id<>10000;`).trim()),0);
  });
  await check('API24', 'Worker reaches terminal state or teacher-review fallback', async () => {
    assert.ok(submit?.lastAnalysisJobId);
    for(let n=0;n<35;n++) {
      const r=await request(student,`/learning/analysis-jobs/${submit.lastAnalysisJobId}`);assert.equal(r.status,200);
      if(['Completed','FallbackCompleted'].includes(r.body.data.status)) {
        assert.equal(r.body.data.terminal,true);
        assert.ok(r.body.data.feedbackUrl);
        assert.equal((await request(student,r.body.data.feedbackUrl.replace('/api/v1',''))).status,200);
        return;
      }
      if(r.body.data.status==='FailedTerminal') throw new Error('Worker failed instead of safe fallback.');
      await new Promise(resolve=>setTimeout(resolve,1000));
    }
    throw new Error('Job still pending/processing; loading is NOT completion.');
  });
  const ui=fixture('CHROME',[{questionId:10000,points:1},{questionId:10006,points:1}],{studentIds:[STUDENT02_A_ID]});
  assert.equal((await request(otherStudent,route(ui)+'/start','POST',{})).status,200);
  assert.equal((await request(otherStudent,route(ui)+'/draft','PUT',{answers:draft,draftVersion:1})).status,200);
  const evidenceDir=path.join(root,'docs/verification/evidence/assignment_submission_acceptance',manifest().runId);
  fs.mkdirSync(evidenceDir,{recursive:true});
  fs.writeFileSync(path.join(evidenceDir,'api_results.json'),JSON.stringify({runId:manifest().runId,results,
    chromeServerHydrationUrl:`http://localhost:3002/hoc-tap/luyen-tap?assignmentId=${ui.assignmentId}`,
    testMode:'real MySQL, separate seed/JWT, Gemini key deliberately absent: provider fallback only',
    generatedAt:new Date().toISOString()},null,2));
  console.log(JSON.stringify({passed:results.filter(r=>r.status==='PASS').length,failed:results.filter(r=>r.status==='FAIL').length,evidenceDir,
    chromeServerHydrationUrl:`http://localhost:3002/hoc-tap/luyen-tap?assignmentId=${ui.assignmentId}`}));
  if(results.some(r=>r.status!=='PASS'))process.exitCode=1;
}
if(require.main===module)run().catch(error=>{console.error(error.message);process.exitCode=1;});
module.exports={run};
