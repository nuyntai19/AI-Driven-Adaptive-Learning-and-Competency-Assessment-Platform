/**
 * Real-Browser Caret Algorithm Smoke Test
 *
 * Verifies that in real Chromium (headless Chrome DevTools Protocol):
 * 1. ContentEditable selection/range placed in the middle of a sentence is preserved across external focus loss (e.g. clicking Casio/toolbar).
 * 2. The 5-step caret restoration sequence (retrieve range -> focus editor -> restore range to selection -> insert node -> place afterRange)
 *    inserts an inline KaTeX formula directly at the preserved caret in the middle of the sentence.
 * 3. Real native browser typing (CDP Input.insertText) immediately follows the inserted formula, never jumping to the beginning or end.
 *
 * NOTE: This is a real-browser caret algorithm smoke test validating the core ContentEditable Range/Selection mechanics in Chromium.
 * It is not an end-to-end (E2E) UI test of the full React application bundle.
 */

const http = require('http');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

async function runChromeCaretSmoke() {
  const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
  if (!fs.existsSync(chromePath)) {
    console.warn('Chrome executable not found at default path:', chromePath);
    console.log('Skipping real-browser smoke test.');
    process.exit(0);
  }

  const userDataDir = path.join(os.tmpdir(), 'chrome_caret_smoke_' + Date.now());
  fs.mkdirSync(userDataDir, { recursive: true });

  console.log('1. Launching headless Chrome for Real-Browser Caret Algorithm Smoke...');
  const chromeProc = spawn(chromePath, [
    '--headless=new',
    '--remote-debugging-port=9222',
    `--user-data-dir=${userDataDir}`,
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-gpu',
    'about:blank'
  ]);

  let versionData = null;
  for (let i = 0; i < 30; i++) {
    await new Promise((r) => setTimeout(r, 300));
    try {
      const res = await new Promise((resolve, reject) => {
        http
          .get('http://127.0.0.1:9222/json/version', (res) => {
            let body = '';
            res.on('data', (chunk) => (body += chunk));
            res.on('end', () => resolve(JSON.parse(body)));
          })
          .on('error', reject);
      });
      if (res && res.webSocketDebuggerUrl) {
        versionData = res;
        break;
      }
    } catch {}
  }

  if (!versionData) {
    console.error('Failed to connect to Chrome debugging port.');
    chromeProc.kill();
    fs.rmSync(userDataDir, { recursive: true, force: true });
    process.exit(1);
  }

  console.log('2. Connected to Chrome WebSocket:', versionData.webSocketDebuggerUrl);

  const ws = new WebSocket(versionData.webSocketDebuggerUrl);
  let id = 1;
  const pending = new Map();

  ws.onmessage = (event) => {
    const msg = JSON.parse(event.data);
    if (msg.id && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      if (msg.error) reject(msg.error);
      else resolve(msg.result);
    }
  };

  await new Promise((resolve) => (ws.onopen = resolve));

  function send(method, params = {}) {
    const msgId = id++;
    return new Promise((resolve, reject) => {
      pending.set(msgId, { resolve, reject });
      ws.send(JSON.stringify({ id: msgId, method, params }));
    });
  }

  const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });

  function sendPage(method, params = {}) {
    const reqId = id++;
    return new Promise((resolve, reject) => {
      pending.set(reqId, { resolve, reject });
      ws.send(JSON.stringify({ id: reqId, sessionId, method, params }));
    });
  }

  await sendPage('Page.enable');
  await sendPage('Runtime.enable');
  await sendPage('DOM.enable');

  console.log('3. Setting up in-browser test harness for contentEditable caret preservation & typing...');
  const htmlContent = `
    <!DOCTYPE html>
    <html>
    <head>
      <meta charset="utf-8">
      <title>Real Browser Caret Algorithm Smoke</title>
      <style>
        body { font-family: sans-serif; padding: 20px; background: #0f172a; color: #f8fafc; }
        .editor-container { border: 1px solid #334155; border-radius: 8px; padding: 12px; background: #1e293b; min-height: 80px; }
        .rich-editable { outline: none; font-size: 14px; line-height: 1.6; }
        .inline-math-node { display: inline-flex; align-items: center; background: #0284c7; color: white; padding: 2px 6px; border-radius: 4px; margin: 0 3px; }
        .casio-btn { margin-top: 15px; padding: 8px 16px; background: #6366f1; color: white; border: none; border-radius: 6px; cursor: pointer; }
      </style>
    </head>
    <body>
      <h3>Real-Browser Caret Algorithm Smoke Surface</h3>
      <div class="editor-container">
        <div id="test-editor" class="rich-editable" contenteditable="true"></div>
      </div>
      <button id="casio-button" class="casio-btn">⌨ External Button: Chèn \\sqrt{x^2+1}</button>
      <div id="status-log" style="margin-top: 15px; font-family: monospace; font-size: 12px; color: #38bdf8;"></div>
    </body>
    </html>
  `;

  await sendPage('Page.navigate', {
    url: 'data:text/html;charset=utf-8,' + encodeURIComponent(htmlContent),
  });
  await new Promise((r) => setTimeout(r, 600));

  console.log('4. Initializing contentEditable text and positioning caret in the middle of sentence...');
  const initResult = await sendPage('Runtime.evaluate', {
    expression: `
      (() => {
        const editor = document.getElementById('test-editor');
        const casioBtn = document.getElementById('casio-button');
        
        // Initial Vietnamese prose
        editor.focus();
        editor.innerHTML = 'Cho một hàm số  đồng biến trên R.';
        
        // Caret placed right after "Cho một hàm số " (index 15)
        const textNode = editor.firstChild;
        const splitOffset = 15;
        
        const range = document.createRange();
        range.setStart(textNode, splitOffset);
        range.setEnd(textNode, splitOffset);
        
        const sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
        
        // Store in global window for simulation of lastValidRangeRef
        window.__lastValidRange = range.cloneRange();
        
        // Simulate clicking outside to Casio button (focus lost)
        casioBtn.focus();
        sel.removeAllRanges();
        
        return {
          activeElementAfterBlur: document.activeElement.id,
          hasSavedRange: !!window.__lastValidRange,
          savedOffset: window.__lastValidRange.startOffset
        };
      })()
    `,
    returnByValue: true,
  });
  console.log('   Init and blur state:', initResult.result.value);

  console.log('5. Executing 5-step insertion sequence (preserving caret from blur)...');
  const insertResult = await sendPage('Runtime.evaluate', {
    expression: `
      (() => {
        const editor = document.getElementById('test-editor');
        const sel = window.getSelection();
        
        // Step 1: Get saved target range before focus
        const targetRange = window.__lastValidRange.cloneRange();
        
        // Step 2: Focus editor container
        editor.focus();
        
        // Step 3: Restore target range into selection while focused
        sel.removeAllRanges();
        sel.addRange(targetRange);
        
        // Step 4: Create and insert math span node
        const mathSpan = document.createElement('span');
        mathSpan.className = 'inline-math-node';
        mathSpan.dataset.latex = '\\\\sqrt{x^2+1}';
        mathSpan.textContent = '√[x²+1]';
        mathSpan.contentEditable = 'false';
        
        targetRange.deleteContents();
        targetRange.insertNode(mathSpan);
        
        // Step 5: Place afterRange into selection at the very end
        const afterRange = document.createRange();
        afterRange.setStartAfter(mathSpan);
        afterRange.collapse(true);
        sel.removeAllRanges();
        sel.addRange(afterRange);
        window.__lastValidRange = afterRange.cloneRange();
        
        return {
          activeElement: document.activeElement.id,
          selectionRangeCount: sel.rangeCount,
          startContainerType: afterRange.startContainer.nodeType,
          startOffset: afterRange.startOffset,
          nodesInEditor: Array.from(editor.childNodes).map(n => n.nodeType === 3 ? ('TEXT:' + n.nodeValue) : ('EL:' + n.className))
        };
      })()
    `,
    returnByValue: true,
  });
  console.log('   After 5-step insertion state:', insertResult.result.value);

  console.log('6. Dispatching real browser native typing (CDP Input.insertText) at preserved caret...');
  await sendPage('Input.insertText', { text: ' liên tục' });
  await new Promise((r) => setTimeout(r, 400));

  console.log('7. Verifying final DOM structure and text placement...');
  const verifyResult = await sendPage('Runtime.evaluate', {
    expression: `
      (() => {
        const editor = document.getElementById('test-editor');
        let serialized = '';
        function walk(n) {
          if (n.nodeType === 3) {
            serialized += (n.nodeValue || '').replace(/\\u00A0/g, ' ');
          } else if (n.nodeType === 1) {
            if (n.classList.contains('inline-math-node')) {
              serialized += '$' + n.dataset.latex + '$';
            } else {
              for (const c of Array.from(n.childNodes)) walk(c);
            }
          }
        }
        walk(editor);
        
        const statusLog = document.getElementById('status-log');
        statusLog.textContent = 'Final: ' + serialized;
        
        const expected = 'Cho một hàm số $\\\\sqrt{x^2+1}$ liên tục đồng biến trên R.';
        return {
          serializedResult: serialized,
          isFormulaInMiddle: serialized.includes('Cho một hàm số $\\\\sqrt{x^2+1}$'),
          isSubsequentTextImmediatelyAfter: serialized.includes('$\\\\sqrt{x^2+1}$ liên tục'),
          fullExactMatch: serialized === expected,
          childNodes: Array.from(editor.childNodes).map(n => n.nodeType === 3 ? ('TEXT:' + n.nodeValue) : ('EL:' + n.className))
        };
      })()
    `,
    returnByValue: true,
  });

  const val = verifyResult.result.value;
  console.log('8. Verification Result in Chrome:', val);

  if (!val.fullExactMatch) {
    console.error('FAILED: Real browser caret insertion did not produce expected output!');
    console.error('Actual:  ', val.serializedResult);
    chromeProc.kill();
    try {
      fs.rmSync(userDataDir, { recursive: true, force: true });
    } catch {}
    process.exit(1);
  }

  console.log('9. SUCCESS: Formula and subsequent typing are 100% verified in real Chromium!');
  chromeProc.kill();
  try {
    fs.rmSync(userDataDir, { recursive: true, force: true });
  } catch {}
  console.log('=== REAL-BROWSER CARET ALGORITHM SMOKE TEST PASSED ===');
  process.exit(0);
}

runChromeCaretSmoke().catch((err) => {
  console.error('Chrome smoke error:', err);
  process.exit(1);
});
