// Runs synthetic integration tests in throwaway databases created/disposed by the test fixture.
// Credentials remain in the child environment; never print the environment/connection string.
const { spawnSync } = require('node:child_process');
const { readEnv, root } = require('./ops/mysql_admin.cjs');
const config = readEnv();
if (!config.MYSQL_ROOT_PASSWORD) throw new Error('Local test database credential is missing.');
const quoted = String(config.MYSQL_ROOT_PASSWORD).replaceAll('"', '""');
const connection = `Server=127.0.0.1;Port=${config.MYSQL_PORT || 3307};User ID=root;Password="${quoted}";Pooling=false;`;
const filter = process.argv[2] || 'FullyQualifiedName~AIAnalysisJobProcessorMySqlTests';
const buildArgs = process.argv.includes('--no-build') ? ['--no-build'] : [];
const result = spawnSync('dotnet', ['test', 'tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj', '--no-restore',
  ...buildArgs, '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '--filter', filter, '-v', 'quiet',
  '--logger', 'trx;LogFileName=ai-processing.trx', '--results-directory', 'tests/TestResults/ai-processing'], {
  cwd: root, stdio: 'inherit', env: { ...process.env, EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING: connection },
});
process.exitCode = result.status ?? 1;
