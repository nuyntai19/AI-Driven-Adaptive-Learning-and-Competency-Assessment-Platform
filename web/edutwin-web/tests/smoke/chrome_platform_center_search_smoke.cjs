/**
 * Browser regression checks for platform center automatic search.
 * Uses isolated catalogue fixtures and an ephemeral browser profile; no live API writes.
 */
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');


const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const alive = (pid) => { try { process.kill(pid, 0); return true; } catch { return false; } };

function chromePath() {
  const candidates = process.env.CHROME_PATH ? [process.env.CHROME_PATH] : [
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    path.join(process.env.LOCALAPPDATA || '', 'Google', 'Chrome', 'Application', 'chrome.exe'),
    '/usr/bin/google-chrome', '/usr/bin/chromium',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  ];
  const found = candidates.find((candidate) => fs.existsSync(candidate));
  if (!found) throw new Error('Chrome not found; set CHROME_PATH. Smoke is NOT marked PASS.');
  return found;
}

async function run() {
  let vite, chrome, ws, profile;
  const pending = new Map();
  let failure;
  try {
    const { createServer } = await import('vite');
    vite = await createServer({
      root: path.resolve(__dirname, '../..'),
      server: { host: '127.0.0.1', port: 0, open: false },
      logLevel: 'error',
    });
    await vite.listen();
    const port = vite.httpServer.address().port;
    profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_platform_center_search_'));
    chrome = spawn(chromePath(), [
      '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
      '--no-first-run', '--no-default-browser-check', '--disable-gpu', 'about:blank',
    ], { stdio: ['ignore','ignore',fs.openSync(path.resolve(__dirname,'../../../../scratch/platform-center-search-chrome.log'),'w')], windowsHide: true });
    chrome.on('error', (error) => { failure = error; });
    const portFile = path.join(profile, 'DevToolsActivePort');
    for (let i = 0; i < 100 && !fs.existsSync(portFile); i++) {
      if (failure) throw failure;
      await delay(100);
    }
    if (!fs.existsSync(portFile)) throw new Error('Chrome DevTools startup timed out.');
    const [debugPort, browserPath] = fs.readFileSync(portFile, 'utf8').trim().split(/\r?\n/);
    ws = new WebSocket(`ws://127.0.0.1:${debugPort}${browserPath}`);
    let id = 0;
    ws.onmessage = ({ data }) => {
      const message = JSON.parse(data);
      const request = pending.get(message.id);
      if (!request) return;
      pending.delete(message.id);
      clearTimeout(request.timer);
      if (message.error) request.reject(new Error(JSON.stringify(message.error)));
      else request.resolve(message.result);
    };
    ws.onclose = () => {
      for (const request of pending.values()) {
        clearTimeout(request.timer);
        request.reject(new Error('Chrome disconnected.'));
      }
      pending.clear();
    };
    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
    function send(method, params = {}, sessionId) {
      return new Promise((resolve, reject) => {
        const requestId = ++id;
        const timer = setTimeout(() => { pending.delete(requestId); reject(new Error(`CDP timeout: ${method}`)); }, 30000);
        pending.set(requestId, { resolve, reject, timer });
        ws.send(JSON.stringify({ id: requestId, method, params, ...(sessionId ? { sessionId } : {}) }));
      });
    }
    const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
    const page = (method, params) => send(method, params, sessionId);
    const evaluate = async (expression) => {
      const result = await page('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
      if (result.exceptionDetails) throw new Error(result.exceptionDetails.text + ': ' + result.exceptionDetails.exception?.description);
      return result.result.value;
    };
    console.log('Browser attached');
    await page('Page.enable');
    await page('Emulation.setDeviceMetricsOverride', { width: 1280, height: 920, deviceScaleFactor: 1, mobile: false });
    await page('Page.navigate', { url: 'http://127.0.0.1:'+port+'/tests/smoke/' + 'platform-center-search-fixture.html' });
    const waitFor = async expression => { for (let i=0;i<150;i++) { if (await evaluate(expression)) return; await delay(100); } throw new Error('Timed out: '+expression); };
    const click = async selector => { await evaluate('document.querySelector('+JSON.stringify(selector)+').click()'); await delay(100); };
    const fill = async (selector,value) => { await evaluate('(() => { const e=document.querySelector('+JSON.stringify(selector)+'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,"value").set.call(e,'+JSON.stringify(value)+'); e.dispatchEvent(new Event("input",{bubbles:true})); })()'); await delay(100); };
    await require('./platform-center-search-checks.cjs')({ page, evaluate, waitFor, click, fill, delay, port });
  } catch (error) {
    console.error('Original failure:',error.stack);
    failure = error;
  } finally {
    // Shut down only resources created by this run. Cleanup failures fail the test.
    if (ws?.readyState === WebSocket.OPEN) {
      try { ws.send(JSON.stringify({ id: 999999, method: 'Browser.close' })); } catch { /* verify exit below */ }
      ws.close();
    }
    if (chrome?.pid) {
      for (let i = 0; i < 30 && alive(chrome.pid); i++) await delay(100);
      if (alive(chrome.pid)) {
        try {
          if (process.platform === 'win32') execFileSync('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' });
          else chrome.kill('SIGKILL');
        } catch { /* verify exit below */ }
        for (let i = 0; i < 20 && alive(chrome.pid); i++) await delay(100);
      }
      if (alive(chrome.pid)) failure ||= new Error('Smoke Chrome process leaked.');
    }
    if (profile && path.resolve(profile).startsWith(path.join(os.tmpdir(),'edutwin_platform_center_search_'))) {
      for (let i = 0; i < 10 && fs.existsSync(profile); i++) {
        try { fs.rmSync(profile, { recursive: true, force: true }); } catch { await delay(150); }
      }
      if (fs.existsSync(profile)) failure ||= new Error('Smoke profile cleanup failed.');
    }
    if (vite) await vite.close();
    if (failure) throw failure;
  }
}

run().catch((error) => { console.error(error.stack); process.exitCode = 1; });


