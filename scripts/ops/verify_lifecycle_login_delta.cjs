// Read-only: compare users against the private pre-migration SQL dump, without printing any values.
const fs=require('node:fs');const path=require('node:path');
const {root,readEnv,mysql,sqlString}=require('./mysql_admin.cjs');
const db=readEnv().MYSQL_DATABASE||'edutwin';
const dump=fs.readFileSync(path.join(root,'storage/backups/2026-10-09-before-lifecycle.sql'),'utf8');
const inserts=[...dump.matchAll(/INSERT INTO `users` VALUES ([\s\S]*?);\r?\n/g)];
if(!inserts.length)throw new Error('Users baseline not found.');
const rows=[];
for(const match of inserts){let row=[],value='',quoted=false,escaped=false,inRow=false;
  const push=()=>{row.push(value);value='';};
  for(const c of match[1]){
    if(escaped){value+=({n:'\n',r:'\r',t:'\t',b:'\b','0':'\0',Z:'\x1a'})[c]??c;escaped=false;continue;}
    if(quoted){if(c==='\\')escaped=true;else if(c==="'")quoted=false;else value+=c;continue;}
    if(c==="'"){quoted=true;continue;}
    if(c==='('){row=[];value='';inRow=true;continue;}
    if(c===')'){push();rows.push(row);inRow=false;continue;}
    if(inRow&&c===',')push();else if(inRow)value+=c;
  }
}
const columns=mysql(`SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=${sqlString(db)} AND TABLE_NAME='users' ORDER BY ORDINAL_POSITION;`).trim().split(/\r?\n/);
const current=mysql('SELECT * FROM users ORDER BY user_id;',db).trimEnd().split(/\r?\n/).map(row=>row.split('\t'));
const id=columns.indexOf('user_id');const baseline=new Map(rows.map(r=>[r[id],r]));
const allowed=new Set(['last_login_at','updated_at','row_version']);const changedFields=new Set();let changedUsers=0;let valid=current.length===rows.length;
for(const row of current){const before=baseline.get(row[id]);if(!before){valid=false;continue;}
  const changes=columns.filter((_,i)=>row[i]!==before[i]);if(changes.length)changedUsers++;
  for(const column of changes){changedFields.add(column);if(!allowed.has(column))valid=false;}
}
console.log(JSON.stringify({onlyLoginMetadataChanged:valid,changedUsers,changedFields:[...changedFields].sort(),userCount:current.length}));
if(!valid)process.exitCode=1;
