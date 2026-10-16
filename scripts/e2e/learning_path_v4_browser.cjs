// Headless Chrome UI checks against the disposable acceptance stack only. No retired harness or primary credentials.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn } = require('node:child_process');
const assert = require('node:assert/strict');
const { manifest, testPassword } = require('./acceptance_stack.cjs');
const delay = ms => new Promise(r => setTimeout(r, ms));
const base = 'http://127.0.0.1:3002';
async function main() {
  manifest();
  const executable = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'].find(p => fs.existsSync(p));
  assert.ok(executable, 'A local Chrome/Edge installation is required');
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_v4_browser_'));
  const chrome = spawn(executable, ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`, '--no-first-run', '--no-default-browser-check', '--window-size=1440,1000', 'about:blank'], { stdio: 'ignore', windowsHide: true });
  let ws;
  try {
    const portFile = path.join(profile, 'DevToolsActivePort');
    for (let i = 0; i < 200 && !fs.existsSync(portFile); i++) await delay(100);
    assert.ok(fs.existsSync(portFile), 'Chrome must expose its owned debugging endpoint');
    const port = fs.readFileSync(portFile, 'utf8').split(/\r?\n/)[0];
    const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
    ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise((r, reject) => { ws.addEventListener('open', r, { once: true }); ws.addEventListener('error', reject, { once: true }); });
    let nextId = 0; const pending = new Map(); const browserErrors = [];
    ws.addEventListener('message', event => {
      const message = JSON.parse(String(event.data));
      if (message.id && pending.has(message.id)) { const p = pending.get(message.id); pending.delete(message.id); clearTimeout(p.timer); message.error ? p.reject(new Error(message.error.message)) : p.resolve(message.result); }
      if (message.method === 'Runtime.exceptionThrown') browserErrors.push(message.params.exceptionDetails.text);
    });
    const cdp = (method, params = {}) => new Promise((resolve, reject) => {
      const id = ++nextId; const timer = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 30000);
      pending.set(id, { resolve, reject, timer }); ws.send(JSON.stringify({ id, method, params }));
    });
    const evaluate = async expression => {
      const r = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
      if (r.exceptionDetails) {
        const debug=await cdp('Runtime.evaluate',{expression:'({url:location.href,body:document.body.innerText.slice(0,14000)})',returnByValue:true});
        fs.writeFileSync(path.join(__dirname,'../../scratch/v4-browser-debug.json'),JSON.stringify(debug.result.value,null,2));
        throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
      } return r.result.value;
    };
    const wait = async (expression, label, limit = 60000) => {
      const end = Date.now() + limit; while (Date.now() < end) { if (await evaluate(expression)) return; await delay(250); }
      fs.writeFileSync(path.join(__dirname, '../../scratch/v4-browser-debug.json'), JSON.stringify(await evaluate(`({url:location.href,body:document.body.innerText.slice(0,10000)})`), null, 2));
      throw new Error(`UI timeout: ${label}; inspect scratch/v4-browser-debug.json`);
    };
    const click = async label => {
      const expression = `(() => { const b = [...document.querySelectorAll('button')].find(b => !b.disabled && b.textContent.includes(${JSON.stringify(label)})); if (!b) return false; b.click(); return true; })()`;
      await wait(expression, `click ${label}`);
    };
    const setInput = async (selector, value) => evaluate(`(() => { const e=document.querySelector(${JSON.stringify(selector)}); if(!e) return false; const proto=e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto,'value').set.call(e,${JSON.stringify(value)}); e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true})); return true; })()`);
    await cdp('Runtime.enable'); await cdp('Page.enable');
    await cdp('Page.navigate', { url: `${base}/login` });
    await wait(`!!document.querySelector('#centerCode')`, 'login form');
    await setInput('#centerCode', process.argv.includes('--center-b') ? 'EDUTWIN_B' : 'EDUTWIN_A'); await setInput('#username', 'student01'); await setInput('#password', testPassword);
    await evaluate(`document.querySelector('button[type="submit"]').click()`);
    await wait(`!['/login','/dang-nhap'].includes(location.pathname) && !document.querySelector('#password')`, 'real authenticated login');
    // Fetch subject/context using the browser's own authenticated cookie session through normal APIs.
    // The isolated API check stores only subject/scope IDs, never a token.
    const scopeFile = path.join(__dirname, '../../scratch/v4-browser-scope.json');
    const scope = JSON.parse(fs.readFileSync(scopeFile, 'utf8'));
    // Regression: normal navigation can omit classId; the hook must use the
    // current class returned by academic-context instead of sending classId: "".
    const route = `${base}/hoc-tap/lo-trinh-hoc-tap?subjectId=${scope.subjectId}`;
    await cdp('Page.navigate', { url: route });
    await wait(`document.body.innerText.includes('Lộ trình học tập cá nhân') && [...document.querySelectorAll('button')].some(b=>b.textContent.includes('Thiết lập mục tiêu')&&!b.disabled)`, 'new path page');
    await click('Xem tuyến đề xuất');
    await wait(`!!document.querySelector('[role="dialog"]') && document.querySelector('[role="dialog"]').innerText.includes('Chọn tuyến học này')`, 'automatic complete recommendation without filling a form');
    assert.ok(!await evaluate(`!!document.querySelector('[role="dialog"] form')`));
    assert.ok(await evaluate(`getComputedStyle(document.querySelector('[role="dialog"]').parentElement).backgroundColor==='rgba(0, 0, 0, 0)'`), 'Modal must have a transparent outside area');
    await evaluate(`(() => {const body=document.querySelector('[role="dialog"] > div');body.scrollTop=body.scrollHeight;})()`);
    assert.ok(await evaluate(`(() => {const r=document.querySelector('[aria-label="Đóng"]').getBoundingClientRect();return r.top>=0&&r.bottom<innerHeight;})()`), 'Close stays visible when proposal content scrolls');
    await evaluate(`document.querySelector('[role="dialog"] > div').scrollTop=0`);
    assert.ok(await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Bài làm cho thấy') && document.querySelector('[role="dialog"]').innerText.includes('Đề xuất cải thiện') && document.querySelector('[role="dialog"]').innerText.includes('Phần nên học')`));
    assert.ok(await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Xác định điểm xuất phát')`), 'No-data learner receives diagnostic, not an unsupported advanced claim');
    const autoDir=path.join(__dirname,'../../storage/v4-browser-evidence');fs.mkdirSync(autoDir,{recursive:true});
    const autoShot=await cdp('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(autoDir,'auto-proposal-desktop.png'),Buffer.from(autoShot.data,'base64'));
    await evaluate(`document.querySelector('[aria-label="Đóng"]').click()`);
    await click('Thiết lập mục tiêu'); await wait(`!!document.querySelector('[role="dialog"] form')`, 'goal form');
    await click('Xem trước lộ trình'); await wait(`document.body.innerText.includes('Chọn tuyến học này')`, 'real proposal preview');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Mệnh đề quan hệ')`), 'English preview must include other real topics');
    assert.ok(!await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('MATH-EQ-')`), 'No hardcoded math path');
    await click('Điều chỉnh mục tiêu');
    await setInput('[role="dialog"] input[type="date"]', '2026-10-30');
    await evaluate(`(() => { const l=[...document.querySelectorAll('[role="dialog"] label')].find(l=>l.textContent.trim()==='Thì'); l.querySelector('input').click(); })()`);
    await click('Xem trước lộ trình'); await wait(`document.body.innerText.includes('Chọn tuyến học này')`, 'deadline and selected skill preview');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Thì')`));
    assert.ok(!await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Mệnh đề quan hệ')`), 'Selecting only tense must not silently include unrelated topics');
    await click('Chọn tuyến học này'); await wait(`!document.querySelector('[role="dialog"]')`, 'proposal accepted');
    assert.ok(await evaluate(`document.body.innerText.includes('Các nhiệm vụ của tuyến đã được cập nhật')`), 'Successful accept has explicit confirmation');
    await click('Mở nhiệm vụ'); await wait(`!!document.querySelector('[role="dialog"] textarea')`, 'actual study renderer');
    assert.ok(await evaluate(`location.pathname.includes('lo-trinh-hoc-tap')`), 'Mission must not navigate to generic adaptive practice');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Tìm hiểu và ví dụ')`));
    await setInput('[role="dialog"] textarea', 'Ghi chú kiểm thử trình duyệt: tôi muốn ôn cách dùng trong câu.');
    await wait(`document.querySelector('[role="dialog"]').innerText.includes('Đã lưu bản nháp trên server')`, 'server autosave');
    const evidenceDir = path.join(__dirname, '../../storage/v4-browser-evidence'); fs.mkdirSync(evidenceDir, { recursive: true });
    const shot = await cdp('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(evidenceDir, 'study-desktop.png'), Buffer.from(shot.data, 'base64'));
    await cdp('Page.reload', { ignoreCache: true }); await wait(`document.body.innerText.includes('Tiếp tục đúng tiến độ')`, 'server run after reload');
    await click('Tiếp tục đúng tiến độ'); await wait(`!!document.querySelector('[role="dialog"] textarea')`, 'resumed activity');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"] textarea').value.includes('Ghi chú kiểm thử trình duyệt')`), 'Restored draft comes from server');
    await evaluate(`document.querySelector('[aria-label="Đóng và giữ tiến độ"]').click()`);
    await wait(`!document.querySelector('[role="dialog"]')`, 'close unfinished study');
    await click('Xem tuyến đề xuất');
    await wait(`!!document.querySelector('[role="dialog"] footer button')`, 'replacement preview footer');
    await evaluate(`(() => {const body=document.querySelector('[role="dialog"] header').nextElementSibling; body.scrollTop=body.scrollHeight;})()`);
    await click('Chọn tuyến học này');
    await wait(`document.querySelector('[role="dialog"] footer [role="alert"]')?.innerText.includes('Tìm hiểu:')`, 'accept conflict names blocking study next to action');
    assert.ok(await evaluate(`(() => {const r=document.querySelector('[role="dialog"] footer [role="alert"]').getBoundingClientRect();return r.top>=0&&r.bottom<=innerHeight;})()`), 'Error remains visible after preview scroll');
    const blockedShot=await cdp('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(evidenceDir,'proposal-blocked-visible.png'),Buffer.from(blockedShot.data,'base64'));
    await click('Tiếp tục buổi này');
    await wait(`!!document.querySelector('[role="dialog"] textarea')`, 'resume from proposal conflict');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"] textarea').value.includes('Ghi chú kiểm thử trình duyệt')`));
    await evaluate(`document.querySelector('[aria-label="Đóng và giữ tiến độ"]').click()`);
    await wait(`!document.querySelector('[role="dialog"]')`, 'close study again');
    await evaluate(`(() => { const h=[...document.querySelectorAll('h4')].find(h=>h.textContent.startsWith('Rèn kỹ năng:'));h.parentElement.querySelector('button').click(); })()`);
    await wait(`!!document.querySelector('[role="alert"] button') && document.querySelector('[role="alert"]').innerText.includes('Tìm hiểu:')`, 'conflict identifies the unfinished study');
    await click('Tiếp tục: Tìm hiểu:');
    await wait(`!!document.querySelector('[role="dialog"] textarea')`, 'conflict resume button opens actual unfinished activity');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"] textarea').value.includes('Ghi chú kiểm thử trình duyệt')`));
    await click('Hoàn thành hoạt động');
    await wait(`!!document.querySelector('[aria-label="Nhận xét trợ giảng AI"]')`, 'study understanding feedback pane');
    assert.ok(await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Ghi chú kiểm thử trình duyệt')`), 'Review shows the actual submitted understanding');
    await click('Xem tổng kết buổi học'); await wait(`document.body.innerText.includes('Đã hoàn thành hoạt động học')`, 'honest learning-only result');
    assert.ok(!await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Xuất sắc! Đạt chuẩn')`));
    await click('Về tuyến học');
    await wait(`!document.querySelector('[role="dialog"]') && [...document.querySelectorAll('button')].some(b=>b.textContent.trim()==='Xem lại')`, 'completed study reflected in agenda');
    assert.ok(!await evaluate(`document.body.innerText.includes('Xem lượt đã làm')`), 'No secondary history button');
    await click('Xem lại');
    await wait(`!!document.querySelector('button[aria-label="Xem lại câu 1"]')`, 'saved study available for review');
    await evaluate(`document.querySelector('button[aria-label="Xem lại câu 1"]').click()`);
    await wait(`document.querySelector('[role="dialog"]')?.innerText.includes('Cách hiểu em đã nộp')`, 'saved study response opens without reevaluation');
    assert.ok(!await evaluate(`document.querySelector('[role="dialog"]').innerText.includes('Đang kiểm tra kết quả đã lưu')`));
    await evaluate(`document.querySelector('[aria-label="Đóng và giữ tiến độ"]').click()`);
    assert.ok(await evaluate(`document.body.innerText.includes('Lộ trình đề xuất cho bạn')`), 'Recommendation must explain recent student work');
    await wait(`(() => { const h=[...document.querySelectorAll('h4')].find(h=>h.textContent.startsWith('Rèn kỹ năng:'));const b=h?.parentElement.querySelector('button');if(!b||b.disabled)return false;b.click();return true;})()`, 'open multi-question practice');
    await wait(`!!document.querySelector('[role="dialog"] form')`, 'practice question form');
    const questionCount = await evaluate(`document.querySelectorAll('[aria-label="Các câu trong buổi"] button').length`);
    assert.ok(questionCount >= 5 && questionCount <= 8, 'Practice must contain a real list of 5–8 distinct issued questions');
    for (let i=0; i<questionCount; i++) {
      const hasOptions=await evaluate(`!!document.querySelector('[role="dialog"] input[type="radio"]')`);
      if(hasOptions) await evaluate(`document.querySelector('[role="dialog"] input[type="radio"]').click()`);
      else await setInput('[role="dialog"] form textarea', '__not_the_fixture_answer__');
      await evaluate(`(() => { const areas=[...document.querySelectorAll('[role="dialog"] form textarea')]; const reasoning=areas.length>1||document.querySelector('[role="dialog"] input[type="radio"]')?areas.at(-1):null; if(reasoning){Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(reasoning,'Tôi đang thử áp dụng quy tắc và đối chiếu ngữ cảnh của câu.');reasoning.dispatchEvent(new Event('input',{bubbles:true}));} })()`);
      await click('Nộp câu trả lời');
      await wait(`!!document.querySelector('[aria-label="Phản hồi câu trả lời"]')`, 'immediate per-question feedback without waiting for AI');
      assert.ok(await evaluate(`document.querySelector('[aria-label="Phản hồi câu trả lời"]').innerText.includes('Đáp án tham khảo')`));
      if(i===0) { const feedbackShot=await cdp('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(evidenceDir,'practice-feedback-desktop.png'),Buffer.from(feedbackShot.data,'base64')); }
      await click(i<questionCount-1 ? 'Câu tiếp theo' : 'Xem tổng kết buổi học');
      if(i<questionCount-1) await wait(`!!document.querySelector('[role="dialog"] form')`, 'next distinct question');
    }
    await evaluate(`document.querySelector('[aria-label="Đóng và giữ tiến độ"]').click()`);
    await wait(`!document.querySelector('[role="dialog"]') && document.body.innerText.includes('Nên xem lại')`, 'new mistakes feed recommendation');
    await wait(`(() => {const h=[...document.querySelectorAll('h4')].find(h=>h.textContent.startsWith('Rèn kỹ năng:'));const b=h?.parentElement.querySelector('button');if(!b||b.disabled||!['Xem lại','Tiếp tục'].includes(b.textContent.trim()))return false;b.click();return true;})()`, 'saved practice can be reopened with the single primary action');
    await wait(`!!document.querySelector('button[aria-label="Xem lại câu 1"]')`, 'persisted review activities loaded');
    await evaluate(`document.querySelector('button[aria-label="Xem lại câu 1"]').click()`);
    await wait(`!!document.querySelector('[aria-label="Phản hồi câu trả lời"]')`, 'restored per-question feedback');
    await evaluate(`document.querySelector('[aria-label="Đóng và giữ tiến độ"]').click()`);
    await click('Xem tuyến đề xuất');
    await wait(`document.querySelector('[role="dialog"]')?.innerText.includes('Chọn tuyến học này')`, 'weakness-based complete recommendation');
    assert.ok(!await evaluate(`!!document.querySelector('[role="dialog"] form')`));
    await click('Điều chỉnh mục tiêu');
    await wait(`!!document.querySelector('[role="dialog"] form')`, 'editable recommended goal');
    assert.ok(await evaluate(`document.querySelectorAll('[role="dialog"] input[type="checkbox"]:checked').length>0`), 'Observed weakness must preselect target skills');
    await evaluate(`document.querySelector('[aria-label="Đóng"]').click()`);
    const insightsShot=await cdp('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(evidenceDir,'insights-desktop.png'),Buffer.from(insightsShot.data,'base64'));
    await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }); await delay(800);
    assert.ok(await evaluate(`document.documentElement.scrollWidth <= innerWidth + 1`), 'Mobile path must not overflow horizontally');
    const mobile = await cdp('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(evidenceDir, 'path-mobile.png'), Buffer.from(mobile.data, 'base64'));
    assert.deepEqual(browserErrors, []);
    fs.writeFileSync(path.join(__dirname, '../../scratch/v4-browser-results.json'), JSON.stringify({ status: 'PASS', checks: ['real login', 'subject-only URL resolves current class', 'automatic complete recommendation before any form', 'diagnostic when evidence is insufficient', 'observations/improvement/focus explanation', 'English scoped preview', 'deadline and selected skill preview', 'accepted route', 'study renderer instead of adaptive practice', 'server autosave', 'reload resume', 'activity-only outcome', '5–8 real practice questions', 'immediate authored feedback per question', 'new mistakes feed recommendation', 'editable preselected weak skills', 'mobile no overflow'], screenshots: evidenceDir }, null, 2));
    console.log('PASS browser: real login, scoped goal/preview, mission renderer, autosave/reload, honest outcome, mobile layout');
  } finally {
    if (ws) ws.close(); chrome.kill();
    await delay(1000);
    const resolved = path.resolve(profile); assert.ok(resolved.startsWith(path.resolve(os.tmpdir()) + path.sep) && path.basename(resolved).startsWith('edutwin_v4_browser_'));
    try { fs.rmSync(resolved, { recursive: true, force: true, maxRetries: 4, retryDelay: 250 }); } catch { /* Own temporary profile may still be locked by Chrome teardown. */ }
  }
}
main().catch(e => { console.error(e.stack ?? e.message); process.exitCode = 1; });
