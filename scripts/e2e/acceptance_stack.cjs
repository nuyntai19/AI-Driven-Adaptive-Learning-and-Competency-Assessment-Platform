const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { root, readEnv, docker, mysql, sqlString } = require('../ops/mysql_admin.cjs');
const manifestPath = path.join(os.tmpdir(), 'edutwin_acceptance_stack.fixture_manifest.json');
const testPassword = 'LocalTestOnly-20261006!'; // Only disposable test accounts, never production credentials.
function manifest() {
  const state = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  assert.match(state.runId, /^[a-f0-9]{32}$/);
  assert.equal(state.database, `edutwin_e2e_${state.runId}`);
  assert.equal(state.databaseUser, `e2e_${state.runId.slice(0, 16)}`);
  return state;
}
async function start() {
  if (fs.existsSync(manifestPath)) throw new Error('Recover the existing acceptance manifest with stop before a new run.');
  const cfg = readEnv();
  const runId = crypto.randomUUID().replaceAll('-', '');
  const state = { runId, database: `edutwin_e2e_${runId}`, databaseUser: `e2e_${runId.slice(0, 16)}`,
    api: 'edutwin-acceptance-api', web: 'edutwin-acceptance-web', createdAt: new Date().toISOString(), fixtures: [] };
  for (const name of [state.api, state.web]) {
    assert.equal(docker(['ps', '-a', '--filter', `name=^/${name}$`, '--format', '{{.Names}}']).trim(), '', `Existing container ${name}: stop.`);
  }
  fs.writeFileSync(manifestPath, JSON.stringify(state, null, 2));
  const dbPassword = crypto.randomBytes(32).toString('hex');
  try {
    mysql(`CREATE DATABASE ${state.database} CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
      CREATE USER '${state.databaseUser}'@'%' IDENTIFIED BY '${dbPassword}';
      GRANT ALL PRIVILEGES ON ${state.database}.* TO '${state.databaseUser}'@'%';`);
    const apiEnv = {
      ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://+:8080',
      ConnectionStrings__Default: `Server=mysql;Port=3306;Database=${state.database};User=${state.databaseUser};Password=${dbPassword}`,
      Jwt__SigningKey: crypto.randomBytes(48).toString('hex'), Jwt__Issuer: 'edutwin-acceptance', Jwt__Audience: 'edutwin-acceptance',
      Seed__Enabled: 'true', Seed__CenterManagerPassword: testPassword,
      PlatformBootstrap__AdminUsername: 'platform.admin', PlatformBootstrap__AdminPassword: testPassword,
      Gemini__ApiKey: '', Gemini__Model: cfg.Gemini__Model || 'gemini-2.5-flash',
      AttachmentStorage__RootPath: '/tmp/acceptance-storage', AttachmentStorage__DataProtectionKeysPath: '/tmp/acceptance-keys',
      Logging__LogLevel__Default: 'Warning', Logging__LogLevel__Microsoft: 'Warning',
    };
    docker(['run', '-d', '--name', state.api, '--network', 'edutwin_default', '--label', `edutwin.e2e.owner=${runId}`,
      '-p', '127.0.0.1:5001:8080', ...Object.keys(apiEnv).flatMap(k => ['-e', k]), 'edutwin-api:latest'],
      { env: { ...process.env, ...apiEnv } });
    docker(['run', '-d', '--name', state.web, '--network', 'edutwin_default', '--label', `edutwin.e2e.owner=${runId}`,
      '-p', '127.0.0.1:3002:80', '-v', `${path.join(root, 'scripts/e2e/acceptance.nginx.conf')}:/etc/nginx/conf.d/default.conf:ro`, 'edutwin-web:latest']);
    for (let n = 0; n < 90; n++) {
      try {
        const response = await fetch('http://localhost:3002/api/v1/auth/login', { method: 'POST', headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ centerCode: 'EDUTWIN_A', username: 'student01', password: testPassword }), signal: AbortSignal.timeout(3000) });
        if (response.status === 200) { console.log(JSON.stringify({ isolatedStackReady: true, database: state.database, url: 'http://localhost:3002', manifestPath })); return state; }
      } catch {}
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    throw new Error('Isolated API did not become ready; inspect its logs.');
  } catch (error) { stop(); throw error; }
}
function stop() {
  if (!fs.existsSync(manifestPath)) return;
  const state = manifest();
  for (const name of [state.web, state.api]) {
    if (!docker(['ps', '-a', '--filter', `name=^/${name}$`, '--format', '{{.Names}}']).trim()) continue;
    assert.equal(docker(['inspect', name, '--format', '{{index .Config.Labels "edutwin.e2e.owner"}}']).trim(), state.runId, 'Unowned container: stop.');
    docker(['rm', '-f', name]);
  }
  mysql(`DROP DATABASE IF EXISTS ${state.database}; DROP USER IF EXISTS '${state.databaseUser}'@'%';`);
  assert.equal(Number(mysql(`SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name='${state.database}';`).trim()), 0);
  fs.unlinkSync(manifestPath);
  console.log('Isolated stack cleanup verified; primary database untouched.');
}
if (require.main === module) {
  Promise.resolve().then(() => process.argv[2] === 'start' ? start() : process.argv[2] === 'stop' ? stop() : Promise.reject(new Error('Use start or stop')))
    .catch(error => { console.error(error.message); process.exitCode = 1; });
}
module.exports = { start, stop, manifest, manifestPath, testPassword };
