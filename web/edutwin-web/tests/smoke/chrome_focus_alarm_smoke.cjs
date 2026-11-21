// Real headless Chrome, production React components, deterministic HTTP fixtures.
// No primary browser profile, account, database or live API is used by this test.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { spawn, execFileSync } = require('node:child_process');
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = [];
async function main() {
  let vite, chrome, ws, profile;
  const pending = new Map();
  const output = path.resolve(__dirname, '../../../../scratch/focus-alarm-verify');
  fs.mkdirSync(output, { recursive: true });
  try {
    const { createServer } = await import('vite');
    vite = await createServer({ root: path.resolve(__dirname, '../..'), server: { host: '127.0.0.1', port: 0, open: false }, logLevel: 'error' });
    await vite.listen();
    const port = vite.httpServer.address().port;
    const executable = [process.env.CHROME_PATH, 'C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].find(p => p && fs.existsSync(p));
    assert.ok(executable, 'Chrome is required; a missing browser is not PASS.');
    profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_focus_ui_'));
    chrome = spawn(executable, ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`, '--no-first-run', '--no-default-browser-check', '--disable-gpu', 'about:blank'], { stdio: 'ignore', windowsHide: true });
    let launchError; chrome.on('error', e => { launchError = e; });
    const portFile = path.join(profile, 'DevToolsActivePort');
    for (let i = 0; i < 200 && !fs.existsSync(portFile); i++) { if (launchError) throw launchError; await sleep(100); }
    assert.ok(fs.existsSync(portFile), 'Chrome did not expose DevTools.');
    const [debugPort, browserPath] = fs.readFileSync(portFile, 'utf8').trim().split(/\r?\n/);
    ws = new WebSocket(`ws://127.0.0.1:${debugPort}${browserPath}`);
    let id = 0;
    const errors = [];
    ws.onmessage = ({ data }) => {
      const m = JSON.parse(data);
      if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.exception?.description ?? m.params.exceptionDetails.text);
      const call = pending.get(m.id); if (!call) return;
      pending.delete(m.id); clearTimeout(call.timer);
      if (m.error) call.reject(new Error(JSON.stringify(m.error))); else call.resolve(m.result);
    };
    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
    const send = (method, params = {}, sessionId) => new Promise((resolve, reject) => {
      const seq = ++id;
      const timer = setTimeout(() => { pending.delete(seq); reject(new Error('CDP timeout: ' + method)); }, 30000);
      pending.set(seq, { resolve, reject, timer }); ws.send(JSON.stringify({ id: seq, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
    const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
    const page = (method, params) => send(method, params, sessionId);
    await page('Page.enable'); await page('Runtime.enable');
    const evaluate = async expression => {
      const r = await page('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
      return r.result.value;
    };
    const wait = async (expression, label) => {
      for (let i = 0; i < 150; i++) { if (await evaluate(expression)) return; await sleep(150); }
      throw new Error('Timed out: ' + label + '\n' + (await evaluate('document.body.innerText')).slice(-3500) + '\n' + errors.join('\n'));
    };
    const open = async scenario => {
      errors.length = 0;
      await page('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
      await page('Page.navigate', { url: `http://127.0.0.1:${port}/tests/smoke/learning-path-fixture.html?scenario=${scenario}` });
      await wait('Boolean(window.__verification && document.querySelector("#root")?.children.length)', scenario + ' mount');
    };
    const click = async expression => {
      const point = await evaluate(`(() => { const e = ${expression}; if (!e || e.disabled) throw new Error('Missing or disabled control'); e.scrollIntoView({block:'center'}); const r=e.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()`);
      await page('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...point });
      await page('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...point }); await sleep(150);
    };
    const button = label => `Array.from(document.querySelectorAll('button')).find(e => e.textContent.trim() === ${JSON.stringify(label)})`;
    const fill = async (selector, value) => evaluate(`(() => { const e=document.querySelector(${JSON.stringify(selector)}); if(!e) throw new Error('Missing input'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)}); e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true})); })()`);
    const capture = async name => { const r = await page('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }); fs.writeFileSync(path.join(output, name + '.png'), Buffer.from(r.data, 'base64')); };
    const clean = async () => { assert.deepEqual(errors, [], 'Uncaught browser errors'); assert.deepEqual(await evaluate('window.__verification.unknown'), [], 'Unhandled fixture requests'); };
    const check = async (name, action) => { await action(); results.push({ name, kind: 'real-chrome-component-fixture', status: 'PASS' }); console.log('PASS ' + name); };

    await open('focus');
    const launcher = 'document.querySelector(".st-focus-launcher")';
    const closePanel = `document.querySelector('[aria-label="Thu gọn góc tập trung"]')`;
    const timerKey = 'edutwin:focus-timer:fixture-center:fixture-user';
    const saved = `JSON.parse(sessionStorage.getItem(${JSON.stringify(timerKey)}))`;
    const trackAudio = async () => evaluate(`(() => {
      window.__chimeNodes = 0;
      const original = AudioContext.prototype.createOscillator;
      AudioContext.prototype.createOscillator = function() {
        const node = original.call(this); const start = node.start.bind(node);
        node.start = (...args) => { window.__chimeNodes++; start(...args); }; return node;
      }; return true;
    })()`);
    await check('absolute alarm rings once and alerts outside the collapsed panel', async () => {
      await trackAudio(); await click(launcher); await click(button('Giờ báo thức'));
      assert.equal(await evaluate(button('Đặt báo thức')+'.disabled'),true);
      const time = await evaluate('(() => {const d=new Date(Date.now()+120000);return String(d.getHours()).padStart(2,"0")+":"+String(d.getMinutes()).padStart(2,"0");})()');
      await fill('#focus-alarm-time', time);
      await click(button('Nghe thử chuông')); await wait('window.__chimeNodes === 3','real Web Audio chime preview');
      await click(button('Đặt báo thức')); await wait(launcher+'.classList.contains("is-running")','alarm running');
      const state = await evaluate(saved); assert.equal(state.mode,'alarm'); assert.equal(state.alarmTime,time);
      await capture('focus-alarm-setting'); await click(closePanel);
      await evaluate(`Date.now = () => ${state.deadline + 1000}; true`);
      await wait('Boolean(document.querySelector(".st-focus-alert"))','collapsed alarm alert');
      await wait('window.__chimeNodes === 6','completion chime');
      await evaluate('window.dispatchEvent(new Event("focus")); document.dispatchEvent(new Event("visibilitychange")); true');
      await sleep(1000); assert.equal(await evaluate('window.__chimeNodes'),6,'No repeat sound on focus/visibility');
      await capture('focus-alarm-finished'); await click(button('Đóng cảnh báo'));
      await page('Page.reload'); await wait('Boolean(document.querySelector(".st-focus-launcher"))','reload dismissed alarm');
      assert.equal(await evaluate('Boolean(document.querySelector(".st-focus-alert"))'),false,'Dismissal persists');
      await clean();
    });
    await check('alarm cancellation never rings and past times clearly show tomorrow', async () => {
      await open('focus'); await click(launcher); await click(button('Giờ báo thức'));
      const past = await evaluate('(() => {const d=new Date(Date.now()-120000);return String(d.getHours()).padStart(2,"0")+":"+String(d.getMinutes()).padStart(2,"0");})()');
      await fill('#focus-alarm-time',past); await wait('document.body.innerText.includes("(ngày mai)")','next-day explanation');
      await click(button('Đặt báo thức')); await wait(launcher+'.classList.contains("is-running")','tomorrow alarm started');
      const state = await evaluate(saved); await click(button('Hủy báo thức')); await click(closePanel);
      await evaluate(`Date.now = () => ${state.deadline + 1000}; true`); await sleep(700);
      assert.equal(await evaluate('Boolean(document.querySelector(".st-focus-alert"))'),false);
      await clean();
    });
    await check('running clock alarm keeps its exact deadline after reload', async () => {
      await open('focus'); await click(launcher); await click(button('Giờ báo thức'));
      const time = await evaluate('(() => {const d=new Date(Date.now()+120000);return String(d.getHours()).padStart(2,"0")+":"+String(d.getMinutes()).padStart(2,"0");})()');
      await fill('#focus-alarm-time',time); await click(button('Đặt báo thức'));
      const state=await evaluate(saved); const origin=await evaluate('performance.timeOrigin');
      await page('Page.reload'); await wait(`performance.timeOrigin !== ${origin} && ${launcher}?.classList.contains("is-running")`,'clock alarm restored');
      const restored=await evaluate(saved); assert.equal(restored.deadline,state.deadline); assert.equal(restored.alarmTime,time);
      await click(launcher); await click(button('Hủy báo thức')); await click(closePanel); await clean();
    });
    await check('muted countdown still alerts, while pause/reset and reload retain countdown behavior', async () => {
      await open('focus'); await click(launcher); await click(button('Đếm ngược')); await click(button('10 phút'));
      await trackAudio();
      await click('Array.from(document.querySelectorAll("label")).find(e=>e.textContent.includes("Phát chuông khi đến giờ"))');
      await click(button('Bắt đầu tập trung')); await wait(launcher+'.classList.contains("is-running")','countdown started');
      const origin=await evaluate('performance.timeOrigin'); await page('Page.reload');
      await wait(`performance.timeOrigin !== ${origin} && ${launcher}?.classList.contains("is-running")`,'countdown restored');
      await trackAudio(); const beforePause=await evaluate('Date.now()'); await evaluate(`Date.now = () => ${beforePause + 2000}; true`);
      await click(launcher); await click(button('Tạm dừng'));
      assert.equal((await evaluate(saved)).deadline,null);
      await click(button('Tiếp tục')); await click(button('Đặt lại'));
      assert.equal((await evaluate(saved)).remaining,600);
      await click(button('Bắt đầu tập trung')); const state=await evaluate(saved); await click(closePanel);
      await evaluate(`Date.now = () => ${state.deadline + 1000}; true`);
      await wait('Boolean(document.querySelector(".st-focus-alert"))','muted completion alert');
      assert.equal((await evaluate(saved)).soundEnabled,false);
      assert.equal(await evaluate("window.__chimeNodes"),0,"Muted expiry schedules no audio");
      await clean();
    });
    await check('alarm controls fit a narrow student viewport', async () => {
      await open('focus'); await click(launcher); await click(button('Giờ báo thức'));
      await page('Emulation.setDeviceMetricsOverride',{width:375,height:812,deviceScaleFactor:1,mobile:true});
      await fill('#focus-alarm-time','20:30');
      assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth + 1'));
      await capture('focus-alarm-mobile'); await clean();
    });
    fs.writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify({ kind: 'real-chrome-components-with-http-fixtures', liveApiVerified: false, results }, null, 2));
  } catch (error) {
    fs.writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify({ results, status: 'FAIL', error: error.message }, null, 2)); throw error;
  } finally {
    for (const call of pending.values()) clearTimeout(call.timer);
    if (ws?.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({id:999999,method:'Browser.close'}));
      await sleep(1000); ws.close();
    }
    if (chrome && chrome.exitCode === null) {
      if (process.platform === 'win32') {
        try { execFileSync('taskkill',['/PID',String(chrome.pid),'/T','/F'],{stdio:'ignore',windowsHide:true}); } catch { /* The browser may already have exited. */ }
      } else chrome.kill();
      await sleep(500);
    }
    if (vite) await vite.close();
    if (profile && path.dirname(path.resolve(profile)) === path.resolve(os.tmpdir()) && path.basename(profile).startsWith('edutwin_focus_ui_')) {
      for (let i=0;i<60 && fs.existsSync(profile);i++) { try { fs.rmSync(profile, { recursive:true, force:true }); } catch { await sleep(300); } }
      if (fs.existsSync(profile)) throw new Error('Temporary browser profile cleanup failed');
    }
  }
}
main().catch(e => { console.error(e.message); process.exitCode = 1; });
