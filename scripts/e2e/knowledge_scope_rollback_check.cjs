// Compatibility smoke test for the pre-change API image against an isolated migrated copy.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { readEnv, docker, mysql, sqlString } = require('../ops/mysql_admin.cjs');
const clone = 'edutwin_scope_test_v51_20261023';
const container = 'edutwin-rollback-compatibility';
const image = 'edutwin-api:rollback-check';
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const marker = crypto.randomUUID().replaceAll('-', '');
async function run() {
  const cfg = readEnv();
  assert.equal(mysql(`SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name=${sqlString(clone)};`).trim(), '1');
  assert.equal(mysql(`SELECT COUNT(*) FROM ${clone}.__EFMigrationsHistory;`).trim(), '52');
  assert.equal(docker(['ps', '-a', '--filter', `name=^/${container}$`, '--format', '{{.Names}}']).trim(), '');
  const dbUser = `rollback_${marker.slice(0, 16)}`;
  const dbPassword = crypto.randomBytes(32).toString('hex');
  let createdUser = false;
  try {
    mysql(`CREATE USER '${dbUser}'@'%' IDENTIFIED BY '${dbPassword}'; GRANT ALL PRIVILEGES ON ${clone}.* TO '${dbUser}'@'%';`);
    createdUser = true;
    const settings = {
      ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://+:8080',
      ConnectionStrings__Default: `Server=mysql;Port=3306;Database=${clone};User=${dbUser};Password=${dbPassword}`,
      Jwt__SigningKey: crypto.randomBytes(48).toString('hex'), Jwt__Issuer: 'edutwin-rollback-check', Jwt__Audience: 'edutwin-rollback-check',
      Seed__Enabled: 'false', Seed__CenterManagerPassword: cfg.Seed__CenterManagerPassword || 'LocalRollbackCheckOnly123!',
      PlatformBootstrap__AdminPassword: 'LocalRollbackCheckOnly123!',
      HealthChecks__MySqlHost: 'mysql', HealthChecks__MySqlPort: '3306',
      Gemini__ApiKey: '', Gemini__Model: cfg.Gemini__Model || 'gemini-2.5-flash',
      AttachmentStorage__RootPath: '/tmp/rollback-storage', AttachmentStorage__DataProtectionKeysPath: '/tmp/rollback-keys',
      Logging__LogLevel__Default: 'Warning', Logging__LogLevel__Microsoft: 'Warning',
    };
    docker(['run', '-d', '--name', container, '--network', 'edutwin_default', '--label', `edutwin.e2e.owner=${marker}`,
      '-p', '127.0.0.1:5003:8080', ...Object.keys(settings).flatMap(key => ['-e', key]), image],
    { env: { ...process.env, ...settings } });
    let ready;
    for (let i = 0; i < 120; i++) {
      try {
        const r = await fetch('http://127.0.0.1:5003/api/v1/health/ready', { signal: AbortSignal.timeout(3000) });
        if (r.ok && (await r.json()).status === 'Healthy') { ready = true; break; }
      } catch {}
      await delay(1000);
    }
    assert.ok(ready, 'Old API image did not become healthy on the migrated clone.');
    const response = await fetch('http://127.0.0.1:5003/api/v1/auth/login', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ centerCode: 'EDUTWIN_A', username: 'teacher.math', password: cfg.Seed__CenterManagerPassword }),
      signal: AbortSignal.timeout(10000),
    });
    assert.equal(response.status, 200, 'Old API login failed on the migrated clone.');
    const token = (await response.json()).data.accessToken;
    const graph = await fetch('http://127.0.0.1:5003/api/v1/knowledge/graph?subjectId=30000000-0000-0000-0000-000000000003',
      { headers: { authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(10000) });
    assert.equal(graph.status, 200, 'Old API graph read failed on the migrated clone.');
    console.log(JSON.stringify({ rollbackImage: image, clone, ready: true, login: 200, oldGraphRead: 200,
      limitation: 'Previous API has subject-only graph; emergency rollback must disable academic writes and be short-lived.' }));
  } finally {
    const found = docker(['ps', '-a', '--filter', `name=^/${container}$`, '--format', '{{.Names}}']).trim();
    if (found) {
      assert.equal(docker(['inspect', container, '--format', '{{index .Config.Labels "edutwin.e2e.owner"}}']).trim(), marker);
      docker(['rm', '-f', container]);
    }
    if (createdUser) mysql(`DROP USER IF EXISTS '${dbUser}'@'%';`);
  }
}
run().catch(error => { console.error(error.message); process.exitCode = 1; });
