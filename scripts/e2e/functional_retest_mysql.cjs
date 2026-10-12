// Relational regression on a dedicated disposable MySQL server, never the primary DB.
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const assert=require('node:assert/strict');
const {spawn,spawnSync}=require('node:child_process');
const root=path.resolve(__dirname,'../..');
const runId=crypto.randomUUID(), name=`edutwin-retest-mysql-${runId.slice(0,8)}`;
const password=crypto.randomBytes(32).toString('hex');
const fullTmpfs=process.argv[2]==='--full-tmpfs';
assert.ok(process.argv[2]===undefined||fullTmpfs,'Use --full-tmpfs or omit the argument for a full run.');
const suffix=fullTmpfs?'-full-tmpfs':'';
const docker=(args,options={})=>{
 const r=spawnSync('docker',args,{encoding:'utf8',...options});
 if(r.error||r.status!==0)throw new Error(`Isolated Docker operation failed: ${r.error?.code||r.status}`);
 return r.stdout.trim();
};
async function run(){
 let created=false;
 try {
  docker(['run','-d','--name',name,'--label',`edutwin.e2e.owner=${runId}`,
    '-p','127.0.0.1:3308:3306',...(fullTmpfs?['--tmpfs','/var/lib/mysql:rw,size=1536m']:[]),
    '-e','MYSQL_ROOT_PASSWORD','-e','MYSQL_ROOT_HOST=%','mysql:8.0.35'],
    {env:{...process.env,MYSQL_ROOT_PASSWORD:password}}); created=true;
  for(let n=0;n<90;n++){
   const ping=spawnSync('docker',['exec','-e','MYSQL_PWD',name,'mysql','-uroot','-N','-e','SELECT 1'],
    {encoding:'utf8',env:{...process.env,MYSQL_PWD:password}});
   if(ping.status===0)break;
   if(n===89)throw new Error('Dedicated MySQL not ready.');
   await new Promise(resolve=>setTimeout(resolve,1000));
  }
  console.log('Dedicated MySQL ready; relational suites enabled.');
  const classes=[];
  function scan(dir){for(const item of fs.readdirSync(dir,{withFileTypes:true})){
    const file=path.join(dir,item.name);
    if(item.isDirectory()&&!['obj','bin'].includes(item.name))scan(file);
    else if(item.isFile()&&item.name.endsWith('.cs')&&item.name!=='QuestionReasoningRequiredDefaultTests.cs'&&
      fs.readFileSync(file,'utf8').includes('EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING'))classes.push(path.basename(file,'.cs'));
  }}
  scan(path.join(root,'tests/EduTwin.BLL.Tests'));
  assert.ok(classes.length>=10);
  const output=fs.openSync(path.join(root,`scratch/functional-retest-mysql${suffix}.log`),'w');
  try {
   const code=await new Promise((resolve,reject)=>{
    const child=spawn('dotnet',['test','tests/EduTwin.BLL.Tests','--no-build','--no-restore','--filter',
      classes.map(c=>`FullyQualifiedName~${c}`).join('|'),
      '--logger',`trx;LogFileName=functional-retest-mysql${suffix}-20261012.trx`,'--logger','console;verbosity=normal',
      '--blame-hang-timeout','3m','--blame-hang-dump-type','none'],
      {cwd:root,env:{...process.env,EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING:`Server=127.0.0.1;Port=3308;User=root;Password=${password};Pooling=false;AllowUserVariables=true;`},
      stdio:['ignore',output,output]});child.on('error',reject);child.on('exit',resolve);
   });
   if(code!==0)throw new Error(`Relational test run failed (${code}); see sanitized test log.`);
   console.log('Relational suites finished successfully.');
  } finally {fs.closeSync(output);}
 } finally {
  if(created){
   assert.equal(docker(['inspect',name,'--format','{{index .Config.Labels "edutwin.e2e.owner"}}']),runId);
   docker(['rm','-f','-v',name]);
   console.log('Dedicated MySQL container and anonymous volume removed; primary server untouched.');
  }
 }
}
run().catch(error=>{console.error(error.message);process.exitCode=1;});
