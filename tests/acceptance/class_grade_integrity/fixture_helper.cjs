const fs = require('node:fs');
const path = require('node:path');
const { execSync } = require('node:child_process');

const WORKSPACE_DIR = process.env.WORKSPACE_ROOT || path.resolve(__dirname, '../../..');

function getDbPassword() {
  if (process.env.MYSQL_PASSWORD) {
    return process.env.MYSQL_PASSWORD.trim();
  }
  const envPath = path.join(WORKSPACE_DIR, '.env');
  if (fs.existsSync(envPath)) {
    const envContent = fs.readFileSync(envPath, 'utf8');
    const match = envContent.match(/^MYSQL_PASSWORD=(.*)$/m);
    if (match) {
      return match[1].trim();
    }
  }
  throw new Error('Cannot find MYSQL_PASSWORD in environment or .env');
}

function executeSql(sql) {
  const pw = getDbPassword();
  execSync(`docker exec -i edutwin-mysql mysql --default-character-set=utf8mb4 -uedutwin_user -p${pw} edutwin`, {
    input: sql,
    encoding: 'utf8',
    stdio: ['pipe', 'pipe', 'pipe'],
  });
}

function queryRows(sql) {
  const pw = getDbPassword();
  const raw = execSync(`docker exec -i edutwin-mysql mysql --default-character-set=utf8mb4 -uedutwin_user -p${pw} edutwin`, {
    input: sql,
    encoding: 'utf8',
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  const lines = raw.trim().split(/\r?\n/).filter(Boolean);
  if (lines.length < 2) return [];
  const headers = lines[0].split('\t');
  return lines.slice(1).map((line) => {
    const cols = line.split('\t');
    const obj = {};
    headers.forEach((h, i) => {
      obj[h] = cols[i] === 'NULL' ? null : (cols[i] ?? null);
    });
    return obj;
  });
}

function resetUiaccFixtures() {
  console.log('[FIXTURE] Resetting all UIACC-prefixed fixtures safely in database...');
  const cleanupSql = `
    DELETE FROM student_assignment_progress WHERE assignment_id IN (SELECT assignment_id FROM assignments WHERE title LIKE 'UIACC%');
    DELETE FROM assignment_targets WHERE assignment_id IN (SELECT assignment_id FROM assignments WHERE title LIKE 'UIACC%');
    DELETE FROM assignment_questions WHERE assignment_id IN (SELECT assignment_id FROM assignments WHERE title LIKE 'UIACC%');
    DELETE FROM assignments WHERE title LIKE 'UIACC%';
    DELETE FROM curriculum_classes WHERE curriculum_id IN (SELECT curriculum_id FROM curriculums WHERE title LIKE 'UIACC%') OR class_id IN (SELECT class_id FROM classes WHERE class_name LIKE 'UIACC%');
    DELETE FROM curriculums WHERE title LIKE 'UIACC%';
    DELETE FROM class_students WHERE class_id IN (SELECT class_id FROM classes WHERE class_name LIKE 'UIACC%');
    DELETE FROM classes WHERE class_name LIKE 'UIACC%';
  `;
  executeSql(cleanupSql);
  console.log('[FIXTURE] UIACC fixtures reset complete (0 residual UIACC records).');
}

function ensureQuestions20030And20031() {
  const rows = queryRows(`SELECT question_id, grade_level, status FROM questions WHERE question_id IN (20030, 20031);`);
  const has10 = rows.some((r) => r.question_id === '20030');
  const has11 = rows.some((r) => r.question_id === '20031');

  if (has10 && has11) {
    executeSql(`UPDATE questions SET status = 'Active', is_deleted = 0 WHERE question_id IN (20030, 20031);`);
    return;
  }

  const sql = `
    INSERT INTO questions (
      question_id, center_id, subject_id, primary_topic_node_id, question_type,
      question_text, correct_answer, solution, difficulty, grade_level,
      estimated_time_seconds, status, created_at, created_by, updated_at,
      updated_by, is_deleted, row_version
    ) VALUES
    (
      20030, 'c0000000-0000-0000-0001-000000000001', '40000000-0000-0000-0000-000000000004', '10003', 'MultipleChoice',
      'Choose the best word to complete the sentence: She speaks English very ____.', 'B', 'Well is an adverb describing the verb speaks.', 2, 10,
      120, 'Active', UTC_TIMESTAMP(6), 'd0000000-0000-0000-0001-000000000003', UTC_TIMESTAMP(6),
      'd0000000-0000-0000-0001-000000000003', 0, 1
    ),
    (
      20031, 'c0000000-0000-0000-0001-000000000001', '40000000-0000-0000-0000-000000000004', '10003', 'MultipleChoice',
      'Advanced Grammar: If he had studied harder, he ____ the university entrance exam.', 'C', 'Conditional sentence type 3: If + past perfect, would have + V3/ed.', 3, 11,
      120, 'Active', UTC_TIMESTAMP(6), 'd0000000-0000-0000-0001-000000000003', UTC_TIMESTAMP(6),
      'd0000000-0000-0000-0001-000000000003', 0, 1
    )
    ON DUPLICATE KEY UPDATE status = 'Active', is_deleted = 0;
  `;
  executeSql(sql);
}

// ──────────────────────────────────────────────────────────────────────────
// Programmatic Assertions for Fail-Closed Acceptance
// ──────────────────────────────────────────────────────────────────────────

function assertClassStudent(className, studentFullNameOrId, options = {}) {
  const sql = `
    SELECT
      cs.class_id,
      c.class_name,
      cs.student_id,
      s.full_name,
      cs.status,
      cs.removed_at,
      cs.grade_level_at_enrollment,
      cs.grade_mismatch_reason,
      cs.exception_approved_by,
      cs.exception_approved_at
    FROM class_students cs
    JOIN classes c ON cs.class_id = c.class_id
    JOIN students s ON cs.student_id = s.student_id
    WHERE c.class_name = '${className}'
      AND (s.student_id = '${studentFullNameOrId}' OR s.full_name LIKE '%${studentFullNameOrId}%')
      AND cs.status = 'Active' AND cs.removed_at IS NULL;
  `;
  const rows = queryRows(sql);
  if (rows.length === 0) {
    throw new Error(`[ASSERT FAIL] No active membership found in class '${className}' for student '${studentFullNameOrId}'`);
  }
  const row = rows[0];

  if (options.expectedGradeLevel !== undefined) {
    if (String(row.grade_level_at_enrollment) !== String(options.expectedGradeLevel)) {
      throw new Error(`[ASSERT FAIL] grade_level_at_enrollment mismatch: expected ${options.expectedGradeLevel}, got ${row.grade_level_at_enrollment}`);
    }
  }

  if (options.expectedReasonSubstr) {
    if (!row.grade_mismatch_reason || !row.grade_mismatch_reason.includes(options.expectedReasonSubstr)) {
      throw new Error(`[ASSERT FAIL] grade_mismatch_reason does not contain '${options.expectedReasonSubstr}': got '${row.grade_mismatch_reason}'`);
    }
  }

  if (options.mustHaveApproval) {
    if (!row.exception_approved_by) {
      throw new Error(`[ASSERT FAIL] exception_approved_by is null for cross-grade student`);
    }
    if (!row.exception_approved_at) {
      throw new Error(`[ASSERT FAIL] exception_approved_at is null for cross-grade student`);
    }
  }

  return row;
}

function assertClassActive(className, expectedGradeLevel = null) {
  const sql = `SELECT class_id, class_name, grade_level, status, is_deleted FROM classes WHERE class_name = '${className}' AND is_deleted = 0;`;
  const rows = queryRows(sql);
  if (rows.length === 0) {
    throw new Error(`[ASSERT FAIL] Class '${className}' does not exist or is marked deleted`);
  }
  if (expectedGradeLevel !== null && Number(rows[0].grade_level) !== Number(expectedGradeLevel)) {
    throw new Error(`[ASSERT FAIL] Class '${className}' grade_level expected ${expectedGradeLevel}, got ${rows[0].grade_level}`);
  }
  return rows[0];
}

function assertClassDeleted(className) {
  const sql = `SELECT class_id, class_name, is_deleted FROM classes WHERE (class_name = '${className}' OR class_name LIKE '${className}#del#%') AND is_deleted = 1;`;
  const rows = queryRows(sql);
  if (rows.length === 0) {
    throw new Error(`[ASSERT FAIL] Class '${className}' was NOT deleted in database`);
  }
  return rows[0];
}

function assertCurriculumCreated(curriculumTitle, expectedGradeLevel) {
  const sql = `SELECT curriculum_id, title, grade_level, review_status, is_deleted FROM curriculums WHERE title = '${curriculumTitle}' AND is_deleted = 0;`;
  const rows = queryRows(sql);
  if (rows.length === 0) {
    throw new Error(`[ASSERT FAIL] Curriculum '${curriculumTitle}' not found in database`);
  }
  if (Number(rows[0].grade_level) !== Number(expectedGradeLevel)) {
    throw new Error(`[ASSERT FAIL] Curriculum '${curriculumTitle}' grade_level expected ${expectedGradeLevel}, got ${rows[0].grade_level}`);
  }
  return rows[0];
}

function assertCurriculumClassLink(curriculumTitle, className, mustBeLinked = true) {
  const sql = `
    SELECT cc.curriculum_id, cc.class_id, c.title, cl.class_name
    FROM curriculum_classes cc
    JOIN curriculums c ON cc.curriculum_id = c.curriculum_id
    JOIN classes cl ON cc.class_id = cl.class_id
    WHERE c.title = '${curriculumTitle}' AND cl.class_name = '${className}';
  `;
  const rows = queryRows(sql);
  if (mustBeLinked && rows.length === 0) {
    throw new Error(`[ASSERT FAIL] Expected class '${className}' to be linked to curriculum '${curriculumTitle}', but no link found in DB`);
  }
  if (!mustBeLinked && rows.length > 0) {
    throw new Error(`[ASSERT FAIL] Class '${className}' was unexpectedly linked to curriculum '${curriculumTitle}' in DB!`);
  }
  return rows;
}

function assertAssignmentPublished(assignmentTitle, options = {}) {
  const sql = `
    SELECT a.assignment_id, a.title, a.status, a.grade_mismatch_reason, a.allow_grade_mismatch, a.is_deleted
    FROM assignments a
    WHERE a.title = '${assignmentTitle}' AND a.is_deleted = 0;
  `;
  const rows = queryRows(sql);
  if (rows.length === 0) {
    throw new Error(`[ASSERT FAIL] Assignment '${assignmentTitle}' not found in DB`);
  }
  const row = rows[0];
  if (row.status !== 'Published') {
    throw new Error(`[ASSERT FAIL] Assignment '${assignmentTitle}' status expected 'Published', got '${row.status}'`);
  }
  if (options.expectedReasonSubstr) {
    if (!row.grade_mismatch_reason || !row.grade_mismatch_reason.includes(options.expectedReasonSubstr)) {
      throw new Error(`[ASSERT FAIL] Assignment '${assignmentTitle}' grade_mismatch_reason does not contain '${options.expectedReasonSubstr}'`);
    }
  }
  if (options.targetStudentId) {
    const tSql = `SELECT * FROM assignment_targets WHERE assignment_id = '${row.assignment_id}' AND student_id = '${options.targetStudentId}';`;
    const tRows = queryRows(tSql);
    if (tRows.length === 0) {
      throw new Error(`[ASSERT FAIL] Target student '${options.targetStudentId}' not found in assignment_targets for '${assignmentTitle}'`);
    }
  }
  return row;
}

module.exports = {
  getDbPassword,
  executeSql,
  queryRows,
  resetUiaccFixtures,
  ensureQuestions20030And20031,
  assertClassStudent,
  assertClassActive,
  assertClassDeleted,
  assertCurriculumCreated,
  assertCurriculumClassLink,
  assertAssignmentPublished,
};
