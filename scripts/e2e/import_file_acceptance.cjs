const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {manifest,testPassword}=require('./acceptance_stack.cjs');
const {execSql}=require('./fixture_manager.cjs');
const dir=path.resolve(__dirname,'../../docs/verification/import-fixtures-2026-10-12');
const evidence=path.resolve(__dirname,'../../docs/verification/evidence/import_f10_f16_20261012');
fs.mkdirSync(evidence,{recursive:true});manifest();
const expected=JSON.parse(fs.readFileSync(path.join(dir,'fixture-manifest.json')));
const subjectId='30000000-0000-0000-0000-000000000003',primaryTopicNodeId='10000';
const base='http://127.0.0.1:3002/api/v1',results=[];
async function check(name,fn){try{const data=await fn();results.push({name,passed:true,...data});console.log('PASS '+name);}catch(e){results.push({name,passed:false,error:e.message});console.log('FAIL '+name+': '+e.message);}}
async function req(token,route,body,method='POST'){
 const r=await fetch(base+route,{method,headers:{authorization:`Bearer ${token}`,...(body instanceof FormData?{}:{'content-type':'application/json'})},...(body===undefined?{}:{body:body instanceof FormData?body:JSON.stringify(body)}),signal:AbortSignal.timeout(30000)});
 const text=await r.text();return {status:r.status,body:text?JSON.parse(text):{}};
}
async function login(username){const r=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({centerCode:'EDUTWIN_A',username,password:testPassword})});assert.equal(r.status,200);return(await r.json()).data.accessToken;}
function total(){return Number(execSql('SELECT COUNT(*) FROM questions;').trim());}
async function run(){
 const teacher=await login('teacher.math'),student=await login('student01'),manager=await login('manager');
 async function preview(name,token=teacher,bytes){const form=new FormData();form.append('file',new Blob([bytes??fs.readFileSync(path.join(dir,name))]),name);return req(token,'/questions/import/preview',form);}
 const confirm=(token,previewToken,extra={})=>req(token,'/questions/import/confirm',{subjectId,primaryTopicNodeId,previewToken,...extra});
 for(const [file,key] of [['mixed-utf8.csv','csv'],['mixed-utf8-bom.csv','csv'],['mixed-inline.xlsx','xlsx'],['mixed-custom-sheet.xlsx','xlsx'],['mixed-table.docx','docx']]){
  const p=await preview(file);let data;
  await check(file+' preview 3/1/2',async()=>{assert.equal(p.status,200);data=p.body.data;
   assert.equal(data.totalRows,3);assert.equal(data.validCount,1);assert.equal(data.invalidCount,2);
   assert.ok(data.errors.some(e=>e.field==='MaxScore'));assert.ok(data.errors.some(e=>e.field==='ReasoningRequired'));
   const {previewToken,...safe}=data;fs.writeFileSync(path.join(evidence,file+'.preview.json'),JSON.stringify(safe,null,2));return {totalRows:3,validCount:1,invalidCount:2};});
  if(!data)continue;
  await check(file+' preserves line breaks and quotes',async()=>{assert.equal(data.validQuestions[0].questionText,expected[key]);assert.equal(data.validQuestions[0].solution,'Có dấu "nháy"');});
  await check(file+' confirm one valid row and reject replay',async()=>{const before=total();const r=await confirm(teacher,data.previewToken);assert.equal(r.status,200);assert.equal(r.body.data.importedCount,1);assert.equal(total(),before+1);
   assert.equal((await confirm(teacher,data.previewToken)).status,400);assert.equal(total(),before+1);
   const q=JSON.parse(Buffer.from(execSql("SELECT HEX(JSON_OBJECT('questionText',question_text,'solution',solution,'maxScore',max_score,'reasoningRequired',reasoning_required)) FROM questions ORDER BY question_id DESC LIMIT 1;").trim(),'hex').toString());
   fs.writeFileSync(path.join(evidence,file+'.stored.json'),JSON.stringify(q,null,2));assert.equal(q.maxScore,10);assert.equal(q.reasoningRequired,1);assert.equal(q.questionText,expected[key]);});
 }
 await check('score bounds preview',async()=>{const p=await preview('scores.csv');assert.equal(p.status,200);assert.equal(p.body.data.totalRows,6);assert.equal(p.body.data.validCount,2);assert.equal(p.body.data.invalidCount,4);});
 for(const f of ['malformed.csv','no-table.docx','corrupt.xlsx','empty.csv'])await check(f+' rejected without database writes',async()=>{const before=total();const p=await preview(f);assert.equal(p.status,400);assert.ok(p.body.detail);assert.equal(total(),before);});
 await check('wrong headers cannot confirm',async()=>{const before=total(),p=await preview('wrong-headers.csv');assert.equal(p.status,200);assert.equal(p.body.data.validCount,0);assert.ok(p.body.data.errors.length);assert.equal((await confirm(teacher,p.body.data.previewToken)).status,400);assert.equal(total(),before);});
 for(const [name,t] of [['Student',student],['Manager',manager]])await check(name+' preview and confirm forbidden',async()=>{const p=await preview('mixed-utf8.csv');assert.equal((await preview('mixed-utf8.csv',t)).status,403);const before=total();assert.equal((await confirm(t,p.body.data.previewToken)).status,403);assert.equal(total(),before);});
 await check('concurrent confirm exactly one winner',async()=>{const p=await preview('mixed-utf8.csv'),before=total();const r=await Promise.all([confirm(teacher,p.body.data.previewToken),confirm(teacher,p.body.data.previewToken)]);assert.deepEqual(r.map(x=>x.status).sort(),[200,400]);assert.equal(total(),before+1);});
 await check('inactive subject confirm rejected without writes',async()=>{const p=await preview('mixed-utf8.csv'),before=total();execSql(`UPDATE subjects SET is_active=0 WHERE subject_id='${subjectId}';`);try{assert.equal((await confirm(teacher,p.body.data.previewToken)).status,404);assert.equal(total(),before);}finally{execSql(`UPDATE subjects SET is_active=1 WHERE subject_id='${subjectId}';`);}});
 await check('in-use topic cannot be deactivated',async()=>{assert.throws(()=>execSql('UPDATE knowledge_nodes SET is_active=0 WHERE node_id=10000;'),/EDUTWIN_ACADEMIC_DEPENDENCY/);assert.equal(execSql('SELECT is_active FROM knowledge_nodes WHERE node_id=10000;').trim(),'1');});
 await check('inactive topic confirm rejected without writes',async()=>{
  const unused=execSql("INSERT INTO knowledge_nodes(center_id,subject_id,node_type,node_code,node_name,order_index,exam_importance,estimated_learning_minutes,is_active,created_at,updated_at,is_deleted,row_version) SELECT center_id,subject_id,'Topic',CONCAT('IMPORT_',UUID()),'Inactive import fixture',0,0,10,0,UTC_TIMESTAMP(),UTC_TIMESTAMP(),0,1 FROM knowledge_nodes WHERE node_id=10000; SELECT LAST_INSERT_ID();").trim();assert.match(unused,/^\d+$/);
  const p=await preview('mixed-utf8.csv'),before=total();assert.equal((await confirm(teacher,p.body.data.previewToken,{primaryTopicNodeId:unused})).status,404);assert.equal(total(),before);
 });
 const body={subjectId,primaryTopicNodeId,questionType:'ShortAnswer',difficulty:1,gradeLevel:10,questionText:'Import score create/update fixture',correctAnswer:'2',solution:'1+1=2',maxScore:10,estimatedTimeSeconds:60,reasoningRequired:false,languageCode:'vi',answerEvaluationMode:'TextExact'};
 const created=await req(teacher,'/questions',body);assert.equal(created.status,201,JSON.stringify(created.body));const id=created.body.data.questionId;
 await check('valid update baseline succeeds',async()=>{const q=await req(teacher,'/questions/'+id,undefined,'GET');const r=await req(teacher,'/questions/'+id,{...body,rowVersion:q.body.data.rowVersion},'PATCH');assert.ok([200,204].includes(r.status),JSON.stringify(r.body));});
 for(const score of [0,-1,1.001,1000]){
  await check('create rejects MaxScore '+score,async()=>{const before=total();assert.equal((await req(teacher,'/questions',{...body,maxScore:score})).status,400);assert.equal(total(),before);});
  await check('update rejects MaxScore '+score,async()=>{const q=await req(teacher,'/questions/'+id,undefined,'GET');assert.equal((await req(teacher,'/questions/'+id,{...body,maxScore:score,rowVersion:q.body.data.rowVersion},'PATCH')).status,400);const after=await req(teacher,'/questions/'+id,undefined,'GET');assert.equal(after.body.data.maxScore,10);assert.equal(after.body.data.rowVersion,q.body.data.rowVersion);});
 }
 for(const score of [0.01,999.99]){
  await check('create accepts valid boundary '+score,async()=>{const r=await req(teacher,'/questions',{...body,maxScore:score});assert.equal(r.status,201);assert.equal(r.body.data.maxScore,score);});
  await check('update accepts valid boundary '+score,async()=>{const q=await req(teacher,'/questions/'+id,undefined,'GET');const r=await req(teacher,'/questions/'+id,{...body,maxScore:score,rowVersion:q.body.data.rowVersion},'PATCH');assert.ok([200,204].includes(r.status));assert.equal((await req(teacher,'/questions/'+id,undefined,'GET')).body.data.maxScore,score);});
 }
 for(const bytes of [Buffer.from('QuestionText,QuestionType,CorrectAnswer\n'+Array(5001).fill('Limit,ShortAnswer,2').join('\n')),Buffer.alloc(10*1024*1024+1,65)])await check('row/file limit '+bytes.length,async()=>{const p=await preview('limits.csv',teacher,bytes);assert.equal(p.status,400);});
 fs.writeFileSync(path.join(evidence,'results.json'),JSON.stringify({results},null,2));console.log(JSON.stringify({passed:results.filter(r=>r.passed).length,failed:results.filter(r=>!r.passed).length}));
 if(results.some(r=>!r.passed))process.exitCode=1;
}
run().catch(e=>{console.error(e.message);process.exitCode=1;fs.writeFileSync(path.join(evidence,'results.json'),JSON.stringify({results,fatal:e.message},null,2));});
