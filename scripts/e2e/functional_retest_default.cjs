const fs=require('node:fs');
const assert=require('node:assert/strict');
const {spawnSync}=require('node:child_process');
const {docker}=require('../ops/mysql_admin.cjs');
const {manifest}=require('./acceptance_stack.cjs');
const state=manifest(), info=JSON.parse(docker(['inspect',state.api]))[0];
assert.equal(info.Config.Labels['edutwin.e2e.owner'],state.runId);
let connection=info.Config.Env.find(s=>s.startsWith('ConnectionStrings__Default=')).slice('ConnectionStrings__Default='.length);
assert.ok(connection.includes(`Database=${state.database};`));
connection=connection.replace('Server=mysql;Port=3306;','Server=127.0.0.1;Port=3307;');
const result=spawnSync('dotnet',['test','tests/EduTwin.BLL.Tests','--no-build','--no-restore','--filter',
 'FullyQualifiedName~MySql_Database_Column_Default_Is_False_When_Available',
 '--logger','trx;LogFileName=functional-retest-mysql-default-20261012.trx'],
 {encoding:'utf8',env:{...process.env,EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING:connection}});
fs.writeFileSync('scratch/functional-retest-mysql-default.log',result.stdout+result.stderr);
assert.equal(result.status,0,'Default-column VSTest failed; inspect local test log.');
console.log('Migrated disposable database default-column VSTest: PASS.');
