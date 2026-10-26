// Real headless Chrome, production React components, deterministic HTTP fixtures.
// No primary browser profile, account, database or live API is used by this test.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { spawn } = require('node:child_process');
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = [];
async function main() {
  let vite, chrome, ws, profile;
  const pending = new Map();
  const output = path.resolve(__dirname, '../../../../scratch/learning-path-verify');
  fs.mkdirSync(output, { recursive: true });
  try {
    const { createServer } = await import('vite');
    vite = await createServer({ root: path.resolve(__dirname, '../..'), server: { host: '127.0.0.1', port: 0, open: false }, logLevel: 'error' });
    await vite.listen();
    const port = vite.httpServer.address().port;
    const executable = [process.env.CHROME_PATH, 'C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].find(p => p && fs.existsSync(p));
    assert.ok(executable, 'Chrome is required; a missing browser is not PASS.');
    profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_v4_ui_'));
    chrome = spawn(executable, ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`, '--no-first-run', '--no-default-browser-check', '--disable-gpu', 'about:blank'], { stdio: 'ignore' });
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
      throw new Error('Timed out: ' + label + '\n' + (await evaluate('document.body.innerText')).slice(-3500));
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

    await open('goal');
    await check('goal form is open, defaults to whole scope, and rejects a past date', async () => {
      assert.equal(await evaluate('Boolean(document.querySelector("details[open]"))'), true);
      const min = await evaluate('document.querySelector("input[type=date]").min'); assert.match(min, /^\d{4}-\d{2}-\d{2}$/);
      await fill('input[type=date]', '2020-01-01'); assert.equal(await evaluate('document.querySelector("input[type=date]").validity.rangeUnderflow'), true);
    });
    await check('pace, cooldown, preview and acceptance use the changed UI controls', async () => {
      await click("document.querySelectorAll('input[name=path-start-mode]')[1]");
      await fill('input[type=date]', new Date(Date.now() + 7 * 86400000).toISOString().slice(0,10));
      await fill('input[placeholder="Ví dụ: 8"]', '8');
      await evaluate(`(() => {const e=Array.from(document.querySelectorAll('label')).find(e=>e.textContent.includes('Nhịp học')).querySelector('select'); e.value='Accelerated'; e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
      await evaluate(`(() => {const e=Array.from(document.querySelectorAll('label')).find(e=>e.textContent.includes('Chờ trước')).querySelector('select'); e.value='7'; e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
      await click(button('Xem trước lộ trình'));
      await wait('window.__verification.goal != null', 'goal request');
      const goal = await evaluate('window.__verification.goal');
      assert.deepEqual(goal.targetSkillIds.sort(), ['base','target']); assert.equal(goal.schedule.questionReuseDelayDays, 7); assert.equal(goal.targetScore, 8); assert.equal(goal.preferredPace, 'Accelerated');
      await wait(`Array.from(document.querySelectorAll('button')).some(e=>e.textContent==='Chọn tuyến học này')`, 'proposal');
      await click(button('Chọn tuyến học này')); await wait('window.__verification.accepted', 'accepted'); await clean(); await capture('goal-desktop');
    });
    await open('timeline');
    await wait('document.body.innerText.includes("Mốc thi lớp đã hủy")', 'timeline');
    await check('class exam and mock are separate route stages; failed prerequisite is disabled', async () => {
      assert.equal(await evaluate(`document.querySelectorAll('article[aria-label="Mốc thi của lớp"]').length`), 1);
      assert.equal(await evaluate(`document.querySelector('article[aria-label="Mốc thi của lớp"]').querySelectorAll('a').length`), 0);
      assert.ok(await evaluate(`Array.from(document.querySelectorAll('h3')).some(e=>e.textContent==='Thi thử tổng hợp')`));
      const disabled = await evaluate(`(() => {const h=Array.from(document.querySelectorAll('h4')).find(e=>e.textContent==='Rèn phần phụ thuộc');return h.parentElement.querySelector('button').disabled;})()`);
      assert.equal(disabled, true); await clean(); await capture('timeline-desktop');
    });
    await check('student route fits a 375px viewport without horizontal overflow', async () => {
      await page('Emulation.setDeviceMetricsOverride', { width: 375, height: 812, deviceScaleFactor: 1, mobile: true }); await sleep(200);
      assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth + 1')); await capture('timeline-mobile');
    });
    await open('player'); await wait('document.body.innerText.includes("Bỏ qua câu này")', 'player skip');
    await check('skip submits skipped=true and reaches an inconclusive terminal screen', async () => {
      await click(button('Bỏ qua câu này')); await wait('document.body.innerText.includes("Câu đã bỏ qua: chưa đủ bằng chứng.")', 'skip evaluation');
      const submit = await evaluate('window.__verification.requests.find(r=>r.url.endsWith("/submissions"))'); assert.equal(submit.body.skipped, true); await clean(); await capture('skip-terminal');
    });
    await open('expired');
    await check('timer expiry evaluates automatically and closes answer entry', async () => {
      await wait('document.body.innerText.includes("Đã hết giờ. Giữ câu đã nộp.")', 'automatic expiry');
      assert.equal(await evaluate('document.querySelectorAll("textarea").length'), 0); await clean(); await capture('expired-terminal');
    });
    await open('teacher'); await wait('document.body.innerText.includes("Điều chỉnh lịch thi đã công bố")', 'teacher schedule');
    await check('published exam schedule saves a partial PATCH and preserves locked content', async () => {
      const panel = '[aria-label="Điều chỉnh lịch thi đã công bố"]';
      await fill(panel+' input[type=number]', '45'); await click(button('Lưu lịch thi'));
      await wait('document.body.innerText.includes("Đã cập nhật lịch thi.")', 'schedule saved');
      const patch = await evaluate('window.__verification.requests.find(r=>r.method==="patch")');
      assert.equal(patch.body.timeLimitMinutes, 45); assert.equal(patch.body.rowVersion, '11'); assert.equal(patch.body.title, undefined); assert.equal(patch.body.questionIds, undefined);
      await clean(); await capture('teacher-schedule');
    });
    await check('teacher schedule editor fits a 375px viewport', async () => {
      await page('Emulation.setDeviceMetricsOverride', { width: 375, height: 812, deviceScaleFactor: 1, mobile: true }); await sleep(200);
      assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth + 1')); await capture('teacher-mobile');
    });
    await open('teacher-denied');
    await check('teacher without update permission cannot see the schedule editor', async () => {
      await wait('document.body.innerText.includes("Thi giữa kỳ kiểm thử")', 'read-only assignment');
      assert.equal(await evaluate(`document.querySelectorAll('[aria-label="Điều chỉnh lịch thi đã công bố"]').length`), 0); await clean();
    });
    fs.writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify({ kind: 'real-chrome-components-with-http-fixtures', liveApiVerified: false, results }, null, 2));
  } catch (error) {
    fs.writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify({ results, status: 'FAIL', error: error.message }, null, 2)); throw error;
  } finally {
    for (const call of pending.values()) clearTimeout(call.timer);
    if (ws?.readyState === WebSocket.OPEN) ws.close();
    if (chrome && chrome.exitCode === null) { chrome.kill(); await sleep(500); }
    if (vite) await vite.close();
    if (profile && path.dirname(path.resolve(profile)) === path.resolve(os.tmpdir()) && path.basename(profile).startsWith('edutwin_v4_ui_')) {
      try { fs.rmSync(profile, { recursive: true, force: true }); } catch { console.warn('Temporary browser profile retained: ' + profile); }
    }
  }
}
main().catch(e => { console.error(e.message); process.exitCode = 1; });
