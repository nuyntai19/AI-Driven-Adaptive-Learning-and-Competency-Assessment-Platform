const assert = require('node:assert/strict');
module.exports = async ({evaluate,waitFor,fill,delay}) => {
  const input = 'input[aria-label="Tìm kiếm trung tâm"]';
  const selectStatus = value => evaluate(`(() => {const e=document.querySelector('select[aria-label="Lọc trạng thái trung tâm"]'); e.value=${JSON.stringify(value)}; e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  const latest = () => evaluate('window.__verification.requests.at(-1).params');
  await waitFor("document.querySelectorAll('tbody tr').length===20");
  assert.equal(await evaluate("Array.from(document.querySelectorAll('button')).some(b=>b.textContent.trim()==='Tìm kiếm')"),false);
  await evaluate("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.trim()==='Sau').click()");
  await waitFor("window.__verification.requests.at(-1).params.page===2 && document.querySelectorAll('tbody tr').length===5");
  // Input should cause one debounced request and reset pagination, without a form submission.
  await evaluate(`(async()=>{
    const r=window.__verification; r.start=r.requests.length;
    const e=document.querySelector(${JSON.stringify(input)});
    for(const value of ['H','Hà','Hà N','Hà Nội']) {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,value);
      e.dispatchEvent(new Event('input',{bubbles:true}));
      await new Promise(resolve=>setTimeout(resolve,40));
    }
  })()`);
  await waitFor("window.__verification.requests.at(-1).params.search==='Hà Nội' && document.querySelectorAll('tbody tr').length===2");
  assert.equal((await latest()).page,1);
  assert.equal(await evaluate('window.__verification.requests.length-window.__verification.start'),1);
  await selectStatus('Suspended');
  await waitFor("document.querySelectorAll('tbody tr').length===1 && document.querySelector('tbody').textContent.includes('HANOI_B')");
  assert.equal((await latest()).search,'Hà Nội'); assert.equal((await latest()).status,'Suspended');
  await selectStatus('');
  await waitFor("document.querySelectorAll('tbody tr').length===2");
  await fill(input,'');
  await waitFor("document.querySelectorAll('tbody tr').length===20 && !window.__verification.requests.at(-1).params.search");
  await selectStatus('Active');
  await waitFor("window.__verification.requests.at(-1).params.status==='Active' && document.body.textContent.includes('tổng số 21 trung tâm')");
  await evaluate("Array.from(document.querySelectorAll('button')).find(b=>b.textContent.trim()==='Sau').click()");
  await waitFor("document.querySelectorAll('tbody tr').length===1 && window.__verification.requests.at(-1).params.page===2");
  await selectStatus('Suspended');
  await waitFor("document.querySelectorAll('tbody tr').length===4 && window.__verification.requests.at(-1).params.page===1");
  console.log('PASS: input searches automatically, fast typing is debounced, status filters combine with text, and both filters reset pagination.');

  // Changing status while a text debounce is pending must apply the current input, not an old search.
  await fill(input,'  manager_lan  ');
  await selectStatus('Active');
  await waitFor("document.querySelectorAll('tbody tr').length===1 && document.querySelector('tbody').textContent.includes('HANOI_A')");
  assert.equal((await latest()).search,'manager_lan'); assert.equal((await latest()).status,'Active');
  await fill(input,'   ');
  await waitFor("document.body.textContent.includes('tổng số 21 trung tâm') && !window.__verification.requests.at(-1).params.search");
  // Superseded requests are aborted; their late responses cannot replace the latest results.
  await evaluate("window.__verification.delays.Trung=2000");
  await fill(input,'Trung');
  await waitFor("window.__verification.requests.at(-1).params.search==='Trung'");
  await fill(input,'HANOI_A');
  await waitFor("document.querySelectorAll('tbody tr').length===1 && document.querySelector('tbody').textContent.includes('HANOI_A')");
  assert.ok(await evaluate("window.__verification.cancelled.includes('Trung')"));
  await delay(350);
  assert.match(await evaluate("document.querySelector('tbody').textContent"),/HANOI_A/);
  await fill(input,'không có trung tâm này');
  await waitFor("document.body.textContent.includes('Không tìm thấy trung tâm nào khớp với điều kiện lọc.')");
  assert.equal(await evaluate("window.__verification.requests.every(r=>r.method==='get')"),true);
  console.log('PASS: changing status flushes pending text, blank input clears only the keyword, older requests are cancelled, and empty results are displayed without mutations.');
};
