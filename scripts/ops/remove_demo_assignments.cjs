// Remove only the two fixed-ID assignments from seed_student_assignments.sql.
// Does not reset accounts, classes, questions, other assignments, or database volumes.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { readEnv, mysql, docker, sqlString: encodedSqlString } = require('./mysql_admin.cjs');
const sqlString = value => `${encodedSqlString(value)} COLLATE utf8mb4_bin`;
assert.ok(process.argv.includes('--delete-known-demo-assignments'), 'Explicit deletion flag required.');
const cfg = readEnv();
assert.equal(cfg.MYSQL_DATABASE || 'edutwin', 'edutwin');
const center = '10000000-0000-0000-0000-000000000001';
const ids = ['a1111111-1111-1111-1111-111111111111', 'a2222222-2222-2222-2222-222222222222'];
const titles = ['Luyện tập tư duy: Phương trình bậc nhất & Lập luận toán học',
  'Khảo sát năng lực đầu năm: Hàm số và Đồ thị (Đã hoàn thành)'];
const scope = `center_id=${sqlString(center)} AND assignment_id IN (${ids.map(sqlString).join(',')})`;
const count = sql => Number(mysql(sql, 'edutwin').trim());
const remaining = count(`SELECT COUNT(*) FROM assignments WHERE ${scope};`);
if (!remaining) { console.log('The two demo assignments are already absent; no writes performed.'); process.exit(0); }
assert.equal(remaining, 2, 'Partial/unexpected fixture state: inspect manually.');
ids.forEach((id, index) => assert.equal(count(`SELECT COUNT(*) FROM assignments WHERE center_id=${sqlString(center)}
  AND assignment_id=${sqlString(id)} AND title=${sqlString(titles[index])}
  AND class_id='50000000-0000-0000-0000-000000000005'
  AND created_by_teacher_id='d0000000-0000-0000-0001-000000000002';`), 1, 'Demo identity changed.'));
assert.equal(count(`SELECT COUNT(*) FROM attempts WHERE ${scope};`), 0, 'Real submissions exist: stop; do not remove evidence.');
const baseline = () => mysql(`SELECT COUNT(*) FROM users; SELECT COUNT(*) FROM classes; SELECT COUNT(*) FROM subjects;
  SELECT COUNT(*) FROM questions; SELECT COUNT(*) FROM centers; SELECT COUNT(*) FROM __EFMigrationsHistory;`, 'edutwin').trim();
const before = baseline();
const otherAssignments = count(`SELECT COUNT(*) FROM assignments WHERE NOT (${scope});`);
const tag = crypto.randomUUID().replaceAll('-', '');
const containerBackup = `/tmp/edutwin_before_demo_reset_${tag}.sql`;
const backupDir = path.join(os.tmpdir(), 'EduTwin-backups');
fs.mkdirSync(backupDir, { recursive: true });
const backup = path.join(backupDir, `20261006_before_demo_reset_${tag}.sql`);
docker(['exec','-e','MYSQL_PWD','edutwin-mysql','mysqldump','-u','root','--single-transaction','--hex-blob',
  '--routines','--triggers','--no-tablespaces','--set-gtid-purged=OFF',`--result-file=${containerBackup}`,'edutwin'],
  { env: { ...process.env, MYSQL_PWD: cfg.MYSQL_ROOT_PASSWORD } });
docker(['cp',`edutwin-mysql:${containerBackup}`,backup]);
assert.ok(fs.statSync(backup).size > 10000, 'Incomplete backup: no deletion allowed.');
const deleted = mysql(`START TRANSACTION;
  SELECT assignment_id FROM assignments WHERE ${scope} FOR UPDATE;
  DELETE FROM student_assignment_progress WHERE ${scope}; SELECT ROW_COUNT();
  DELETE FROM assignment_targets WHERE ${scope}; SELECT ROW_COUNT();
  DELETE FROM assignment_questions WHERE ${scope}; SELECT ROW_COUNT();
  DELETE FROM assignments WHERE ${scope}; SELECT ROW_COUNT();
  COMMIT;`, 'edutwin').trim().split(/\r?\n/).slice(2).map(Number);
for (const table of ['assignments','student_assignment_progress','assignment_targets','assignment_questions']) {
  assert.equal(count(`SELECT COUNT(*) FROM ${table} WHERE ${scope};`), 0, `${table}: cleanup incomplete.`);
}
assert.equal(baseline(), before, 'Core inventory changed.');
assert.equal(count('SELECT COUNT(*) FROM assignments;'), otherAssignments, 'Other assignments changed.');
assert.equal(count('SELECT @@FOREIGN_KEY_CHECKS;'), 1);
console.log(JSON.stringify({ removedDemoAssignments:2, deletedProgress:deleted[0], deletedTargets:deleted[1],
  deletedQuestionLinks:deleted[2], deletedAssignments:deleted[3], coreInventoryUnchanged:true, foreignKeysEnabled:true,
  backup, backupSha256:crypto.createHash('sha256').update(fs.readFileSync(backup)).digest('hex') },null,2));
