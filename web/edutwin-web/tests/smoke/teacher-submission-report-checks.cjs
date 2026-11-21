const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

module.exports = async ({ page, evaluate, waitFor, port }) => {
  const card = `Array.from(document.querySelectorAll('article')).find(e => e.textContent.includes('Tỷ lệ nộp bài tập'))`;
  for (const [scenario, percentage, counts] of [['targeted', '100%', '1/1'], ['pending', '50%', '1/2'], ['weighted', '25%', '1/4'], ['none', '0%', '0/0']]) {
    await page('Page.navigate', { url: `http://127.0.0.1:${port}/tests/smoke/learning-path-fixture.html?scenario=teacher-reports&submissions=${scenario}` });
    await waitFor(`document.body.textContent.includes('${counts} lượt giao bài đã nộp')`);
    const text = await evaluate(`(${card}).textContent`);
    assert.ok(text.includes(percentage), `${scenario}: expected ${percentage} in ${text}`);
    assert.equal(await evaluate('window.__verification.unknown.length'), 0);
    assert.equal(await evaluate('window.__verification.requests.some(r => r.method !== "get")'), false);
    if (scenario === 'targeted') {
      const shot = await page('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true });
      fs.writeFileSync(path.resolve(__dirname, '../../../../scratch/teacher-submission-rate.png'), Buffer.from(shot.data, 'base64'));
      await evaluate(`Array.from(document.querySelectorAll('button')).find(b => b.textContent.includes('In Báo Cáo Lớp')).click()`);
      await waitFor(`document.body.textContent.includes('BÁO CÁO TỔNG KẾT HỌC TẬP LỚP HỌC')`);
      assert.equal(await evaluate(`Array.from(document.querySelectorAll('span')).find(e => e.textContent === 'Tỷ lệ nộp bài tập chung:').parentElement.querySelector('strong').textContent`), '100%');
    }
  }
  console.log('PASS: real teacher report renders targeted 1/1 = 100%, pending 1/2 = 50%, weighted 1/4 = 25%, and no assignments = 0% with no mutations.');
};
