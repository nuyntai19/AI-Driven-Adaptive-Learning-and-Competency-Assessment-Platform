// Private local backup + fingerprints for the approved knowledge scope migration.
// No credential, academic content or student data is printed. Test restores cannot target the live DB.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { root, readEnv, mysql, docker, sqlString } = require('./mysql_admin.cjs');
const env = readEnv();
const database = env.MYSQL_DATABASE || 'edutwin';
if (database !== 'edutwin') throw new Error('Only the named local EduTwin database is supported.');
const tag = process.argv.find(a => a.startsWith('--name='))?.slice(7);
if (!tag || !/^[a-z0-9-]{1,100}$/.test(tag)) throw new Error('Use --name=<unique lowercase backup name>.');
const directory = path.join(root, 'storage', 'backups');
const proofPath = path.join(directory, `${tag}.json`);
const backupPath = path.join(directory, `${tag}.sql`);
const tables = ['users', 'students', 'classes', 'class_students', 'knowledge_nodes', 'knowledge_edges',
  'curriculums', 'curriculum_nodes', 'curriculum_classes', 'class_curriculum_applications',
  'questions', 'question_options', 'question_knowledge_nodes', 'assignments', 'assignment_questions',
  'assignment_targets', 'student_assignment_progress', 'attempts', 'attempt_attachments',
  'reasoning_analyses', 'evidence_assessments', 'student_twins', 'knowledge_twins', 'behavior_twins',
  'twin_update_history', 'student_subject_goals', 'recommendations', 'learning_paths', 'learning_path_items'];
const identifier = value => {
  if (!/^[a-z_][a-z0-9_]*$/.test(value)) throw new Error('Unexpected SQL identifier.');
  return `\`${value}\``;
};
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
function schema() {
  return Object.fromEntries(tables.map(table => {
    const columns = mysql(`SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=${sqlString(database)} AND TABLE_NAME=${sqlString(table)} ORDER BY ORDINAL_POSITION;`).trim().split(/\r?\n/);
    const keys = mysql(`SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=${sqlString(database)} AND TABLE_NAME=${sqlString(table)} AND INDEX_NAME='PRIMARY' ORDER BY SEQ_IN_INDEX;`).trim().split(/\r?\n/);
    columns.forEach(identifier); keys.forEach(identifier);
    return [table, { columns, keys }];
  }));
}
function fingerprint(shape, db = database) {
  return Object.fromEntries(Object.entries(shape).map(([table, { columns, keys }]) => {
    // Explicit columns let us prove pre-existing data unchanged after an additive migration.
    const rows = mysql(`SET SESSION TRANSACTION READ ONLY; START TRANSACTION READ ONLY; SELECT ${columns.map(identifier).join(',')} FROM ${identifier(table)} ORDER BY ${keys.map(identifier).join(',')}; COMMIT;`, db);
    const count = Number(mysql(`SELECT COUNT(*) FROM ${identifier(table)};`, db).trim());
    return [table, { count, sha256: hash(rows) }];
  }));
}
if (process.argv.includes('--capture')) {
  fs.mkdirSync(directory, { recursive: true });
  if (fs.existsSync(proofPath) || fs.existsSync(backupPath)) throw new Error('Backup name already exists; refusing overwrite.');
  const dump = docker(['exec', '-e', 'MYSQL_PWD', `${env.COMPOSE_PROJECT_NAME || 'edutwin'}-mysql`, 'mysqldump', '-u', 'root',
    '--single-transaction', '--hex-blob', '--routines', '--triggers', '--no-tablespaces', '--set-gtid-purged=OFF',
    '--default-character-set=utf8mb4', database], { env: { ...process.env, MYSQL_PWD: env.MYSQL_ROOT_PASSWORD } });
  if (Buffer.byteLength(dump) < 10000) throw new Error('Backup appears incomplete.');
  fs.writeFileSync(backupPath, dump, { flag: 'wx' });
  const shape = schema();
  const proof = { capturedAt: new Date().toISOString(), backupSha256: hash(dump), shape, tables: fingerprint(shape) };
  fs.writeFileSync(proofPath, JSON.stringify(proof, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ backupPath, proofPath, backupSha256: proof.backupSha256,
    tableCounts: Object.fromEntries(Object.entries(proof.tables).map(([t, v]) => [t, v.count])) }));
} else if (process.argv.includes('--verify')) {
  const baseline = JSON.parse(fs.readFileSync(proofPath, 'utf8'));
  const verifyDatabase = process.argv.find(a => a.startsWith('--database='))?.slice(11) || database;
  if (verifyDatabase !== database && !/^edutwin_scope_test_[a-z0-9_]{1,40}$/.test(verifyDatabase))
    throw new Error('Verification is limited to the local database and owned test schemas.');
  const current = fingerprint(baseline.shape, verifyDatabase);
  const changedTables = tables.filter(t => JSON.stringify(baseline.tables[t]) !== JSON.stringify(current[t]));
  console.log(JSON.stringify({ database: verifyDatabase, protectedDataUnchanged: changedTables.length === 0, changedTables,
    counts: Object.fromEntries(Object.entries(current).map(([t, v]) => [t, v.count])) }));
  if (changedTables.length) process.exitCode = 1;
} else {
  const target = process.argv.find(a => a.startsWith('--restore-test='))?.slice(15);
  if (!target || !/^edutwin_scope_test_[a-z0-9_]{1,40}$/.test(target) || target === database)
    throw new Error('Use --capture, --verify or --restore-test=edutwin_scope_test_<unique suffix>.');
  const proof = JSON.parse(fs.readFileSync(proofPath, 'utf8'));
  const dump = fs.readFileSync(backupPath, 'utf8');
  if (hash(dump) !== proof.backupSha256) throw new Error('Backup checksum does not match.');
  mysql(`CREATE DATABASE ${identifier(target)} CHARACTER SET utf8mb4;`);
  // Restored triggers must execute as the test restore user; the original app definer only has live-schema grants.
  // This changes test schema ownership only. The checksum-verified original backup stays untouched.
  const testDump = dump.replace(/DEFINER=`[^`]*`@`[^`]*`/g, 'DEFINER=CURRENT_USER');
  mysql(testDump, target);
  const restored = fingerprint(proof.shape, target);
  const mismatches = tables.filter(t => JSON.stringify(restored[t]) !== JSON.stringify(proof.tables[t]));
  console.log(JSON.stringify({ restoredDatabase: target, backupVerified: mismatches.length === 0, mismatches }));
  if (mismatches.length) process.exitCode = 1;
}
