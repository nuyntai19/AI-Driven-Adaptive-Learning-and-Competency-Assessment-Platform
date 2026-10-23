// Real HTTP assertions against the owner-verified disposable stack only. Not browser evidence.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const { docker, root } = require('../ops/mysql_admin.cjs');
const base = 'http://127.0.0.1:3002/api/v1';
const subject = '30000000-0000-0000-0000-000000000003';
const class12 = '51000000-0000-0000-0000-000000000012';
const version12 = 'e1000000-0000-0000-0000-000000000012';
const results = [];
async function request(token, route, method = 'GET', body) {
  const response = await fetch(base + route, { method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(20000) });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null };
}
async function login(username, centerCode = 'EDUTWIN_A') {
  const r = await request(null, '/auth/login', 'POST', { username, centerCode, password: testPassword });
  assert.equal(r.status, 200, `Login ${username}: ${JSON.stringify(r.body)}`);
  return r.body.data.accessToken;
}
async function check(name, action) {
  try { await action(); results.push({ name, status: 'PASS' }); console.log(`PASS ${name}`); }
  catch (error) { results.push({ name, status: 'FAIL', error: error.message }); console.error(`FAIL ${name}: ${error.message}`); }
}
const graph = (query) => `/knowledge/graph?${new URLSearchParams({ subjectId: subject, ...query })}`;
const ids = data => data.nodes.map(n => n.nodeId).sort();
async function run() {
  const state = manifest();
  for (const name of [state.api, state.web])
    assert.equal(docker(['inspect', name, '--format', '{{index .Config.Labels "edutwin.e2e.owner"}}']).trim(), state.runId);
  const teacher = await login('teacher.math');
  const englishTeacher = await login('teacher.english');
  const foreignTeacher = await login('teacher.math', 'EDUTWIN_B');
  const manager = await login('manager');
  await check('Grade library isolation and explicit external prerequisite', async () => {
    const a = await request(teacher, graph({ gradeLevel: 10 })); const b = await request(teacher, graph({ gradeLevel: 12 }));
    assert.equal(a.status, 200); assert.equal(b.status, 200);
    assert.deepEqual(ids(a.body.data), ['10000']); assert.deepEqual(ids(b.body.data), ['10002']);
    assert.equal(b.body.data.externalPrerequisites[0].sourceNodeId, '10001');
  });
  await check('Grade node picker uses approved membership', async () => {
    const r = await request(teacher, `/knowledge/nodes?subjectId=${subject}&gradeLevel=11`);
    assert.equal(r.status, 200); assert.deepEqual(r.body.data.map(n => n.nodeId), ['10001']);
  });
  await check('Canonical catalog labels declared grades', async () => {
    const r = await request(teacher, graph({})); assert.equal(r.status, 200);
    assert.deepEqual(r.body.data.nodes.find(n => n.nodeId === '10000').gradeLevels, [10]);
  });
  await check('Class scope and year mismatch', async () => {
    const r = await request(teacher, graph({ classId: class12, academicYear: '2026-2027' }));
    assert.equal(r.status, 200); assert.deepEqual(ids(r.body.data), ['10002']);
    assert.equal((await request(teacher, graph({ classId: class12, academicYear: '2027-2028' }))).status, 400);
  });
  await check('Incomplete history/year scope never broadens to whole subject', async () => {
    assert.equal((await request(teacher, graph({ history: true }))).status, 400);
    assert.equal((await request(teacher, graph({ academicYear: '2026-2027' }))).status, 400);
  });
  await check('Class ownership and tenant isolation', async () => {
    assert.ok([403, 404].includes((await request(englishTeacher, graph({ classId: class12 }))).status));
    assert.ok([403, 404].includes((await request(foreignTeacher, graph({ classId: class12 }))).status));
  });
  await check('Manager cannot author academic versions', async () => {
    assert.equal((await request(manager, '/knowledge/graph/versions', 'POST', { subjectId: subject, gradeLevel: 10 })).status, 403);
  });
  await check('Direct curriculum request rejects wrong-grade node', async () => {
    const r = await request(teacher, '/curriculums', 'POST', { subjectId: subject, title: 'Scope acceptance invalid', gradeLevel: 10, nodeIds: ['10002'] });
    assert.equal(r.status, 409); assert.equal(r.body.errorCode, 'ACADEMIC_SCOPE_CONFLICT');
    assert.deepEqual(r.body.scopeConflict.invalidNodeIds, ['10002']);
  });
  let draft;
  await check('Create curriculum pins the approved version', async () => {
    const r = await request(teacher, '/curriculums', 'POST', { subjectId: subject, title: 'Scope acceptance draft', gradeLevel: 10, nodeIds: ['10000'] });
    assert.equal(r.status, 201, JSON.stringify(r.body)); draft = r.body.data;
    assert.equal(draft.graphVersionId, 'e1000000-0000-0000-0000-000000000010');
  });
  await check('Rejected grade update preserves stored composition', async () => {
    const r = await request(teacher, `/curriculums/${draft.curriculumId}`, 'PATCH', { title: draft.title, gradeLevel: 12, rowVersion: draft.rowVersion });
    assert.equal(r.status, 409); const saved = await request(teacher, `/curriculums/${draft.curriculumId}`);
    assert.equal(saved.body.data.gradeLevel, 10); assert.deepEqual(saved.body.data.nodeIds, ['10000']);
  });
  await check('Atomic grade/node/planned-class repair succeeds', async () => {
    const r = await request(teacher, `/curriculums/${draft.curriculumId}`, 'PATCH', { title: draft.title, gradeLevel: 12,
      graphVersionId: version12, nodeIds: ['10002'], classIds: [class12], rowVersion: draft.rowVersion });
    assert.equal(r.status, 200, JSON.stringify(r.body)); draft = r.body.data;
    assert.equal(draft.gradeLevel, 12); assert.deepEqual(draft.nodeIds, ['10002']); assert.deepEqual(draft.classIds, [class12]);
  });
  await check('Publish never replaces an occupied primary application implicitly', async () => {
    const r = await request(teacher, `/curriculums/${draft.curriculumId}/publish`, 'POST', { rowVersion: draft.rowVersion });
    assert.equal(r.status, 409); const original = await request(teacher, graph({ classId: class12 })); assert.deepEqual(ids(original.body.data), ['10002']);
  });
  await check('Publish pins topology and freezes curriculum composition', async () => {
    let r = await request(teacher, `/curriculums/${draft.curriculumId}`, 'PATCH', { title: draft.title, gradeLevel: 12,
      nodeIds: ['10002'], classIds: [], rowVersion: draft.rowVersion });
    assert.equal(r.status, 200, JSON.stringify(r.body)); draft = r.body.data;
    r = await request(teacher, `/curriculums/${draft.curriculumId}/publish`, 'POST', { rowVersion: draft.rowVersion });
    assert.equal(r.status, 200, JSON.stringify(r.body)); draft = r.body.data;
    assert.equal((await request(teacher, `/curriculums/${draft.curriculumId}/nodes`, 'PUT', { nodeIds: [], rowVersion: draft.rowVersion })).status, 409);
  });
  let fork;
  await check('Fork and publish a version without rewriting existing curriculum', async () => {
    let r = await request(teacher, '/knowledge/graph/versions', 'POST', { subjectId: subject, gradeLevel: 12, sourceGraphVersionId: version12, title: 'Scope acceptance fork' });
    assert.equal(r.status, 200, JSON.stringify(r.body)); fork = r.body.data;
    r = await request(teacher, `/knowledge/graph/versions/${fork.graphVersionId}`, 'PUT', { rowVersion: fork.rowVersion,
      nodeMappings: fork.nodeMappings, edgeMappings: fork.edgeMappings.map(e => ({ ...e, weight: 0.5 })) });
    assert.equal(r.status, 200, JSON.stringify(r.body)); fork = r.body.data;
    r = await request(teacher, `/knowledge/graph/versions/${fork.graphVersionId}/publish`, 'POST', { rowVersion: fork.rowVersion });
    assert.equal(r.status, 200, JSON.stringify(r.body)); fork = r.body.data;
    const current = await request(teacher, graph({ gradeLevel: 12 })); assert.equal(current.body.data.externalPrerequisites[0].weight, 0.5);
    const pinned = await request(teacher, graph({ curriculumId: draft.curriculumId }));
    assert.equal(pinned.body.data.resolvedScope.graphVersionId, version12); assert.equal(pinned.body.data.externalPrerequisites[0].weight, 1);
  });
  await check('Published version mutation is rejected', async () => {
    const r = await request(teacher, `/knowledge/graph/versions/${fork.graphVersionId}`, 'PUT', { rowVersion: fork.rowVersion, nodeMappings: [], edgeMappings: [] });
    assert.equal(r.status, 409);
  });
  await check('List filters class/year and returns permitted class metadata', async () => {
    const r = await request(teacher, `/curriculums?classId=${class12}&academicYear=2026-2027&gradeLevel=12`);
    assert.equal(r.status, 200); assert.ok(r.body.data.length >= 1);
    for (const c of r.body.data) assert.ok(c.applicationClasses.every(a => a.academicYear === '2026-2027'));
  });
  const report = { kind: 'real-http-mysql', database: state.database, checkedAt: new Date().toISOString(), results,
    passed: results.filter(r => r.status === 'PASS').length, failed: results.filter(r => r.status === 'FAIL').length };
  fs.mkdirSync(path.join(root, 'storage/verification'), { recursive: true });
  fs.writeFileSync(path.join(root, 'storage/verification/knowledge-scope-api-results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, failed: report.failed }));
  if (report.failed) process.exitCode = 1;
}
run().catch(error => { console.error(error.message); process.exitCode = 1; });
