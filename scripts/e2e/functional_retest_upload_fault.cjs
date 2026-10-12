const fs=require('node:fs');
const assert=require('node:assert/strict');
const {docker}=require('../ops/mysql_admin.cjs');
const {manifest}=require('./acceptance_stack.cjs');
const {execSql,STUDENT02_A_ID}=require('./fixture_manager.cjs');
const state=manifest();
assert.equal(docker(['inspect',state.web,'--format','{{index .Config.Labels "edutwin.e2e.owner"}}']).trim(),state.runId);
const config='scripts/e2e/acceptance.nginx.conf',backup='scratch/answer-system-retest-nginx-original.conf';
const mode=process.argv[2];
if(mode==='on'){
 assert.ok(!fs.existsSync(backup),'Fault already enabled; restore first.');
 const original=fs.readFileSync(config,'utf8');
 assert.ok(!original.includes('location = /api/v1/learning/attempts/attachments/prepare-upload'));
 fs.writeFileSync(backup,original);
 const inserted='    location = /api/v1/learning/attempts/attachments/prepare-upload { return 503; }\n    location /api/';
 fs.writeFileSync(config,original.replace('    location /api/',inserted));
 try{docker(['exec',state.web,'nginx','-t']);docker(['exec',state.web,'nginx','-s','reload']);}
 catch(error){fs.writeFileSync(config,original);throw error;}
 const before=Number(execSql(`SELECT COUNT(*) FROM attempts WHERE student_id='${STUDENT02_A_ID}' AND assignment_id IS NULL;`).trim());
 fs.writeFileSync('scratch/answer-system-retest-upload-proof.json',JSON.stringify({runId:state.runId,before},null,2));
 console.log('Owned web proxy upload fault enabled; attempt count baseline recorded.');
}else if(mode==='off'){
 assert.ok(fs.existsSync(backup));fs.writeFileSync(config,fs.readFileSync(backup));
 docker(['exec',state.web,'nginx','-t']);docker(['exec',state.web,'nginx','-s','reload']);fs.unlinkSync(backup);
 console.log('Owned proxy restored and validated.');
}else if(mode==='check-blocked'){
 const proof=JSON.parse(fs.readFileSync('scratch/answer-system-retest-upload-proof.json'));
 assert.equal(proof.runId,state.runId);
 const after=Number(execSql(`SELECT COUNT(*) FROM attempts WHERE student_id='${STUDENT02_A_ID}' AND assignment_id IS NULL;`).trim());
 assert.equal(after,proof.before,'Attempt was created despite failed attachment upload.');
 fs.writeFileSync('scratch/answer-system-retest-upload-proof.json',JSON.stringify({...proof,afterFailedUpload:after,blocked:true},null,2));
 console.log('Failed image upload created no attempt: PASS.');
}else throw new Error('Use on, off or check-blocked.');
