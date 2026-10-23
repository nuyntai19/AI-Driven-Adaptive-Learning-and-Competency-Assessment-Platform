// Headless browser acceptance against the disposable stack. Uses an owned profile and synthetic accounts only.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn } = require('node:child_process');
const assert = require('node:assert/strict');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const { docker, root } = require('../ops/mysql_admin.cjs');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const base = 'http://127.0.0.1:3002';
async function main() {
  const state = manifest();
  for (const name of [state.api, state.web])
    assert.equal(docker(['inspect', name, '--format', '{{index .Config.Labels "edutwin.e2e.owner"}}']).trim(), state.runId);
  const executable = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    path.join(process.env.LOCALAPPDATA || '', 'Google/Chrome/Application/chrome.exe')].find(p => fs.existsSync(p));
  assert.ok(executable, 'A local Chrome/Edge installation is required; no browser result is fabricated.');
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_scope_browser_'));
  const chrome = spawn(executable, ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`, '--no-first-run',
    '--no-default-browser-check', '--window-size=1440,1000', 'about:blank'], { stdio: 'ignore', windowsHide: true });
  let ws;
  const evidence = path.join(root, 'storage/verification/knowledge-scope-browser'); fs.mkdirSync(evidence, { recursive: true });
  try {
    const portFile = path.join(profile, 'DevToolsActivePort');
    for (let i = 0; i < 200 && !fs.existsSync(portFile); i++) await delay(100);
    assert.ok(fs.existsSync(portFile), 'Owned browser debugging port must become available.');
    const port = fs.readFileSync(portFile, 'utf8').split(/\r?\n/)[0];
    const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
    ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { ws.addEventListener('open', resolve, { once: true }); ws.addEventListener('error', reject, { once: true }); });
    const pending = new Map(); const errors = []; let id = 0;
    ws.addEventListener('message', event => {
      const m = JSON.parse(String(event.data));
      if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); clearTimeout(p.timer); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
      if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.text);
    });
    const cdp = (method, params = {}) => new Promise((resolve, reject) => {
      const requestId = ++id; const timer = setTimeout(() => { pending.delete(requestId); reject(new Error(`CDP timeout: ${method}`)); }, 30000);
      pending.set(requestId, { resolve, reject, timer }); ws.send(JSON.stringify({ id: requestId, method, params }));
    });
    const evaluate = async expression => {
      const r = await cdp('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
      return r.result.value;
    };
    const wait = async (expression, label) => {
      const deadline = Date.now() + 60000;
      while (Date.now() < deadline) { if (await evaluate(expression)) return; await delay(250); }
      fs.writeFileSync(path.join(evidence, 'failure.json'), JSON.stringify(await evaluate('({url:location.href,body:document.body.innerText.slice(0,18000)})'), null, 2));
      throw new Error(`UI timeout: ${label}; inspect failure.json`);
    };
    const setInput = async (selector, value) => evaluate(`(() => {const e=document.querySelector(${JSON.stringify(selector)});if(!e)throw Error('Missing input');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    const grade = async value => evaluate(`(() => {const l=[...document.querySelectorAll('label')].find(l=>l.textContent.includes('Khối học áp dụng'));const s=l?.parentElement.querySelector('select');if(!s)throw Error('Missing grade select');s.value=${JSON.stringify(String(value))};s.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    const shot = async name => { const s = await cdp('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(evidence, name), Buffer.from(s.data, 'base64')); };
    await cdp('Runtime.enable'); await cdp('Page.enable');
    await cdp('Page.navigate', { url: `${base}/login` }); await wait(`!!document.querySelector('#password')`, 'login form');
    await setInput('#centerCode', 'EDUTWIN_A'); await setInput('#username', 'teacher.math'); await setInput('#password', testPassword);
    await evaluate(`document.querySelector('button[type="submit"]').click()`);
    await wait(`!['/login','/dang-nhap'].includes(location.pathname)&&!document.querySelector('#password')`, 'teacher authentication');
    const subject = '30000000-0000-0000-0000-000000000003';
    await cdp('Page.navigate', { url: `${base}/giao-vien/giao-trinh/tao-moi?subjectId=${subject}` });
    await wait(`document.body.innerText.includes('Khối học áp dụng')`, 'curriculum editor');
    await grade(10); await wait(`document.body.innerText.includes('MATH-FUNCTIONS')&&!document.body.innerText.includes('MATH-ANTIDERIVATIVE')`, 'Grade10 topic library');
    await evaluate(`(() => {const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='+ Thêm');if(!b)throw Error('Missing add-topic');b.click();})()`);
    await wait(`document.body.innerText.includes('(1 đã chọn)')`, 'topic selection');
    await grade(12); await wait(`document.body.innerText.includes('MATH-ANTIDERIVATIVE')&&document.body.innerText.includes('không thuộc khung đã chọn')`, 'Grade change conflict');
    assert.ok(await evaluate(`document.body.innerText.includes('Hàm số')&&document.body.innerText.includes('Nguyên hàm')`), 'Keep incompatible old selection alongside new library for explicit repair.');
    const editorGraphLink = await evaluate(`[...document.querySelectorAll('a')].find(a=>a.textContent.includes('Mở Đồ Thị Tri Thức'))?.href`);
    assert.ok(editorGraphLink.includes('gradeLevel=12') && !editorGraphLink.includes('curriculumId='));
    await shot('editor-grade-change-conflict.png');
    await evaluate(`document.querySelector('[role="alert"] button').click()`);
    await wait(`!document.body.innerText.includes('không thuộc khung đã chọn')`, 'explicitly remove incompatible selection');
    await cdp('Page.navigate', { url: `${base}/giao-vien/do-thi-tri-thuc?subjectId=${subject}&classId=51000000-0000-0000-0000-000000000012&academicYear=2026-2027` });
    await wait(`document.body.innerText.includes('Nguyên hàm')&&document.body.innerText.includes('Lớp Toán 12')`, 'class graph');
    assert.ok(await evaluate(`document.body.innerText.includes('2026-2027')`)); await shot('class-12-graph.png');
    await cdp('Page.navigate', { url: `${base}/giao-vien/giao-trinh?subjectId=${subject}` });
    await wait(`!!document.querySelector('[aria-label="Lọc giáo trình theo khối"]')`, 'list filters');
    await evaluate(`(() => {const s=document.querySelector('[aria-label="Lọc giáo trình theo khối"]');s.value='12';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    await wait(`location.search.includes('gradeLevel=12')&&document.body.innerText.includes('Khung mẫu Toán 12')&&!document.body.innerText.includes('Khung mẫu Toán 10')`, 'grade-filtered list');
    await shot('curriculum-list-grade-12.png');
    await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }); await delay(500);
    assert.ok(await evaluate('document.documentElement.scrollWidth<=innerWidth+1'), 'Curriculum list must fit mobile viewport.');
    await shot('curriculum-list-mobile.png'); assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(evidence, 'results.json'), JSON.stringify({ status: 'PASS', database: state.database,
      checks: ['real teacher login', 'grade10 library', 'grade12 library', 'retained incompatible selection', 'explicit repair', 'unsaved scoped graph link', 'class/year graph', 'list grade filter', 'mobile list', 'no browser exception'], screenshots: evidence }, null, 2));
    console.log(JSON.stringify({ browser: 'PASS', evidence }));
  } finally {
    ws?.close(); chrome.kill(); await delay(750);
    const owned = path.resolve(profile);
    assert.ok(owned.startsWith(path.resolve(os.tmpdir()) + path.sep) && path.basename(owned).startsWith('edutwin_scope_browser_'));
    try { fs.rmSync(owned, { recursive: true, force: true, maxRetries: 4, retryDelay: 250 }); } catch { /* Own profile can remain locked briefly. */ }
  }
}
main().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
