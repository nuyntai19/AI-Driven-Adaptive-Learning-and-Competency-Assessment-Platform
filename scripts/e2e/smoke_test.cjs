const { ChromeClient, loginUser } = require('./chrome_client.cjs');

(async () => {
  const client = new ChromeClient();
  try {
    console.log('Testing Chrome startup...');
    await client.start();
    console.log('Logging in as student01...');
    await loginUser(client, 'student01');
    const path = await client.eval('window.location.pathname');
    console.log('Logged in path:', path);
    const ss = await client.captureScreenshot('e2e_smoke_login.png');
    console.log('Screenshot saved:', ss);
  } finally {
    await client.close();
  }
})().catch(e => {
  console.error('E2E smoke test error:', e);
  process.exit(1);
});
