const { test } = require('node:test');
const assert = require('node:assert/strict');
const { BASE_URL, validateManifest, validateOwner, roleAccount } = require('./safety.cjs');
const state = () => {
  const runId = '1234567890abcdef1234567890abcdef';
  return { runId, database: `edutwin_e2e_${runId}`, databaseUser: `e2e_${runId.slice(0, 16)}`,
    api: 'edutwin-acceptance-api', web: 'edutwin-acceptance-web' };
};
test('accepts a correctly named disposable stack', () => assert.equal(validateManifest(state(), BASE_URL).database, state().database));
for (const url of ['http://localhost:3000', 'http://127.0.0.1:5000', 'https://example.org']) {
  test(`refuses non-isolated origin ${url}`, () => assert.throws(() => validateManifest(state(), url)));
}
for (const field of ['runId', 'database', 'databaseUser', 'api', 'web']) {
  test(`refuses an altered ${field}`, () => assert.throws(() => validateManifest({ ...state(), [field]: 'primary' }, BASE_URL)));
}
test('verifies both API and web Docker ownership', () => {
  const s = state();
  assert.doesNotThrow(() => validateOwner(s, { [s.api]: s.runId, [s.web]: s.runId }));
  assert.throws(() => validateOwner(s, { [s.api]: s.runId, [s.web]: 'other-run' }));
});
test('only known disposable seed accounts are selectable', () => {
  assert.equal(roleAccount('student').centerCode, 'EDUTWIN_A');
  assert.throws(() => roleAccount('__proto__'));
});
