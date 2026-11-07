// Real HTTP/MySQL acceptance against acceptance_stack.cjs only.
// This suite is not browser E2E and never starts, stops or cleans up a stack.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { root, sqlString } = require('../ops/mysql_admin.cjs');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const { execSql, CENTER_A_ID, STUDENT01_A_ID } = require('./fixture_manager.cjs');

const base = 'http://127.0.0.1:3002/api/v1';
const prefix = '/adaptive-practice';
const mathSubjectId = '30000000-0000-0000-0000-000000000003';
const args = process.argv.slice(2);
const argIndex = args.indexOf('--results-dir');
const requestedResults = argIndex >= 0 ? args[argIndex + 1] : args[0];
if (argIndex >= 0 && !requestedResults) throw new Error('--results-dir requires a directory.');
const resultsDirectory = path.resolve(requestedResults || path.join(root,
  'docs/verification/evidence/adaptive_practice_acceptance', new Date().toISOString().replaceAll(':', '-')));

const definitions = [
  ['APAPI01', 'Unauthenticated adaptive access is denied'],
  ['APAPI02', 'Student login in isolated schema'],
  ['APAPI03', 'Second student login in isolated schema'],
  ['APAPI04', 'Teacher login in isolated schema'],
  ['APAPI05', 'Other-center student login in isolated schema'],
  ['APAPI06', 'Weakness proposal explains target without issuing questions'],
  ['APAPI07', 'Learning Path mode returns a meaningful proposal'],
  ['APAPI08', 'Custom topic mode preserves selected topic'],
  ['APAPI09', 'Adaptive persistence tables exist in isolated schema'],
  ['APAPI10', 'Start a ten-minute session'],
  ['APAPI11', 'Ten-minute session has four/eight question limits'],
  ['APAPI12', 'Repeated start returns the active session'],
  ['APAPI13', 'First question belongs to the selected session topic'],
  ['APAPI14', 'Repeated next request preserves unanswered question identity'],
  ['APAPI15', 'Progressive hint is persisted on issued item'],
  ['APAPI16', 'Second student cannot read another session'],
  ['APAPI17', 'Second student cannot request another students hint'],
  ['APAPI18', 'Pause persists Paused state'],
  ['APAPI19', 'Paused session does not issue a new question'],
  ['APAPI20', 'Resume preserves the current question'],
  ['APAPI21', 'Submit stores one graded answer and updates session progress'],
  ['APAPI22', 'Same-submission replay does not duplicate attempt or evidence'],
  ['APAPI23', 'Session progress remains one answer after replay'],
  ['APAPI24', 'Voluntary close produces an incomplete-goal summary'],
  ['APAPI25', 'Summary can be retrieved after close'],
  ['APAPI26', 'Second student cannot read another students summary'],
  ['APAPI27', 'Closed session appears in student history'],
  ['APAPI28', 'Closed session cannot issue another question'],
  ['APAPI29', 'Closed session cannot accept a new answer'],
  ['APAPI30', 'Follow-up practice creates a distinct session and preserves history'],
  ['APAPI31', 'Fifteen-minute session has five/twelve limits'],
  ['APAPI32', 'Twenty-minute session has six/fifteen limits'],
  ['APAPI33', 'Teacher cannot see sessions through an unrelated class scope'],
  ['APAPI34', 'Student cannot use teacher monitoring endpoint'],
  ['APAPI35', 'Other-center student cannot read session or summary'],
  ['APAPI36', 'Teacher cannot create a student practice session'],
];
const results = definitions.map(([id, name]) => ({ id, name, kind: 'real-http-mysql', status: 'NOT_RUN' }));
const ctx = {};
let state;
let setupError;

function safeError(error) {
  return String(error?.message || error).replace(/Bearer\s+[A-Za-z0-9._-]+/gi, 'Bearer [redacted]').slice(0, 1600);
}

async function request(token, route, method = 'GET', body) {
  const response = await fetch(base + route, {
    method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(15000),
  });
  const text = await response.text();
  let responseBody = null;
  if (text) {
    try { responseBody = JSON.parse(text); }
    catch { throw new Error(`${method} ${route}: HTTP ${response.status}, response is not JSON.`); }
  }
  return { status: response.status, body: responseBody };
}

function expectStatus(response, expected, action) {
  assert.equal(response.status, expected, `${action}: expected HTTP ${expected}, got ${response.status}` +
    (response.body?.errorCode ? ` (${response.body.errorCode})` : ''));
  return response.body;
}

function assertDenied(response, action) {
  assert.ok([403, 404].includes(response.status), `${action}: expected 403/404, got ${response.status}`);
}

async function check(id, dependencies, action) {
  const result = results.find(item => item.id === id);
  const missing = dependencies.filter(key => !ctx[key]);
  if (missing.length) {
    Object.assign(result, { status: 'BLOCKED', reason: `Missing prerequisites: ${missing.join(', ')}` });
    console.log(`${id} BLOCKED ${result.reason}`);
    return;
  }
  try {
    const evidence = await action();
    Object.assign(result, { status: 'PASS', ...(evidence ? { evidence } : {}) });
    console.log(`${id} PASS ${result.name}`);
  } catch (error) {
    Object.assign(result, { status: 'FAIL', error: safeError(error) });
    console.error(`${id} FAIL ${result.error}`);
  }
}

async function login(username, centerCode = 'EDUTWIN_A') {
  const response = await request(null, '/auth/login', 'POST', { username, centerCode, password: testPassword });
  const body = expectStatus(response, 200, `Login ${centerCode}/${username}`);
  assert.ok(body?.data?.accessToken, 'Login must return data.accessToken');
  return body.data.accessToken;
}

function createPayload(minutes) {
  return { subjectId: mathSubjectId, topicNodeId: String(ctx.proposal.topicNodeId),
    practiceMode: 'CustomTopic', timeBudgetMinutes: minutes };
}

const sessionRoute = () => `${prefix}/sessions/${ctx.session.sessionId}`;

function questionCount() {
  return Number(execSql(`SELECT COUNT(*) FROM adaptive_practice_session_items WHERE center_id=${sqlString(CENTER_A_ID)};`).trim());
}

function attemptCount() {
  return Number(execSql(`SELECT COUNT(*) FROM attempts WHERE center_id=${sqlString(CENTER_A_ID)}
    AND student_id=${sqlString(STUDENT01_A_ID)} AND client_submission_id=${sqlString(ctx.submissionId)};`).trim());
}

async function run() {
  // The manifest validates the generated schema/user names. No primary schema may be selected.
  state = manifest();
  assert.equal(state.database, `edutwin_e2e_${state.runId}`);
  assert.match(state.database, /^edutwin_e2e_[a-f0-9]{32}$/);
  await check('APAPI01', [], async () => expectStatus(await request(null, `${prefix}/history`), 401, 'Anonymous history'));
  for (const [id, key, username, center] of [
    ['APAPI02', 'student', 'student01', 'EDUTWIN_A'],
    ['APAPI03', 'otherStudent', 'student02', 'EDUTWIN_A'],
    ['APAPI04', 'teacher', 'teacher.math', 'EDUTWIN_A'],
    ['APAPI05', 'otherCenter', 'student01', 'EDUTWIN_B'],
  ]) await check(id, [], async () => { ctx[key] = await login(username, center); });

  await check('APAPI09', [], async () => {
    const tables = execSql("SELECT table_name FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'adaptive_practice_%';")
      .trim().split(/\r?\n/);
    for (const table of ['adaptive_practice_sessions', 'adaptive_practice_session_items', 'adaptive_practice_session_summaries'])
      assert.ok(tables.includes(table), `Missing migration table ${table}`);
    ctx.tables = true;
  });
  await check('APAPI06', ['student'], async () => {
    const before = ctx.tables ? questionCount() : null;
    const proposal = expectStatus(await request(ctx.student, `${prefix}/proposal?subjectId=${mathSubjectId}&mode=Weakness`), 200, 'Weakness proposal');
    assert.equal(proposal.subjectId, mathSubjectId);
    assert.ok(proposal.topicNodeId && proposal.selectionReason && proposal.focusSkillDescription);
    assert.ok(proposal.targetMastery >= 0 && proposal.targetMastery <= 100);
    assert.ok(Array.isArray(proposal.availableTopics));
    if (before !== null) assert.equal(questionCount(), before, 'Proposal must not issue a question');
    ctx.proposal = proposal;
  });
  await check('APAPI07', ['student'], async () => {
    const proposal = expectStatus(await request(ctx.student, `${prefix}/proposal?subjectId=${mathSubjectId}&mode=LearningPath`), 200, 'Learning Path proposal');
    assert.equal(proposal.defaultMode, 'LearningPath');
    assert.ok(proposal.topicNodeId && proposal.selectionReason);
  });
  await check('APAPI08', ['student', 'proposal'], async () => {
    const proposal = expectStatus(await request(ctx.student, `${prefix}/proposal?subjectId=${mathSubjectId}&customTopicId=${ctx.proposal.topicNodeId}&mode=CustomTopic`), 200, 'Custom topic proposal');
    assert.equal(String(proposal.topicNodeId), String(ctx.proposal.topicNodeId));
    assert.equal(proposal.defaultMode, 'CustomTopic');
  });
  await check('APAPI10', ['student', 'proposal', 'tables'], async () => {
    const session = expectStatus(await request(ctx.student, `${prefix}/sessions`, 'POST', createPayload(10)), 201, 'Create session');
    assert.ok(session.sessionId);
    ctx.session = session;
    assert.equal(session.status, 'InProgress');
    assert.equal(String(session.topicNodeId), String(ctx.proposal.topicNodeId));
    return { sessionId: session.sessionId };
  });
  await check('APAPI11', ['session'], async () => {
    assert.equal(ctx.session.timeBudgetMinutes, 10);
    assert.equal(ctx.session.minimumQuestions, 4);
    assert.equal(ctx.session.maximumQuestions, 8);
  });
  await check('APAPI12', ['student', 'session'], async () => {
    const replay = expectStatus(await request(ctx.student, `${prefix}/sessions`, 'POST', createPayload(10)), 201, 'Repeated start');
    assert.equal(replay.sessionId, ctx.session.sessionId);
  });
  await check('APAPI13', ['student', 'session'], async () => {
    const question = expectStatus(await request(ctx.student, `${sessionRoute()}/next-question`), 200, 'Next question');
    assert.ok(question?.sessionItemId && question.questionId, 'Fixture bank must contain an eligible question');
    ctx.question = question;
    assert.equal(String(question.topicNodeId), String(ctx.session.topicNodeId));
    assert.equal(question.sequenceNumber, 1);
    assert.ok(question.selectionReasonCode && question.selectionReasonText);
    assert.equal(question.correctAnswer, undefined, 'Question response must not expose answer');
    return { questionId: String(question.questionId), sessionItemId: String(question.sessionItemId) };
  });
  await check('APAPI14', ['student', 'question'], async () => {
    const repeated = expectStatus(await request(ctx.student, `${sessionRoute()}/next-question`), 200, 'Repeated next');
    assert.equal(String(repeated?.sessionItemId), String(ctx.question.sessionItemId));
  });
  await check('APAPI15', ['student', 'question'], async () => {
    const hint = expectStatus(await request(ctx.student, `${sessionRoute()}/hint?sessionItemId=${ctx.question.sessionItemId}&level=2`), 200, 'Level 2 hint');
    assert.equal(hint.hintLevel, 2);
    assert.ok(hint.hintContent && hint.supportLevelDescription);
    const issued = expectStatus(await request(ctx.student, `${sessionRoute()}/next-question`), 200, 'Read support');
    assert.ok(issued.hintsUsedCount >= 2);
  });
  await check('APAPI16', ['otherStudent', 'session'], async () => assertDenied(await request(ctx.otherStudent, sessionRoute()), 'Foreign student session'));
  await check('APAPI17', ['otherStudent', 'question'], async () => assertDenied(await request(ctx.otherStudent,
    `${sessionRoute()}/hint?sessionItemId=${ctx.question.sessionItemId}&level=1`), 'Foreign student hint'));
  await check('APAPI18', ['student', 'session'], async () => {
    const paused = expectStatus(await request(ctx.student, `${sessionRoute()}/pause`, 'POST'), 200, 'Pause');
    assert.equal(paused.status, 'Paused');
    ctx.paused = true;
    assert.equal(expectStatus(await request(ctx.student, sessionRoute()), 200, 'Read paused session').status, 'Paused');
  });
  await check('APAPI19', ['student', 'paused'], async () => {
    const response = await request(ctx.student, `${sessionRoute()}/next-question`);
    assert.ok([200, 204, 409].includes(response.status));
    if (response.status === 200) assert.equal(response.body, null);
  });
  await check('APAPI20', ['student', 'question', 'paused'], async () => {
    const resumed = expectStatus(await request(ctx.student, `${sessionRoute()}/resume`, 'POST'), 200, 'Resume');
    assert.equal(resumed.status, 'InProgress');
    assert.equal(String(expectStatus(await request(ctx.student, `${sessionRoute()}/next-question`), 200, 'Question after resume').sessionItemId), String(ctx.question.sessionItemId));
    ctx.resumed = true;
  });
  await check('APAPI21', ['student', 'question', 'resumed', 'tables'], async () => {
    assert.match(String(ctx.question.questionId), /^\d+$/);
    const rows = execSql(`SELECT JSON_OBJECT('answer',correct_answer,'type',question_type,'mode',answer_evaluation_mode)
      FROM questions WHERE center_id=${sqlString(CENTER_A_ID)} AND question_id=${String(ctx.question.questionId)};`).trim();
    const bank = JSON.parse(rows);
    assert.notEqual(bank.mode, 'Manual', 'Use a deterministic bank fixture for synchronous submit assertions');
    ctx.submissionId = crypto.randomUUID();
    ctx.submission = { clientSubmissionId: ctx.submissionId, questionId: String(ctx.question.questionId),
      sessionItemId: String(ctx.question.sessionItemId), finalAnswer: bank.answer,
      reasoningText: 'Isolated deterministic fixture: use the provided rule and check the result.', timeSpentSeconds: 35 };
    ctx.feedback = expectStatus(await request(ctx.student, `${sessionRoute()}/submit`, 'POST', ctx.submission), 200, 'Submit');
    assert.equal(ctx.feedback.isCorrect, true);
    assert.equal(attemptCount(), 1);
    assert.equal(expectStatus(await request(ctx.student, sessionRoute()), 200, 'Progress').questionCount, 1);
    ctx.submitted = true;
  });
  await check('APAPI22', ['student', 'submitted'], async () => {
    const beforeEvidence = execSql(`SELECT COALESCE(SUM(evidence_count),0) FROM knowledge_twins
      WHERE center_id=${sqlString(CENTER_A_ID)} AND student_id=${sqlString(STUDENT01_A_ID)};`).trim();
    const replay = expectStatus(await request(ctx.student, `${sessionRoute()}/submit`, 'POST', ctx.submission), 200, 'Replay submission');
    assert.equal(replay.isCorrect, ctx.feedback.isCorrect);
    assert.equal(attemptCount(), 1);
    assert.equal(execSql(`SELECT COALESCE(SUM(evidence_count),0) FROM knowledge_twins
      WHERE center_id=${sqlString(CENTER_A_ID)} AND student_id=${sqlString(STUDENT01_A_ID)};`).trim(), beforeEvidence);
  });
  await check('APAPI23', ['student', 'submitted'], async () => assert.equal(
    expectStatus(await request(ctx.student, sessionRoute()), 200, 'Progress after replay').questionCount, 1));
  await check('APAPI24', ['student', 'session'], async () => {
    const summary = expectStatus(await request(ctx.student, `${sessionRoute()}/close?closeReason=StudentStopped`, 'POST'), 200, 'Close');
    ctx.summary = summary;
    ctx.closed = true;
    assert.equal(summary.sessionId, ctx.session.sessionId);
    assert.notEqual(summary.goalAchievementStatus, 'Achieved');
    assert.ok(summary.summaryConclusion && summary.recommendedNextAction);
    assert.equal(expectStatus(await request(ctx.student, sessionRoute()), 200, 'Closed session').closeReason, 'StudentStopped');
  });
  await check('APAPI25', ['student', 'closed'], async () => {
    const summary = expectStatus(await request(ctx.student, `${sessionRoute()}/summary`), 200, 'Summary');
    assert.equal(summary.sessionId, ctx.session.sessionId);
    assert.equal(summary.totalQuestions, ctx.submitted ? 1 : 0);
    assert.equal(summary.totalQuestions, summary.correctCount + summary.incorrectCount + summary.partialCount + summary.pendingCount);
  });
  await check('APAPI26', ['otherStudent', 'closed'], async () => assertDenied(await request(ctx.otherStudent,
    `${sessionRoute()}/summary`), 'Foreign student summary'));
  await check('APAPI27', ['student', 'closed'], async () => {
    const history = expectStatus(await request(ctx.student, `${prefix}/history?subjectId=${mathSubjectId}`), 200, 'History');
    assert.ok(history.some(item => item.sessionId === ctx.session.sessionId));
  });
  await check('APAPI28', ['student', 'closed'], async () => {
    const response = await request(ctx.student, `${sessionRoute()}/next-question`);
    assert.ok([200, 204, 409].includes(response.status));
    if (response.status === 200) assert.equal(response.body, null);
  });
  await check('APAPI29', ['student', 'closed', 'submitted'], async () => {
    const response = await request(ctx.student, `${sessionRoute()}/submit`, 'POST', {
      ...ctx.submission, clientSubmissionId: crypto.randomUUID() });
    assert.ok([400, 409].includes(response.status), `Closed submit must be rejected, got ${response.status}`);
  });
  await check('APAPI30', ['student', 'closed', 'proposal'], async () => {
    const next = expectStatus(await request(ctx.student, `${prefix}/sessions`, 'POST', createPayload(10)), 201, 'Follow-up session');
    assert.notEqual(next.sessionId, ctx.session.sessionId);
    const previous = expectStatus(await request(ctx.student, sessionRoute()), 200, 'Previous session');
    assert.equal(previous.status, 'Closed');
    ctx.followUp = next;
    // Predecessor linkage has no current DTO field: browser/API linkage remains a known gap.
  });
  for (const [id, minutes, minimum, maximum] of [['APAPI31', 15, 5, 12], ['APAPI32', 20, 6, 15]]) {
    await check(id, ['student', 'proposal', 'tables'], async () => {
      const activeResponse = await request(ctx.student, `${prefix}/active-session?subjectId=${mathSubjectId}`);
      const active = expectStatus(activeResponse, 200, 'Active session before time-limit fixture');
      if (active?.sessionId) expectStatus(await request(ctx.student, `${prefix}/sessions/${active.sessionId}/close?closeReason=StudentStopped`, 'POST'), 200, 'Close owned fixture session');
      const session = expectStatus(await request(ctx.student, `${prefix}/sessions`, 'POST', createPayload(minutes)), 201, `${minutes}-minute session`);
      assert.equal(session.timeBudgetMinutes, minutes);
      assert.equal(session.minimumQuestions, minimum);
      assert.equal(session.maximumQuestions, maximum);
      expectStatus(await request(ctx.student, `${prefix}/sessions/${session.sessionId}/close?closeReason=StudentStopped`, 'POST'), 200, 'Close limit fixture');
    });
  }
  await check('APAPI33', ['teacher', 'closed'], async () => {
    const response = await request(ctx.teacher, `${prefix}/teacher/sessions?classId=${crypto.randomUUID()}&subjectId=${mathSubjectId}`);
    if ([403, 404].includes(response.status)) return;
    const sessions = expectStatus(response, 200, 'Unrelated teacher class scope');
    assert.deepEqual(sessions, [], 'Unrelated class cannot return students practice sessions');
  });
  await check('APAPI34', ['student'], async () => expectStatus(await request(ctx.student,
    `${prefix}/teacher/sessions`), 403, 'Student teacher endpoint'));
  await check('APAPI35', ['otherCenter', 'closed'], async () => {
    assertDenied(await request(ctx.otherCenter, sessionRoute()), 'Other-center session');
    assertDenied(await request(ctx.otherCenter, `${sessionRoute()}/summary`), 'Other-center summary');
  });
  await check('APAPI36', ['teacher', 'proposal'], async () => expectStatus(await request(ctx.teacher,
    `${prefix}/sessions`, 'POST', createPayload(10)), 403, 'Teacher creating student session'));
}

function writeReport() {
  for (const result of results) if (result.status === 'NOT_RUN') {
    result.status = 'BLOCKED';
    result.reason = setupError || 'Suite did not reach this case.';
  }
  let sourceHead = 'unavailable';
  try { sourceHead = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(); } catch {}
  const totals = Object.fromEntries(['PASS', 'FAIL', 'BLOCKED'].map(status => [status.toLowerCase(), results.filter(item => item.status === status).length]));
  const report = { generatedAt: new Date().toISOString(), sourceHead, runId: state?.runId,
    isolatedSchema: state?.database, baseUrl: base, testMode: 'real HTTP/MySQL acceptance; NOT browser E2E',
    ownership: 'Requires acceptance_stack manifest; all SQL uses fixture_manager.execSql and its generated isolated schema.',
    assumptions: ['Existing disposable seed bank has an eligible deterministic question.',
      'Adaptive endpoints return raw DTOs; login alone uses data.accessToken.',
      'No live AI credentials, quality or async callback assertions.',
      'Follow-up test checks distinct session/history; predecessor linkage is absent from the current contract.',
      'Student session activity in the disposable schema is intentionally retained for browser inspection until stack cleanup.'],
    ...(setupError ? { setupError } : {}), totals, results };
  fs.mkdirSync(resultsDirectory, { recursive: true });
  fs.writeFileSync(path.join(resultsDirectory, 'adaptive_api_results.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({ ...totals, sourceHead, resultsDirectory, testMode: report.testMode }));
  if (totals.fail || totals.blocked) process.exitCode = 1;
  return report;
}

if (require.main === module) {
  if (args.includes('--list')) {
    console.log(JSON.stringify({ kind: 'real-http-mysql', browserE2E: false,
      cases: definitions.map(([id, name]) => ({ id, name })) }, null, 2));
  } else {
    run().catch(error => {
      setupError = safeError(error);
      console.error(setupError);
      process.exitCode = 1;
    }).finally(writeReport);
  }
}

module.exports = { run, writeReport };
