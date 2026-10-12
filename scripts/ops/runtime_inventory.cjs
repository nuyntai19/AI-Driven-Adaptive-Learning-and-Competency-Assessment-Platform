// Read-only deployment/database inventory. Never print environment credentials.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { root, mysql, docker } = require('./mysql_admin.cjs');
const sql = `
 SELECT 'migrations',COUNT(*) FROM __EFMigrationsHistory;
 SELECT 'orphan_analysis',COUNT(*) FROM reasoning_analyses r LEFT JOIN attempts a ON a.attempt_id=r.attempt_id WHERE a.attempt_id IS NULL;
 SELECT 'orphan_evidence',COUNT(*) FROM evidence_assessments e LEFT JOIN attempts a ON a.attempt_id=e.attempt_id WHERE a.attempt_id IS NULL;
 SELECT 'orphan_history',COUNT(*) FROM twin_update_history h LEFT JOIN attempts a ON a.attempt_id=h.attempt_id WHERE a.attempt_id IS NULL;
 SELECT 'orphan_paths',COUNT(*) FROM learning_paths p LEFT JOIN attempts a ON a.attempt_id=p.generated_from_attempt_id WHERE p.generated_from_attempt_id IS NOT NULL AND a.attempt_id IS NULL;
 SELECT 'orphan_knowledge',COUNT(*) FROM knowledge_twins k LEFT JOIN attempts a ON a.attempt_id=k.last_attempt_id WHERE k.last_attempt_id IS NOT NULL AND a.attempt_id IS NULL;
 SELECT 'audit_delete_trigger',COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA='edutwin' AND TRIGGER_NAME='tr_evidence_append_only_delete' AND ACTION_STATEMENT LIKE '%append-only%';
 SELECT 'fk_checks',@@FOREIGN_KEY_CHECKS;
 SELECT 'attempts',COUNT(*) FROM attempts;
 SELECT 'behavior_twins',COUNT(*) FROM behavior_twins;
 SELECT 'assignments',COUNT(*) FROM assignments;
`;
const database = Object.fromEntries(mysql(sql, 'edutwin').trim().split(/\r?\n/).map(row => {
  const [key, value] = row.split('\t'); return [key, Number(value)];
}));
const containers = {};
for (const name of ['edutwin-web','edutwin-api','edutwin-mysql','edutwin-adminer']) {
  const info = JSON.parse(docker(['inspect', name]))[0];
  containers[name] = { running: info.State.Running, status: info.State.Status,
    health: info.State.Health?.Status ?? null, image: info.Image };
  if (['edutwin-web','edutwin-api'].includes(name)) {
    containers[name].matchesLatestImage = info.Image === JSON.parse(docker(['image','inspect',`${name}:latest`]))[0].Id;
  }
}
const localHtmlHash = crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'web/edutwin-web/dist/index.html'))).digest('hex');
const containerHtmlHash = docker(['exec','edutwin-web','sha256sum','/usr/share/nginx/html/index.html']).trim().split(/\s/)[0];
console.log(JSON.stringify({ database, containers, webHtmlMatchesLocalBuild: localHtmlHash===containerHtmlHash },null,2));
