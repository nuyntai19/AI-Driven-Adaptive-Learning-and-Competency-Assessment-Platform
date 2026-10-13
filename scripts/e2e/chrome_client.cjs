const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn } = require('node:child_process');

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const WORKSPACE_DIR = process.env.WORKSPACE_ROOT || path.resolve(__dirname, '../..');
const EVIDENCE_DIR = path.join(WORKSPACE_DIR, 'docs/verification/evidence/assignment_submission_acceptance');
fs.mkdirSync(EVIDENCE_DIR, { recursive: true });

// Historical diagnostic client, retained for traceability only. It must never
// authenticate against the primary database or be mistaken for acceptance.
const SEED_PASSWORD = require('./acceptance_stack.cjs').testPassword;

function chromePath() {
  const candidates = [
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    path.join(process.env.LOCALAPPDATA || '', 'Google', 'Chrome', 'Application', 'chrome.exe'),
  ];
  const p = candidates.find((c) => fs.existsSync(c));
  if (!p) throw new Error('Chrome executable not found');
  return p;
}

class ChromeClient {
  constructor(sharedProfile = null) {
    throw new Error('Retired diagnostic harness: its logs did not prove acceptance. Use assignment_api_acceptance.cjs against acceptance_stack.cjs and perform browser checks separately (see scripts/e2e/README.md).');
    this.chrome = null;
    this.profile = sharedProfile;
    this.isSharedProfile = Boolean(sharedProfile);
    this.ws = null;
    this.id = 0;
    this.pending = new Map();
    this.sessionId = null;
    this.targetId = null;
    this.consoleLogs = [];
    this.pageErrors = [];
    this.networkErrors = [];
  }

  async start(options = {}) {
    const cp = chromePath();
    if (!this.profile) {
      this.profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_chrome_e2e_'));
    }

    const chromeArgs = [
      '--headless=new',
      '--remote-debugging-port=0',
      `--user-data-dir=${this.profile}`,
      '--no-first-run',
      '--no-default-browser-check',
      '--disable-gpu',
      '--window-size=1440,900',
      'about:blank',
    ];

    this.chrome = spawn(cp, chromeArgs, { stdio: 'ignore' });

    const portFile = path.join(this.profile, 'DevToolsActivePort');
    for (let i = 0; i < 60 && !fs.existsSync(portFile); i++) {
      await delay(100);
    }
    if (!fs.existsSync(portFile)) throw new Error('Chrome startup timed out');

    const [debugPort, browserPath] = fs.readFileSync(portFile, 'utf8').trim().split(/\r?\n/);
    this.debugPort = debugPort;
    this.browserPath = browserPath;
    this.ws = new WebSocket(`ws://127.0.0.1:${debugPort}${browserPath}`);
    await new Promise((resolve, reject) => {
      this.ws.onopen = resolve;
      this.ws.onerror = reject;
    });

    this.ws.onmessage = ({ data }) => {
      const msg = JSON.parse(data);
      if (msg.id) {
        const req = this.pending.get(msg.id);
        if (!req) return;
        this.pending.delete(msg.id);
        if (msg.error) req.reject(new Error(JSON.stringify(msg.error)));
        else req.resolve(msg.result);
      } else if (msg.method === 'Runtime.consoleAPICalled') {
        const text = msg.params.args?.map(a => a.value ?? a.description ?? '').join(' ') || '';
        this.consoleLogs.push({ type: msg.params.type, text, time: Date.now() });
      } else if (msg.method === 'Runtime.exceptionThrown') {
        const desc = msg.params.exceptionDetails?.exception?.description || msg.params.exceptionDetails?.text;
        this.pageErrors.push(desc);
      } else if (msg.method === 'Network.responseReceived') {
        if (msg.params.response.status >= 400 && msg.params.response.status !== 409) {
          this.networkErrors.push({
            url: msg.params.response.url,
            status: msg.params.response.status,
            statusText: msg.params.response.statusText
          });
        }
      }
    };

    const { targetId } = await this.send('Target.createTarget', { url: 'about:blank' });
    this.targetId = targetId;
    const { sessionId } = await this.send('Target.attachToTarget', { targetId, flatten: true });
    this.sessionId = sessionId;

    await this.call('Page.enable');
    await this.call('Runtime.enable');
    await this.call('Network.enable');
    await this.call('Emulation.setDeviceMetricsOverride', {
      width: options.width || 1440,
      height: options.height || 900,
      deviceScaleFactor: 1,
      mobile: Boolean(options.mobile),
    });
  }

  // Create a second tab attached to the same browser instance / profile
  async createTab(url = 'about:blank') {
    const { targetId } = await this.send('Target.createTarget', { url });
    const { sessionId } = await this.send('Target.attachToTarget', { targetId, flatten: true });

    const tabClient = new ChromeTab(this, targetId, sessionId);
    await tabClient.init();
    return tabClient;
  }

  send(method, params = {}) {
    return new Promise((resolve, reject) => {
      const reqId = ++this.id;
      this.pending.set(reqId, { resolve, reject });
      this.ws.send(JSON.stringify({ id: reqId, method, params, ...(this.sessionId ? { sessionId: this.sessionId } : {}) }));
    });
  }

  call(method, params = {}) {
    return this.send(method, params);
  }

  async eval(expression) {
    const res = await this.call('Runtime.evaluate', {
      expression,
      returnByValue: true,
      awaitPromise: true,
    });
    if (res.exceptionDetails) {
      throw new Error(`Eval failed: ${res.exceptionDetails.text} (${res.exceptionDetails.exception?.description || ''})`);
    }
    return res.result?.value;
  }

  async navigate(url) {
    await this.call('Page.navigate', { url });
    await delay(1200);
  }

  async waitSelector(selector, timeoutMs = 10000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const exists = await this.eval(`Boolean(document.querySelector(${JSON.stringify(selector)}))`);
      if (exists) return true;
      await delay(150);
    }
    throw new Error(`Timeout waiting for selector: ${selector}`);
  }

  async click(selector) {
    await this.waitSelector(selector);
    const rect = await this.eval(`(() => {
      const el = document.querySelector(${JSON.stringify(selector)});
      if (!el) throw new Error('Missing element ' + ${JSON.stringify(selector)});
      el.scrollIntoView({ behavior: 'instant', block: 'center' });
      const r = el.getBoundingClientRect();
      return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
    })()`);
    await this.call('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...rect });
    await this.call('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...rect });
    await delay(300);
  }

  async fill(selector, value) {
    await this.waitSelector(selector);
    await this.eval(`(() => {
      const el = document.querySelector(${JSON.stringify(selector)});
      if (!el) throw new Error('Missing element ' + ${JSON.stringify(selector)});
      el.focus();
      const proto = el instanceof HTMLTextAreaElement ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
      const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
      if (setter) {
        setter.call(el, ${JSON.stringify(value)});
      } else {
        el.value = ${JSON.stringify(value)};
      }
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    })()`);
    await delay(200);
  }

  async setNetworkOffline(offline = true) {
    await this.call('Network.emulateNetworkConditions', {
      offline,
      latency: offline ? 0 : 20,
      downloadThroughput: offline ? 0 : 10000000,
      uploadThroughput: offline ? 0 : 10000000,
    });
  }

  async setBlockedUrls(urls = []) {
    await this.call('Network.setBlockedURLs', { urls });
  }

  async setDarkMode(dark = true) {
    await this.call('Emulation.setEmulatedMedia', {
      media: 'screen',
      features: [{ name: 'prefers-color-scheme', value: dark ? 'dark' : 'light' }],
    });
    await this.eval(`(() => {
      if (${dark}) document.documentElement.classList.add('dark');
      else document.documentElement.classList.remove('dark');
    })()`);
    await delay(300);
  }

  async setViewport(width, height, mobile = false) {
    await this.call('Emulation.setDeviceMetricsOverride', {
      width,
      height,
      deviceScaleFactor: 1,
      mobile,
    });
    await delay(300);
  }

  async captureScreenshot(filename) {
    await delay(600);
    const { data } = await this.call('Page.captureScreenshot', { format: 'png' });
    const fullPath = path.join(EVIDENCE_DIR, filename);
    fs.writeFileSync(fullPath, Buffer.from(data, 'base64'));
    return fullPath;
  }

  async close() {
    if (this.ws) {
      try { this.ws.close(); } catch {}
    }
    if (this.chrome) {
      try { this.chrome.kill(); } catch {}
    }
    if (this.profile && !this.isSharedProfile) {
      try { fs.rmSync(this.profile, { recursive: true, force: true }); } catch {}
    }
  }
}

class ChromeTab {
  constructor(client, targetId, sessionId) {
    this.client = client;
    this.targetId = targetId;
    this.sessionId = sessionId;
  }

  async init() {
    await this.call('Page.enable');
    await this.call('Runtime.enable');
    await this.call('Network.enable');
  }

  send(method, params = {}) {
    return new Promise((resolve, reject) => {
      const reqId = ++this.client.id;
      this.client.pending.set(reqId, { resolve, reject });
      this.client.ws.send(JSON.stringify({ id: reqId, method, params, sessionId: this.sessionId }));
    });
  }

  call(method, params = {}) {
    return this.send(method, params);
  }

  async eval(expression) {
    const res = await this.call('Runtime.evaluate', {
      expression,
      returnByValue: true,
      awaitPromise: true,
    });
    if (res.exceptionDetails) {
      throw new Error(`Tab eval failed: ${res.exceptionDetails.text} (${res.exceptionDetails.exception?.description || ''})`);
    }
    return res.result?.value;
  }

  async navigate(url) {
    await this.call('Page.navigate', { url });
    await delay(1200);
  }

  async waitSelector(selector, timeoutMs = 10000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const exists = await this.eval(`Boolean(document.querySelector(${JSON.stringify(selector)}))`);
      if (exists) return true;
      await delay(150);
    }
    throw new Error(`Timeout waiting for selector: ${selector}`);
  }

  async click(selector) {
    await this.waitSelector(selector);
    const rect = await this.eval(`(() => {
      const el = document.querySelector(${JSON.stringify(selector)});
      if (!el) throw new Error('Missing element ' + ${JSON.stringify(selector)});
      el.scrollIntoView({ behavior: 'instant', block: 'center' });
      const r = el.getBoundingClientRect();
      return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
    })()`);
    await this.call('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...rect });
    await this.call('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...rect });
    await delay(300);
  }

  async fill(selector, value) {
    await this.waitSelector(selector);
    await this.eval(`(() => {
      const el = document.querySelector(${JSON.stringify(selector)});
      if (!el) throw new Error('Missing element ' + ${JSON.stringify(selector)});
      el.focus();
      const proto = el instanceof HTMLTextAreaElement ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
      const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
      if (setter) {
        setter.call(el, ${JSON.stringify(value)});
      } else {
        el.value = ${JSON.stringify(value)};
      }
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    })()`);
    await delay(200);
  }

  async captureScreenshot(filename) {
    await delay(600);
    const { data } = await this.call('Page.captureScreenshot', { format: 'png' });
    const fullPath = path.join(EVIDENCE_DIR, filename);
    fs.writeFileSync(fullPath, Buffer.from(data, 'base64'));
    return fullPath;
  }

  async close() {
    await this.client.send('Target.closeTarget', { targetId: this.targetId });
  }
}

async function loginUser(client, username, centerCode = 'EDUTWIN_A') {
  await client.navigate('http://localhost:3000/dang-nhap');
  await client.waitSelector('#centerCode');
  await client.fill('#centerCode', centerCode);
  await client.fill('#username', username);
  await client.fill('#password', SEED_PASSWORD);
  await client.click('button[type="submit"]');

  for (let i = 0; i < 40; i++) {
    const loc = await client.eval('window.location.pathname');
    if (loc !== '/dang-nhap') {
      break;
    }
    const err = await client.eval('document.querySelector("[role=\'alert\']")?.innerText');
    if (err) throw new Error(`Login failed on UI: ${err}`);
    await delay(200);
  }
  await delay(1000);
}

module.exports = { ChromeClient, loginUser, delay, EVIDENCE_DIR, SEED_PASSWORD, WORKSPACE_DIR };
