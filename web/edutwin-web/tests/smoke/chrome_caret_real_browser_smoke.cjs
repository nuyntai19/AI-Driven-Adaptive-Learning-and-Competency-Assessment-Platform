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

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn, execSync } = require('child_process');

function resolveChromePath() {
  if (process.env.CHROME_PATH) {
    return fs.existsSync(process.env.CHROME_PATH) ? process.env.CHROME_PATH : null;
  }

  const candidates = [
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    path.join(process.env.LOCALAPPDATA || '', 'Google\\Chrome\\Application\\chrome.exe'),
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
    '/usr/bin/chromium-browser',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  ].filter(Boolean);

  for (const c of candidates) {
    if (fs.existsSync(c)) {
      return c;
    }
  }
  return null;
}

function waitForProcessExit(proc, timeoutMs = 3000) {
  return new Promise((resolve) => {
    let resolved = false;
    const timer = setTimeout(() => {
      if (!resolved) {
        resolved = true;
        resolve(false);
      }
    }, timeoutMs);

    proc.once('exit', () => {
      if (!resolved) {
        resolved = true;
        clearTimeout(timer);
        resolve(true);
      }
    });
  });
}

function isProcessAlive(pid) {
  if (!pid) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

async function runChromeCaretSmoke() {
  const chromePath = resolveChromePath();
  if (!chromePath) {
    console.log('[SKIPPED] Chrome executable not found. Real-browser smoke test skipped.');
    process.exitCode = 0;
    return;
  }

  const userDataDir = path.join(os.tmpdir(), 'chrome_caret_smoke_' + Date.now() + '_' + Math.random().toString(36).slice(2, 8));
  fs.mkdirSync(userDataDir, { recursive: true });

  let chromeProc = null;
  let ws = null;
  let isTestPassed = false;
  let testError = null;

  try {
    console.log('1. Launching headless Chrome with ephemeral port (--remote-debugging-port=0)...');
    chromeProc = spawn(chromePath, [
      '--headless=new',
      '--remote-debugging-port=0',
      `--user-data-dir=${userDataDir}`,
      '--no-first-run',
      '--no-default-browser-check',
      '--disable-gpu',
      'about:blank'
    ], { stdio: ['ignore', 'ignore', 'ignore'] });

    const chromePid = chromeProc.pid;
    console.log('   Chrome spawned with PID:', chromePid);

    // Wait for DevToolsActivePort
    const portFilePath = path.join(userDataDir, 'DevToolsActivePort');
    let devToolsInfo = null;

    for (let i = 0; i < 50; i++) {
      await new Promise((r) => setTimeout(r, 100));
      if (fs.existsSync(portFilePath)) {
        try {
          const raw = fs.readFileSync(portFilePath, 'utf8').trim().split(/\r?\n/);
          if (raw.length >= 2 && raw[0]) {
            devToolsInfo = {
              port: raw[0].trim(),
              browserPath: raw[1].trim()
            };
            break;
          }
        } catch {}
      }
    }

    if (!devToolsInfo) {
      throw new Error('Timed out waiting for Chrome DevToolsActivePort to be written.');
    }

    const wsUrl = `ws://127.0.0.1:${devToolsInfo.port}${devToolsInfo.browserPath.startsWith('/') ? devToolsInfo.browserPath : '/' + devToolsInfo.browserPath}`;
    console.log('2. Connected to Chrome DevTools endpoint:', wsUrl);

    ws = new WebSocket(wsUrl);
    let msgId = 1;
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

    await new Promise((resolve, reject) => {
      ws.onopen = resolve;
      ws.onerror = reject;
    });

    function send(method, params = {}) {
      const id = msgId++;
      return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        ws.send(JSON.stringify({ id, method, params }));
      });
    }

    const { targetId } = await send('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });

    function sendPage(method, params = {}) {
      const id = msgId++;
      return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        ws.send(JSON.stringify({ id, sessionId, method, params }));
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
        <button id="casio-button" class="casio-btn">External Button: Casio Formula Insertion</button>
        <div id="status-log" style="margin-top: 15px; font-family: monospace; font-size: 12px; color: #38bdf8;"></div>
      </body>
      </html>
    `;

    await sendPage('Page.navigate', {
      url: 'data:text/html;charset=utf-8,' + encodeURIComponent(htmlContent),
    });
    await new Promise((r) => setTimeout(r, 500));

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
      throw new Error(`Real browser caret insertion did not match expected output! Actual: ${val.serializedResult}`);
    }

    console.log('9. SUCCESS: Formula and subsequent typing verified in Chromium!');
    console.log('=== REAL-BROWSER CARET ALGORITHM SMOKE TEST PASSED ===');
    isTestPassed = true;
  } catch (err) {
    testError = err;
    console.error('Smoke test failure:', err.message);
  } finally {
    console.log('10. Cleaning up Chrome processes and temporary profile...');

    // A. Request browser shutdown via CDP
    if (ws && ws.readyState === WebSocket.OPEN) {
      try {
        ws.send(JSON.stringify({ id: 99999, method: 'Browser.close' }));
      } catch {}
      try {
        ws.close();
      } catch {}
    }

    // B. Wait for chromeProc to exit gracefully
    if (chromeProc) {
      const exited = await waitForProcessExit(chromeProc, 3000);
      if (!exited && chromeProc.pid) {
        console.log('   Process still active after timeout, terminating process tree...');
        if (process.platform === 'win32') {
          try {
            execSync(`taskkill /pid ${chromeProc.pid} /T /F`, { stdio: 'ignore' });
          } catch {}
        } else {
          try {
            chromeProc.kill('SIGKILL');
          } catch {}
        }
        await waitForProcessExit(chromeProc, 2000);
      }
    }

    // C. Verify process is no longer alive
    if (chromeProc && chromeProc.pid) {
      const alive = isProcessAlive(chromeProc.pid);
      if (alive) {
        console.warn(`   Warning: Chrome process PID ${chromeProc.pid} is still alive.`);
      } else {
        console.log(`   Chrome process PID ${chromeProc.pid} confirmed terminated.`);
      }
    }

    // D. Delete userDataDir with retry loop
    if (userDataDir && fs.existsSync(userDataDir)) {
      let dirRemoved = false;
      for (let attempt = 0; attempt < 10; attempt++) {
        try {
          fs.rmSync(userDataDir, { recursive: true, force: true });
          if (!fs.existsSync(userDataDir)) {
            dirRemoved = true;
            break;
          }
        } catch {
          await new Promise((r) => setTimeout(r, 150));
        }
      }
      if (dirRemoved) {
        console.log('   Temporary profile directory cleanly removed.');
      } else {
        console.warn(`   Warning: Could not remove temporary profile directory: ${userDataDir}`);
      }
    }

    process.exitCode = isTestPassed ? 0 : 1;
    if (testError) {
      throw testError;
    }
  }
}

runChromeCaretSmoke().catch((err) => {
  console.error('Fatal smoke test runner error:', err.message);
  process.exitCode = 1;
});
