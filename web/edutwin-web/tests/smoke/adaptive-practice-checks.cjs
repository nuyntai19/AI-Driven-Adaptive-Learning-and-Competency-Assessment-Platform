const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

module.exports = async ({ page, evaluate, waitFor, delay, port }) => {
  const selectTopic = async value => {
    await evaluate(`(() => { const e = document.querySelector('#topic-dropdown'); e.value = ${JSON.stringify(value)}; e.dispatchEvent(new Event('change', { bubbles: true })); })()`);
  };
  const customMode = async () => {
    await evaluate(`Array.from(document.querySelectorAll('button')).find(b => b.textContent.includes('Tự chọn chủ đề')).click()`);
    await waitFor("document.querySelector('#topic-dropdown')?.options.length === 3");
  };
  await waitFor("document.querySelector('#practice-proposal-title')?.textContent.includes('Vectơ')");
  await customMode();
  const options = await evaluate("Array.from(document.querySelector('#topic-dropdown').options, o => o.textContent)");
  assert.match(options[0], /Hàm số.*61%.*Tạm chưa luyện được.*chưa đóng/);
  assert.match(options[1], /80%.*Có thể luyện.*9 câu/);
  assert.match(options[2], /78%.*Có thể luyện.*6 câu/);

  await selectTopic('function');
  await waitFor("document.querySelector('h2') && document.body.textContent.includes('Hàm số · Tạm chưa luyện được')");
  assert.equal(await evaluate("Array.from(document.querySelectorAll('button')).some(b => !b.disabled && b.textContent.includes('Bắt đầu'))"), false);
  assert.match(await evaluate("document.querySelector('#topic-availability').textContent"), /chưa đóng/);
  assert.equal(await evaluate("document.querySelector('#topic-dropdown').value"), 'function');

  // The reason remains readable with a narrow viewport, without opening a native dropdown.
  await page('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
  assert.equal(await evaluate("document.documentElement.scrollWidth <= innerWidth + 1"), true);
  const screenshot = await page('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
  fs.writeFileSync(path.resolve(__dirname, '../../../../scratch/practice-blocked-topic-mobile.png'), Buffer.from(screenshot.data, 'base64'));
  await page('Emulation.setDeviceMetricsOverride', { width: 1280, height: 920, deviceScaleFactor: 1, mobile: false });

  await selectTopic('quadratic');
  await waitFor("document.querySelector('#practice-proposal-title')?.textContent.includes('bậc hai')");
  await waitFor("Array.from(document.querySelectorAll('button')).some(b => !b.disabled && b.textContent.includes('Bắt đầu'))");
  await evaluate("Array.from(document.querySelectorAll('button')).find(b => b.textContent.includes('Bắt đầu')).click()");
  await waitFor("document.body.textContent.includes('Phiên kiểm thử đã bắt đầu')");

  // Even if every topic is blocked, the full catalogue must remain visible.
  await page('Page.navigate', { url: `http://127.0.0.1:${port}/tests/smoke/adaptive-practice-fixture.html?state=empty` });
  await waitFor("document.body.textContent.includes('Chưa có câu hỏi luyện tập hợp lệ')");
  await customMode();
  assert.equal(await evaluate("document.querySelector('#topic-dropdown').options.length"), 3);
  assert.equal(await evaluate("Array.from(document.querySelectorAll('button')).some(b => !b.disabled && b.textContent.includes('Bắt đầu'))"), false);
  console.log('PASS: full topic catalogue, blocked choice reason, start guard, available choice starts, all-blocked catalogue, mobile layout.');
  await require('./practice-session-checks.cjs')({ page, evaluate, waitFor, delay, port });
};
