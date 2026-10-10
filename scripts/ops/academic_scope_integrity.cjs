// Local backup + read-only integrity proof. Never prints credentials or submissions.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { root, readEnv, mysql, docker, sqlString } = require('./mysql_admin.cjs');
const env = readEnv(); const db = env.MYSQL_DATABASE || 'edutwin';
const directory = path.join(root, 'storage/backups');
const name = process.argv.find(a=>a.startsWith('--name='))?.slice(7) || '2026-10-08-before-academic-scope';
if (!/^[a-z0-9-]{1,80}$/.test(name)) throw new Error('Invalid backup name.');
const proof = path.join(directory, name+'.json');
const backup = path.join(directory, name+'.sql');
const tables = ['assignments','assignment_questions','assignment_targets','student_assignment_progress','attempts',
  'reasoning_analyses','attempt_attachments','curriculums','curriculum_nodes','curriculum_classes','student_twins','student_subject_goals',
  'students','users','class_students','knowledge_nodes','knowledge_twins','twin_update_history'];
if (name !== '2026-10-08-before-academic-scope') tables.push('class_curriculum_applications');
function fingerprint() {
  return Object.fromEntries(tables.map(table => {
    const keys = mysql(`SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=${sqlString(db)} AND TABLE_NAME=${sqlString(table)} AND INDEX_NAME='PRIMARY' ORDER BY SEQ_IN_INDEX;`).trim().split(/\r?\n/);
    if (keys.some(k => !/^[a-z_]+$/.test(k))) throw new Error(`Invalid primary key: ${table}`);
    const rows = mysql(`SELECT * FROM \`${table}\` ORDER BY ${keys.map(k=>`\`${k}\``).join(',')};`, db);
    return [table, {count:Number(mysql(`SELECT COUNT(*) FROM \`${table}\`;`,db).trim()),sha256:crypto.createHash('sha256').update(rows).digest('hex')}];
  }));
}
if (process.argv.includes('--capture')) {
  fs.mkdirSync(directory,{recursive:true});
  if (fs.existsSync(proof) || fs.existsSync(backup)) throw new Error('A pre-migration backup already exists; refusing to overwrite.');
  const dump = docker(['exec','-e','MYSQL_PWD',`${env.COMPOSE_PROJECT_NAME||'edutwin'}-mysql`,'mysqldump','-u','root',
    '--single-transaction','--routines','--triggers','--no-tablespaces','--default-character-set=utf8mb4',db],
    {env:{...process.env,MYSQL_PWD:env.MYSQL_ROOT_PASSWORD}});
  fs.writeFileSync(backup,dump,{flag:'wx'});
  const data={capturedAt:new Date().toISOString(),backupSha256:crypto.createHash('sha256').update(dump).digest('hex'),tables:fingerprint()};
  fs.writeFileSync(proof,JSON.stringify(data,null,2),{flag:'wx'});
  console.log(JSON.stringify({backupPath:backup,proofPath:proof,backupSha256:data.backupSha256,tableCounts:Object.fromEntries(Object.entries(data.tables).map(([t,v])=>[t,v.count]))}));
} else if (process.argv.includes('--verify')) {
  const baseline=JSON.parse(fs.readFileSync(proof,'utf8')); const current=fingerprint();
  const changed=tables.filter(table=>JSON.stringify(baseline.tables[table])!==JSON.stringify(current[table]));
  console.log(JSON.stringify({protectedHistoryUnchanged:changed.length===0,changedTables:changed,tableCounts:Object.fromEntries(Object.entries(current).map(([t,v])=>[t,v.count]))}));
  if(changed.length) process.exitCode=1;
} else {
  console.log(JSON.stringify({mode:'read-only',tables:fingerprint()}));
}
