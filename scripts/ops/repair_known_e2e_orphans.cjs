// One-time repair of the verified 2026-10-05 E2E artifact. Never a generic orphan purge.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { readEnv, docker, mysql } = require('./mysql_admin.cjs');

const config = readEnv();
const database = config.MYSQL_DATABASE || 'edutwin';
assert.equal(database, 'edutwin', 'This repair is only for the verified local database.');
assert.ok(process.argv.includes('--approved-maintenance'), 'Explicit approved maintenance flag is required.');
const center = '10000000-0000-0000-0000-000000000001';
const student = 'd0000000-0000-0000-0001-000000000004';
const subject = '30000000-0000-0000-0000-000000000003';
const learningPath = 'dc01e924-7676-4c0f-ab88-40809e53533d';
const count = (sql) => Number(mysql(sql, database).trim());
if (count('SELECT COUNT(*) FROM twin_update_history WHERE history_id=1 AND attempt_id=1;') === 0) {
  console.log('Known repair already applied; no writes performed.');
  process.exit(0);
}
assert.equal(count('SELECT COUNT(*) FROM attempts;'), 0, 'Live attempts exist: manual replay required, not deletion.');
assert.equal(count('SELECT COUNT(*) FROM twin_update_history;'), 1, 'Other history exists: stop.');
assert.equal(count(`SELECT COUNT(*) FROM twin_update_history WHERE history_id=1 AND attempt_id=1 AND analysis_id=1
 AND center_id='${center}' AND student_id='${student}' AND subject_id='${subject}' AND previous_mastery=0
 AND created_at='2026-10-05 16:37:00.970973';`), 1, 'Artifact identity changed.');
assert.equal(count(`SELECT COUNT(*) FROM behavior_twins WHERE student_id='${student}' AND subject_id='${subject}'
 AND attempt_count=1 AND created_at='2026-10-05 16:37:00.970973';`), 1);
assert.equal(count(`SELECT COUNT(*) FROM knowledge_twins WHERE last_attempt_id=1 AND center_id='${center}'
 AND evidence_count=1 AND created_at='2026-10-05 16:37:00.970973';`), 1);

const tag = crypto.randomUUID().replaceAll('-', '');
const containerBackup = `/tmp/edutwin_pre_e2e_repair_${tag}.sql`;
const backupDir = path.join(os.tmpdir(), 'EduTwin-backups');
fs.mkdirSync(backupDir, { recursive: true });
const backup = path.join(backupDir, `20261006_before_e2e_repair_${tag}.sql`);
docker(['exec', '-e', 'MYSQL_PWD', 'edutwin-mysql', 'mysqldump', '-u', 'root', '--single-transaction',
  '--hex-blob', '--routines', '--triggers', '--no-tablespaces', '--set-gtid-purged=OFF',
  `--result-file=${containerBackup}`, database], { env: { ...process.env, MYSQL_PWD: config.MYSQL_ROOT_PASSWORD } });
docker(['cp', `edutwin-mysql:${containerBackup}`, backup]);
assert.ok(fs.statSync(backup).size > 10000, 'Backup incomplete; no repair allowed.');
const trigger = JSON.parse(mysql(`SELECT JSON_OBJECT('body',ACTION_STATEMENT,'mode',SQL_MODE,'definer',DEFINER)
 FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA='edutwin' AND TRIGGER_NAME='tr_evidence_append_only_delete';`, database).trim());
assert.ok(trigger.body.includes('append-only') && trigger.body.includes('SIGNAL'), 'Unexpected audit trigger: stop.');
const [definerUser, definerHost] = trigger.definer.split('@');
assert.match(definerUser, /^[a-zA-Z0-9_]+$/);
assert.match(definerHost, /^[a-zA-Z0-9_.%:-]+$/);
const triggerBackup = path.join(backupDir, `trigger_before_repair_${tag}.json`);
fs.writeFileSync(triggerBackup, JSON.stringify(trigger, null, 2));
const restoreTrigger = () => mysql(`SET SESSION sql_mode='${trigger.mode}';
 DELIMITER $$
 CREATE DEFINER=\`${definerUser}\`@\`${definerHost}\` TRIGGER tr_evidence_append_only_delete
 BEFORE DELETE ON evidence_assessments FOR EACH ROW ${trigger.body}$$
 DELIMITER ;`, database);
try {
  mysql('DROP TRIGGER tr_evidence_append_only_delete;', database);
  mysql(`START TRANSACTION;
 DELETE FROM learning_path_items WHERE center_id='${center}' AND learning_path_id='${learningPath}';
 DELETE FROM learning_paths WHERE center_id='${center}' AND learning_path_id='${learningPath}' AND generated_from_attempt_id=1;
 DELETE FROM recommendations WHERE center_id='${center}' AND student_id='${student}' AND source_attempt_id=1;
 DELETE FROM knowledge_twins WHERE center_id='${center}' AND last_attempt_id=1 AND evidence_count=1;
 DELETE FROM behavior_twins WHERE center_id='${center}' AND student_id='${student}' AND subject_id='${subject}'
   AND attempt_count=1 AND created_at='2026-10-05 16:37:00.970973';
 DELETE FROM twin_update_history WHERE center_id='${center}' AND history_id=1 AND attempt_id=1;
 DELETE FROM evidence_assessments WHERE center_id='${center}' AND evidence_assessment_id=1 AND attempt_id=1 AND analysis_id=1;
 DELETE FROM reasoning_analyses WHERE center_id='${center}' AND analysis_id=1 AND attempt_id=1;
 COMMIT;`, database);
} finally {
  if (count("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA='edutwin' AND TRIGGER_NAME='tr_evidence_append_only_delete';") === 0) restoreTrigger();
}
assert.equal(count("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA='edutwin' AND TRIGGER_NAME='tr_evidence_append_only_delete' AND ACTION_STATEMENT LIKE '%append-only%';"), 1);
assert.equal(count('SELECT COUNT(*) FROM reasoning_analyses WHERE attempt_id=1;'), 0);
assert.equal(count('SELECT COUNT(*) FROM evidence_assessments WHERE attempt_id=1;'), 0);
// Foreign-key checks remain enabled throughout; only the explicitly approved trigger is restored after maintenance.
const hash = crypto.createHash('sha256').update(fs.readFileSync(backup)).digest('hex');
console.log(JSON.stringify({ repairedKnownArtifact: true, backup, backupSha256: hash, triggerBackup, auditTriggerRestored: true, foreignKeysKeptEnabled: true }));
