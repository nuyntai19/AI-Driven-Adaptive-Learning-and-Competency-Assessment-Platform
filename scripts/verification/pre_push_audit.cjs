// Read-only release inventory and credential scan. Never prints matching values.
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const root = execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim();
const git = args => execFileSync('git', args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
const files = [...new Set([
  ...git(['diff', '--name-only', '-z', 'HEAD']).split('\0'),
  ...git(['ls-files', '--others', '--exclude-standard', '-z']).split('\0')
].filter(Boolean))].sort();
const groups = {}, findings = [], large = [], inventory = [];
let textCount = 0, binaryCount = 0, bytes = 0;
const patterns = [
  ['Google API key', /AIza[A-Za-z0-9_-]{35}/],
  ['Configured Gemini credential', /AQ\.Ab8[A-Za-z0-9_-]{30,}/],
  ['Provider API credential', /(?:gsk_|sk-proj-|sk-ant-)[A-Za-z0-9_-]{30,}/],
  ['Private key material', /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/],
  ['JWT credential', /eyJ[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}/]
];
for (const name of files) {
  const full = path.join(root, name);
  if (!fs.existsSync(full)) { inventory.push({ path: name, deleted: true }); continue; }
  const data = fs.readFileSync(full); bytes += data.length;
  const group = name.startsWith('docs/verification/evidence/') ? 'Verification evidence'
    : name.startsWith('src/') ? name.split('/')[1]
    : name.startsWith('web/') ? 'Web frontend'
    : name.startsWith('tests/') ? 'Backend tests'
    : name.startsWith('docs/') ? 'Reports and plans'
    : name.startsWith('scripts/') ? 'Operational and verification scripts' : 'Root configuration';
  groups[group] = (groups[group] || 0) + 1;
  if (data.length > 1024 * 1024) large.push({ path: name, bytes: data.length });
  const binary = data.includes(0) || /\.(png|jpe?g|webp|gif|pdf|docx|zip)$/i.test(name);
  inventory.push({ path: name, bytes: data.length, binary });
  if (binary) { binaryCount++; continue; }
  textCount++;
  data.toString('utf8').split(/\r?\n/).forEach((line, index) => {
    for (const [kind, regex] of patterns)
      if (regex.test(line)) findings.push({ path: name, line: index + 1, kind });
  });
}
const result = { baseline: git(['rev-parse', '--short', 'HEAD']).trim(), count: files.length,
  textCount, binaryCount, bytes, groups, large, credentialFindings: findings,
  envIgnored: git(['check-ignore', '.env']).trim() === '.env' };
if (process.argv.includes('--inventory')) result.inventory = inventory;
console.log(JSON.stringify(result, null, 2));
process.exitCode = findings.length || !result.envIgnored ? 1 : 0;
