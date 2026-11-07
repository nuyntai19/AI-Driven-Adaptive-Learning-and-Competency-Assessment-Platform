const assert = require('node:assert/strict');

const BASE_URL = 'http://127.0.0.1:3002';
function validateManifest(state, baseUrl) {
  assert.equal(baseUrl, BASE_URL, 'Cypress only supports the disposable acceptance web on port 3002.');
  assert.match(state.runId, /^[a-f0-9]{32}$/);
  assert.equal(state.database, `edutwin_e2e_${state.runId}`);
  assert.equal(state.databaseUser, `e2e_${state.runId.slice(0, 16)}`);
  assert.equal(state.api, 'edutwin-acceptance-api');
  assert.equal(state.web, 'edutwin-acceptance-web');
  return state;
}
function validateOwner(state, labels) {
  for (const name of [state.api, state.web]) {
    assert.equal(labels[name], state.runId, `Container ${name} does not belong to this acceptance run.`);
  }
}
const roles = Object.freeze({
  manager: { username: 'manager', centerCode: 'EDUTWIN_A' },
  teacher: { username: 'teacher.math', centerCode: 'EDUTWIN_A' },
  student: { username: 'student01', centerCode: 'EDUTWIN_A' },
  student2: { username: 'student02', centerCode: 'EDUTWIN_A' },
  foreignStudent: { username: 'student01', centerCode: 'EDUTWIN_B' },
  foreignTeacher: { username: 'teacher.math', centerCode: 'EDUTWIN_B' },
  platform: { username: 'platform.admin', centerCode: 'PLATFORM' },
});
function roleAccount(role) {
  assert.ok(Object.hasOwn(roles, role), 'Unknown disposable role.');
  return roles[role];
}
module.exports = { BASE_URL, validateManifest, validateOwner, roleAccount };
