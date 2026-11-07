const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { BASE_URL, validateManifest, validateOwner, roleAccount } = require('./safety.cjs');
const acceptance = require('../../../scripts/e2e/acceptance_stack.cjs');
const { docker } = require('../../../scripts/ops/mysql_admin.cjs');
const fixtures = require('../../../scripts/e2e/fixture_manager.cjs');

function verify(baseUrl = BASE_URL) {
  const state = validateManifest(acceptance.manifest(), baseUrl);
  const labels = Object.fromEntries([state.api, state.web].map(name => [name,
    docker(['inspect', name, '--format', '{{index .Config.Labels "edutwin.e2e.owner"}}']).trim()]));
  validateOwner(state, labels);
  return state;
}
async function login(role) {
  verify();
  const account = roleAccount(role);
  const response = await fetch(`${BASE_URL}/api/v1/auth/login`, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ ...account, password: acceptance.testPassword }), signal: AbortSignal.timeout(15000),
  });
  assert.equal(response.status, 200, `Disposable ${role} login failed (${response.status}); no credentials logged.`);
  const body = await response.json();
  assert.ok(body.data?.accessToken, 'Disposable login returned no access token.');
  const claims = JSON.parse(Buffer.from(body.data.accessToken.split('.')[1], 'base64url').toString());
  assert.equal(claims.iss, 'edutwin-acceptance', 'Refusing token from a primary runtime.');
  return body.data.accessToken;
}
function register(on, config) {
  on('before:run', () => { verify(config.baseUrl); });
  on('task', {
    'isolated:verify'() { const state = verify(config.baseUrl); return { runId: state.runId }; },
    'isolated:protectedRoutes'() {
      verify(config.baseUrl);
      return require('./route-surface.cjs').protectedRoutes(path.resolve(__dirname, '../../..'));
    },
    'isolated:login': login,
    'isolated:credentials'(role) { verify(config.baseUrl); return { ...roleAccount(role), password: acceptance.testPassword }; },
    'isolated:png'() { verify(config.baseUrl); return require('./fixtures.cjs').png().toString('base64'); },
    'isolated:assignment'({ title = 'Validation fixture' } = {}) {
      verify(config.baseUrl);
      assert.equal(typeof title, 'string');
      assert.ok(title.length <= 180);
      return fixtures.createAssignmentFixture({ tag: 'CYPRESS', title,
        questionConfigs: [{ questionId: 10000, points: 1 }, { questionId: 10006, points: 1 }] });
    },
    'isolated:count'(domain) {
      verify(config.baseUrl);
      const tables = { questions: 'questions', classes: 'classes', students: 'students', teachers: 'teachers',
        attempts: 'attempts', curriculums: 'curriculums', nodes: 'knowledge_nodes', adaptiveSessions: 'adaptive_practice_sessions' };
      assert.ok(Object.hasOwn(tables, domain), 'Unsupported count domain.');
      return Number(fixtures.execSql(`SELECT COUNT(*) FROM ${tables[domain]};`).trim());
    },
    'isolated:assertAdaptiveSchema'() {
      const state = verify(config.baseUrl);
      const tables = fixtures.execSql(`SELECT table_name FROM information_schema.tables WHERE table_schema='${state.database}' AND table_name IN ('adaptive_practice_sessions','adaptive_practice_session_items','adaptive_practice_session_summaries');`)
        .trim().split(/\r?\n/).filter(Boolean);
      assert.equal(tables.length, 3, 'Adaptive migrations missing: this is a failed precondition, not a skipped/passed test.');
      return true;
    },
  });
  on('after:run', results => {
    const dir = path.join(config.projectRoot, 'results/cypress');
    fs.mkdirSync(dir, { recursive: true });
    // Only summary metadata; requests, responses, credentials and browser storage are deliberately excluded.
    fs.writeFileSync(path.join(dir, 'summary.json'), JSON.stringify({
      totalTests: results.totalTests, totalPassed: results.totalPassed,
      totalFailed: results.totalFailed, totalPending: results.totalPending, totalSkipped: results.totalSkipped,
      specs: (results.runs || []).map(run => ({ spec: run.spec.relative,
        tests: run.tests.map(test => ({ title: test.title.join(' / '), state: test.state })) })),
    }, null, 2));
  });
  return config;
}
module.exports = { register, verify };
