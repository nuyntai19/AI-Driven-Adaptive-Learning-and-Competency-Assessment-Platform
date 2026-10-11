// Read-only rollout evidence. Outputs operational counts only, never academic payloads.
const fs = require('node:fs');
const path = require('node:path');
const { root, readEnv, mysql } = require('./mysql_admin.cjs');
const database = readEnv().MYSQL_DATABASE || 'edutwin';
const query = sql => mysql(sql, database).trim();
const monitoring = "('platform.operations.read','platform.ai_usage.read','platform.storage.read')";
const rootId = '00000000-0000-0000-0000-000000000001';
const migration = '20261011085301_AddPlatformOperationalMonitoring';
const result = {
  observedAt: new Date().toISOString(),
  migrationApplied: query(`SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId='${migration}';`) === '1',
  migrationCount: Number(query('SELECT COUNT(*) FROM __EFMigrationsHistory;')),
  permissions: Number(query(`SELECT COUNT(*) FROM permissions WHERE permission_code IN ${monitoring};`)),
  rootGrants: Number(query(`SELECT COUNT(*) FROM role_permissions rp JOIN permissions p USING(permission_id) WHERE rp.center_id='${rootId}' AND rp.account_type='PlatformAdmin' AND p.permission_code IN ${monitoring};`)),
  nonRootGrants: Number(query(`SELECT COUNT(*) FROM role_permissions rp JOIN permissions p USING(permission_id) WHERE (rp.center_id<>'${rootId}' OR rp.account_type<>'PlatformAdmin') AND p.permission_code IN ${monitoring};`)),
  providerCallRows: Number(query('SELECT COUNT(*) FROM platform_ai_calls;')),
  activeAlertRows: Number(query('SELECT COUNT(*) FROM platform_ops_alerts WHERE is_active=1;')),
  storageSnapshot: null,
};
const payload = query("SELECT payload FROM platform_ops_snapshots WHERE snapshot_key='storage';");
if (payload) {
  const data = JSON.parse(payload);
  result.storageSnapshot = {
    capturedAt: data.CapturedAt,
    centerCount: data.Centers.length,
    attachmentCount: data.Centers.reduce((sum, c) => sum + c.AttachmentCount, 0),
    attachmentBytes: data.Centers.reduce((sum, c) => sum + c.AttachmentBytes, 0),
    questionImageCount: data.Centers.reduce((sum, c) => sum + c.QuestionImageCount, 0),
    questionImageBytes: data.Centers.reduce((sum, c) => sum + c.QuestionImageBytes, 0),
    volumeTotalBytes: data.VolumeTotalBytes,
    volumeFreeBytes: data.VolumeFreeBytes,
    backupStatus: data.Backup.Status,
    backupBytes: data.Backup.Bytes,
    latestBackupRestoreVerified: data.Backup.LastRestoreStatus === 'Success' && !!data.Backup.Sha256 && data.Backup.Sha256 === data.Backup.RestoreVerifiedSha256,
  };
}
result.gatesPassed = result.migrationApplied && result.permissions === 3 && result.rootGrants >= 3 && result.nonRootGrants === 0 && !!result.storageSnapshot;
const directory = path.join(root, 'storage', 'verification');
fs.mkdirSync(directory, { recursive: true });
fs.writeFileSync(path.join(directory, `platform-operations-${Date.now()}.json`), JSON.stringify(result, null, 2));
console.log(JSON.stringify(result));
if (!result.gatesPassed) process.exitCode = 1;
