// Runs only on the validated disposable acceptance schema.
const fs=require('node:fs');
const crypto=require('node:crypto');
const assert=require('node:assert/strict');
const zlib=require('node:zlib');
const {manifest,testPassword}=require('./acceptance_stack.cjs');
const {execSql}=require('./fixture_manager.cjs');
manifest();
const base='http://127.0.0.1:5001/api/v1',results=[];
const pass=id=>{results.push({id,passed:true});console.log(`PASS ${id}`);};
function png(){
 function crc32(data){let crc=0xffffffff;for(const b of data){crc^=b;for(let i=0;i<8;i++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);}return(crc^0xffffffff)>>>0;}
 function chunk(type,data){const name=Buffer.from(type),length=Buffer.alloc(4),crc=Buffer.alloc(4);length.writeUInt32BE(data.length);crc.writeUInt32BE(crc32(Buffer.concat([name,data])));return Buffer.concat([length,name,data,crc]);}
 const ihdr=Buffer.alloc(13);ihdr.writeUInt32BE(32,0);ihdr.writeUInt32BE(32,4);ihdr[8]=8;ihdr[9]=2;
 return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',ihdr),chunk('IDAT',zlib.deflateSync(Buffer.alloc((32*3+1)*32))),chunk('IEND',Buffer.alloc(0))]);
}
(async()=>{
 const login=await fetch(base+'/auth/login',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({centerCode:'EDUTWIN_A',username:'student01',password:testPassword})});
 assert.equal(login.status,200);const token=(await login.json()).data.accessToken;
 async function submit(body){const r=await fetch(base+'/learning/attempts',{method:'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},body:JSON.stringify(body),signal:AbortSignal.timeout(30000)});return {status:r.status,body:await r.json()};}
 const answer=execSql('SELECT option_id FROM question_options WHERE question_id=10000 AND is_correct=1 LIMIT 1;').trim();
 const payload=()=>({clientSubmissionId:crypto.randomUUID(),questionId:'10000',classId:'51000000-0000-0000-0000-000000000010',finalAnswer:answer,
  reasoningText:'Retry regression: substitute x=3 in y=2x+1 to obtain y=7.',confidence:80,timeSpentSeconds:20,answerChanges:1,skipped:false});
 function count(body){assert.match(body.clientSubmissionId,/^[a-f0-9-]{36}$/i);return Number(execSql(`SELECT COUNT(*) FROM attempts WHERE client_submission_id='${body.clientSubmissionId}';`).trim());}
 const original=payload();assert.equal((await submit(original)).status,202); // Treat response as unavailable to the client.
 const replay=await submit(original);assert.equal(replay.status,202);assert.equal(count(original),1);pass('PRACTICE_REPLAY_AFTER_DISCARDED_RESPONSE_HAS_ONE_ATTEMPT');
 assert.equal((await submit({...original,timeSpentSeconds:999})).status,409);assert.equal(count(original),1);pass('PRACTICE_REPLAY_REJECTS_PAYLOAD_DRIFT_WITHOUT_DUPLICATION');
 const form=new FormData();form.append('file',new Blob([png()],{type:'image/png'}),'scratchpad.png');
 const upload=await fetch(base+'/learning/attempts/attachments/prepare-upload',{method:'POST',headers:{authorization:`Bearer ${token}`},body:form});assert.equal(upload.status,200);
 const image={...payload(),drawingUploadToken:(await upload.json()).data.drawingUploadToken};
 assert.equal((await submit(image)).status,202);assert.equal((await submit(image)).status,202);assert.equal(count(image),1);
 assert.equal(Number(execSql(`SELECT COUNT(*) FROM attempt_attachments f JOIN attempts a ON a.attempt_id=f.attempt_id WHERE a.client_submission_id='${image.clientSubmissionId}';`).trim()),1);
 pass('PRACTICE_REPLAY_USES_CONSUMED_UPLOAD_TOKEN_WITH_ONE_ATTACHMENT');
 const corrected=payload();assert.equal((await submit({...corrected,confidence:101})).status,400);assert.equal(count(corrected),0);
 assert.equal((await submit(corrected)).status,202);assert.equal(count(corrected),1);pass('PRACTICE_VALIDATION_CORRECTION_REUSES_UNCONSUMED_ID');
})().catch(e=>{results.push({passed:false,error:e.message});console.error(e.message);process.exitCode=1;})
 .finally(()=>fs.writeFileSync('scratch/functional-retest-practice-retry-results.json',JSON.stringify({results},null,2)));
