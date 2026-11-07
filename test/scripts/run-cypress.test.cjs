'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { parseArgs, runSuite } = require('./run-cypress.cjs');

function harness(run) {
  const events = [];
  return {
    events,
    stack: {
      start: async () => { events.push('start'); },
      stop: async () => { events.push('stop'); },
      manifest: () => { events.push('manifest'); return {}; },
    },
    cypress: { run: async options => { events.push('run'); return run(options); } },
  };
}

test('runner restricts CLI spec selection to local Cypress specs', () => {
  for (const spec of ['../other.cy.cjs', 'cypress/e2e/../../other.cy.cjs', 'C:/other.cy.cjs', 'https://example.com/a.cy.cjs']) {
    assert.throws(() => parseArgs(['--spec', spec]));
  }
  assert.equal(parseArgs(['--spec', 'cypress/e2e/auth.cy.cjs']).spec, 'cypress/e2e/auth.cy.cjs');
  assert.throws(() => parseArgs(['--baseUrl', 'http://localhost:3000']));
});

test('a crashed Cypress process still cleans up the stack this run created', async () => {
  const deps = harness(() => { throw new Error('Browser crashed'); });
  await assert.rejects(runSuite(parseArgs([]), deps), /Browser crashed/);
  assert.deepEqual(deps.events, ['start', 'run', 'stop']);
});

test('an existing stack is checked and never removed by a reused run', async () => {
  const deps = harness(() => ({ totalTests: 3, totalFailed: 0, totalPending: 0, totalSkipped: 0 }));
  assert.equal(await runSuite(parseArgs(['--reuse-stack']), deps), 0);
  assert.deepEqual(deps.events, ['manifest', 'run']);
});

test('unverified reuse prevents the browser from starting', async () => {
  const deps = harness(() => assert.fail('Must not run Cypress'));
  deps.stack.manifest = () => { throw new Error('Manifest missing'); };
  await assert.rejects(runSuite(parseArgs(['--reuse-stack']), deps), /Manifest missing/);
  assert.deepEqual(deps.events, []);
});

test('zero execution, config failure, failed, pending, and skipped tests fail the gate', async () => {
  for (const result of [undefined, { status: 'failed', failures: 1 }, { totalTests: 0 },
    { totalTests: 3, totalFailed: 1 }, { totalTests: 3, totalPending: 1 }, { totalTests: 3, totalSkipped: 1 }]) {
    const deps = harness(() => result);
    assert.equal(await runSuite(parseArgs([]), deps), 1);
    assert.deepEqual(deps.events, ['start', 'run', 'stop']);
  }
});

test('startup failure does not try to remove a stack it did not create', async () => {
  const deps = harness(() => assert.fail('Must not run Cypress'));
  deps.stack.start = async () => { throw new Error('Existing manifest'); };
  await assert.rejects(runSuite(parseArgs([]), deps), /Existing manifest/);
  assert.deepEqual(deps.events, []);
});

test('Electron host flag is absent in the browser child and restored after a crash', async () => {
  const previous = process.env.ELECTRON_RUN_AS_NODE;
  process.env.ELECTRON_RUN_AS_NODE = '1';
  try {
    const deps = harness(() => {
      assert.equal(process.env.ELECTRON_RUN_AS_NODE, undefined);
      throw new Error('Spawn failed');
    });
    await assert.rejects(runSuite(parseArgs([]), deps), /Spawn failed/);
    assert.equal(process.env.ELECTRON_RUN_AS_NODE, '1');
    assert.deepEqual(deps.events, ['start', 'run', 'stop']);
  } finally {
    if (previous === undefined) delete process.env.ELECTRON_RUN_AS_NODE;
    else process.env.ELECTRON_RUN_AS_NODE = previous;
  }
});
