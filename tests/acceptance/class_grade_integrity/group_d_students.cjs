const { ChromeClient, loginUser, logoutUser, delay, WEB_URL } = require('./chrome_client.cjs');
const { queryRows } = require('./fixture_helper.cjs');

async function runGroupD() {
  const client = new ChromeClient();
  await client.start();

  try {
    console.log('=== RUNNING GROUP D: STUDENT - TARGETED DISTRIBUTION & ISOLATION ===');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 16: Log in as target student student05 (Bảo Lễ Hồ)
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 16: Verify targeted assignment visibility for student05...');
    await loginUser(client, 'student05');

    await client.navigate(`${WEB_URL}/hoc-tap/bai-tap`);
    await delay(2000);

    // Programmatic UI assertion: verify UIACC-ENG-HW-G10 is visible
    const s16Assert = await client.eval(`(() => {
      const text = document.body.innerText;
      const isVisible = text.includes('UIACC-ENG-HW-G10');
      const cards = Array.from(document.querySelectorAll('div, tr, li')).filter(el => el.textContent.includes('UIACC-ENG-HW-G10'));
      return { isVisible, cardCount: cards.length };
    })()`);

    if (!s16Assert.isVisible) {
      throw new Error('[ASSERT FAIL Scenario 16] UIACC-ENG-HW-G10 NOT visible for target student05 (Bảo Lễ Hồ)!');
    }

    // Programmatic DB assertion: verify assignment_targets contains student05
    const targetDbRows = queryRows(`
      SELECT at.assignment_id, at.student_id, a.title
      FROM assignment_targets at
      JOIN assignments a ON at.assignment_id = a.assignment_id
      WHERE a.title = 'UIACC-ENG-HW-G10' AND at.student_id = 'd0000000-0000-0000-0001-000000000008';
    `);
    if (targetDbRows.length === 0) {
      throw new Error('[ASSERT FAIL Scenario 16] Target student05 not found in assignment_targets table in DB!');
    }
    console.log('[ASSERT PASS Scenario 16] Verified: Target student student05 sees assignment on UI and in DB');

    await client.captureScreenshot('16_student_target_assignment_visible.png');
    console.log('PASS: Scenario 16 - 16_student_target_assignment_visible.png captured');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 17: Log in as non-target student student01 (Duy Bảo Trịnh)
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 17: Verify assignment isolation for non-target student01...');
    await logoutUser(client);
    await loginUser(client, 'student01');

    await client.navigate(`${WEB_URL}/hoc-tap/bai-tap`);
    await delay(2000);

    // Programmatic UI assertion: verify UIACC-ENG-HW-G10 is completely hidden
    const s17Assert = await client.eval(`(() => {
      const text = document.body.innerText;
      return { isHidden: !text.includes('UIACC-ENG-HW-G10') };
    })()`);

    if (!s17Assert.isHidden) {
      throw new Error('[ASSERT FAIL Scenario 17] SECURITY VIOLATION: UIACC-ENG-HW-G10 IS VISIBLE to non-target student01 (Duy Bảo Trịnh)!');
    }

    // Programmatic DB assertion: verify assignment_targets does NOT contain student01
    const nonTargetDbRows = queryRows(`
      SELECT at.assignment_id, at.student_id, a.title
      FROM assignment_targets at
      JOIN assignments a ON at.assignment_id = a.assignment_id
      WHERE a.title = 'UIACC-ENG-HW-G10' AND at.student_id = 'd0000000-0000-0000-0001-000000000004';
    `);
    if (nonTargetDbRows.length > 0) {
      throw new Error('[ASSERT FAIL Scenario 17] SECURITY VIOLATION: Non-target student01 found in assignment_targets table in DB!');
    }
    console.log('[ASSERT PASS Scenario 17] Verified: Non-target student student01 has zero access on UI and in DB');

    await client.captureScreenshot('17_student_non_target_assignment_hidden.png');
    console.log('PASS: Scenario 17 - 17_student_non_target_assignment_hidden.png captured (Assignment completely isolated)');

    console.log('=== GROUP D COMPLETED SUCCESSFULLY ===');
  } finally {
    await client.close();
  }
}

if (require.main === module) {
  runGroupD().catch((err) => {
    console.error('Group D failed:', err);
    process.exit(1);
  });
}

module.exports = { runGroupD };
