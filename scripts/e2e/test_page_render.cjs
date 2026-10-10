const { ChromeClient, loginUser } = require('./chrome_client.cjs');
const { createAssignmentFixture, cleanupFixtures } = require('./fixture_manager.cjs');

(async () => {
  cleanupFixtures();
  const fixture = createAssignmentFixture({
    tag: 'RENDER_TEST',
    title: 'Bài tập kiểm thử tổng hợp',
  });
  console.log('Created fixture:', fixture.assignmentId);

  const client = new ChromeClient();
  try {
    await client.start();
    await loginUser(client, 'student01');
    const playerUrl = `http://localhost:3000/hoc-tap/luyen-tap?assignmentId=${fixture.assignmentId}`;
    console.log('Navigating to player:', playerUrl);
    await client.navigate(playerUrl);

    // Wait for player to render
    await client.waitSelector('button, input, textarea', 15000);
    const title = await client.eval('document.title');
    console.log('Page title:', title);

    const ss = await client.captureScreenshot('player_initial_render.png');
    console.log('Screenshot saved:', ss);

    // Inspect buttons and inputs on the page
    const elementsInfo = await client.eval(`(() => {
      const buttons = Array.from(document.querySelectorAll('button')).map(b => b.innerText.trim()).filter(Boolean);
      const inputs = Array.from(document.querySelectorAll('input, textarea')).map(i => ({
        tag: i.tagName,
        type: i.type,
        placeholder: i.placeholder,
        id: i.id,
        name: i.name
      }));
      return { buttons, inputs };
    })()`);
    console.log('Page elements:', JSON.stringify(elementsInfo, null, 2));

  } finally {
    await client.close();
    cleanupFixtures();
  }
})().catch(e => {
  console.error('Error:', e);
  cleanupFixtures();
  process.exit(1);
});
