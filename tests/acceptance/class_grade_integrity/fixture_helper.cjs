const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execSync } = require('node:child_process');

const WORKSPACE_DIR = process.env.WORKSPACE_ROOT || path.resolve(__dirname, '../../..');
const MANIFEST_FILE = path.join(__dirname, '.fixture_manifest.json');

// In-memory registry of entities created specifically by this test run
const runRegistry = {
  runId: null,
  classIds: new Set(),
  curriculumIds: new Set(),
  assignmentIds: new Set(),
};

function readManifestFile() {
  if (fs.existsSync(MANIFEST_FILE)) {
    try {
      return JSON.parse(fs.readFileSync(MANIFEST_FILE, 'utf8'));
    } catch (e) {
      return null;
    }
  }
  return null;
}

function writeManifestFile() {
  const data = {
    runId: runRegistry.runId,
    updatedAt: new Date().toISOString(),
    status: 'RUNNING',
    ownedEntities: {
      classIds: Array.from(runRegistry.classIds),
      curriculumIds: Array.from(runRegistry.curriculumIds),
      assignmentIds: Array.from(runRegistry.assignmentIds),
    },
  };
  fs.writeFileSync(MANIFEST_FILE, JSON.stringify(data, null, 2), 'utf8');
}

function initializeRunManifest() {
  runRegistry.runId = `UIACC-RUN-${Date.now().toString(36).toUpperCase()}-${Math.random().toString(36).substring(2, 6).toUpperCase()}`;
  runRegistry.classIds.clear();
  runRegistry.curriculumIds.clear();
  runRegistry.assignmentIds.clear();
  writeManifestFile();
  console.log(`[MANIFEST] Initialized fresh test session manifest: ${runRegistry.runId}`);
}

const UUID_REGEX = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function assertValidUuid(id, entityType = 'entity') {
  if (typeof id !== 'string' || !UUID_REGEX.test(id.trim())) {
    throw new Error(`[SECURITY FAIL] Invalid ${entityType} ID format (expected UUID v4): '${id}'`);
  }
  return id.trim();
}

const TARGET_CENTER_ID = '10000000-0000-0000-0000-000000000001';

const ALLOWED_CLASS_NAMES = new Set([
  'UIACC-GRADE10-CLASS',
  'UIACC-GRADE11-CLASS',
  'UIACC-INACTIVE-CLASS',
  'UIACC-EMPTY-CLASS',
]);

function isAllowedClassName(name) {
  if (!name) return false;
  return ALLOWED_CLASS_NAMES.has(name) || name.startsWith('UIACC-EMPTY-CLASS#del#');
}

const ALLOWED_CURRICULUM_TITLES = new Set(['UIACC-ENG-G10']);
const ALLOWED_ASSIGNMENT_TITLES = new Set(['UIACC-ENG-HW-G10']);

function resetRunRegistry() {
  runRegistry.runId = null;
  runRegistry.classIds.clear();
  runRegistry.curriculumIds.clear();
  runRegistry.assignmentIds.clear();
}

function registerCreatedClassId(id) {
  if (id) {
    const validId = assertValidUuid(id, 'class');
    runRegistry.classIds.add(validId);
    writeManifestFile();
  }
}

function registerCreatedCurriculumId(id) {
  if (id) {
    const validId = assertValidUuid(id, 'curriculum');
    runRegistry.curriculumIds.add(validId);
    writeManifestFile();
  }
}

function registerCreatedAssignmentId(id) {
  if (id) {
    const validId = assertValidUuid(id, 'assignment');
    runRegistry.assignmentIds.add(validId);
    writeManifestFile();
  }
}

function getTrackedIds() {
  return {
    runId: runRegistry.runId,
    classIds: Array.from(runRegistry.classIds),
    curriculumIds: Array.from(runRegistry.curriculumIds),
    assignmentIds: Array.from(runRegistry.assignmentIds),
  };
}

// ──────────────────────────────────────────────────────────────────────────
// Secure MySQL connection: Zero secrets in command-line arguments or logs
// ──────────────────────────────────────────────────────────────────────────

function ensureClientConfigFile() {
  const envPath = path.join(WORKSPACE_DIR, '.env');
  let pw = process.env.MYSQL_PASSWORD;
  if (!pw && fs.existsSync(envPath)) {
    const envContent = fs.readFileSync(envPath, 'utf8');
    const match = envContent.match(/^MYSQL_PASSWORD=(.*)$/m);
    if (match) {
      pw = match[1].trim();
    }
  }
  if (!pw) {
    throw new Error('MYSQL_PASSWORD not found in environment or .env');
  }

  // Write option file inside container via stdin - NEVER in CLI args or process table
  const cnfContent = `[client]\nuser=edutwin_user\npassword=${pw}\nhost=localhost\ndefault-character-set=utf8mb4\n`;
  execSync(
    'docker exec -i edutwin-mysql sh -c "cat > /etc/mysql/fixture_client.cnf && chmod 600 /etc/mysql/fixture_client.cnf"',
    { input: cnfContent, encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }
  );
}

function removeClientConfigFile() {
  let removeErr = null;
  try {
    execSync('docker exec edutwin-mysql rm -f /etc/mysql/fixture_client.cnf', {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  } catch (err) {
    removeErr = err;
  }

  // Fail-closed verification: credential file MUST NOT remain in container
  let fileCheck = '';
  try {
    fileCheck = execSync('docker exec edutwin-mysql test -f /etc/mysql/fixture_client.cnf && echo EXISTS || echo GONE', {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    }).trim();
  } catch (err) {
    throw new Error(`[SECURITY FAIL] Failed to verify removal of credential file /etc/mysql/fixture_client.cnf: ${err.message}`);
  }

  if (fileCheck.includes('EXISTS')) {
    throw new Error('[SECURITY FAIL] /etc/mysql/fixture_client.cnf still exists in container after deletion attempt!');
  }

  if (removeErr) {
    throw new Error(`[SECURITY FAIL] Error executing removal of credential file: ${removeErr.message}`);
  }

  console.log('[SECURITY] Credential option file /etc/mysql/fixture_client.cnf successfully removed and verified absent from container.');
}

// Safety-net hooks: Ensure credential file is purged even if standalone script exits or process is interrupted
process.on('exit', () => {
  try {
    const check = execSync('docker exec edutwin-mysql test -f /etc/mysql/fixture_client.cnf && echo EXISTS || echo GONE', {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    }).trim();
    if (check.includes('EXISTS')) {
      execSync('docker exec edutwin-mysql rm -f /etc/mysql/fixture_client.cnf', {
        encoding: 'utf8',
        stdio: ['pipe', 'pipe', 'pipe'],
      });
    }
  } catch (e) {
    // Process is exiting
  }
});

process.on('SIGINT', () => {
  try {
    removeClientConfigFile();
  } catch (e) {}
  process.exit(130);
});

process.on('SIGTERM', () => {
  try {
    removeClientConfigFile();
  } catch (e) {}
  process.exit(143);
});

function executeSql(sql) {
  try {
    execSync('docker exec -i edutwin-mysql mysql --defaults-file=/etc/mysql/fixture_client.cnf edutwin', {
      input: sql,
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  } catch (err) {
    // If the cnf file is missing, initialize it and retry once
    ensureClientConfigFile();
    execSync('docker exec -i edutwin-mysql mysql --defaults-file=/etc/mysql/fixture_client.cnf edutwin', {
      input: sql,
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  }
}

function queryRows(sql) {
  let raw = '';
  try {
    raw = execSync('docker exec -i edutwin-mysql mysql --defaults-file=/etc/mysql/fixture_client.cnf edutwin', {
      input: sql,
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  } catch (err) {
    ensureClientConfigFile();
    raw = execSync('docker exec -i edutwin-mysql mysql --defaults-file=/etc/mysql/fixture_client.cnf edutwin', {
      input: sql,
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe'],
    });
  }

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

// ──────────────────────────────────────────────────────────────────────────
// Scoped ID-Based Fixture Cleanup: Strictly validated UUIDs & two-way ownership
// ──────────────────────────────────────────────────────────────────────────

function deleteEntitiesByIds({ classIds = [], curriculumIds = [], assignmentIds = [] }) {
  if (assignmentIds.length > 0) {
    const validatedIds = assignmentIds.map((id) => assertValidUuid(id, 'assignment'));
    const idList = validatedIds.map((id) => `'${id}'`).join(',');
    executeSql(`
      DELETE FROM student_assignment_progress WHERE assignment_id IN (${idList});
      DELETE FROM assignment_targets WHERE assignment_id IN (${idList});
      DELETE FROM assignment_questions WHERE assignment_id IN (${idList});
      DELETE FROM assignments WHERE assignment_id IN (${idList}) AND center_id = '${TARGET_CENTER_ID}' AND title LIKE 'UIACC%';
    `);
  }
  if (curriculumIds.length > 0) {
    const validatedIds = curriculumIds.map((id) => assertValidUuid(id, 'curriculum'));
    const idList = validatedIds.map((id) => `'${id}'`).join(',');
    executeSql(`
      DELETE FROM curriculum_classes WHERE curriculum_id IN (${idList});
      DELETE FROM curriculums WHERE curriculum_id IN (${idList}) AND center_id = '${TARGET_CENTER_ID}' AND title LIKE 'UIACC%';
    `);
  }
  if (classIds.length > 0) {
    const validatedIds = classIds.map((id) => assertValidUuid(id, 'class'));
    const idList = validatedIds.map((id) => `'${id}'`).join(',');
    executeSql(`
      DELETE FROM curriculum_classes WHERE class_id IN (${idList});
      DELETE FROM class_students WHERE class_id IN (${idList});
      DELETE FROM classes WHERE class_id IN (${idList}) AND center_id = '${TARGET_CENTER_ID}' AND (class_name LIKE 'UIACC%' OR class_name LIKE 'UIACC%#del#%');
    `);
  }
}

// Strict Two-Way Verification:
// 1. Every ID in manifest MUST exist in DB, belong to TARGET_CENTER_ID, and match allowed fixture patterns (rejects extra/foreign IDs).
// 2. Every fixture entity currently in DB MUST be present in manifest (rejects unowned DB records).
function verifyManifestOwnershipStrict(manifest) {
  if (!manifest || typeof manifest !== 'object' || !manifest.ownedEntities) {
    throw new Error('[FAIL-FAST OWNERSHIP VIOLATION] Invalid or missing manifest object');
  }

  const { classIds = [], curriculumIds = [], assignmentIds = [] } = manifest.ownedEntities;

  // Direction A (Manifest -> DB): Reject extraneous, non-existent, cross-center, or non-fixture IDs
  for (const cid of classIds) {
    assertValidUuid(cid, 'class');
    const rows = queryRows(`SELECT class_id, class_name, center_id FROM classes WHERE class_id = '${cid}';`);
    if (rows.length === 0) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims class ID '${cid}' but record does not exist in DB! Aborting.`);
    }
    const r = rows[0];
    if (r.center_id !== TARGET_CENTER_ID) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims class ID '${cid}' belonging to unauthorized center '${r.center_id}'! Aborting.`);
    }
    if (!isAllowedClassName(r.class_name)) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims class ID '${cid}' with non-fixture name '${r.class_name}'! Aborting.`);
    }
  }

  for (const cuid of curriculumIds) {
    assertValidUuid(cuid, 'curriculum');
    const rows = queryRows(`SELECT curriculum_id, title, center_id FROM curriculums WHERE curriculum_id = '${cuid}';`);
    if (rows.length === 0) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims curriculum ID '${cuid}' but record does not exist in DB! Aborting.`);
    }
    const r = rows[0];
    if (r.center_id !== TARGET_CENTER_ID) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims curriculum ID '${cuid}' belonging to unauthorized center '${r.center_id}'! Aborting.`);
    }
    if (!ALLOWED_CURRICULUM_TITLES.has(r.title)) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims curriculum ID '${cuid}' with non-fixture title '${r.title}'! Aborting.`);
    }
  }

  for (const aid of assignmentIds) {
    assertValidUuid(aid, 'assignment');
    const rows = queryRows(`SELECT assignment_id, title, center_id FROM assignments WHERE assignment_id = '${aid}';`);
    if (rows.length === 0) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims assignment ID '${aid}' but record does not exist in DB! Aborting.`);
    }
    const r = rows[0];
    if (r.center_id !== TARGET_CENTER_ID) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims assignment ID '${aid}' belonging to unauthorized center '${r.center_id}'! Aborting.`);
    }
    if (!ALLOWED_ASSIGNMENT_TITLES.has(r.title)) {
      throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Manifest claims assignment ID '${aid}' with non-fixture title '${r.title}'! Aborting.`);
    }
  }

  // Direction B (DB -> Manifest): Reject unowned fixture records in DB
  const dbClasses = queryRows(`SELECT class_id, class_name FROM classes WHERE center_id = '${TARGET_CENTER_ID}' AND (class_name IN ('UIACC-GRADE10-CLASS', 'UIACC-GRADE11-CLASS', 'UIACC-INACTIVE-CLASS', 'UIACC-EMPTY-CLASS') OR class_name LIKE 'UIACC-EMPTY-CLASS#del#%');`);
  const dbCurriculums = queryRows(`SELECT curriculum_id, title FROM curriculums WHERE center_id = '${TARGET_CENTER_ID}' AND title = 'UIACC-ENG-G10';`);
  const dbAssignments = queryRows(`SELECT assignment_id, title FROM assignments WHERE center_id = '${TARGET_CENTER_ID}' AND title = 'UIACC-ENG-HW-G10';`);

  const manifestClassSet = new Set(classIds);
  const manifestCurriculumSet = new Set(curriculumIds);
  const manifestAssignmentSet = new Set(assignmentIds);

  const unowned = [];
  for (const c of dbClasses) {
    if (!manifestClassSet.has(c.class_id)) {
      unowned.push({ type: 'class', id: c.class_id, name: c.class_name });
    }
  }
  for (const cu of dbCurriculums) {
    if (!manifestCurriculumSet.has(cu.curriculum_id)) {
      unowned.push({ type: 'curriculum', id: cu.curriculum_id, title: cu.title });
    }
  }
  for (const a of dbAssignments) {
    if (!manifestAssignmentSet.has(a.assignment_id)) {
      unowned.push({ type: 'assignment', id: a.assignment_id, title: a.title });
    }
  }

  if (unowned.length > 0) {
    throw new Error(`[FAIL-FAST OWNERSHIP VIOLATION] Database contains ${unowned.length} fixture record(s) not claimed in manifest ${manifest.runId}: ${JSON.stringify(unowned)}. Aborting run to protect unowned data!`);
  }
}

function cleanupRunFixtures() {
  const tracked = getTrackedIds();
  const count = tracked.classIds.length + tracked.curriculumIds.length + tracked.assignmentIds.length;
  if (count > 0) {
    console.log(`[FIXTURE] Verifying two-way ownership and cleaning ${count} run-scoped fixtures for session ${tracked.runId}...`);
    verifyManifestOwnershipStrict({
      runId: tracked.runId,
      ownedEntities: {
        classIds: tracked.classIds,
        curriculumIds: tracked.curriculumIds,
        assignmentIds: tracked.assignmentIds,
      },
    });
    deleteEntitiesByIds(tracked);
    console.log('[FIXTURE] Run-scoped cleanup complete.');
  }
  resetRunRegistry();
  if (fs.existsSync(MANIFEST_FILE)) {
    try { fs.unlinkSync(MANIFEST_FILE); } catch (e) {}
  }
}

// Preflight check: Strict two-way ownership verification before starting
function preflightCheckAndVerifyOwnership() {
  const assignRows = queryRows(`SELECT assignment_id, title FROM assignments WHERE center_id = '${TARGET_CENTER_ID}' AND title = 'UIACC-ENG-HW-G10'`);
  const curRows = queryRows(`SELECT curriculum_id, title FROM curriculums WHERE center_id = '${TARGET_CENTER_ID}' AND title = 'UIACC-ENG-G10'`);
  const classRows = queryRows(
    `SELECT class_id, class_name FROM classes WHERE center_id = '${TARGET_CENTER_ID}' AND (class_name IN ('UIACC-GRADE10-CLASS', 'UIACC-GRADE11-CLASS', 'UIACC-INACTIVE-CLASS', 'UIACC-EMPTY-CLASS') OR class_name LIKE 'UIACC-EMPTY-CLASS#del#%')`
  );

  const foundCount = assignRows.length + curRows.length + classRows.length;
  if (foundCount === 0) {
    if (fs.existsSync(MANIFEST_FILE)) {
      try { fs.unlinkSync(MANIFEST_FILE); } catch (e) {}
    }
    initializeRunManifest();
    return;
  }

  const manifest = readManifestFile();
  if (!manifest || !manifest.ownedEntities) {
    throw new Error(
      `[FAIL-FAST OWNERSHIP VIOLATION] Database contains ${foundCount} pre-existing fixture record(s) matching acceptance names, but NO valid manifest file exists on disk to prove ownership. Found: ` +
      JSON.stringify({ assignments: assignRows, curriculums: curRows, classes: classRows }) +
      '. Harness will NOT auto-delete unowned records. Please verify the environment manually.'
    );
  }

  // Strict two-way validation: manifest <-> DB
  verifyManifestOwnershipStrict(manifest);

  console.log(`[MANIFEST RECOVERY] Verified strict two-way ownership of ${foundCount} leftover fixtures from interrupted session ${manifest.runId}. Cleaning verified owned IDs...`);
  deleteEntitiesByIds(manifest.ownedEntities);
  if (fs.existsSync(MANIFEST_FILE)) {
    try { fs.unlinkSync(MANIFEST_FILE); } catch (e) {}
  }
  console.log('[MANIFEST RECOVERY] Interrupted session cleanup complete.');

  initializeRunManifest();
}


// ──────────────────────────────────────────────────────────────────────────
// Database Baseline Snapshot and Cryptographic Content Hash Verification
// ──────────────────────────────────────────────────────────────────────────

function takeDatabaseSnapshot() {
  const counts = {};
  const cRows = queryRows(`
    SELECT 'users' as tbl, count(*) as cnt FROM users
    UNION ALL SELECT 'centers', count(*) FROM centers
    UNION ALL SELECT 'classes', count(*) FROM classes WHERE class_name NOT LIKE 'UIACC%'
    UNION ALL SELECT 'curriculums', count(*) FROM curriculums WHERE title NOT LIKE 'UIACC%'
    UNION ALL SELECT 'assignments', count(*) FROM assignments WHERE title NOT LIKE 'UIACC%'
    UNION ALL SELECT 'questions', count(*) FROM questions;
  `);
  for (const r of cRows) counts[r.tbl] = Number(r.cnt);

  const data = {
    users: queryRows('SELECT user_id, username, role_name, status, center_id, is_deleted FROM users ORDER BY user_id;'),
    centers: queryRows('SELECT center_id, center_code, center_name, status, is_deleted FROM centers ORDER BY center_id;'),
    classes: queryRows("SELECT class_id, class_name, grade_level, status, is_deleted FROM classes WHERE class_name NOT LIKE 'UIACC%' ORDER BY class_id;"),
    curriculums: queryRows("SELECT curriculum_id, title, grade_level, review_status, is_deleted FROM curriculums WHERE title NOT LIKE 'UIACC%' ORDER BY curriculum_id;"),
    assignments: queryRows("SELECT assignment_id, title, status, is_deleted FROM assignments WHERE title NOT LIKE 'UIACC%' ORDER BY assignment_id;"),
    questions: queryRows("SELECT question_id, grade_level, status, is_deleted FROM questions ORDER BY question_id;"),
  };

  const serialized = JSON.stringify(data);
  const contentHash = crypto.createHash('sha256').update(serialized).digest('hex');

  return { counts, contentHash, data };
}

function assertDatabaseIntegrityUnchanged(baseline) {
  const current = takeDatabaseSnapshot();
  const mismatches = [];

  for (const tbl of Object.keys(baseline.counts)) {
    if (baseline.counts[tbl] !== current.counts[tbl]) {
      mismatches.push(`Table count mismatch in ${tbl}: baseline=${baseline.counts[tbl]}, current=${current.counts[tbl]}`);
    }
  }

  if (baseline.contentHash !== current.contentHash) {
    for (const tbl of Object.keys(baseline.data)) {
      const baseJson = JSON.stringify(baseline.data[tbl]);
      const currJson = JSON.stringify(current.data[tbl]);
      if (baseJson !== currJson) {
        mismatches.push(`Content change detected in non-fixture table '${tbl}'!`);
      }
    }
  }

  if (mismatches.length > 0) {
    throw new Error(`[ASSERT FAIL] Non-fixture database integrity violation!\n${mismatches.join('\n')}`);
  }

  console.log(`[ASSERT PASS] Non-fixture database integrity verified:`);
  console.log(`  - Row counts matched across all 6 tables: ${JSON.stringify(current.counts)}`);
  console.log(`  - SHA-256 cryptographic content state hash identical: ${current.contentHash.substring(0, 16)}...`);
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
  executeSql,
  queryRows,
  resetRunRegistry,
  registerCreatedClassId,
  registerCreatedCurriculumId,
  registerCreatedAssignmentId,
  getTrackedIds,
  cleanupRunFixtures,
  preflightCheckAndVerifyOwnership,
  removeClientConfigFile,
  takeDatabaseSnapshot,
  assertDatabaseIntegrityUnchanged,
  assertClassStudent,
  assertClassActive,
  assertClassDeleted,
  assertCurriculumCreated,
  assertCurriculumClassLink,
  assertAssignmentPublished,
};
