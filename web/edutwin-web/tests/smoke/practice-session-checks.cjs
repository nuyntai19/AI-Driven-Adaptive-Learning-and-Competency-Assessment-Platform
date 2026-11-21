const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

module.exports = async ({ page, evaluate, waitFor, delay, port }) => {
  const clickText = async text => {
    await evaluate(`(() => { const b = Array.from(document.querySelectorAll('button')).find(b => b.textContent.trim() === ${JSON.stringify(text)}); if (!b || b.disabled) throw new Error('Unavailable button: ' + ${JSON.stringify(text)}); b.click(); })()`);
  };
  const fillWork = async text => {
    await evaluate(`(() => { const e = document.querySelector('[contenteditable="true"]'); e.textContent = ${JSON.stringify(text)}; e.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText' })); })()`);
  };
  const workReady = "!!document.querySelector('[contenteditable=\"true\"]') && !document.querySelector('[role=dialog]')";
  await page('Page.navigate', { url: `http://127.0.0.1:${port}/tests/smoke/practice-session-fixture.html` });
  await waitFor("document.body.textContent.includes('Phiên luyện tập đang tạm dừng.')");
  await clickText('Tiếp tục luyện tập');
  await waitFor(workReady);
  const work = 'Bài giải đang viết dở: xét hai nghiệm -2 và 3';
  await fillWork(work);
  await waitFor(`document.querySelector('[contenteditable="true"]').textContent === ${JSON.stringify(work)}`);

  for (let i = 0; i < 5; i++) {
    await clickText('Tạm dừng');
    await waitFor("!!document.querySelector('[role=dialog]')");
    await clickText('Rời khỏi trang');
    await waitFor("document.body.textContent.includes('Bạn có phiên luyện tập đang làm dở')");
    await clickText('Tiếp tục luyện tập');
    await waitFor(workReady);
    assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), work);
    assert.match(await evaluate("document.querySelector('header').textContent"), /Câu 2/);
    assert.equal(await evaluate("window.practiceSessionFixture.submits"), 0);
  }
  assert.equal(await evaluate("window.practiceSessionFixture.requestsWhilePaused"), 0);

  await clickText('Tạm dừng');
  await waitFor("!!document.querySelector('[role=dialog]')");
  await clickText('Tiếp tục ngay');
  await waitFor(workReady);
  assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), work);

  // Pause/resume failures must keep work and expose a retry instead of throwing an unhandled rejection.
  await evaluate("window.practiceSessionFixture.failPause = true");
  await clickText('Tạm dừng');
  await waitFor("document.body.textContent.includes('Chưa tạm dừng được phiên')");
  assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), work);
  await evaluate("window.practiceSessionFixture.failPause = false");
  await clickText('Tạm dừng');
  await waitFor("!!document.querySelector('[role=dialog]')");
  await evaluate("window.practiceSessionFixture.failResume = true");
  await clickText('Tiếp tục ngay');
  await waitFor("document.querySelector('[role=dialog]').textContent.includes('Chưa tiếp tục được phiên')");
  await evaluate("window.practiceSessionFixture.failResume = false");
  await clickText('Tiếp tục ngay');
  await waitFor(workReady);

  // A failed overview resume stays on the overview and can be retried.
  await clickText('Tạm dừng');
  await waitFor("!!document.querySelector('[role=dialog]')");
  await clickText('Rời khỏi trang');
  await waitFor("document.body.textContent.includes('Bạn có phiên luyện tập đang làm dở')");
  await evaluate("window.practiceSessionFixture.failResume = true");
  await clickText('Tiếp tục luyện tập');
  await waitFor("document.body.textContent.includes('Chưa tiếp tục được phiên')");
  assert.equal(await evaluate("!!document.querySelector('#practice-modes-title')"), true);
  await evaluate("window.practiceSessionFixture.failResume = false");
  await clickText('Tiếp tục luyện tập');
  await waitFor(workReady);
  assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), work);

  // Reopening a paused page without any in-memory question still restores the saved draft.
  await page('Page.reload');
  await waitFor("document.body.textContent.includes('Phiên luyện tập đang tạm dừng.')");
  await clickText('Tiếp tục luyện tập');
  await waitFor(workReady);
  assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), work);
  const shot = await page('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
  fs.writeFileSync(path.resolve(__dirname, '../../../../scratch/practice-repeated-resume.png'), Buffer.from(shot.data, 'base64'));

  // A successful empty response or question error must leave a visible recovery action.
  await page('Page.reload');
  await waitFor("document.body.textContent.includes('Phiên luyện tập đang tạm dừng.')");
  await evaluate("window.practiceSessionFixture.emptyQuestion = true");
  await clickText('Tiếp tục luyện tập');
  await waitFor("document.body.textContent.includes('Chưa tải được câu hỏi hiện tại')");
  await evaluate("window.practiceSessionFixture.emptyQuestion = false; window.practiceSessionFixture.failQuestion = true");
  await clickText('Thử lại');
  await waitFor("document.querySelector('[role=alert]')?.textContent.includes('Chưa tải được câu hỏi hiện tại')");
  await evaluate("window.practiceSessionFixture.failQuestion = false");
  await clickText('Thử lại');
  await waitFor(workReady);
  assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), work);

  // MCQ selection and its separate explanation survive the same route transitions.
  await page('Page.navigate', { url: `http://127.0.0.1:${port}/tests/smoke/practice-session-fixture.html?mcq=true` });
  await waitFor("document.body.textContent.includes('Phiên luyện tập đang tạm dừng.')");
  await clickText('Tiếp tục luyện tập');
  await waitFor(workReady);
  await evaluate("document.querySelector('fieldset button').click()");
  await fillWork('Em xét dấu giữa hai nghiệm.');
  await clickText('Tạm dừng');
  await waitFor("!!document.querySelector('[role=dialog]')");
  await clickText('Rời khỏi trang');
  await waitFor("document.body.textContent.includes('Bạn có phiên luyện tập đang làm dở')");
  await clickText('Tiếp tục luyện tập');
  await waitFor(workReady);
  assert.equal(await evaluate("document.querySelector('fieldset button')?.getAttribute('aria-pressed')"), 'true');
  assert.equal(await evaluate("document.querySelector('[contenteditable=true]').textContent"), 'Em xét dấu giữa hai nghiệm.');
  assert.equal(await evaluate("window.practiceSessionFixture.submits"), 0);
  console.log('PASS: stale cache race; five pause/leave/resume cycles; correct question and draft; reload; pause/resume failures; overview retry; empty/error question recovery; MCQ selection and explanation.');
};
