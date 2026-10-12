const fs=require('node:fs');
const assert=require('node:assert/strict');
const crypto=require('node:crypto');
const {testPassword}=require('./acceptance_stack.cjs');
const fixture=JSON.parse(fs.readFileSync('scratch/functional-retest-path-ui.json'));
const base='http://127.0.0.1:5001/api/v1',subject='30000000-0000-0000-0000-000000000003',classId='51000000-0000-0000-0000-000000000010';
const results=[],pass=id=>{results.push({id,passed:true});console.log(`PASS ${id}`);};
async function login(username){const r=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({centerCode:'EDUTWIN_A',username,password:testPassword})});assert.equal(r.status,200);return (await r.json()).data.accessToken;}
async function get(token,query){const r=await fetch(base+'/students/me/learning-path/detailed?'+query,{headers:{authorization:`Bearer ${token}`},signal:AbortSignal.timeout(30000)});return {status:r.status,body:await r.json()};}
(async()=>{
 const own=await login('student02'),other=await login('student01');
 const scope=`subjectId=${subject}&classId=${classId}`,historical=`${scope}&pathId=${fixture.pathId}`;
 const saved=await get(own,historical);assert.equal(saved.status,200);assert.equal(saved.body.data.learningPathId,fixture.pathId);assert.equal(saved.body.data.progressPercentage,33.3);assert.equal(saved.body.data.phases[0].weeks[0].checkpoint.completedTasks,1);pass('ORIGINAL_VERSION_PRESERVES_COMPLETED_SESSION_AND_PROGRESS');
 const current=await get(own,scope);assert.equal(current.status,200);assert.notEqual(current.body.data.learningPathId,fixture.pathId);pass('CURRENT_AND_SAVED_PLANS_HAVE_SEPARATE_IDENTITIES');
 assert.equal((await get(other,historical)).status,404);pass('SAVED_PLAN_OWNER_BOUNDARY');
 assert.equal((await get(own,`subjectId=${crypto.randomUUID()}&classId=${classId}&pathId=${fixture.pathId}`)).status,404);pass('SAVED_PLAN_SUBJECT_BOUNDARY');
 assert.equal((await get(own,`subjectId=${subject}&pathId=${fixture.pathId}`)).status,404);pass('SAVED_PLAN_CLASS_BOUNDARY');
 assert.equal((await get(own,`${scope}&pathId=${crypto.randomUUID()}`)).status,404);pass('MISSING_SAVED_PLAN_RETURNS_NOT_FOUND');
 fs.writeFileSync('scratch/functional-retest-plan-version-data.json',JSON.stringify({saved:saved.body.data,current:current.body.data},null,2));
})().catch(error=>{results.push({passed:false,error:error.message});console.error(error.message);process.exitCode=1;}).finally(()=>fs.writeFileSync('scratch/functional-retest-plan-version-results.json',JSON.stringify({results},null,2)));
