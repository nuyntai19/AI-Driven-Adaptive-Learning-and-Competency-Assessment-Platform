/**
 * Browser regression checks for teacher Twin telemetry and individual remediation.
 * Uses isolated catalogue fixtures and an ephemeral browser profile; no live API writes.
 */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const alive = (pid) => { try { process.kill(pid, 0); return true; } catch { return false; } };

function chromePath() {
  const candidates = process.env.CHROME_PATH ? [process.env.CHROME_PATH] : [
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
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
    profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_math_smoke_'));
    chrome = spawn(chromePath(), [
      '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
      '--no-first-run', '--no-default-browser-check', '--disable-gpu', 'about:blank',
    ], { stdio: 'ignore', windowsHide: true });
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
    await page('Page.enable');
    await page('Emulation.setDeviceMetricsOverride', { width: 1280, height: 920, deviceScaleFactor: 1, mobile: false });
    await page('Page.navigate', { url: 'http://127.0.0.1:'+port+'/tests/smoke/twin-remediation-fixture.html' });
    const waitFor = async expression => { for (let i=0;i<150;i++) { if (await evaluate(expression)) return; await delay(100); } throw new Error('Timed out: '+expression); };
    const click = async selector => { await evaluate('document.querySelector('+JSON.stringify(selector)+').click()'); await delay(100); };
    const set = async (selector,value) => { await evaluate('(() => { const e=document.querySelector('+JSON.stringify(selector)+'); const proto=e.tagName==="SELECT"?HTMLSelectElement.prototype:HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto,"value").set.call(e,'+JSON.stringify(value)+'); e.dispatchEvent(new Event(e.tagName==="SELECT"?"change":"input",{bubbles:true})); })()'); await delay(100); };
    const screenshot = async name => { const result=await page('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.resolve(__dirname,'../../../../scratch/'+name+'.png'),Buffer.from(result.data,'base64')); };
    await waitFor("document.querySelector('#assignment-class-select')?.value === 'class1'");
    await set('#title','Bổ trợ riêng');
    await evaluate("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.includes('2. Chọn câu hỏi')).click()");
    await waitFor("document.body.textContent.includes('Câu hỏi')");
    await evaluate("document.querySelector('input[type=checkbox]').click()");
    await evaluate("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.includes('3. Đối tượng')).click()");
    await waitFor("document.querySelectorAll('input[type=checkbox]').length === 2");
    assert.deepEqual(await evaluate("Array.from(document.querySelectorAll('input[type=checkbox]')).map(e=>e.checked)"),[true,false]);
    console.log('PASS B4: paired topic context preserves class and selected student');
    await click('#multiple');
    await waitFor("document.querySelector('#assignment-class-select')?.value === ''");
    await waitFor("document.body.textContent.includes('Vui lòng chọn lớp phù hợp')");
    await set('#assignment-class-select','class2');
    await set('#title','Bổ trợ chọn lớp');
    await evaluate("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.includes('2. Chọn câu hỏi')).click()");
    await waitFor("!!document.querySelector('input[type=checkbox]')");
    await evaluate("document.querySelector('input[type=checkbox]').click()");
    await evaluate("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.includes('3. Đối tượng')).click()");
    await waitFor("document.querySelectorAll('input[type=checkbox]').length === 2");
    assert.deepEqual(await evaluate("Array.from(document.querySelectorAll('input[type=checkbox]')).map(e=>e.checked)"),[true,false]);
    console.log('PASS B4: multiple classes require choice and retain individual recipient');
    await click('#unique');
    await waitFor("document.querySelector('#assignment-class-select')?.value === 'class1'");
    console.log('PASS B4: single matching class preselected for legacy links');
    await click('#twin');
    await waitFor("document.body.textContent.includes('Đúng trong luyện tập')");
    assert.equal(await evaluate("window.smokeState.requests.some(u=>u.includes('/students/me/twin/history'))"),false);
    assert.equal(await evaluate("document.body.textContent.includes('30 giây')"),true);
    console.log('PASS B5: teacher Twin shows adaptive history and telemetry without calling student-self API');
  } catch (error) {
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
          if (process.platform === 'win32') execFileSync('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true });
          else chrome.kill('SIGKILL');
        } catch { /* verify exit below */ }
        for (let i = 0; i < 20 && alive(chrome.pid); i++) await delay(100);
      }
      if (alive(chrome.pid)) failure ||= new Error('Smoke Chrome process leaked.');
    }
    if (profile && path.dirname(path.resolve(profile)) === path.resolve(os.tmpdir()) && path.basename(profile).startsWith('edutwin_math_smoke_')) {
      for (let i = 0; i < 60 && fs.existsSync(profile); i++) {
        try { fs.rmSync(profile, { recursive: true, force: true }); } catch { await delay(300); }
      }
      if (fs.existsSync(profile)) failure ||= new Error('Smoke profile cleanup failed.');
    }
    if (vite) await vite.close();
    if (failure) throw failure;
  }
}

run().catch((error) => { console.error(error.stack); process.exitCode = 1; });
