// Run only against the disposable acceptance stack. Prepares fixtures for browser regression QA.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const { execSql, createAssignmentFixture } = require('./fixture_manager.cjs');
const base = 'http://127.0.0.1:3002/api/v1';
const eng = '40000000-0000-0000-0000-000000000004';
const engClass = '61000000-0000-0000-0000-000000000010';
const math = '30000000-0000-0000-0000-000000000003';
const results = [];
async function req(token, path, method = 'GET', body) {
  const response = await fetch(base + path, { method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(20000) });
  const text = await response.text(); return { status: response.status, data: text ? JSON.parse(text) : null };
}
async function login(username) {
  const r = await req(null, '/auth/login', 'POST', { centerCode: 'EDUTWIN_A', username, password: testPassword });
  assert.equal(r.status, 200); return r.data.data.accessToken;
}
async function check(name, fn) { await fn(); results.push({ name, status: 'PASS' }); console.log('PASS ' + name); }
async function main() {
  manifest(); const manager = await login('manager'), student = await login('student01');
  await check('Renaming subject through management API preserves its stable ENG code', async () => {
    const r = await req(manager, `/subjects/${eng}`); assert.equal(r.status, 200);
    const s = r.data.data;
    const changed = await req(manager, `/subjects/${eng}`, 'PATCH', { subjectCode: s.subjectCode, subjectName: 'Academic Communication QA', description: s.description, isActive: true, rowVersion: s.rowVersion });
    assert.equal(changed.status, 200, JSON.stringify(changed.data)); assert.equal(changed.data.data.subjectCode, 'ENG');
  });
  const [question, topic] = execSql(`SELECT question_id,primary_topic_node_id FROM questions WHERE subject_id='${eng}' AND center_id='10000000-0000-0000-0000-000000000001' AND is_deleted=0 ORDER BY (question_type='ShortAnswer') DESC,question_id LIMIT 1;`).trim().split('\t');
  const teacher = execSql(`SELECT teacher_id FROM classes WHERE class_id='${engClass}';`).trim();
  const englishAssignment = createAssignmentFixture({ tag: 'FIX_ENG', title: 'Mã ENG sau đổi tên môn', classId: engClass, teacherId: teacher, questionConfigs: [{ questionId: Number(question), points: 1 }] });
  await check('Assignment detail returns renamed display name and ENG code', async () => {
    const r = await req(student, `/students/me/assignments/${englishAssignment.assignmentId}`); assert.equal(r.status, 200);
    assert.equal(r.data.data.subjectCode, 'ENG'); assert.equal(r.data.data.subjectName, 'Academic Communication QA');
  });
  let session;
  await check('Adaptive proposal and session carry ENG independent of renamed display name', async () => {
    const p = await req(student, `/adaptive-practice/proposal?subjectId=${eng}&classId=${engClass}&mode=Weakness`); assert.equal(p.status, 200);
    assert.equal(p.data.subjectCode, 'ENG');
    const r = await req(student, '/adaptive-practice/sessions', 'POST', { subjectId: eng, classId: engClass, topicNodeId: String(p.data.topicNodeId ?? topic), practiceMode: 'Weakness', timeBudgetMinutes: 10 });
    assert.equal(r.status, 201, JSON.stringify(r.data)); session = r.data; assert.equal(session.subjectCode, 'ENG');
  });
  await check('No-evidence dashboard explicitly reports that prediction is unavailable', async () => {
    const r = await req(student, `/students/me/dashboard?subjectId=${eng}&classId=${engClass}`); assert.equal(r.status, 200, JSON.stringify(r.data));
    const dto = r.data.data ?? r.data; assert.equal(dto.goal.hasPrediction, false);
  });
  await check('Known seeded quadratic rule has correct signs', async () => {
    const text = execSql(`SELECT description FROM knowledge_nodes WHERE subject_id='${math}' AND node_code='MATH10-QUADRATIC' LIMIT 1;`);
    assert.match(text, /ngoài khoảng hai nghiệm cùng dấu với a/); assert.match(text, /trong khoảng hai nghiệm trái dấu với a/);
  });
  await check('Data correction repairs old seed wording while preserving a teacher-authored replacement', async () => {
    execSql(`UPDATE knowledge_nodes SET description=REPLACE(description,'khi Δ > 0, ngoài khoảng hai nghiệm cùng dấu với a, trong khoảng hai nghiệm trái dấu với a','ngoài khoảng hai nghiệm trái dấu với a, trong khoảng hai nghiệm cùng dấu với a') WHERE center_id='10000000-0000-0000-0000-000000000001' AND node_code='MATH10-QUADRATIC';
      UPDATE knowledge_nodes SET description='QA custom teacher lesson' WHERE center_id='20000000-0000-0000-0000-000000000002' AND node_code='MATH10-QUADRATIC';`);
    const source = fs.readFileSync('src/EduTwin.DAL/Persistence/Migrations/20261120160000_CorrectSeedQuadraticSignRule.cs', 'utf8');
    const sql = source.match(/migrationBuilder\.Sql\("""([\s\S]*?)"""\);/)[1]; execSql(sql);
    assert.match(execSql("SELECT description FROM knowledge_nodes WHERE center_id='10000000-0000-0000-0000-000000000001' AND node_code='MATH10-QUADRATIC';"), /ngoài khoảng hai nghiệm cùng dấu với a/);
    assert.equal(execSql("SELECT description FROM knowledge_nodes WHERE center_id='20000000-0000-0000-0000-000000000002' AND node_code='MATH10-QUADRATIC';").trim(), 'QA custom teacher lesson');
  });
  const emptyUsername = 'qa.fixes.empty.' + Date.now();
  await check('Prepare an isolated student with no class for empty-state browser checks', async () => {
    const r = await req(manager, '/students', 'POST', { username: emptyUsername, temporaryPassword: testPassword, fullName: 'Học sinh QA chưa có lớp', gradeLevel: 10, classIds: [] });
    assert.equal(r.status, 201);
  });
  const withheld = createAssignmentFixture({ tag: 'FIX_WITHHELD', title: 'Kết quả chưa công bố', questionConfigs: [{ questionId: 10000, points: 1 }] });
  execSql(`UPDATE assignments SET assignment_kind='Midterm',exam_at=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 1 HOUR) WHERE assignment_id='${withheld.assignmentId}';`);
  await check('Prepare a submitted exam with withheld results', async () => {
    assert.equal((await req(student, `/students/me/assignments/${withheld.assignmentId}/start`, 'POST', {})).status, 200);
    assert.equal((await req(student, `/students/me/assignments/${withheld.assignmentId}/submit`, 'POST', { answers: [{ questionId: '10000', finalAnswer: '10000', reasoningText: 'QA: thay x = 3 vào y = 2x + 1 được 7.', confidence: 80, timeSpentSeconds: 12, answerChanges: 0 }] })).status, 200);
    const r = await req(student, `/students/me/assignments/${withheld.assignmentId}`); assert.equal(r.data.data.resultsPublishedAt, null);
    assert.ok(r.data.data.questions.every(q => q.effectiveIsCorrect == null));
  });
  execSql(`UPDATE assignments SET status='Closed',due_at=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 1 MINUTE) WHERE assignment_id='${withheld.assignmentId}';`);
  await check('Closed but unpublished exam does not leak answers or solutions through bookmarks', async () => {
    assert.equal((await req(student, '/students/me/question-bookmarks/10000', 'PUT')).status, 204);
    const r = await req(student, '/students/me/question-bookmarks'); const mark = r.data.find(q => q.questionId === '10000');
    assert.ok(mark); assert.equal(mark.correctAnswer, null); assert.equal(mark.solution, null);
  });
  const dir = 'docs/verification/evidence/student-fixes-2026-11-20'; fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(dir + '/api-results.json', JSON.stringify({ results, englishAssignment, englishQuestionId: question, practiceSessionId: session.sessionId, emptyUsername, withheld }, null, 2));
  console.log(JSON.stringify({ passed: results.length, englishAssignment: englishAssignment.assignmentId, englishQuestionId: question, practiceSessionId: session.sessionId }));
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
