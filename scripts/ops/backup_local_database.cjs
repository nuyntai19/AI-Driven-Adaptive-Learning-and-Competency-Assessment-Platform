// Export a private local backup before a forward migration/repair. No database writes.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { readEnv, docker } = require('./mysql_admin.cjs');
const config = readEnv();
const database = config.MYSQL_DATABASE || 'edutwin';
assert.equal(database, 'edutwin', 'Only the local EduTwin database is in scope.');
assert.ok(config.MYSQL_ROOT_PASSWORD, 'Local credential is missing.');
const tag = crypto.randomUUID().replaceAll('-', '');
const container = `${config.COMPOSE_PROJECT_NAME || 'edutwin'}-mysql`;
const containerFile = `/tmp/edutwin_backup_${tag}.sql`;
const directory = path.join(os.tmpdir(), 'EduTwin-backups');
fs.mkdirSync(directory, { recursive: true });
const backup = path.join(directory, `${new Date().toISOString().slice(0, 10)}_before_math_repair_${tag}.sql`);
docker(['exec', '-e', 'MYSQL_PWD', container, 'mysqldump', '-u', 'root', '--single-transaction',
  '--hex-blob', '--routines', '--triggers', '--no-tablespaces', '--set-gtid-purged=OFF',
  `--result-file=${containerFile}`, database], { env: { ...process.env, MYSQL_PWD: config.MYSQL_ROOT_PASSWORD } });
docker(['cp', `${container}:${containerFile}`, backup]);
const bytes = fs.statSync(backup).size;
assert.ok(bytes > 10000, 'Backup is incomplete.');
console.log(JSON.stringify({ backup, bytes, sha256: crypto.createHash('sha256').update(fs.readFileSync(backup)).digest('hex') }));
