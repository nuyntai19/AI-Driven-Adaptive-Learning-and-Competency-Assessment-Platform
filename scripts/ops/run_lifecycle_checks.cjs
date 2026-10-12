// Credentials stay in the child environment; never echo connection strings or keys.
const { spawnSync } = require('node:child_process');
const { readEnv, root, mysql } = require('./mysql_admin.cjs');
const e = readEnv();
const cs = `Server=127.0.0.1;Port=${e.MYSQL_PORT||3306};Database=${e.MYSQL_DATABASE||'edutwin'};User=root;Password=${e.MYSQL_ROOT_PASSWORD};`;
const mode = process.argv[2];
if (mode === '--state') {
  console.log(mysql("SELECT ID,DB,COMMAND,TIME,STATE FROM information_schema.PROCESSLIST WHERE DB LIKE 'edutwin_dash_%';"));
  process.exit(0);
}
if (mode === '--inspect') {
  console.log(mysql("SELECT status,learning_scope,COUNT(*) FROM classes GROUP BY status,learning_scope; SELECT action_type,actor_user_id IS NULL,COUNT(*) FROM authorization_audit_logs WHERE target_type='Class' GROUP BY action_type,actor_user_id IS NULL;",e.MYSQL_DATABASE||'edutwin'));
  process.exit(0);
}
const args = (mode === '--test' || mode === '--test-lifecycle' || mode === '--test-academic') ? ['test','tests/EduTwin.BLL.Tests','--no-build','--filter',
  mode === '--test-academic' ? 'FullyQualifiedName~AcademicLifecycleDependencies_RealSql' :
  mode === '--test-lifecycle' ? 'FullyQualifiedName~ClassLifecycle_RealSql' :
  'FullyQualifiedName~ClassLifecycle_RealSql|FullyQualifiedName~AcademicScopeAndApplicationHistory_RealSql|FullyQualifiedName~WorkspaceSummary','--verbosity','normal'] :
  mode === '--migrate' ? ['ef','database','update','--project','src/EduTwin.DAL','--startup-project','src/EduTwin.DAL'] : null;
if (!args) throw new Error('Use --test, --test-academic, or --migrate.');
const result = spawnSync('dotnet',args,{cwd:root,encoding:'utf8',maxBuffer:8*1024*1024,
  env:{...process.env,ConnectionStrings__Default:cs,EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING:cs}});
const redact = text => String(text||'').replaceAll(cs,'[connection redacted]').replaceAll(e.MYSQL_ROOT_PASSWORD,'[redacted]');
process.stdout.write(redact(result.stdout));process.stderr.write(redact(result.stderr));
process.exitCode = result.status ?? 1;
