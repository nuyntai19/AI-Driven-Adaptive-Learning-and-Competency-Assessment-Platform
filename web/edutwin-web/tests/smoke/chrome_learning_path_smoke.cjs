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

    await open('placement');
    await check('placement keeps deferred questions editable and submits only after explicit confirmation', async () => {
      await wait('document.body.innerText.includes("Để làm sau")', 'placement question');
      await click(button('Để làm sau'));
      await wait(`Boolean(document.querySelector('[aria-label="Câu 1, Đã để sau"]'))`, 'question deferred');
      assert.equal(await evaluate('window.__verification.requests.filter(r=>r.url.endsWith("/submissions")).length'), 0);
      await click(`document.querySelector('button[aria-label="Câu 1, Đã để sau"]')`);
      await wait(`document.querySelector('[aria-current="step"]')?.textContent === "1"`, 'revisit deferred question');
      await click(button('Kiểm tra và nộp phần này'));
      await wait('document.body.innerText.includes("Câu chưa nộp: 1, 2, 3")', 'review missing questions');
      assert.equal(await evaluate('window.__verification.requests.filter(r=>r.url.endsWith("/evaluate-v4")).length'), 0);
      await click(button('Nộp và hoàn thành'));
      await wait('document.body.innerText.includes("Câu đã bỏ qua: chưa đủ bằng chứng.")', 'placement evaluation after confirmation');
      const result = await evaluate('window.__verification.requests.filter(r=>r.url.endsWith("/submissions") || r.url.endsWith("/evaluate-v4"))');
      assert.equal(result.length, 4);
      assert.ok(result.slice(0, 3).every(r => r.body.skipped));
      assert.equal(result[3].params.confirmSubmission, true);
      await clean();
    });
    await open('notifications');
    await check('dark inbox navigates both ways and returns from an emptied last page', async () => {
      await wait('document.body.innerText.includes("Trang 1 / 2")', 'first inbox page');
      assert.equal(await evaluate(button('← Trước') + '.disabled'), true);
      await click(button('Sau →'));
      await wait('document.body.innerText.includes("Trang 2 / 2")', 'second inbox page');
      assert.equal(await evaluate(button('Sau →') + '.disabled'), true);
      await click(button('← Trước'));
      await wait('document.body.innerText.includes("Trang 1 / 2")', 'previous inbox page');
      await click(button('Sau →'));
      await wait('document.body.innerText.includes("Thông báo kiểm thử 21")', 'only item on last page');
      await click('document.querySelector("li button")');
      await wait('document.body.innerText.includes("Ẩn khỏi hộp thư của tôi")', 'notification details');
      await click(button('Ẩn khỏi hộp thư của tôi'));
      await wait('document.body.innerText.includes("Trang 1 / 1")', 'refill previous page after hide');
      assert.equal(await evaluate(button('← Trước') + '.disabled && ' + button('Sau →') + '.disabled'), true);
      assert.equal(await evaluate(`getComputedStyle(document.querySelector('select')).colorScheme`), 'dark');
      await clean();
    });
    await open('focus');
    await check('focus countdown survives player-style page unmount and reload', async () => {
      await click(button('Góc tập trung'));
      await click(button('10 phút'));
      await click(button('Bắt đầu tập trung'));
      await wait('Boolean(document.querySelector(".st-focus-launcher.is-running"))', 'focus timer started');
      const beforeReload = await evaluate('performance.timeOrigin');
      await page('Page.reload');
      await wait(`performance.timeOrigin !== ${beforeReload} && Boolean(document.querySelector(".st-focus-launcher.is-running"))`, 'restore running timer');
      assert.match(await evaluate('document.querySelector(".st-focus-launcher span").textContent'), /^(09:[0-5][0-9]|10:00)$/);
      await clean();
    });

    await open('placement');
    await check('formula popover stays above the MathLive keyboard in a short window', async () => {
      await page('Emulation.setDeviceMetricsOverride', { width: 900, height: 590, deviceScaleFactor: 1, mobile: false });
      await wait(`Boolean(document.querySelector('button[title^="Chèn công thức toán"]'))`, 'rich math editor');
      await click(`document.querySelector('button[title^="Chèn công thức toán"]')`);
      await wait('Boolean(document.querySelector(".rich-math-popover math-field")?.shadowRoot && window.mathVirtualKeyboard)', 'load MathLive');
      await evaluate('window.mathVirtualKeyboard.show(); true');
      await wait(`(() => {
        const keyboard = window.mathVirtualKeyboard;
        const popover = document.querySelector(".rich-math-popover");
        return keyboard.visible && keyboard.boundingRect.height > 0 && popover.getBoundingClientRect().bottom <= keyboard.boundingRect.top + 1;
      })()`, 'formula dialog clears the keyboard');
      await clean();
    });

    await open('goal-graph');
    await check('placement registration automatically adds required graph foundations', async () => {
      await click(`Array.from(document.querySelectorAll('label')).find(e=>e.textContent.trim().startsWith('Kỹ năng nền'))?.querySelector('input')`);
      await wait('document.body.innerText.includes("Nền tảng bắt buộc được thêm theo đồ thị tri thức")', 'required foundation');
      assert.equal(await evaluate(`Boolean(document.querySelector('input[name="prerequisite-choice"]'))`), false);
      await click(`Array.from(document.querySelectorAll('button')).find(e=>e.textContent.trim()==='Bắt đầu kiểm tra')`);
      await wait('window.__verification.accepted', 'placement accepted');
      assert.deepEqual(await evaluate('window.__verification.goal.targetSkillIds'), ['target']);
      assert.deepEqual(await evaluate('window.__verification.goal.selfReportedPrerequisiteSkillIds'), []);
      await clean();
    });
    await open('goal');
    await check('goal defaults to placement and direct study rejects a past date', async () => {
      assert.equal(await evaluate(`document.querySelector('input[name="path-start-mode"]').checked`), true);
      await click("document.querySelectorAll('input[name=path-start-mode]')[1]");
      await wait('Boolean(document.querySelector("details[open] input[type=date]"))', 'direct study date settings');
      const min = await evaluate('document.querySelector("input[type=date]").min'); assert.match(min, /^\d{4}-\d{2}-\d{2}$/);
      await fill('input[type=date]', '2020-01-01'); assert.equal(await evaluate('document.querySelector("input[type=date]").validity.rangeUnderflow'), true);
    });
    await check('pace, cooldown, preview and acceptance use the changed UI controls', async () => {
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
    await open('exam-schedule');
    await check('exam link opens the schedule for the selected exam', async () => {
      await wait('document.body.innerText.includes("Ôn 30 phút hôm nay.")', 'exam schedule shown');
      assert.equal(await evaluate(`document.querySelector('[role="tab"][aria-selected="true"]').textContent.startsWith("Hôm nay")`), true);
      assert.equal(await evaluate(`document.querySelector('[aria-labelledby="study-schedule-title"] a').getAttribute('href')`),
        '/hoc-tap/bai-tap/upcoming?subjectId=subject&classId=class');
      await clean();
    });
    await open('exam-schedule-no-plan');
    await check('exam link shows the exam and explains an unavailable study schedule', async () => {
      await wait('document.body.innerText.includes("Em chưa có lịch ôn.")', 'no schedule fallback shown');
      assert.equal(await evaluate(`document.querySelector('[aria-labelledby="study-schedule-title"] h3').textContent`), 'Thi giữa kỳ kiểm thử');
      await clean();
    });
    await open('student-assignments');
    await check('student assignments separate regular work from midterms and finals', async () => {
      await wait('document.body.innerText.includes("Bài luyện của em")', 'regular assignment');
      assert.equal(await evaluate('document.body.innerText.includes("Kiểm tra giữa kỳ A")'), false);
      await click(`Array.from(document.querySelectorAll('[aria-label="Loại bài"] button')).find(e=>e.textContent.startsWith('Bài kiểm tra'))`);
      await wait('document.body.innerText.includes("Kiểm tra giữa kỳ A") && document.body.innerText.includes("Kiểm tra cuối kỳ B")', 'exam assignments');
      assert.equal(await evaluate('document.body.innerText.includes("Bài luyện của em")'), false);
      await clean();
    });
    await open('teacher-students');
    await check('student management and its student profile have no report export controls', async () => {
      await wait('document.body.innerText.includes("Học sinh kiểm thử")', 'student management rows');
      await click(`Array.from(document.querySelectorAll('button')).find(e=>e.textContent.includes('Hồ sơ →'))`);
      await wait('Boolean(document.querySelector("[role=dialog]"))', 'student profile');
      assert.doesNotMatch(await evaluate('document.body.innerText'), /Xuất Excel|Xuất CSV|In Phiếu Đánh Giá/);
      await clean();
    });
    await open('teacher-reports');
    await check('reporting retains individual and class export actions', async () => {
      await wait('document.body.innerText.includes("Báo cáo cá nhân")', 'reporting table');
      assert.ok(await evaluate(`Boolean(document.querySelector('[aria-label="Xuất Excel cá nhân cho Học sinh kiểm thử"]'))`));
      assert.ok(await evaluate(`Boolean(document.querySelector('[aria-label="Xuất CSV cá nhân cho Học sinh kiểm thử"]'))`));
      assert.match(await evaluate('document.body.innerText'), /Xuất Excel Cả Lớp/);
      await clean();
    });
    await open('timeline');
    await wait('document.body.innerText.includes("Thi thử tự luyện")', 'timeline');
    const reviewTab = title => `Array.from(document.querySelectorAll('[role="tab"]')).find(e=>e.textContent.startsWith(${JSON.stringify(title)}))`;
    await check('a single divider separates study stages from mock exams and review stages', async () => {
      const items = await evaluate(`Array.from(document.querySelectorAll('ol[aria-label="Các chặng của lộ trình"] > li')).map(li => ({
        title: li.querySelector('button b')?.textContent,
        divider: li.querySelector('hr')?.previousElementSibling.textContent ?? null
      }))`);
      assert.deepEqual(items.slice(-3), [
        { title: 'Chặng học tiếp theo', divider: null },
        { title: 'Thi thử tự luyện', divider: 'Ôn tập và thi thử' },
        { title: 'Ôn xác nhận', divider: null },
      ]);
      assert.equal(items.filter(item => item.divider).length, 1);
    });
    await check('class exams stay outside the route, and only the published midterm has a review tab', async () => {
      const stage = title => `Array.from(document.querySelectorAll('button[aria-pressed]')).find(e=>e.querySelector('b')?.textContent===${JSON.stringify(title)})`;
      assert.equal(await evaluate(`Boolean(${stage('Mốc thi lớp đã hủy')})`), false);
      await click(stage('Thi thử tự luyện'));
      await wait(`document.body.innerText.includes("Bài thi thử bấm giờ để tự đánh giá trước kỳ thi.")`, 'selected self-practice exam');
      await click(stage('Kỹ năng phụ thuộc'));
      const disabled = await evaluate(`(() => {const h=Array.from(document.querySelectorAll('h4')).find(e=>e.textContent==='Rèn phần phụ thuộc');return h?.closest('li')?.querySelector('button')?.disabled;})()`);
      assert.equal(disabled, true);
      assert.equal(await evaluate(`Boolean(${reviewTab('Ôn tập cuối kỳ')})`), false);
      await click(reviewTab('Ôn tập giữa kỳ'));
      await wait('document.body.innerText.includes("Giữa kỳ đã công bố")', 'published midterm review');
      assert.match(await evaluate(`document.querySelector('[aria-labelledby="midterm-review-title"]').innerText`), /Ôn tập giữa kỳ: Kỹ năng phụ thuộc/);
      await clean(); await capture('timeline-desktop');
    });
    await check('student route fits a 375px viewport without horizontal overflow', async () => {
      await page('Emulation.setDeviceMetricsOverride', { width: 375, height: 812, deviceScaleFactor: 1, mobile: true }); await sleep(200);
      assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth + 1')); await capture('timeline-mobile');
    });
    await open('timeline-final');
    await check('published final and midterm reviews are shown under their own tabs', async () => {
      await wait(`Boolean(${reviewTab('Ôn tập cuối kỳ')})`, 'final review tab');
      await click(reviewTab('Ôn tập cuối kỳ'));
      await wait('document.body.innerText.includes("Đề cuối kỳ tiếng Anh")', 'published final review');
      assert.equal(await evaluate('document.body.innerText.includes("Giữa kỳ đã công bố")'), false);
      assert.match(await evaluate(`document.querySelector('[aria-labelledby="final-review-title"]').innerText`), /Ôn tập cuối kỳ: Kỹ năng phụ thuộc/);
      await clean();
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
    await open('teacher-grading');
    await check('teacher grading filters distinguish regular, midterm, final and mock assignments', async () => {
      for (const [filter, title] of [["Bài thường", "Bài luyện thường"], ["Giữa kỳ", "Đề giữa kỳ"], ["Cuối kỳ", "Đề cuối kỳ"], ["Thi thử", "Đề thi thử"]]) {
        await click(button(filter));
        await wait(`document.body.innerText.includes(${JSON.stringify(title)})`, filter);
        assert.deepEqual(await evaluate(`Array.from(document.querySelectorAll('h3')).map(h=>h.textContent)`), [title]);
      }
      await clean();
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
