const { ChromeClient, loginUser } = require('./chrome_client.cjs');
const { createAssignmentFixture, cleanupFixtures } = require('./fixture_manager.cjs');

(async () => {
  cleanupFixtures();
  const fixture = createAssignmentFixture({
    tag: 'DEBUG',
    title: 'Debug Assignment Render',
  });

  const client = new ChromeClient();
  try {
    await client.start();
    await loginUser(client, 'student01');
    const playerUrl = `http://localhost:3000/hoc-tap/luyen-tap?assignmentId=${fixture.assignmentId}`;
    console.log('Navigating to:', playerUrl);
    await client.navigate(playerUrl);
    await require('./chrome_client.cjs').delay(3000);

    const currentUrl = await client.eval('window.location.href');
    console.log('Current URL:', currentUrl);
    const bodyHtml = await client.eval('document.body.innerHTML');
    console.log('Body HTML length:', bodyHtml.length);
    console.log('Body HTML snippet:', bodyHtml.slice(0, 500));
    console.log('Console logs:', client.consoleLogs);
    console.log('Page errors:', client.pageErrors);
    console.log('Network errors:', client.networkErrors);

    await client.captureScreenshot('debug_player_page.png');
  } finally {
    await client.close();
    cleanupFixtures();
  }
})().catch(console.error);
