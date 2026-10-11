// Actual local backup / isolated restore proof. Never restores over the live database.
// Run with --capture --name=<unique-name>, then --verify --name=<same-name>.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { root } = require('./mysql_admin.cjs');
const name = process.argv.find(x => x.startsWith('--name='))?.slice(7);
if (!name || !/^[a-z0-9-]{1,80}$/.test(name)) throw new Error('A unique validated backup name is required.');
const capture = process.argv.includes('--capture');
const verify = process.argv.includes('--verify');
if (capture === verify) throw new Error('Choose exactly one of --capture or --verify.');
const directory = path.join(root, 'storage', 'platform-ops');
const manifestPath = path.join(directory, 'backup-status.json');
fs.mkdirSync(directory, {recursive: true});
let previous = {};
try { previous = JSON.parse(fs.readFileSync(manifestPath, 'utf8')); } catch { /* First observation. */ }
const allowed = ['Status', 'LastAttemptAt', 'LastSuccessAt', 'Bytes', 'Sha256', 'LastRestoreVerifiedAt', 'RestoreVerifiedSha256', 'LastRestoreStatus'];
const manifest = Object.fromEntries(allowed.filter(k => k in previous).map(k => [k, previous[k]]));
function persist() {
  const temporary = path.join(directory, `backup-status-${process.pid}.tmp`);
  fs.writeFileSync(temporary, JSON.stringify(manifest, null, 2));
  fs.renameSync(temporary, manifestPath);
}
const script = capture ? 'academic_scope_integrity.cjs' : 'verify_notification_backup.cjs';
const result = spawnSync(process.execPath, [path.join(__dirname, script), `--name=${name}`, ...(capture ? ['--capture'] : [])],
  {cwd: root, encoding: 'utf8', maxBuffer: 4 * 1024 * 1024});
if (result.status !== 0) {
  if (capture) { manifest.Status = 'Failed'; manifest.LastAttemptAt = new Date().toISOString(); persist(); }
  else { manifest.LastRestoreStatus = 'Failed'; persist(); }
  // The child may contain protected table counts; do not print any dump or credentials.
  console.error(`${capture ? 'Backup' : 'Isolated restore verification'} failed; existing successful proof was retained.`);
  process.exitCode = 1;
} else {
  const proof = JSON.parse(fs.readFileSync(path.join(root, 'storage', 'backups', `${name}.json`), 'utf8'));
  const bytes = fs.statSync(path.join(root, 'storage', 'backups', `${name}.sql`)).size;
  if (capture) Object.assign(manifest, {Status: 'Success', LastAttemptAt: new Date().toISOString(), LastSuccessAt: proof.capturedAt, Bytes: bytes, Sha256: proof.backupSha256});
  else { manifest.LastRestoreVerifiedAt = new Date().toISOString(); manifest.RestoreVerifiedSha256 = proof.backupSha256; manifest.LastRestoreStatus = 'Success'; }
  persist(); console.log(JSON.stringify({mode: capture ? 'backup-observed' : 'isolated-restore-verified', ...manifest}));
}
