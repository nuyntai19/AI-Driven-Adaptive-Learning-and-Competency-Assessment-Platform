// Local forward-only rollout. Preserves volumes and user data; requires a reviewed EF SQL script.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { root, readEnv, docker, mysql } = require('./mysql_admin.cjs');
const config = readEnv();
const database = config.MYSQL_DATABASE || 'edutwin';
assert.equal(database, 'edutwin', 'Only the local EduTwin database is in scope.');
const previous = '20261006140310_AddAcademicSharingAndRubricHistory';
const target = '20261006173556_AddAIProcessingCoordination';
const sqlPath = path.join(root, 'storage', 'migrations', `${target}.sql`);
const sql = fs.readFileSync(sqlPath, 'utf8');
assert.ok(!/\b(DROP|TRUNCATE)\b|\bDELETE\s+FROM\b|\bUPDATE\s+/i.test(sql), 'Unexpected destructive SQL.');
for (const table of ['ai_analysis_checkpoints', 'ai_provider_quota_states', 'ai_student_post_processing_jobs'])
  assert.ok(sql.includes(table), 'Incomplete reviewed migration.');
const project = config.COMPOSE_PROJECT_NAME || 'edutwin';
const api = `${project}-api`; const container = `${project}-mysql`;
const migration = () => mysql('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;', database).trim();
const active = () => Number(mysql("SELECT COUNT(*) FROM ai_analysis_jobs WHERE status IN ('Pending','Processing');", database).trim());
const inventory = () => mysql(`SELECT JSON_OBJECT('users',(SELECT COUNT(*) FROM users),
 'students',(SELECT COUNT(*) FROM students),'assignments',(SELECT COUNT(*) FROM assignments),
 'attempts',(SELECT COUNT(*) FROM attempts),'evidence',(SELECT COUNT(*) FROM evidence_assessments),
 'history',(SELECT COUNT(*) FROM twin_update_history));`, database).trim();
assert.ok([previous, target].includes(migration()), 'Unexpected schema version; inspect before rollout.');
assert.equal(active(), 0, 'Wait for active analysis jobs before rollout.');
let started = false;
const tag = crypto.randomUUID().replaceAll('-', '');
let backup;
async function run() {
  docker(['compose', 'stop', 'api']);
  try {
    assert.equal(active(), 0, 'A submission arrived before shutdown; restart and drain first.');
    const before = inventory();
    const directory = path.join(os.tmpdir(), 'EduTwin-backups'); fs.mkdirSync(directory, { recursive: true });
    backup = path.join(directory, `${new Date().toISOString().slice(0, 10)}_before_ai_processing_${tag}.sql`);
    const remote = `/tmp/edutwin_ai_processing_${tag}.sql`;
    docker(['exec', '-e', 'MYSQL_PWD', container, 'mysqldump', '-u', 'root', '--single-transaction',
      '--hex-blob', '--routines', '--triggers', '--no-tablespaces', '--set-gtid-purged=OFF',
      `--result-file=${remote}`, database], { env: { ...process.env, MYSQL_PWD: config.MYSQL_ROOT_PASSWORD } });
    docker(['cp', `${container}:${remote}`, backup]);
    assert.ok(fs.statSync(backup).size > 10000, 'Incomplete backup; migration not applied.');
    if (migration() === previous) mysql(sql, database);
    assert.equal(migration(), target); assert.equal(inventory(), before, 'User data changed during migration.');
    docker(['compose', 'up', '-d', '--no-deps', 'api']); started = true;
    const url = `http://localhost:${config.API_PORT || 5000}/api/v1/health/ready`;
    let ready = false;
    for (let attempt = 0; attempt < 60; attempt++) {
      try { const response = await fetch(url, { signal: AbortSignal.timeout(1500) }); if (response.ok) { ready = true; break; } } catch {}
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert.ok(ready, 'API did not become ready. Backup is retained.');
    assert.equal(inventory(), before, 'User data inventory changed on startup.');
    console.log(JSON.stringify({ ready, migration: migration(), userDataPreserved: true, backup,
      backupSha256: crypto.createHash('sha256').update(fs.readFileSync(backup)).digest('hex'),
      migrationSha256: crypto.createHash('sha256').update(sql).digest('hex'), activeJobs: active() }));
  } finally {
    if (!started) docker(['start', api]);
  }
}
run().catch(error => { console.error(error.message); process.exitCode = 1; });
