// Additive rollout only. Never reset database/volumes or regrade submitted attempts.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { readEnv, root, docker, mysql } = require('./mysql_admin.cjs');
const config = readEnv(); const database = config.MYSQL_DATABASE || 'edutwin';
assert.equal(database, 'edutwin');
const previous = '20261007054616_AddAIAnalysisProfileProvenance';
const target = '20261007091024_AddAIGradingProposals';
const sql = fs.readFileSync(path.join(root, 'storage', 'migrations', `${target}.sql`), 'utf8');
assert.ok(!/\b(DROP|TRUNCATE|DELETE|UPDATE)\b/i.test(sql), 'Unexpected destructive statement.');
assert.equal((sql.match(/ALTER TABLE/gi) || []).length, 3);
for (const column of ['suggested_score', 'suggested_rubric_grade_json', 'uses_alternative_method']) assert.ok(sql.includes(`ADD \`${column}\``));
const project = config.COMPOSE_PROJECT_NAME || 'edutwin'; const container = `${project}-mysql`;
const migration = () => mysql('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;', database).trim();
const active = () => Number(mysql("SELECT COUNT(*) FROM ai_analysis_jobs WHERE status IN ('Pending','Processing');", database).trim());
const inventory = () => mysql(`SELECT JSON_OBJECT('users',(SELECT COUNT(*) FROM users),
 'students',(SELECT COUNT(*) FROM students),'classes',(SELECT COUNT(*) FROM classes),
 'questions',(SELECT COUNT(*) FROM questions),'assignments',(SELECT COUNT(*) FROM assignments),
 'attempts',(SELECT COUNT(*) FROM attempts),'analysis',(SELECT COUNT(*) FROM reasoning_analyses),
 'evidence',(SELECT COUNT(*) FROM evidence_assessments),'history',(SELECT COUNT(*) FROM twin_update_history));`, database).trim();
const submissionsHash = () => crypto.createHash('sha256').update(mysql(`SELECT JSON_OBJECT('id',attempt_id,
 'answer',final_answer,'reasoning',reasoning_text,'score',awarded_score,'correct',is_correct,'status',status,'created',created_at)
 FROM attempts ORDER BY attempt_id;`, database)).digest('hex');
assert.ok([previous, target].includes(migration()), 'Unexpected schema version.');
assert.equal(active(), 0, 'Active grading jobs; wait before rollout.');
async function run() {
  let restarted = false;
  docker(['compose', 'stop', 'api']);
  try {
    assert.equal(active(), 0, 'A submission arrived before shutdown; drain before rollout.');
    const before = inventory(); const submittedBefore = submissionsHash();
    const directory = path.join(os.tmpdir(), 'EduTwin-backups'); fs.mkdirSync(directory, { recursive: true });
    const tag = crypto.randomUUID().replaceAll('-', '');
    const backup = path.join(directory, `2026-10-07_before_ai_grading_proposals_${tag}.sql`);
    const remote = `/tmp/edutwin_ai_grading_proposals_${tag}.sql`;
    docker(['exec', '-e', 'MYSQL_PWD', container, 'mysqldump', '-u', 'root', '--single-transaction', '--hex-blob',
      '--routines', '--triggers', '--no-tablespaces', '--set-gtid-purged=OFF', `--result-file=${remote}`, database],
      { env: { ...process.env, MYSQL_PWD: config.MYSQL_ROOT_PASSWORD } });
    docker(['cp', `${container}:${remote}`, backup]);
    assert.ok(fs.statSync(backup).size > 10000, 'Incomplete backup.');
    if (migration() === previous) mysql(sql, database);
    assert.equal(migration(), target); assert.equal(inventory(), before); assert.equal(submissionsHash(), submittedBefore);
    docker(['compose', 'up', '-d', '--no-deps', 'api', 'web']); restarted = true;
    let ready = false;
    for (let i = 0; i < 60; i++) {
      try { const response = await fetch(`http://localhost:${config.API_PORT || 5000}/api/v1/health/ready`, { signal: AbortSignal.timeout(1500) });
        if (response.ok) { ready = true; break; } } catch {}
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert.ok(ready, 'API readiness failed; backup retained.');
    assert.equal(inventory(), before, 'User data inventory changed on startup.');
    assert.equal(submissionsHash(), submittedBefore, 'Submitted answers/grades changed on startup.');
    console.log(JSON.stringify({ ready, migration: migration(), userDataPreserved: true, submissionsUnchanged: true,
      backup, backupSha256: crypto.createHash('sha256').update(fs.readFileSync(backup)).digest('hex'), activeJobs: active() }));
  } finally { if (!restarted) docker(['start', `${project}-api`]); }
}
run().catch(error => { console.error(error.message); process.exitCode = 1; });
