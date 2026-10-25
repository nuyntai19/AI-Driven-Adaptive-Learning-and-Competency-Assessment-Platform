// Build prerequisite gate and an honest case-by-case E2E execution report.
// A successful gate does not execute browser checks or claim API/browser PASS.
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync, spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8' }).trim();
const branch = git('branch', '--show-current');
const requiredBranch = 'origin/SkinnySnail/adaptive-practice';
if (branch !== requiredBranch) {
  console.log(JSON.stringify({ status: 'SKIPPED', branch, reason: `Requires branch ${requiredBranch}` }));
  process.exit(0);
}

const revision = git('rev-parse', 'HEAD');
const args = process.argv.slice(2);
const date = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
const outputArgument = args.indexOf('--results-dir');
if (outputArgument >= 0 && !args[outputArgument + 1]) throw new Error('--results-dir requires a path.');
const output = path.resolve(outputArgument >= 0 ? args[outputArgument + 1]
  : path.join(root, 'docs/verification', `adaptive-practice-e2e-${date}`));
fs.mkdirSync(output, { recursive: true });
const sourceFile = path.join(output, 'build-source-revision.txt');
const readExistingFailure = args.includes('--from-failed-build-logs');
if (readExistingFailure) {
  if (fs.readFileSync(sourceFile, 'utf8').trim() !== revision)
    throw new Error('Existing build logs are not from the current source revision.');
} else fs.writeFileSync(sourceFile, revision + '\n');

const buildDefinitions = [
  { id: 'BUILD_API', file: 'api', context: '.', dockerfile: 'src/EduTwin.API/Dockerfile', image: `edutwin-adaptive-e2e-api:${revision.slice(0, 7)}` },
  { id: 'BUILD_WEB', file: 'web', context: 'web/edutwin-web', dockerfile: 'web/edutwin-web/Dockerfile', image: `edutwin-adaptive-e2e-web:${revision.slice(0, 7)}` },
];
const builds = [];
for (const build of buildDefinitions) {
  const logFile = path.join(output, `${build.file}-build.txt`);
  const exitFile = path.join(output, `${build.file}-build-exit-code.txt`);
  let exitCode;
  if (readExistingFailure) exitCode = Number(fs.readFileSync(exitFile, 'utf8').trim());
  else {
    console.log(`Building ${build.id} from ${revision}...`);
    const result = spawnSync('docker', ['build', '--progress=plain', '--label',
      `org.opencontainers.image.revision=${revision}`, '-f', build.dockerfile, '-t', build.image, build.context],
    { cwd: root, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    exitCode = result.status ?? 1;
    fs.writeFileSync(logFile, [result.stdout, result.stderr, result.error?.message].filter(Boolean).join('\n'));
    fs.writeFileSync(exitFile, String(exitCode) + '\n');
  }
  if (!Number.isInteger(exitCode)) throw new Error(`Invalid ${build.id} exit code.`);
  const log = fs.readFileSync(logFile, 'utf8');
  const errorPattern = build.file === 'api' ? /(\/[^\s]+\.cs\(\d+,\d+\): error CS\d+:[^\r\n]+)/g
    : /(src\/[^\s]+\.tsx?\(\d+,\d+\): error TS\d+:[^\r\n]+)/g;
  const compilerErrors = [...new Set([...log.matchAll(errorPattern)].map(match => match[1]))];
  builds.push({ id: build.id, status: exitCode === 0 ? 'PASS' : 'FAIL', exitCode,
    image: build.image, evidence: path.basename(logFile), compilerErrorCount: compilerErrors.length, compilerErrors });
}
if (readExistingFailure && builds.every(build => build.exitCode === 0))
  throw new Error('--from-failed-build-logs is only for documenting failed build prerequisites.');

const apiCatalog = JSON.parse(execFileSync(process.execPath,
  [path.join(__dirname, 'adaptive_practice_api_acceptance.cjs'), '--list'], { cwd: root, encoding: 'utf8' }));
const browserCatalog = [
  ['APWEB01', 'Login and open adaptive overview; show a goal before a question'],
  ['APWEB02', 'Switch Weakness, LearningPath and CustomTopic modes'],
  ['APWEB03', 'Select subject, topic and 10/15/20 minute budget; start session'],
  ['APWEB04', 'Question, progress, selection reason and answer controls render'],
  ['APWEB05', 'Request progressive hints and verify support usage'],
  ['APWEB06', 'Submit an answer; verify graded feedback and next question'],
  ['APWEB07', 'Pause, reload, resume and retain question/progress/timer'],
  ['APWEB08', 'Voluntarily stop and distinguish incomplete from achieved'],
  ['APWEB09', 'Summary counts, skill evaluation and next action match API data'],
  ['APWEB10', 'Create follow-up session and retain linked prior history'],
  ['APWEB11', 'Reopen history and a closed session without issuing new questions'],
  ['APWEB12', 'Teacher sees practice monitoring within the assigned class'],
].map(([id, name]) => ({ id, name }));
const buildFailure = builds.some(build => build.status === 'FAIL');
const reason = buildFailure ? 'Current source cannot produce API/web images. E2E runtime was not started.'
  : 'Build prerequisites passed; HTTP/MySQL suite and approved browser interactions have not been run by this gate.';
const cases = [
  ...apiCatalog.cases.map(item => ({ ...item, kind: 'HTTP/MySQL integration' })),
  ...browserCatalog.map(item => ({ ...item, kind: 'Browser E2E' })),
].map(item => ({ ...item, status: buildFailure ? 'BLOCKED' : 'NOT_RUN', reason }));
const report = { generatedAt: new Date().toISOString(), timezone: 'Asia/Saigon', branch, sourceRevision: revision,
  status: buildFailure ? 'BLOCKED_BEFORE_E2E' : 'READY_FOR_E2E', builds,
  functionalTotals: { executed: 0, passed: 0, failed: 0, blocked: buildFailure ? cases.length : 0,
    notRun: buildFailure ? 0 : cases.length }, cases,
  limitations: ['No API/MySQL or browser E2E case was executed by this prerequisite gate.',
    'Existing application images were not used as evidence for this source revision.',
    'No test schema was created and no student data was written.'] };
fs.writeFileSync(path.join(output, 'e2e-results.json'), JSON.stringify(report, null, 2) + '\n');
const escape = value => String(value).replaceAll('|', '\\|').replaceAll('\n', ' ');
const markdown = [
  `# Adaptive Practice E2E – ${date}`,
  '', `Branch: \`${branch}\`. HEAD: \`${revision}\`.`, '',
  buildFailure ? '**BLOCKED: chưa thể thực thi E2E trên source hiện tại vì build Docker thất bại.**'
    : '**Build đã qua; các test nghiệp vụ vẫn NOT_RUN cho tới khi thực thi HTTP và browser checks.**',
  '', '| Kiểm tra điều kiện chạy | Kết quả | Lỗi biên dịch phân biệt | Log |', '|---|---|---:|---|',
  ...builds.map(build => `| ${build.id} | ${build.status} (exit ${build.exitCode}) | ${build.compilerErrorCount} | [Log](${build.evidence}) |`),
  '', `Đã chạy **0 test chức năng**. ${apiCatalog.cases.length} ca HTTP/MySQL và ${browserCatalog.length} ca browser ${buildFailure ? 'BLOCKED' : 'NOT_RUN'}. ` +
    'Lỗi build là lỗi điều kiện khởi chạy, không được tính thành FAIL hay PASS cho các luồng chức năng.',
  '', '| Mã | Loại | Kịch bản | Kết quả |', '|---|---|---|---|',
  ...cases.map(item => `| ${item.id} | ${item.kind} | ${escape(item.name)} | ${item.status} |`),
  '', 'Không tạo schema test hay ghi dữ liệu học sinh. Không dùng image cũ để suy ra kết quả của HEAD này.',
  '', 'Sau khi sửa build: chạy gate lại, khởi tạo acceptance stack bằng đúng image vừa build, chạy API suite rồi kiểm tra trình duyệt theo README. ' +
    'API suite là kiểm tra HTTP/MySQL; chưa thay thế browser E2E.',
  '', '[Chi tiết nguyên nhân và cách chạy lại](README.md) · [Kết quả JSON](e2e-results.json)', '',
].join('\n');
fs.writeFileSync(path.join(output, 'RESULTS.md'), markdown);
console.log(JSON.stringify({ branch, sourceRevision: revision, status: report.status,
  builds: builds.map(({ id, status, compilerErrorCount }) => ({ id, status, compilerErrorCount })),
  functionalTotals: report.functionalTotals, output }, null, 2));
if (buildFailure) process.exitCode = 1;
