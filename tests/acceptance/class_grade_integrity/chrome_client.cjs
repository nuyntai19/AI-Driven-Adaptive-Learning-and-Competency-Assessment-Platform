const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawn, execSync } = require('node:child_process');

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const WORKSPACE_DIR = process.env.WORKSPACE_ROOT || path.resolve(__dirname, '../../..');
const EVIDENCE_DIR = path.join(WORKSPACE_DIR, 'docs/verification/evidence/class-grade-integrity-2026-10-04');
fs.mkdirSync(EVIDENCE_DIR, { recursive: true });

function getSeedPassword() {
  if (process.env.SEED_PASSWORD) {
    return process.env.SEED_PASSWORD.trim();
  }
  const envPath = path.join(WORKSPACE_DIR, '.env');
  if (fs.existsSync(envPath)) {
    const envContent = fs.readFileSync(envPath, 'utf8');
    const match = envContent.match(/^Seed__CenterManagerPassword=(.*)$/m);
    if (match) {
      return match[1].trim();
    }
  }
  throw new Error('Cannot find Seed__CenterManagerPassword in environment or .env');
}

const SEED_PASSWORD = getSeedPassword();
const WEB_URL = process.env.WEB_URL || 'http://localhost:3000';
const API_URL = process.env.API_URL || 'http://localhost:5000';

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
  constructor() {
    this.chrome = null;
    this.profile = null;
    this.ws = null;
    this.id = 0;
    this.pending = new Map();
    this.sessionId = null;
  }

  async start() {
    const cp = chromePath();
    this.profile = fs.mkdtempSync(path.join(os.tmpdir(), 'edutwin_chrome_acc_'));
    this.chrome = spawn(cp, [
      '--headless=new',
      '--remote-debugging-port=0',
      `--user-data-dir=${this.profile}`,
      '--no-first-run',
      '--no-default-browser-check',
      '--disable-gpu',
      '--window-size=1440,900',
      'about:blank',
    ], { stdio: 'ignore' });

    const portFile = path.join(this.profile, 'DevToolsActivePort');
    let lines = null;
    for (let i = 0; i < 80; i++) {
      try {
        if (fs.existsSync(portFile)) {
          const content = fs.readFileSync(portFile, 'utf8').trim();
          const parts = content.split(/\r?\n/);
          if (parts.length >= 2 && parts[0]) {
            lines = parts;
            break;
          }
        }
      } catch (e) {
        // file locked, retry
      }
      await delay(200);
    }
    if (!lines) throw new Error('Chrome startup timed out or DevToolsActivePort unreadable');
    const [debugPort, browserPath] = lines;
    this.ws = new WebSocket(`ws://127.0.0.1:${debugPort}${browserPath}`);
    await new Promise((resolve, reject) => {
      this.ws.onopen = resolve;
      this.ws.onerror = reject;
    });

    this.ws.onmessage = ({ data }) => {
      const msg = JSON.parse(data);
      const req = this.pending.get(msg.id);
      if (!req) return;
      this.pending.delete(msg.id);
      if (msg.error) req.reject(new Error(JSON.stringify(msg.error)));
      else req.resolve(msg.result);
    };

    const { targetId } = await this.send('Target.createTarget', { url: 'about:blank' });
    const { sessionId } = await this.send('Target.attachToTarget', { targetId, flatten: true });
    this.sessionId = sessionId;

    await this.call('Page.enable');
    await this.call('Runtime.enable');
    await this.call('Emulation.setDeviceMetricsOverride', {
      width: 1440,
      height: 900,
      deviceScaleFactor: 1,
      mobile: false,
    });
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
    await delay(1000);
  }

  async waitSelector(selector, timeoutMs = 10000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const exists = await this.eval(`Boolean(document.querySelector(${JSON.stringify(selector)}))`);
      if (exists) return true;
      await delay(200);
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

  async selectOption(selector, value) {
    await this.waitSelector(selector);
    await this.eval(`(() => {
      const el = document.querySelector(${JSON.stringify(selector)});
      if (!el) throw new Error('Missing element ' + ${JSON.stringify(selector)});
      el.focus();
      const setter = Object.getOwnPropertyDescriptor(window.HTMLSelectElement.prototype, 'value')?.set;
      if (setter) {
        setter.call(el, ${JSON.stringify(String(value))});
      } else {
        el.value = ${JSON.stringify(String(value))};
      }
      el.dispatchEvent(new Event('change', { bubbles: true }));
    })()`);
    await delay(200);
  }

  async captureScreenshot(filename) {
    await delay(600);
    const { data } = await this.call('Page.captureScreenshot', { format: 'png' });
    const fullPath = path.join(EVIDENCE_DIR, filename);
    fs.writeFileSync(fullPath, Buffer.from(data, 'base64'));
    console.log(`[EVIDENCE] Saved screenshot: ${filename}`);
    return fullPath;
  }

  async close() {
    if (this.ws) {
      try { this.ws.close(); } catch {}
    }
    if (this.chrome && this.chrome.pid) {
      try {
        if (process.platform === 'win32') {
          execSync(`taskkill /PID ${this.chrome.pid} /T /F`, { stdio: 'ignore' });
        } else {
          this.chrome.kill();
        }
      } catch {}
    }
    await delay(400);
    if (this.profile) {
      try { fs.rmSync(this.profile, { recursive: true, force: true }); } catch {}
    }
  }
}

async function loginUser(client, username) {
  console.log(`Logging in as ${username}...`);
  await client.navigate(`${WEB_URL}/dang-nhap`);
  await client.waitSelector('#centerCode');
  await client.fill('#centerCode', 'EDUTWIN_A');
  await client.fill('#username', username);
  await client.fill('#password', SEED_PASSWORD);
  await client.click('button[type="submit"]');

  let redirected = false;
  for (let i = 0; i < 40; i++) {
    const loc = await client.eval('window.location.pathname');
    if (loc !== '/dang-nhap') {
      redirected = true;
      break;
    }
    const err = await client.eval('document.querySelector("[role=\'alert\']")?.innerText');
    if (err) throw new Error(`Login failed on UI for ${username}: ${err}`);
    await delay(200);
  }
  if (!redirected) {
    const curPath = await client.eval('window.location.pathname');
    const err = await client.eval('document.querySelector("[role=\'alert\']")?.innerText');
    throw new Error(`Login redirect timed out on ${curPath}. Error on page: ${err || 'none'}`);
  }
  await delay(1000);
  const currentPath = await client.eval('window.location.pathname');
  console.log(`Login successful for ${username}. Current route: ${currentPath}`);
}

async function logoutUser(client) {
  try {
    await client.call('Network.clearBrowserCookies');
  } catch {}
  await client.eval(`(async () => {
    try { await fetch('/api/v1/auth/logout', { method: 'POST' }); } catch {}
    localStorage.clear();
    sessionStorage.clear();
    window.location.href = '/dang-nhap';
  })()`);
  await delay(1500);
  await client.waitSelector('#centerCode');
}

module.exports = {
  ChromeClient,
  loginUser,
  logoutUser,
  delay,
  EVIDENCE_DIR,
  WORKSPACE_DIR,
  SEED_PASSWORD,
  WEB_URL,
  API_URL,
};
