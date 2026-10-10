const fs = require('node:fs');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { mysql, sqlString } = require('../ops/mysql_admin.cjs');
const { manifest, manifestPath } = require('./acceptance_stack.cjs');
const CENTER_A_ID = '10000000-0000-0000-0000-000000000001';
const CENTER_B_ID = '20000000-0000-0000-0000-000000000002';
const CLASS_MATH_A_ID = '50000000-0000-0000-0000-000000000005';
const TEACHER_MATH_A_ID = 'd0000000-0000-0000-0001-000000000002';
const STUDENT01_A_ID = 'd0000000-0000-0000-0001-000000000004';
const STUDENT02_A_ID = 'd0000000-0000-0000-0001-000000000005';
const STUDENT01_B_ID = 'd0000000-0000-0000-0002-000000000004';
function execSql(sql) { return mysql(sql, manifest().database); }
function createAssignmentFixture({ tag, title, timeLimitMinutes = 60, dueHoursFromNow = 48,
  questionConfigs = [{ questionId: 10000, points: 1 }, { questionId: 10001, points: 1 }],
  centerId = CENTER_A_ID, classId = CLASS_MATH_A_ID, teacherId = TEACHER_MATH_A_ID, studentIds = [STUDENT01_A_ID] }) {
  const state = manifest();
  const assignmentId = crypto.randomUUID();
  const fullTitle = 'E2E_ACC_' + tag + '_' + title;
  const dueAt = new Date(Date.now() + dueHoursFromNow * 3600 * 1000);
  const dueIso = dueAt.toISOString().slice(0, 19).replace('T', ' ');
  assert.ok(timeLimitMinutes === null || Number.isInteger(timeLimitMinutes) && timeLimitMinutes > 0);
  for (const q of questionConfigs) assert.ok(Number.isSafeInteger(q.questionId) && Number.isFinite(q.points) && q.points > 0);
  state.fixtures.push({ assignmentId, centerId, fullTitle });
  fs.writeFileSync(manifestPath, JSON.stringify(state, null, 2));
  let sql = `START TRANSACTION; INSERT INTO assignments (assignment_id,center_id,class_id,created_by_teacher_id,title,instructions,
    due_at,time_limit_minutes,status,published_at,target_mode,allow_grade_mismatch,created_at,updated_at,is_deleted,row_version)
    VALUES (${sqlString(assignmentId)},${sqlString(centerId)},${sqlString(classId)},${sqlString(teacherId)},${sqlString(fullTitle)},
    'Isolated acceptance fixture',${sqlString(dueIso)},${timeLimitMinutes ?? 'NULL'},'Published',UTC_TIMESTAMP(),'WholeClass',0,UTC_TIMESTAMP(),UTC_TIMESTAMP(),0,1);`;
  questionConfigs.forEach((q, index) => {
    sql += `INSERT INTO assignment_questions (center_id,assignment_id,question_id,order_index,points,created_at,is_voided,void_reason,voided_at,voided_by_user_id)
      VALUES (${sqlString(centerId)},${sqlString(assignmentId)},${q.questionId},${index},${q.points},UTC_TIMESTAMP(),${q.isVoided ? 1 : 0},
      ${q.isVoided ? "'Isolated void test'" : 'NULL'},${q.isVoided ? 'UTC_TIMESTAMP()' : 'NULL'},${q.isVoided ? sqlString(teacherId) : 'NULL'});`;
  });
  studentIds.forEach(id => {
    const progressId = crypto.randomBytes(6).readUIntBE(0, 6).toString();
    sql += `INSERT INTO assignment_targets(center_id,assignment_id,student_id,target_source,created_at,created_by)
      VALUES (${sqlString(centerId)},${sqlString(assignmentId)},${sqlString(id)},'WholeClass',UTC_TIMESTAMP(),${sqlString(teacherId)});
      INSERT INTO student_assignment_progress(progress_id,center_id,assignment_id,student_id,status,completed_question_count,total_question_count,
      created_at,updated_at,created_by,updated_by,is_deleted,row_version) VALUES (${progressId},${sqlString(centerId)},${sqlString(assignmentId)},${sqlString(id)},
      'NotStarted',0,${questionConfigs.length},UTC_TIMESTAMP(),UTC_TIMESTAMP(),${sqlString(teacherId)},${sqlString(teacherId)},0,1);`;
  });
  execSql(sql + 'COMMIT;');
  return { assignmentId, fullTitle, dueAt };
}
function cleanupFixtures() {
  // Never delete immutable evidence: the owner drops the entire isolated schema in stop().
  manifest();
  return 0;
}
module.exports = { execSql, createAssignmentFixture, cleanupFixtures, CENTER_A_ID, CENTER_B_ID, CLASS_MATH_A_ID,
  TEACHER_MATH_A_ID, STUDENT01_A_ID, STUDENT02_A_ID, STUDENT01_B_ID };
