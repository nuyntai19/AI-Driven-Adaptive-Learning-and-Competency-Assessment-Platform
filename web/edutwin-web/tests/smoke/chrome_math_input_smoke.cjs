/**
 * Real React/MathLive component smoke, not full actor/API E2E.
 * Starts an isolated Vite server, clicks UI buttons, dispatches native keyboard events,
 * and asserts exact LaTeX plus controlled React state. No credentials or persisted profile.
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
    ], { stdio: 'ignore' });
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
    await page('Page.navigate', { url: `http://127.0.0.1:${port}/tests/smoke/math-input-fixture.html` });
    let ready = false;
    for (let i = 0; i < 100; i++) {
      ready = await evaluate(`Boolean(document.querySelector('math-field')?.shadowRoot?.querySelector('.ML__content'))`);
      if (ready) break;
      await delay(100);
    }
    assert.equal(ready, true, 'Production VisualMathField did not mount.');

    const click = async (selector) => {
      const rect = await evaluate(`(() => {
        const element = document.querySelector(${JSON.stringify(selector)});
        if (!element) throw new Error('Missing UI element');
        const r = element.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
      })()`);
      await page('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...rect });
      await page('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...rect });
      await delay(100);
    };
    const key = async (key, code, windowsVirtualKeyCode, modifiers = 0) => {
      await page('Input.dispatchKeyEvent', { type: 'keyDown', key, code, windowsVirtualKeyCode, modifiers });
      await page('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode, modifiers });
    };
    const type = async (text) => {
      for (const char of text) {
        const code = /^\d$/.test(char) ? `Digit${char}` : `Key${char.toUpperCase()}`;
        await page('Input.dispatchKeyEvent', { type: 'keyDown', key: char, code, text: char, windowsVirtualKeyCode: char.toUpperCase().charCodeAt(0) });
        await page('Input.dispatchKeyEvent', { type: 'keyUp', key: char, code });
      }
      await delay(100);
    };
    const verify = async (expected, name) => {
      const state = await evaluate(`({ field: document.querySelector('math-field').value, react: document.querySelector('#latex').textContent })`);
      assert.equal(state.field, expected, `${name}: MathLive structure`);
      assert.equal(state.react, expected, `${name}: React controlled state`);
      console.log(`PASS ${name}: ${expected}`);
    };

    await click('#sqrt');
    await type('123');
    await verify('\\sqrt{123}', 'toolbar radical + native typing');
    await key('ArrowRight', 'ArrowRight', 39);
    await type('4');
    await verify('\\sqrt{123}4', 'caret exits radical without replacing it');

    await click('#clear');
    await click('#abs');
    await type('45');
    await verify('\\left|45\\right|', 'toolbar absolute value + native typing');

    await click('#clear');
    await click('math-field');
    await type('67');
    await key('a', 'KeyA', 65, 2); // Ctrl+A is a real selection, not a mocked selection property.
    assert.equal(await evaluate(`document.querySelector('math-field').selectionIsCollapsed`), false);
    await click('#sqrt');
    await verify('\\sqrt{67}', 'toolbar wraps selected expression');

    await click('#clear');
    await click('#fraction');
    await type('1');
    await key('Tab', 'Tab', 9);
    await type('2');
    // MathLive serializes single-token numerator/denominator without redundant braces.
    await verify('\\frac12', 'fraction placeholders + native Tab navigation');

    await click('#clear');
    await click('math-field');
    const keyboardToggle = await evaluate(`(() => {
      const b = document.querySelector('math-field').shadowRoot.querySelector('[part~="virtual-keyboard-toggle"]');
      if (!b) throw new Error('MathLive virtual keyboard toggle not found');
      const r=b.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2};
    })()`);
    await page('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...keyboardToggle });
    await page('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...keyboardToggle });
    await delay(300);
    const radicalKey = await evaluate(`(() => {
      const b=Array.from(document.querySelectorAll('[data-keycap-value]')).find(b => {
        const r=b.getBoundingClientRect(); return b.dataset.keycapValue.includes('sqrt') && r.width>0 && r.height>0;
      });
      if (!b) throw new Error('Visible MathLive radical key not found');
      const r=b.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2};
    })()`);
    await page('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...radicalKey });
    await page('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...radicalKey });
    await type('89');
    await verify('\\sqrt{89}', 'real virtual keyboard radical + native typing');

    await click('#clear');
    const textButtons = await evaluate(`Array.from(document.querySelectorAll('button')).map(b => ({text: b.textContent, id:b.id}))`);
    assert(textButtons.some((button) => button.text.includes('Bàn phím')), 'Text mode toggle must exist.');
    // Use a native mouse click on the real component's text-mode toggle.
    const textRect = await evaluate(`(() => { const b=Array.from(document.querySelectorAll('button')).find(b => b.textContent.includes('Bàn phím')); const r=b.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()`);
    await page('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...textRect });
    await page('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...textRect });
    await click('math-field');
    await page('Input.insertText', { text: 'Mã #1 x + y' });
    await delay(100);
    // Assert the production onChange payload, including the existing MathLive plain-text compatibility fix.
    const plain = await evaluate(`document.querySelector('#plain').textContent`);
    assert.equal(plain, 'Mã #1 x + y', 'Submitted text-mode whitespace and Vietnamese prose must be exact.');
    console.log('PASS native text input: exact Vietnamese prose, hash and whitespace');
    console.log('ALL PRODUCTION MATH INPUT SMOKE CHECKS PASSED');
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
          if (process.platform === 'win32') execFileSync('taskkill', ['/PID', String(chrome.pid), '/T', '/F'], { stdio: 'ignore' });
          else chrome.kill('SIGKILL');
        } catch { /* verify exit below */ }
        for (let i = 0; i < 20 && alive(chrome.pid); i++) await delay(100);
      }
      if (alive(chrome.pid)) failure ||= new Error('Smoke Chrome process leaked.');
    }
    if (profile) {
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
