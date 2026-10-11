// Restore rehearsal in a uniquely named disposable schema. Never restores over the live database.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { root, mysql, sqlString, readEnv } = require('./mysql_admin.cjs');
const name = process.argv.find(arg => arg.startsWith('--name='))?.slice(7);
if (!name || !/^[a-z0-9-]{1,80}$/.test(name)) throw new Error('A validated backup name is required.');
const directory = path.join(root, 'storage', 'backups');
const proof = JSON.parse(fs.readFileSync(path.join(directory, `${name}.json`), 'utf8'));
const dump = fs.readFileSync(path.join(directory, `${name}.sql`), 'utf8');
if (crypto.createHash('sha256').update(dump).digest('hex') !== proof.backupSha256) throw new Error('Backup checksum mismatch.');
// A single-database dump must not switch the destination selected by this script.
if (/^\s*(?:USE\s|CREATE\s+DATABASE\s|DROP\s+DATABASE\s)/mi.test(dump)) throw new Error('Database-switching dump is not allowed.');
const schema = `edutwin_notify_restore_${crypto.randomUUID().replaceAll('-', '')}`;
let created = false;
try {
  mysql(`CREATE DATABASE \`${schema}\` CHARACTER SET utf8mb4;`); created = true;
  mysql(dump, schema);
  const tables = Object.keys(proof.tables);
  if (tables.some(table => !/^[a-z_]+$/.test(table))) throw new Error('Invalid protected table name.');
  const restored = Object.fromEntries(tables.map(table => {
    const keys = mysql(`SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=${sqlString(schema)} AND TABLE_NAME=${sqlString(table)} AND INDEX_NAME='PRIMARY' ORDER BY SEQ_IN_INDEX;`).trim().split(/\r?\n/);
    if (!keys.length || keys.some(key => !/^[a-z_]+$/.test(key))) throw new Error('Invalid primary key.');
    const rows = mysql(`SELECT * FROM \`${table}\` ORDER BY ${keys.map(key => `\`${key}\``).join(',')};`, schema);
    return [table, { count: Number(mysql(`SELECT COUNT(*) FROM \`${table}\`;`, schema).trim()), sha256: crypto.createHash('sha256').update(rows).digest('hex') }];
  }));
  const changedTables = tables.filter(table => JSON.stringify(proof.tables[table]) !== JSON.stringify(restored[table]));
  const result = { mode: 'isolated-restore-rehearsal', backupSha256: proof.backupSha256, protectedTables: tables.length, changedTables, restoredExactly: !changedTables.length };
  if (process.argv.includes('--compare-live')) {
    const live = readEnv().MYSQL_DATABASE || 'edutwin';
    const academicChangedTables = tables.filter(table => table !== 'users').filter(table => {
      const keys = mysql(`SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=${sqlString(schema)} AND TABLE_NAME=${sqlString(table)} AND INDEX_NAME='PRIMARY' ORDER BY SEQ_IN_INDEX;`).trim().split(/\r?\n/);
      const rows = mysql(`SELECT * FROM \`${table}\` ORDER BY ${keys.map(key => `\`${key}\``).join(',')};`, live);
      return crypto.createHash('sha256').update(rows).digest('hex') !== proof.tables[table].sha256;
    });
    // Only these three fields may change during the explicit default-role backfill.
    // Identity, password hash, status, tenant and every other user field remain protected.
    const sessionFields = new Set(['auth_version', 'row_version', 'updated_at']);
    const columns = mysql(`SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=${sqlString(schema)} AND TABLE_NAME='users' ORDER BY ORDINAL_POSITION;`).trim().split(/\r?\n/).filter(column => !sessionFields.has(column));
    if (columns.some(column => !/^[a-z_]+$/.test(column))) throw new Error('Invalid user column.');
    const core = database => mysql(`SELECT ${columns.map(column => `\`${column}\``).join(',')} FROM users ORDER BY user_id;`, database);
    const userIdentityDataUnchanged = core(schema) === core(live);
    const versions = database => new Map(mysql('SELECT user_id,auth_version,row_version,updated_at FROM users ORDER BY user_id;', database).trim().split(/\r?\n/).map(row => { const [id, auth, version, updated] = row.split('\t'); return [id, { auth: BigInt(auth), version: BigInt(version), updated }]; }));
    const before = versions(schema); const after = versions(live);
    let changedUserSessionRows = 0;
    const userSessionChangesExpected = before.size === after.size && [...before].every(([id, previous]) => {
      const current = after.get(id);
      if (!current) return false;
      if (current.auth === previous.auth && current.version === previous.version && current.updated === previous.updated) return true;
      changedUserSessionRows++;
      return current.auth === previous.auth + 1n && current.version === previous.version + 1n && current.updated >= previous.updated;
    });
    Object.assign(result, { mode: 'isolated-restore-and-live-comparison', academicChangedTables, userIdentityDataUnchanged, userSessionChangesExpected, changedUserSessionRows,
      liveProtectedDataUnchanged: !academicChangedTables.length && userIdentityDataUnchanged && userSessionChangesExpected });
    if (!result.liveProtectedDataUnchanged) process.exitCode = 1;
  }
  fs.mkdirSync(path.join(root, 'storage', 'verification'), { recursive: true });
  fs.writeFileSync(path.join(root, 'storage', 'verification', `notification-restore-${Date.now()}.json`), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
  if (changedTables.length) process.exitCode = 1;
} finally {
  // Only the fixed-prefix UUID created by this invocation is ever removed.
  if (created && /^edutwin_notify_restore_[a-f0-9]{32}$/.test(schema)) mysql(`DROP DATABASE \`${schema}\`;`);
}
