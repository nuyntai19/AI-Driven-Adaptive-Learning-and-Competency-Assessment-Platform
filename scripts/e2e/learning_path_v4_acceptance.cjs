// Real HTTP/MySQL regression for the V4 path. Requires the owned disposable acceptance stack.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const { execSql, CENTER_A_ID } = require('./fixture_manager.cjs');
const { sqlString } = require('../ops/mysql_admin.cjs');
const base = 'http://127.0.0.1:3002/api/v1';
const results = [];
async function request(token, route, method = 'GET', body) {
  const res = await fetch(base + route, { method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(30000) });
  const raw = await res.text(); let data; try { data = raw ? JSON.parse(raw) : null; } catch { throw new Error(`${route}: HTTP ${res.status}, non-JSON response`); }
  return { status: res.status, data };
}
async function ok(token, route, method = 'GET', body) {
  const r = await request(token, route, method, body);
  assert.equal(r.status, 200, `${method} ${route}: ${r.status}, ${r.data?.detail ?? r.data?.title ?? ''}`); return r.data;
}
async function login(username, centerCode = 'EDUTWIN_A') {
  const r = await ok(null, '/auth/login', 'POST', { username, centerCode, password: testPassword }); return r.data.accessToken;
}
async function check(name, action) {
  try { await action(); results.push({ name, status: 'PASS', kind: 'http-mysql' }); console.log(`PASS ${name}`); }
  catch (e) { results.push({ name, status: 'FAIL', error: e.message }); console.error(`FAIL ${name}: ${e.message}`); }
}
async function main() {
  manifest(); // Fail closed: never run this fixture on the primary database.
  const student = await login('student02'); const other = await login('student01'); const anotherCenter = await login('student01', 'EDUTWIN_B');
  const subjects = (await ok(student, '/subjects?isActive=true')).data;
  const english = subjects.find(s => /Anh|English/i.test(s.subjectName)); assert.ok(english);
  const subject = english.subjectId;
  const context = (await ok(student, `/students/me/academic-context?subjectId=${subject}`)).data;
  const classId = context.selectedClassId || context.classes.find(c => !c.isHistorical)?.classId;
  const scope = `subjectId=${subject}${classId ? `&classId=${classId}` : ''}`;
  const browserContext = (await ok(other, `/students/me/academic-context?subjectId=${subject}`)).data;
  const browserClassId = browserContext.selectedClassId || browserContext.classes.find(c => !c.isHistorical)?.classId;
  fs.writeFileSync(path.join(__dirname, '../../scratch/v4-browser-scope.json'), JSON.stringify({ subjectId: subject, classId: browserClassId }));
  let map, proposal, accepted, studyRun;
  await check('English map uses real topics and never pilot algebra', async () => {
    map = await ok(student, `/students/me/learning-paths/capability-map?${scope}`);
    assert.ok(map.skills.length >= 2);
    assert.ok(map.skills.some(s => /quan hệ/i.test(s.skillName)));
    assert.ok(map.skills.every(s => !/MATH-EQ-|phương trình|dấu ngoặc/i.test(s.skillId + s.skillName)));
  });
  await check('Goal proposal covers selected skills, not repeated tense weeks', async () => {
    // The real seed has too few auto-gradable fresh items for some skills. Add explicit synthetic
    // questions to this owned disposable schema only; never lower the five-question policy.
    const skill = map.skills.find(s => s.availability !== 'PrerequisiteBlocked') ?? map.skills[0];
    assert.match(skill.skillId, /^\d+$/);
    let sql = 'CREATE TEMPORARY TABLE v4_owned_question_fixture LIKE questions;';
    for (let i = 0; i < 6; i++) {
      const id = crypto.randomBytes(5).readUIntBE(0, 5);
      sql += `DELETE FROM v4_owned_question_fixture;
        INSERT INTO v4_owned_question_fixture SELECT * FROM questions WHERE center_id=${sqlString(CENTER_A_ID)} AND subject_id=${sqlString(subject)} LIMIT 1;
        UPDATE v4_owned_question_fixture SET question_id=${id},primary_topic_node_id=${skill.skillId},question_type='ShortAnswer',
          answer_evaluation_mode='TextExact',question_text=${sqlString(`Isolated V4 fixture question ${i + 1}`)},correct_answer='fixture',
          solution='Isolated fixture explanation',has_image=0,status='Active',is_deleted=0,max_score=10,reasoning_required=1;
        INSERT INTO questions SELECT * FROM v4_owned_question_fixture;`;
    }
    execSql(sql + 'DROP TEMPORARY TABLE v4_owned_question_fixture;');
    const goal = await ok(student, '/students/me/learning-goals', 'POST', { subjectId: subject, classId, title: 'Isolated V4 English regression',
      goalType: 'Foundation', targetSkillIds: map.skills.map(s => s.skillId), availableMinutesPerDay: 15, availableDaysPerWeek: 5 });
    proposal = await ok(student, '/students/me/path-proposals', 'POST', { goalId: goal.goalId, subjectId: subject, classId });
    assert.ok(proposal.milestones.length >= 2);
    assert.equal(new Set(proposal.milestones.map(m => m.title)).size, proposal.milestones.length);
    assert.ok(proposal.milestones.flatMap(m => m.missions).some(m => m.purpose === 'Study'));
    assert.ok(proposal.milestones.flatMap(m => m.missions).some(m => m.purpose === 'IndependentCheck'));
  });
  await check('Concurrent accepts activate one path; concurrent starts return one persisted run', async () => {
    const responses = await Promise.all([1, 2, 3].map(() => ok(student, `/students/me/path-proposals/${proposal.proposalId}/accept`, 'POST')));
    assert.equal(new Set(responses.map(r => r.learningPathId)).size, 1); accepted = responses[0];
    const study = proposal.milestones.flatMap(m => m.missions).find(m => m.purpose === 'Study' && m.canStart);
    const runs = await Promise.all([1, 2, 3].map(() => ok(student, `/students/me/missions/${study.missionId}/start`, 'POST')));
    assert.equal(new Set(runs.map(r => String(r.sessionRunId))).size, 1); studyRun = runs[0];
    assert.equal(studyRun.activities[0].activityType, 'Study'); assert.ok(studyRun.activities[0].content); assert.equal(studyRun.activities[0].question, null);
  });
  await check('Study draft resumes through a fresh authenticated session, stale revision cannot overwrite', async () => {
    const a = studyRun.activities[0];
    const saved = await ok(student, `/students/me/activity-runs/${a.activityRunId}/draft`, 'PUT', { expectedRevision: a.draftRevision, draftPayload: '{"finalAnswer":"draft from device one"}' });
    assert.equal(saved.hasConflict, false);
    const secondDevice = await login('student02');
    const restored = await ok(secondDevice, `/students/me/activity-runs/${a.activityRunId}`);
    assert.ok(restored.draftPayload.includes('draft from device one'));
    const stale = await ok(secondDevice, `/students/me/activity-runs/${a.activityRunId}/draft`, 'PUT', { expectedRevision: 0, draftPayload: 'overwrite' });
    assert.equal(stale.hasConflict, true);
  });
  await check('Unfinished conflict names the exact owned mission and resumes it without creating a run', async () => {
    const otherMission = proposal.milestones.flatMap(m => m.missions).find(m => m.missionId !== studyRun.sessionId && m.canStart);
    assert.ok(otherMission);
    const conflict = await request(student, `/students/me/missions/${otherMission.missionId}/start`, 'POST');
    assert.equal(conflict.status, 409);
    assert.equal(conflict.data.errorCode, 'LEARNING_MISSION_IN_PROGRESS');
    assert.equal(conflict.data.ongoingMission.missionId, studyRun.sessionId);
    assert.equal(conflict.data.ongoingMission.runId, studyRun.sessionRunId);
    assert.equal(conflict.data.ongoingMission.title, studyRun.title);
    const resumed = await ok(student, `/students/me/missions/${conflict.data.ongoingMission.missionId}/start`, 'POST');
    assert.equal(resumed.sessionRunId, studyRun.sessionRunId);
  });
  await check('Accepting a different route explains blockers and preserves the active route', async () => {
    const goal = await ok(student, '/students/me/learning-goals', 'POST', { subjectId: subject, classId, title: 'Blocked replacement fixture',
      goalType: 'Foundation', targetSkillIds: map.skills.map(s => s.skillId), availableMinutesPerDay: 15, availableDaysPerWeek: 5 });
    const next = await ok(student, '/students/me/path-proposals', 'POST', { goalId: goal.goalId, subjectId: subject, classId });
    const before = execSql('SELECT COUNT(*) FROM learning_paths;').trim();
    const conflict = await request(student, `/students/me/path-proposals/${next.proposalId}/accept`, 'POST');
    assert.equal(conflict.status, 409); assert.equal(conflict.data.errorCode, 'LEARNING_PATH_CHANGE_BLOCKED');
    const blockers = conflict.data.blockingMissions;
    assert.equal(blockers.length, 1); assert.equal(blockers[0].missionId, studyRun.sessionId);
    assert.equal(blockers[0].title, studyRun.title); assert.equal(blockers[0].status, 'InProgress'); assert.equal(blockers[0].canResume, true);
    assert.equal(execSql('SELECT COUNT(*) FROM learning_paths;').trim(), before);
    assert.equal((await ok(student, `/students/me/learning-agenda?${scope}`)).learningPathId, accepted.learningPathId);
    for (const token of [other, anotherCenter]) {
      const denied = await request(token, `/students/me/path-proposals/${next.proposalId}/accept`, 'POST');
      assert.ok([403, 404].includes(denied.status)); assert.equal(denied.data.blockingMissions, undefined);
    }
  });
  await check('Other students and tenants cannot start/read this mission', async () => {
    for (const token of [other, anotherCenter]) {
      assert.ok([403, 404].includes((await request(token, `/students/me/missions/${studyRun.sessionId}/start`, 'POST')).status));
      assert.ok([403, 404].includes((await request(token, `/students/me/activity-runs/${studyRun.activities[0].activityRunId}`)).status));
    }
  });
  await check('Study completion creates no synthetic Attempt, no fake mastery and evaluates idempotently', async () => {
    const before = execSql('SELECT COUNT(*) FROM attempts;').trim(); const a = studyRun.activities[0];
    const body = { clientSubmissionId: crypto.randomUUID(), finalAnswer: 'I understand the published introduction and still need to practise.' };
    await ok(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', body);
    await ok(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', body);
    assert.equal(execSql('SELECT COUNT(*) FROM attempts;').trim(), before);
    const result = await ok(student, `/students/me/mission-runs/${studyRun.sessionRunId}/evaluate-v4`, 'POST');
    assert.equal(result.outcome, 'ActivityOnlyCompleted'); assert.deepEqual(result.provenSkills, []);
    const decisions = execSql('SELECT COUNT(*) FROM path_decisions;').trim();
    await ok(student, `/students/me/mission-runs/${studyRun.sessionRunId}/evaluate-v4`, 'POST');
    assert.equal(execSql('SELECT COUNT(*) FROM path_decisions;').trim(), decisions);
    const runCount = execSql('SELECT COUNT(*) FROM learning_path_session_runs;').trim();
    const saved = await ok(student, `/students/me/missions/${studyRun.sessionId}/runs/${studyRun.sessionRunId}`);
    assert.equal(saved.status, 'Completed'); assert.equal(typeof saved.outcome, 'string');
    assert.equal(saved.activities[0].submittedResponse, body.finalAnswer);
    assert.equal(execSql('SELECT COUNT(*) FROM path_decisions;').trim(), decisions);
    assert.equal(execSql('SELECT COUNT(*) FROM learning_path_session_runs;').trim(), runCount);
  });
  await check('Independent check issues real questions, blocks assistance and submits through canonical grading', async () => {
    const agenda = await ok(student, `/students/me/learning-agenda?${scope}&availableMinutes=30`);
    const checkMission = agenda.missions.find(m => m.purpose === 'IndependentCheck' && m.canStart); assert.ok(checkMission);
    const run = await ok(student, `/students/me/missions/${checkMission.missionId}/start`, 'POST');
    assert.equal(run.activities.length, 5);
    for (const a of run.activities) {
      assert.ok(a.question?.question?.questionId);
      assert.equal(a.content, null);
      const q = a.question.question;
      const help = await ok(student, `/students/me/activity-runs/${a.activityRunId}/assistance`, 'POST', { stepIndex: 0 });
      assert.equal(help.isBlocked, true); assert.ok(!help.hint);
      const body = { clientSubmissionId: crypto.randomUUID(), questionId: String(q.questionId), finalAnswer: q.options?.[0]?.optionId || '__incorrect_fixture_answer__',
        reasoningText: 'Isolated test of the canonical submission and evidence pipeline.', timeSpentSeconds: 30 };
      const response = await ok(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', body);
      if (a === run.activities[0]) assert.equal((await request(student, `/students/me/activity-runs/${a.activityRunId}/ai-feedback`, 'POST')).status, 409, 'AI must not leak independent-check answers before all items are submitted');
      assert.ok(response.attemptId);
      const replay = await ok(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', body); assert.equal(replay.attemptId, response.attemptId);
      assert.equal((await request(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', { ...body, finalAnswer: 'changed' })).status, 409);
    }
    const result = await ok(student, `/students/me/mission-runs/${run.sessionRunId}/evaluate-v4`, 'POST');
    assert.ok(['PendingGrading', 'PendingTeacherReview', 'InsufficientEvidence', 'NeedsSupport', 'Passed'].includes(result.outcome));
    if (result.outcome === 'Passed') assert.ok(Number(execSql(`SELECT COUNT(*) FROM issued_items i JOIN activity_runs a ON a.activity_run_id=i.activity_run_id AND a.center_id=i.center_id JOIN evidence_assessments e ON e.attempt_id=i.attempt_id AND e.center_id=i.center_id WHERE a.session_run_id=${Number(run.sessionRunId)} AND e.requires_teacher_review=0;`).trim()) >= 5);
    const active = (await ok(student, `/students/me/learning-agenda?${scope}`)).learningPathId;
    assert.equal(active, accepted.learningPathId, 'Worker must not supersede the V4 route');
  });
  await check('Recent answers explain recommendation; practice is a distinct sequence with immediate feedback', async () => {
    const agenda = await ok(student, `/students/me/learning-agenda?${scope}`);
    assert.ok(agenda.insights.answerCount >= 5);
    assert.ok(agenda.insights.suggestedSkillIds.length > 0);
    const skill = agenda.insights.skills.find(s => s.wrongCount > 0);
    assert.ok(skill); assert.ok(skill.recentAnswers.length > 0);
    const practice = agenda.missions.find(m => m.purpose === 'Practice' && m.skillId === skill.skillId && m.canStart);
    assert.ok(practice);
    const run = await ok(student, `/students/me/missions/${practice.missionId}/start`, 'POST');
    assert.ok(run.activities.length >= 5 && run.activities.length <= 8);
    assert.equal(new Set(run.activities.map(a => String(a.question.question.questionId))).size, run.activities.length);
    assert.ok(run.activities.every(a => !a.learningFeedback));
    const a = run.activities[0]; const q = a.question.question;
    const body = { clientSubmissionId: crypto.randomUUID(), questionId: String(q.questionId), finalAnswer: q.options?.[0]?.optionId || '__incorrect_fixture_answer__', reasoningText: 'Real HTTP practice feedback regression.', timeSpentSeconds: 20 };
    const submitted = await ok(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', body);
    assert.ok(submitted.learningFeedback);
    assert.ok(submitted.learningFeedback.correctAnswer); assert.ok(submitted.learningFeedback.explanation);
    const restored = await ok(student, `/students/me/activity-runs/${a.activityRunId}`);
    assert.equal(restored.learningFeedback.correctAnswer, submitted.learningFeedback.correctAnswer);
    const replay = await ok(student, `/students/me/activity-runs/${a.activityRunId}/submissions`, 'POST', body);
    assert.equal(replay.attemptId, submitted.attemptId);
  });
  await check('Completed work can be read without starting a new run; tutor is owner-scoped', async () => {
    const before=execSql('SELECT COUNT(*) FROM learning_path_session_runs;').trim();
    const saved=await ok(student,`/students/me/missions/${studyRun.sessionId}/runs/${studyRun.sessionRunId}`);
    assert.equal(saved.sessionRunId,studyRun.sessionRunId);assert.ok(saved.activities[0].submittedResponse.includes('published introduction'));
    assert.equal(execSql('SELECT COUNT(*) FROM learning_path_session_runs;').trim(),before);
    for(const token of [other,anotherCenter]){
      assert.ok([403,404].includes((await request(token,`/students/me/missions/${studyRun.sessionId}/runs/${studyRun.sessionRunId}`)).status));
      assert.ok([403,404].includes((await request(token,`/students/me/activity-runs/${studyRun.activities[0].activityRunId}/ai-feedback`,'POST')).status));
    }
    const ai=await ok(student,`/students/me/activity-runs/${studyRun.activities[0].activityRunId}/ai-feedback`,'POST');
    assert.ok(['Generated','Unavailable'].includes(ai.status));
    if(ai.status==='Unavailable')assert.ok(!ai.aiSolution);
  });
  fs.mkdirSync(path.join(__dirname, '../../scratch'), { recursive: true });
  fs.writeFileSync(path.join(__dirname, '../../scratch/v4-api-acceptance.json'), JSON.stringify({ generatedAt: new Date().toISOString(), results }, null, 2));
  if (results.some(r => r.status !== 'PASS')) process.exitCode = 1;
}
main().catch(e => { console.error(e.message); process.exitCode = 1; });
