// Scoped, additive local repair for the two known EDUTWIN_A demo classes.
// Never rewrites assignments, targets, progress, attempts, analyses or scores.
// Default is a read-only plan; --apply runs after an operator database backup.
const crypto = require('node:crypto');
const { mysql, readEnv, sqlString } = require('./mysql_admin.cjs');
const db = readEnv().MYSQL_DATABASE || 'edutwin';
const center = '10000000-0000-0000-0000-000000000001';
const actor = 'd0000000-0000-0000-0001-000000000001';
const eq = (column, value) => `BINARY ${column}=BINARY ${sqlString(value)}`;
const old = [
  { id: '50000000-0000-0000-0000-000000000005', prefix: '51000000', subject: '30000000-0000-0000-0000-000000000003', teacher: 'd0000000-0000-0000-0001-000000000002', name: 'Toán' },
  { id: '60000000-0000-0000-0000-000000000006', prefix: '61000000', subject: '40000000-0000-0000-0000-000000000004', teacher: 'd0000000-0000-0000-0001-000000000003', name: 'Tiếng Anh' },
];
const planned = old.flatMap(o => [10, 11, 12].map(grade => ({ ...o, oldId: o.id,
  id: `${o.prefix}-0000-0000-0000-${String(grade).padStart(12, '0')}`, grade, className: `Lớp ${o.name} ${grade}` })));
const historyTables = ['assignments', 'assignment_questions', 'assignment_targets', 'student_assignment_progress',
  'attempts', 'reasoning_analyses', 'attempt_attachments', 'curriculums', 'curriculum_nodes', 'curriculum_classes',
  'student_twins', 'student_subject_goals'];
function historyFingerprint() {
  return Object.fromEntries(historyTables.map(table => {
    const primaryColumns = mysql(`SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=${sqlString(db)} AND TABLE_NAME=${sqlString(table)} AND INDEX_NAME='PRIMARY' ORDER BY SEQ_IN_INDEX;`)
      .trim().split(/\r?\n/).filter(Boolean);
    if (!primaryColumns.length || primaryColumns.some(c => !/^[a-z_]+$/.test(c))) throw new Error(`Invalid primary key for ${table}`);
    const rows = mysql(`SELECT * FROM \`${table}\` WHERE ${eq('center_id', center)} ORDER BY ${primaryColumns.map(c => `\`${c}\``).join(',')};`, db);
    const count = Number(mysql(`SELECT COUNT(*) FROM \`${table}\` WHERE ${eq('center_id', center)};`, db).trim());
    return [table, { count, sha256: crypto.createHash('sha256').update(rows).digest('hex') }];
  }));
}
function assertions() {
  const a = [`INSERT INTO repair_assert SELECT IF(COUNT(*)=1,1,0) FROM centers WHERE ${eq('center_id', center)} AND center_code='EDUTWIN_A' AND status='Active' AND is_deleted=0;`,
    `INSERT INTO repair_assert SELECT IF(COUNT(*)=1,1,0) FROM users WHERE ${eq('center_id', center)} AND ${eq('user_id', actor)} AND role_name='CenterManager' AND status='Active' AND is_deleted=0;`];
  for (const o of old) a.push(`INSERT INTO repair_assert SELECT IF(COUNT(*)=1,1,0) FROM classes WHERE ${eq('center_id', center)} AND ${eq('class_id', o.id)} AND ${eq('teacher_id', o.teacher)} AND ${eq('subject_id', o.subject)} AND grade_level IS NULL AND status='Active' AND is_deleted=0;`);
  for (const p of planned) {
    a.push(`INSERT INTO repair_assert SELECT IF(COUNT(*)=0,1,0) FROM classes WHERE (${eq('class_id', p.id)} OR (${eq('center_id', center)} AND ${eq('class_name', p.className)} AND academic_year='2026-2027')) AND NOT (${eq('center_id', center)} AND ${eq('class_id', p.id)} AND ${eq('teacher_id', p.teacher)} AND ${eq('subject_id', p.subject)} AND grade_level<=>${p.grade} AND status='Active' AND is_deleted=0);`);
  }
  return a.join('\n');
}
const before = historyFingerprint();
console.log(JSON.stringify({ mode: process.argv.includes('--apply') ? 'apply' : 'plan', center: 'EDUTWIN_A',
  classes: planned.map(p => ({ id: p.id, name: p.className, grade: p.grade })), preservedHistory: before }));
if (!process.argv.includes('--apply')) process.exit(0);
const statements = [];
for (const p of planned) {
  statements.push(`INSERT INTO classes (class_id,center_id,teacher_id,subject_id,class_name,academic_year,grade_level,status,created_at,created_by,updated_at,updated_by,is_deleted,row_version)
    SELECT ${sqlString(p.id)},center_id,teacher_id,subject_id,${sqlString(p.className)},academic_year,${p.grade},'Active',UTC_TIMESTAMP(6),${sqlString(actor)},UTC_TIMESTAMP(6),${sqlString(actor)},0,1
    FROM classes c WHERE ${eq('c.center_id', center)} AND ${eq('c.class_id', p.oldId)} AND NOT EXISTS(SELECT 1 FROM classes n WHERE ${eq('n.class_id', p.id)});`);
  statements.push(`INSERT INTO class_students (center_id,class_id,student_id,joined_at,status,created_by,grade_level_at_enrollment)
    SELECT s.center_id,${sqlString(p.id)},s.student_id,UTC_TIMESTAMP(6),'Active',${sqlString(actor)},s.grade_level FROM students s
    JOIN class_students legacy ON BINARY legacy.center_id=BINARY s.center_id AND BINARY legacy.student_id=BINARY s.student_id
    JOIN users u ON BINARY u.center_id=BINARY s.center_id AND BINARY u.user_id=BINARY s.student_id
    WHERE ${eq('s.center_id', center)} AND ${eq('legacy.class_id', p.oldId)} AND legacy.status='Active' AND s.grade_level=${p.grade}
      AND s.is_deleted=0 AND u.is_deleted=0 AND u.status='Active' AND u.role_name='Student'
      AND NOT EXISTS(SELECT 1 FROM class_students n WHERE BINARY n.center_id=BINARY s.center_id AND BINARY n.student_id=BINARY s.student_id AND ${eq('n.class_id', p.id)});`);
}
for (const o of old) statements.push(`UPDATE classes SET class_name=${sqlString(`Lớp ${o.name} — Dữ liệu kiểm thử cũ`)}, updated_at=UTC_TIMESTAMP(6),updated_by=${sqlString(actor)},row_version=row_version+1
  WHERE ${eq('center_id', center)} AND ${eq('class_id', o.id)} AND NOT (${eq('class_name', `Lớp ${o.name} — Dữ liệu kiểm thử cũ`)});`);
const planJson = JSON.stringify({ classIds: planned.map(p => p.id), oldClassesPreserved: old.map(o => o.id), historyBefore: before });
statements.push(`INSERT INTO authorization_audit_logs(center_id,actor_user_id,action_type,target_type,target_id,after_data,reason,trace_id,created_at,created_by)
  SELECT ${sqlString(center)},${sqlString(actor)},'DemoGradeClassesSplit','Center',${sqlString(center)},${sqlString(planJson)},
    ${sqlString('Tách lớp mẫu theo khối; giữ lớp kiểm thử cũ và toàn bộ bài tập/bài làm/điểm, không rút học sinh khỏi lớp cũ.')},'ops:split-demo-grades:2026-10-08',UTC_TIMESTAMP(6),${sqlString(actor)}
  WHERE NOT EXISTS(SELECT 1 FROM authorization_audit_logs WHERE ${eq('center_id', center)} AND trace_id='ops:split-demo-grades:2026-10-08');`);
const sql = `SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; START TRANSACTION;
  SELECT class_id FROM classes WHERE ${eq('center_id', center)} FOR UPDATE;
  SELECT student_id FROM students WHERE ${eq('center_id', center)} FOR UPDATE;
  CREATE TEMPORARY TABLE repair_assert(ok INT NOT NULL CHECK(ok=1));
  ${assertions()}\n${statements.join('\n')}\nCOMMIT;`;
mysql(sql, db);
const after = historyFingerprint();
if (JSON.stringify(before) !== JSON.stringify(after)) throw new Error('Historical data changed unexpectedly; inspect backup and audit before continuing.');
console.log(JSON.stringify({ applied: true, historicalRowsUnchanged: true,
  distribution: mysql(`SELECT c.class_name,c.grade_level,s.grade_level,COUNT(cs.student_id) FROM classes c
    LEFT JOIN class_students cs ON BINARY cs.center_id=BINARY c.center_id AND BINARY cs.class_id=BINARY c.class_id AND cs.status='Active'
    LEFT JOIN students s ON BINARY s.center_id=BINARY cs.center_id AND BINARY s.student_id=BINARY cs.student_id
    WHERE ${eq('c.center_id', center)} GROUP BY c.class_name,c.grade_level,s.grade_level ORDER BY c.class_name,s.grade_level;`, db).trim() }));
