const assert = require('node:assert/strict');
module.exports = async ({ page, evaluate, waitFor, fill }) => {
  const refresh = () => evaluate("document.querySelector('header button').click()");
  const success = () => waitFor("document.body.textContent.includes('Đã cập nhật thông báo mới nhất.') && !document.querySelector('header button').disabled");
  const failure = () => waitFor("document.body.textContent.includes('Chưa làm mới được đầy đủ thông báo.') && !document.querySelector('header button').disabled");
  const select = (index, value) => evaluate(`(() => { const e = document.querySelectorAll('details select')[${index}]; e.value = '${value}'; e.dispatchEvent(new Event('change', {bubbles:true})); })()`);
  await waitFor("document.body.textContent.includes('Trang 1 / 2')");

  await page('Network.enable');
  await page('Network.emulateNetworkConditions', {offline:true, latency:0, downloadThroughput:-1, uploadThroughput:-1});
  await waitFor('!navigator.onLine');
  await refresh(); await failure();
  assert.equal(await evaluate("document.body.textContent.includes('Đã cập nhật thông báo mới nhất.')"), false);
  await page('Network.emulateNetworkConditions', {offline:false, latency:0, downloadThroughput:-1, uploadThroughput:-1});
  await waitFor('navigator.onLine');

  // A real GET must update both the visible list and the unread badge, with repeat clicks blocked.
  await evaluate(`(() => {
    const r = window.__verification;
    Object.assign(r.notices[0], {title:'Thông báo mới từ máy chủ', readAt:null});
    r.delayMs = 700; r.start = r.requests.length;
    const b = document.querySelector('header button'); b.click(); b.click();
  })()`);
  await waitFor("document.querySelector('header button').disabled && document.querySelector('header button').getAttribute('aria-busy') === 'true'");
  assert.match(await evaluate("document.querySelector('header button').textContent"), /Đang làm mới/);
  await success();
  assert.equal(await evaluate("window.__verification.requests.slice(window.__verification.start).length"), 2);
  assert.match(await evaluate("document.querySelector('ul').textContent"), /Thông báo mới từ máy chủ/);
  assert.match(await evaluate("document.querySelector('nav button').textContent"), /Đã nhận\s+1/);
  await evaluate("window.__verification.delayMs = 0");

  // Refresh the current page without dropping pagination, then preserve combined inbox filters.
  await evaluate("Array.from(document.querySelectorAll('button')).find(b => b.textContent.includes('Sau →')).click()");
  await waitFor("document.body.textContent.includes('Trang 2 / 2')");
  await refresh(); await success();
  assert.equal(await evaluate("window.__verification.requests.filter(r => r.url === '/notifications').at(-1).params.page"), 2);
  await evaluate("Array.from(document.querySelectorAll('button')).find(b => b.textContent.includes('← Trước')).click(); document.querySelector('details summary').click()");
  await fill('input[type=search]', 'Thông báo mới');
  await select(0, 'Request'); await select(1, 'Important');
  await evaluate(`(() => {
    document.querySelectorAll('input[type=date]').forEach(e => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'2026-11-18');
      e.dispatchEvent(new Event('change',{bubbles:true}));
    });
    Array.from(document.querySelectorAll('button')).find(b => b.textContent === 'Chưa đọc').click();
  })()`);
  await waitFor("document.querySelectorAll('li').length === 1 && !!window.__verification.requests.filter(r => r.url === '/notifications').at(-1).params.until");
  await evaluate("window.__verification.notices[0].body = 'Nội dung được cập nhật sau khi lọc.'");
  await refresh(); await success();
  assert.match(await evaluate("document.querySelector('ul').textContent"), /Nội dung được cập nhật sau khi lọc/);
  const filters = await evaluate("window.__verification.requests.filter(r => r.url === '/notifications').at(-1).params");
  assert.equal(filters.search, 'Thông báo mới'); assert.equal(filters.category, 'Request');
  assert.equal(filters.priority, 'Important'); assert.equal(filters.unreadOnly, true);
  assert.ok(filters.from); assert.ok(filters.until);
  assert.equal(await evaluate("document.querySelector('input[type=search]').value"), 'Thông báo mới');

  // Any failed inbox or summary fetch must show failure, and a subsequent click must recover.
  for (const url of ['/notifications', '/notifications/summary']) {
    await evaluate(`window.__verification.failPath = ${JSON.stringify(url)}`);
    await refresh(); await failure();
    assert.equal(await evaluate("document.body.textContent.includes('Đã cập nhật thông báo mới nhất.')"), false);
    await evaluate("window.__verification.failPath = null");
    await refresh(); await success();
    await waitFor("document.querySelectorAll('li').length === 1");
  }
  console.log('PASS: refresh updates received notifications and badge, preserves page/filters, blocks duplicate clicks, and recovers from list/summary failures.');

  // The same top-level control updates sent list, selected detail, and dispatch tracking.
  await evaluate("Array.from(document.querySelectorAll('nav button')).find(b => b.textContent === 'Đã gửi').click()");
  await waitFor("document.querySelector('ul')?.textContent.includes('Thông báo đã gửi kiểm thử')");
  await evaluate("document.querySelector('li button').click()");
  await waitFor("!!document.querySelector('article[aria-label=\"Chi tiết đã gửi\"] h2')");
  await evaluate("Object.assign(window.__verification.sent, {title:'Thông báo đã gửi mới cập nhật', deliveredCount:2, readCount:1}); window.__verification.start = window.__verification.requests.length");
  await refresh(); await success();
  assert.match(await evaluate("document.querySelector('ul').textContent"), /Thông báo đã gửi mới cập nhật/);
  assert.equal(await evaluate("document.querySelector('article h2').textContent"), 'Thông báo đã gửi mới cập nhật');
  const sentRequests = await evaluate("window.__verification.requests.slice(window.__verification.start).map(r => r.url)");
  assert.ok(sentRequests.includes('/notifications/sent')); assert.ok(sentRequests.includes('/notifications/sent/sent-fixture'));
  assert.match(await evaluate("document.querySelector('article').textContent"), /Đã phân phối2Đã đọc1/);
  await evaluate("Array.from(document.querySelectorAll('nav button')).find(b => b.textContent === 'Theo dõi gửi').click()");
  await waitFor("document.body.textContent.includes('Đã phân phối: 1')");
  await evaluate("window.__verification.dispatchCompleted = 5; window.__verification.start = window.__verification.requests.length");
  await refresh(); await success();
  assert.match(await evaluate("document.body.textContent"), /Đã phân phối: 5/);
  assert.ok(await evaluate("window.__verification.requests.slice(window.__verification.start).some(r => r.url === '/notification-operations')"));
  await evaluate("window.__verification.failPath = '/notification-operations'");
  await refresh(); await failure();
  await evaluate("window.__verification.failPath = null");
  await refresh(); await success();
  assert.equal(await evaluate("window.__verification.requests.every(r => r.method === 'get')"), true);
  console.log('PASS: the header refresh updates sent list/detail and dispatch tracking, reports dispatch failures, and performs no notification mutations.');
};
