const { ChromeClient, loginUser, logoutUser, delay, WEB_URL } = require('./chrome_client.cjs');

async function runGroupD() {
  const client = new ChromeClient();
  await client.start();

  try {
    console.log('=== RUNNING GROUP D: STUDENT - TARGETED DISTRIBUTION & ISOLATION ===');

    // Scenario 16: Log in as target student student05 (Bảo Lễ Hồ)
    console.log('Testing Scenario 16: Verify targeted assignment visibility for student05...');
    await loginUser(client, 'student05');

    await client.navigate(`${WEB_URL}/hoc-tap/bai-tap`);
    await delay(1500);

    // Assert UIACC-ENG-HW-G10 is present
    const isVisible = await client.eval(`Boolean(document.body.innerText.includes('UIACC-ENG-HW-G10'))`);
    if (!isVisible) {
      throw new Error('UIACC-ENG-HW-G10 NOT visible for target student05 (Bảo Lễ Hồ)');
    }

    await client.captureScreenshot('16_student_target_assignment_visible.png');
    console.log('PASS: 16_student_target_assignment_visible.png captured');

    // Scenario 17: Log in as non-target student student01 (Duy Bảo Trịnh)
    console.log('Testing Scenario 17: Verify assignment isolation for non-target student01...');
    await logoutUser(client);
    await loginUser(client, 'student01');

    await client.navigate(`${WEB_URL}/hoc-tap/bai-tap`);
    await delay(1500);

    // Assert UIACC-ENG-HW-G10 is NOT present
    const isHidden = await client.eval(`!Boolean(document.body.innerText.includes('UIACC-ENG-HW-G10'))`);
    if (!isHidden) {
      throw new Error('SECURITY VIOLATION: UIACC-ENG-HW-G10 IS VISIBLE to non-target student01 (Duy Bảo Trịnh)!');
    }

    await client.captureScreenshot('17_student_non_target_assignment_hidden.png');
    console.log('PASS: 17_student_non_target_assignment_hidden.png captured (Assignment completely isolated)');

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
