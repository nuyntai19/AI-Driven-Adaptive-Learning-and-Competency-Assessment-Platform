const { runGroupA } = require('./group_a_class_lifecycle.cjs');
const { runGroupB } = require('./group_b_curriculums_questions.cjs');
const { runGroupC } = require('./group_c_assignments.cjs');
const { runGroupD } = require('./group_d_students.cjs');
const { runGroupE } = require('./group_e_security.cjs');
const {
  takeDatabaseSnapshot,
  assertDatabaseSnapshotUnchanged,
  resetRunRegistry,
  cleanupRunFixtures,
  cleanPriorRunFixturesIfAny,
} = require('./fixture_helper.cjs');

async function main() {
  const startTime = Date.now();
  console.log('========================================================================');
  console.log('   EDUTWIN POST-R09 CLASS & GRADE INTEGRITY ACCEPTANCE TEST SUITE');
  console.log('   Target: Real Live Stack (Backend API: 5000, Web Frontend: 3000)');
  console.log('   Branch: student/answer | Mode: Headless Chrome CDP');
  console.log('========================================================================\n');

  // 1. Clean prior leftover test IDs if an earlier process crashed, and reset registry
  cleanPriorRunFixturesIfAny();
  resetRunRegistry();

  // 2. Capture baseline snapshot of non-fixture data to verify zero contamination
  const baselineSnapshot = takeDatabaseSnapshot();
  console.log('[INTEGRITY] Baseline snapshot taken before run:', JSON.stringify(baselineSnapshot));

  const suiteResults = [];

  const groups = [
    { name: 'Group A: Center Manager - Class Lifecycle & Grade Integrity (01-08)', runner: runGroupA },
    { name: 'Group B: Teacher - Curriculums & Question Grade Integrity (09-12)', runner: runGroupB },
    { name: 'Group C: Teacher - Assignment Creation & Validation Guards (13-15)', runner: runGroupC },
    { name: 'Group D: Student - Targeted Distribution & Isolation (16-17)', runner: runGroupD },
    { name: 'Group E: Security Auditor - Access Control & Backend Security Audit (18)', runner: runGroupE },
  ];

  try {
    for (const group of groups) {
      const groupStart = Date.now();
      console.log(`\n>>> STARTING: ${group.name}`);
      try {
        await group.runner();
        const duration = ((Date.now() - groupStart) / 1000).toFixed(1);
        console.log(`>>> FINISHED: ${group.name} [PASSED] in ${duration}s\n`);
        suiteResults.push({ group: group.name, status: 'PASSED', duration: `${duration}s` });
      } catch (err) {
        const duration = ((Date.now() - groupStart) / 1000).toFixed(1);
        console.error(`>>> FAILED: ${group.name} in ${duration}s:`, err);
        suiteResults.push({ group: group.name, status: 'FAILED', error: err.message, duration: `${duration}s` });
        throw err;
      }
    }
  } finally {
    // 3. Clean up ONLY entities with registered IDs created during this run
    cleanupRunFixtures();

    // 4. Verify non-fixture database records remain completely untouched
    assertDatabaseSnapshotUnchanged(baselineSnapshot);
  }

  const totalDuration = ((Date.now() - startTime) / 1000).toFixed(1);
  console.log('========================================================================');
  console.log('   FINAL ACCEPTANCE RESULTS: 18/18 SCENARIOS COMPLETED');
  console.log('========================================================================');
  console.table(suiteResults);
  console.log(`Total Execution Time: ${totalDuration}s`);
  console.log('All authentic screenshots captured in docs/verification/evidence/class-grade-integrity-2026-10-04/');
  console.log('========================================================================\n');
}

if (require.main === module) {
  main().catch((err) => {
    console.error('Master runner failed:', err);
    process.exit(1);
  });
}

module.exports = { main };
