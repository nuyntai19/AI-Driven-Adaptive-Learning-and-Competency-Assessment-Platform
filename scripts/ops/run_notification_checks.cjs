// Synthetic databases only. Do not apply migrations or inject notices into the user's database.
const { spawnSync } = require('node:child_process');
const { mkdirSync, writeFileSync } = require('node:fs');
const path = require('node:path');
const { readEnv, root, mysql } = require('./mysql_admin.cjs');
const config = readEnv();
if (process.argv.includes('--state')) {
  console.log(mysql("SELECT ID,DB,COMMAND,TIME,STATE FROM information_schema.PROCESSLIST WHERE DB LIKE 'edutwin_notify_%';"));
  process.exit(0);
}
if (process.argv.includes('--send-inventory')) {
  console.log(mysql("SELECT 'send_permissions',COUNT(*) FROM permissions WHERE permission_code IN ('notifications.messages.send_scoped','notifications.messages.read_sent_own','platform.notifications.send_scoped','platform.notifications.read_sent_own'); SELECT 'orphan_notification_messages',COUNT(*) FROM notification_messages m LEFT JOIN users u ON u.center_id=m.center_id AND u.user_id=m.sender_user_id WHERE u.user_id IS NULL;", config.MYSQL_DATABASE || 'edutwin'));
  process.exit(0);
}
if (process.argv.includes('--inventory')) {
  console.log(mysql("SELECT 'temporary_notification_schemas',COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'edutwin_notify_%'; SELECT 'notification_messages',COUNT(*) FROM notification_messages; SELECT 'notification_events',COUNT(*) FROM notification_events; SELECT 'notification_deliveries',COUNT(*) FROM notification_deliveries; SELECT 'notification_triggers',COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME LIKE 'trg_notification_%';", config.MYSQL_DATABASE || 'edutwin'));
  process.exit(0);
}
if (!config.MYSQL_ROOT_PASSWORD) throw new Error('Local MySQL test credential is missing.');
const escaped = String(config.MYSQL_ROOT_PASSWORD).replaceAll('"', '""');
const connection = `Server=127.0.0.1;Port=${config.MYSQL_PORT || 3307};User ID=root;Password="${escaped}";Pooling=false;`;
const customFilter = process.argv.find(arg => arg.startsWith('--filter='))?.slice('--filter='.length);
const filter = customFilter || (process.argv.includes('--benchmark') ? 'FullyQualifiedName~NotificationMySqlTests.BusinessPerformance' : process.argv.includes('--teacher-scope') ? 'FullyQualifiedName~TeacherClassStudents_QueryAndArchivedRevalidation_RealSql' : 'FullyQualifiedName~NotificationMySqlTests');
const migrationName = process.argv.includes('--operations') ? 'AddNotificationOperations' : process.argv.includes('--business') ? 'AddNotificationBusinessContextAndActivity' : process.argv.includes('--lifecycle') ? 'AddNotificationLifecycle' : 'AddNotificationManualSend';
const args = process.argv.includes('--generate-migration') ? ['ef', 'migrations', 'add', migrationName, '--project', 'src/EduTwin.DAL', '--output-dir', 'Persistence/Migrations'] :
  ['test', 'tests/EduTwin.BLL.Tests', '--no-restore', '--no-build', '--filter', filter, '-v', 'minimal', ...(process.argv.includes('--benchmark') ? ['--logger', 'console;verbosity=detailed'] : [])];
const result = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8', maxBuffer: 8 * 1024 * 1024,
  env: { ...process.env, ConnectionStrings__Default: connection, EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING: connection } });
const redact = value => String(value || '').replaceAll(connection, '[connection redacted]').replaceAll(config.MYSQL_ROOT_PASSWORD, '[redacted]');
const stdout = redact(result.stdout); const stderr = redact(result.stderr);
// Generated evidence only; no credentials and no test payloads are written to tracked source.
const evidenceDirectory = path.join(root, 'storage', 'verification');
mkdirSync(evidenceDirectory, { recursive: true });
const evidencePath = path.join(evidenceDirectory, `notification-checks-${Date.now()}.log`);
writeFileSync(evidencePath, `${stdout}\n${stderr}`, 'utf8');
console.log(`Redacted test evidence: ${evidencePath}`);
process.stdout.write(stdout.length > 12000 ? stdout.slice(-12000) : stdout);
process.stderr.write(stderr.length > 12000 ? stderr.slice(-12000) : stderr);
process.exitCode = result.status ?? 1;
