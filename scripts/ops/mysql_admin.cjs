const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '../..');
function readEnv() {
  const values = {};
  for (const line of fs.readFileSync(path.join(root, '.env'), 'utf8').split(/\r?\n/)) {
    const match = line.match(/^\s*([^#=]+)=(.*)$/);
    if (!match) continue;
    values[match[1].trim()] = match[2].trim().replace(/^(['"])(.*)\1$/, '$2');
  }
  return values;
}
function docker(args, options = {}) {
  const result = spawnSync('docker', args, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, ...options });
  if (result.error) throw new Error(`Docker could not start: ${result.error.code}`);
  if (result.status !== 0) throw new Error(`Docker operation failed (${result.status}): ${result.stderr?.slice(0, 1500)}`);
  return result.stdout;
}
function mysql(sql, database, extraArgs = []) {
  const config = readEnv();
  if (!config.MYSQL_ROOT_PASSWORD) throw new Error('Local MySQL credential is not configured.');
  return docker(['exec', '-i', '-e', 'MYSQL_PWD', `${config.COMPOSE_PROJECT_NAME || 'edutwin'}-mysql`,
    'mysql', '-u', 'root', '--default-character-set=utf8mb4', '--batch', '--raw', '--skip-column-names',
    ...extraArgs, ...(database ? [database] : [])],
  { input: sql, env: { ...process.env, MYSQL_PWD: config.MYSQL_ROOT_PASSWORD } });
}
function sqlString(value) {
  // Encoding avoids quote/backslash interpretation and preserves Unicode.
  return `CONVERT(0x${Buffer.from(String(value), 'utf8').toString('hex')} USING utf8mb4)`;
}
module.exports = { root, readEnv, docker, mysql, sqlString };
