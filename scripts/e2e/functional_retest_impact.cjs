const assert=require('node:assert/strict');
const fs=require('node:fs');
const crypto=require('node:crypto');
const {execSql}=require('./fixture_manager.cjs');
const {testPassword}=require('./acceptance_stack.cjs');
const base='http://127.0.0.1:5001/api/v1', subject='30000000-0000-0000-0000-000000000003', classId='51000000-0000-0000-0000-000000000010';
const results=[], pass=id=>{results.push({id,passed:true});console.log(`PASS ${id}`);};
async function request(token,url,method='GET',body){const r=await fetch(base+url,{method,signal:AbortSignal.timeout(30000),headers:{authorization:`Bearer ${token}`,...(body instanceof FormData?{}:{'content-type':'application/json'})},...(body===undefined?{}:{body:body instanceof FormData?body:JSON.stringify(body)})});const text=await r.text();return {status:r.status,body:text?JSON.parse(text):{}};}
async function login(username){const r=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({centerCode:'EDUTWIN_A',username,password:testPassword})});assert.equal(r.status,200);return (await r.json()).data.accessToken;}
async function run(){
 const teacher=await login('teacher.math'), student=await login('student01');
 async function preview(){const form=new FormData();form.append('file',new Blob(['QuestionText,QuestionType,CorrectAnswer,Solution\nImpact fixture,ShortAnswer,2,Explanation']),'impact.csv');const r=await request(teacher,'/questions/import/preview','POST',form);assert.equal(r.status,200);return r.body.data.previewToken;}
 const body=token=>({subjectId:subject,primaryTopicNodeId:'10000',previewToken:token});
 const token=await preview();
 const race=await Promise.all([request(teacher,'/questions/import/confirm','POST',body(token)),request(teacher,'/questions/import/confirm','POST',body(token))]);
 assert.deepEqual(race.map(r=>r.status).sort(),[200,400]);pass('IMPORT_CONCURRENT_CONFIRM_ONE_WINNER');
 for(const [id,table,key] of [['SUBJECT','subjects',`subject_id='${subject}'`]]){
  const previewToken=await preview();const before=Number(execSql("SELECT COUNT(*) FROM questions WHERE question_text='Impact fixture';").trim());
  execSql(`UPDATE ${table} SET is_active=0 WHERE ${key};`);
  try{const rejected=await request(teacher,'/questions/import/confirm','POST',body(previewToken));assert.equal(rejected.status,404);assert.equal(Number(execSql("SELECT COUNT(*) FROM questions WHERE question_text='Impact fixture';").trim()),before);pass(`IMPORT_REVALIDATES_INACTIVE_${id}_WITHOUT_WRITES`);}
  finally{execSql(`UPDATE ${table} SET is_active=1 WHERE ${key};`);}
 }
 assert.throws(()=>execSql('UPDATE knowledge_nodes SET is_active=0 WHERE node_id=10000;'),/EDUTWIN_ACADEMIC_DEPENDENCY/);
 assert.equal(execSql('SELECT is_active FROM knowledge_nodes WHERE node_id=10000;').trim(),'1');pass('ACTIVE_DEPENDENCY_GUARD_PRESERVES_IN_USE_TOPIC');
 const unusedId=execSql("INSERT INTO knowledge_nodes(center_id,subject_id,node_type,node_code,node_name,order_index,exam_importance,estimated_learning_minutes,is_active,created_at,updated_at,is_deleted,row_version) SELECT center_id,subject_id,'Topic',CONCAT('RETEST_',UUID()),'Inactive retest fixture',0,0,10,0,UTC_TIMESTAMP(),UTC_TIMESTAMP(),0,1 FROM knowledge_nodes WHERE node_id=10000; SELECT LAST_INSERT_ID();").trim();
 assert.match(unusedId,/^\d+$/);
 const unusedToken=await preview();const countBefore=Number(execSql("SELECT COUNT(*) FROM questions WHERE question_text='Impact fixture';").trim());
 const inactive=await request(teacher,'/questions/import/confirm','POST',{...body(unusedToken),primaryTopicNodeId:unusedId});assert.equal(inactive.status,404);
 assert.equal(Number(execSql("SELECT COUNT(*) FROM questions WHERE question_text='Impact fixture';").trim()),countBefore);pass('IMPORT_REVALIDATES_INACTIVE_TOPIC_WITHOUT_WRITES');
 const generate=()=>request(student,'/students/me/learning-path/generate','POST',{subjectId:subject,classId,targetWeeks:1,daysPerWeek:3,minutesPerDay:20,targetMastery:80,forceRegenerate:true});
 const generated=await generate();assert.equal(generated.status,200);const session=generated.body.data.phases[0].weeks[0].sessions[0];assert.ok(session.recommendedQuestionIds.length);
 const route=s=>`/students/me/learning-path/sessions/${s.sessionId}?subjectId=${subject}&classId=${classId}`;
 assert.equal((await request(student,route(session),'PATCH',{status:'NotStarted',classId})).status,400);pass('SESSION_REJECTS_INVALID_TRANSITION');
 assert.equal((await request(student,route(session),'PATCH',{status:'InProgress',classId})).status,204);
 const started=(await request(student,route(session))).body.data.startedAtUtc;
 assert.equal((await request(student,route(session),'PATCH',{status:'InProgress',classId})).status,204);
 assert.equal((await request(student,route(session))).body.data.startedAtUtc,started);pass('SESSION_START_RETRY_PRESERVES_CLOCK');
 for(const id of session.recommendedQuestionIds){
  assert.match(id,/^\d+$/);
  const [type,mode]=execSql(`SELECT question_type,answer_evaluation_mode FROM questions WHERE question_id=${id};`).trim().split('\t');
  const answer=type==='MultipleChoice'?execSql(`SELECT option_id FROM question_options WHERE question_id=${id} AND is_correct=0 LIMIT 1;`).trim():mode==='Coordinate2D'?'(-99999, -99999)':'-99999';
  const r=await request(student,'/learning/attempts','POST',{clientSubmissionId:crypto.randomUUID(),questionId:id,classId,finalAnswer:answer,reasoningText:'Impact regression: intentionally incorrect valid answer.',confidence:80,timeSpentSeconds:15,answerChanges:1});assert.equal(r.status,202,JSON.stringify(r.body));
 }
 assert.equal((await request(student,route(session),'PATCH',{status:'Evaluate',classId})).status,204);
 assert.equal((await request(student,route(session))).body.data.status,'NeedsReview');
 assert.equal((await request(student,route(session),'PATCH',{status:'Completed',classId})).status,400);pass('WRONG_ANSWERS_REQUIRE_REVIEW_NOT_COMPLETION');
 assert.equal((await request(student,route(session),'PATCH',{status:'InProgress',classId})).status,204);
 assert.equal((await request(student,route(session),'PATCH',{status:'Completed',classId})).status,400);
 const next=await request(student,`/learning/next-question?subjectId=${subject}&classId=${classId}&sessionId=${session.sessionId}`);assert.equal(next.status,200);pass('SESSION_RETRY_REQUIRES_NEW_EVIDENCE');
 const theory=await generate();assert.equal(theory.status,200);const empty=theory.body.data.phases[0].weeks[0].sessions[0];const pid=empty.sessionId.slice(0,36);assert.match(pid,/^[0-9a-f-]{36}$/i);
 // Construct an offline-activity fixture in the disposable path; no production writes.
 execSql(`UPDATE learning_paths SET plan_json=JSON_SET(plan_json,'$[0].weeks[0].sessions[0].recommendedQuestionIds',JSON_ARRAY()),row_version=row_version+1 WHERE learning_path_id='${pid}';`);
 assert.equal((await request(student,route(empty),'PATCH',{status:'InProgress',classId})).status,204);
 assert.equal((await request(student,route(empty),'PATCH',{status:'Completed',classId})).status,400);
 assert.equal((await request(student,route(empty),'PATCH',{status:'Evaluate',classId})).status,204);
 assert.equal((await request(student,route(empty))).body.data.status,'InProgress');pass('OFFLINE_SESSION_NOT_AUTOMATICALLY_COMPLETED');
 assert.equal((await request(student,route(empty),'PATCH',{status:'Completed',classId,acknowledgedActivities:true})).status,204);
 assert.equal((await request(student,route(empty))).body.data.status,'Completed');
 assert.equal((await request(student,route(empty),'PATCH',{status:'Completed',classId,acknowledgedActivities:true})).status,204);pass('OFFLINE_ACK_AND_COMPLETION_RETRY');
}
run().catch(error=>{results.push({passed:false,error:error.message});console.error(error.message);process.exitCode=1;}).finally(()=>fs.writeFileSync('scratch/functional-retest-impact-results.json',JSON.stringify({results},null,2)));
