// Run the approved EF migrations without echoing local credentials.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { root, readEnv } = require('./mysql_admin.cjs');
const config = readEnv();
const target = process.argv.find(a => a.startsWith('--database='))?.slice(11);
const live = target === 'edutwin';
if (!target || (!/^edutwin_scope_test_[a-z0-9_]{1,40}$/.test(target) && !live))
  throw new Error('Specify the named local database or an isolated edutwin_scope_test_ database.');
if (live) {
  const backupName = process.argv.find(a => a.startsWith('--backup-name='))?.slice(14);
  if (!process.argv.includes('--apply-local') || !backupName || !/^[a-z0-9-]{1,100}$/.test(backupName) ||
      !fs.existsSync(path.join(root, 'storage/backups', `${backupName}.json`)) ||
      !fs.existsSync(path.join(root, 'storage/backups', `${backupName}.sql`)))
    throw new Error('Local application requires --apply-local and an existing --backup-name.');
}
if (!config.MYSQL_ROOT_PASSWORD) throw new Error('Local database credential is missing.');
const check = process.argv.includes('--check-model');
const args = ['ef', ...(check ? ['migrations', 'has-pending-model-changes'] : ['database', 'update']),
  '--no-build', '-p', 'src/EduTwin.DAL', '-s', 'src/EduTwin.DAL'];
const result = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8', maxBuffer: 24 * 1024 * 1024,
  env: { ...process.env, ConnectionStrings__Default:
    `Server=127.0.0.1;Port=${config.MYSQL_PORT || 3307};Database=${target};User=root;Password=${config.MYSQL_ROOT_PASSWORD};SslMode=Disabled;AllowPublicKeyRetrieval=True;` } });
let output = `${result.stdout || ''}${result.stderr || ''}`;
output = output.split(config.MYSQL_ROOT_PASSWORD).join('[REDACTED]');
fs.mkdirSync(path.join(root, 'storage/verification'), { recursive: true });
const logPath = path.join(root, 'storage/verification', `${target}-${check ? 'model' : 'migration'}.txt`);
fs.writeFileSync(logPath, output);
console.log(output.slice(-12000));
console.log(JSON.stringify({ database: target, mode: check ? 'check-model' : 'migrate', exitCode: result.status, logPath }));
process.exitCode = result.status ?? 1;
