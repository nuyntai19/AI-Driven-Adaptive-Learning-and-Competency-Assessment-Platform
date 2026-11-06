'use strict';

const path = require('node:path');

const help = `Usage: npm run e2e -- [--reuse-stack] [--headed] [--browser electron|chrome|edge|firefox] [--spec cypress/e2e/example.cy.cjs]

The default run creates and cleans up the repository's disposable acceptance stack.
--reuse-stack keeps an already running, ownership-verified acceptance stack alive.
Build API/web images and start the local MySQL service first; see test/README.md.
`;

function parseArgs(argv) {
  const options = { reuseStack: false, headed: false, browser: 'electron' };
  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index];
    if (arg === '--help' || arg === '-h') return { help: true };
    if (arg === '--reuse-stack') options.reuseStack = true;
    else if (arg === '--headed') options.headed = true;
    else if (arg === '--browser') {
      const browser = argv[++index];
      if (!['electron', 'chrome', 'edge', 'firefox'].includes(browser)) {
        throw new Error('--browser requires electron, chrome, edge, or firefox.');
      }
      options.browser = browser;
    } else if (arg === '--spec') {
      const spec = argv[++index]?.replaceAll('\\', '/');
      if (!spec || !/^cypress\/e2e\/[A-Za-z0-9_/*.,-]+\.cy\.(?:c?js|ts)$/.test(spec)
          || spec.split('/').includes('..')) {
        throw new Error('--spec must name a spec below test/cypress/e2e/.');
      }
      options.spec = spec;
    } else {
      throw new Error(`Unknown option: ${arg}. Use --help.`);
    }
  }
  return options;
}

async function runSuite(options, { stack, cypress }) {
  let createdStack = false;
  try {
    if (options.reuseStack) stack.manifest();
    else {
      await stack.start();
      createdStack = true;
    }
    // Electron-hosted terminals can inherit this flag from their host app. It
    // makes Cypress.exe behave like node.exe and reject --smoke-test/--ping.
    // Clear it only while spawning Cypress, then restore the caller's state.
    const electronRunAsNode = process.env.ELECTRON_RUN_AS_NODE;
    let result;
    try {
      delete process.env.ELECTRON_RUN_AS_NODE;
      result = await cypress.run({
        project: path.resolve(__dirname, '..'),
        configFile: 'cypress.config.cjs',
        browser: options.browser,
        headed: options.headed,
        ...(options.spec ? { spec: options.spec } : {}),
      });
    } finally {
      if (electronRunAsNode === undefined) delete process.env.ELECTRON_RUN_AS_NODE;
      else process.env.ELECTRON_RUN_AS_NODE = electronRunAsNode;
    }
    // A missing browser, config error, or zero selected tests is never a pass.
    if (!result || result.status === 'failed' || result.failures
        || !Number.isInteger(result.totalTests) || result.totalTests < 1) return 1;
    return result.totalFailed > 0 || result.totalPending > 0 || result.totalSkipped > 0 ? 1 : 0;
  } finally {
    if (createdStack) await stack.stop();
  }
}

async function main(argv = process.argv.slice(2)) {
  const options = parseArgs(argv);
  if (options.help) {
    process.stdout.write(help);
    return 0;
  }
  let cypress;
  try { cypress = require('cypress'); }
  catch { throw new Error('Cypress is not installed. Run npm ci inside test/ first.'); }
  const stack = require('../../scripts/e2e/acceptance_stack.cjs');
  return runSuite(options, { stack, cypress });
}

if (require.main === module) {
  main().then(code => { process.exitCode = code; }).catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}

module.exports = { parseArgs, runSuite, main };
