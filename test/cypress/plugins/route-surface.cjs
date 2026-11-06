const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const HTTP = new Map(['Get', 'Post', 'Put', 'Patch', 'Delete', 'Head', 'Options'].map(verb => [`Http${verb}`, verb.toUpperCase()]));
const PUBLIC_ACTIONS = new Set(['AuthController.Login', 'AuthController.Refresh', 'AuthController.Logout', 'HealthController.Live', 'HealthController.Ready']);
const GUID = '11111111-1111-4111-8111-111111111111';

// A deliberately bounded C# attribute reader, not a general C# compiler. It reads
// the checked-in controllers on every run and refuses unsupported route syntax.
// Strings/comments are lexical tokens so text inside an action cannot invent routes.
function tokens(source) {
  const output = [];
  let i = 0;
  function quoted(start, prefix = '') {
    const verbatim = prefix.includes('@');
    const interpolated = prefix.includes('$');
    let cursor = start + prefix.length;
    const delimiter = source.slice(cursor).match(/^"+/)?.[0];
    if (delimiter && delimiter.length >= 3) {
      const end = source.indexOf(delimiter, cursor + delimiter.length);
      assert.ok(end >= 0, 'Unterminated C# raw string.');
      return { end: end + delimiter.length, value: source.slice(cursor + delimiter.length, end), literal: false };
    }
    assert.equal(source[cursor], '"');
    cursor++;
    let content = '';
    while (cursor < source.length) {
      const c = source[cursor];
      if (!verbatim && c === '\\') { content += source.slice(cursor, cursor + 2); cursor += 2; continue; }
      if (verbatim && c === '"' && source[cursor + 1] === '"') { content += '"'; cursor += 2; continue; }
      if (c === '"') return { end: cursor + 1, value: verbatim ? content : JSON.parse(`"${content}"`), literal: !interpolated };
      if (interpolated && c === '{' && source[cursor + 1] !== '{') {
        // Interpolation may contain ordinary/nested strings and braces. Its
        // content is opaque here; only action-level braces must be tokenized.
        let depth = 1; cursor++;
        while (cursor < source.length && depth) {
          const nestedPrefix = source.slice(cursor).match(/^(?:\$@|@\$|\$|@)?(?=")/)?.[0];
          if (source[cursor] === '"' || nestedPrefix) { cursor = quoted(cursor, nestedPrefix || '').end; continue; }
          if (source[cursor] === '{') depth++;
          if (source[cursor] === '}') depth--;
          cursor++;
        }
        assert.equal(depth, 0, 'Unterminated interpolation.');
        continue;
      }
      if (interpolated && (source.slice(cursor, cursor + 2) === '{{' || source.slice(cursor, cursor + 2) === '}}')) {
        content += source.slice(cursor, cursor + 2); cursor += 2; continue;
      }
      content += c; cursor++;
    }
    throw new Error('Unterminated C# string.');
  }
  while (i < source.length) {
    if (/\s/.test(source[i])) { i++; continue; }
    if (source.slice(i, i + 2) === '//') { const end = source.indexOf('\n', i); i = end < 0 ? source.length : end + 1; continue; }
    if (source.slice(i, i + 2) === '/*') { const end = source.indexOf('*/', i + 2); assert.ok(end >= 0, 'Unterminated comment.'); i = end + 2; continue; }
    const prefix = source.slice(i).match(/^(?:\$@|@\$|\$|@)?(?=")/)?.[0];
    if (source[i] === '"' || prefix) {
      const string = quoted(i, prefix || ''); output.push({ type: 'string', value: string.value, literal: string.literal }); i = string.end; continue;
    }
    if (source[i] === "'") {
      i++; if (source[i] === '\\') i += 2; else i++;
      assert.equal(source[i++], "'", 'Unsupported/unterminated character literal.');
      output.push({ type: 'char', value: '' }); continue;
    }
    const identifier = source.slice(i).match(/^[A-Za-z_][A-Za-z0-9_]*/)?.[0];
    if (identifier) { output.push({ type: 'id', value: identifier }); i += identifier.length; continue; }
    if (source.slice(i, i + 2) === '=>') { output.push({ value: '=>' }); i += 2; continue; }
    output.push({ value: source[i++] });
  }
  return output;
}

function matching(list, start, open, close) {
  assert.equal(list[start]?.value, open);
  let depth = 0;
  for (let i = start; i < list.length; i++) {
    if (list[i].type === 'string' || list[i].type === 'char') continue;
    if (list[i].value === open) depth++;
    if (list[i].value === close && --depth === 0) return i;
  }
  throw new Error(`Unbalanced ${open}${close} in controller source.`);
}

function splitAttributes(list) {
  const groups = []; let start = 0; let depth = 0;
  for (let i = 0; i <= list.length; i++) {
    const value = list[i]?.value;
    if (value === '(') depth++;
    if (value === ')') depth--;
    if (i === list.length || value === ',' && depth === 0) { groups.push(list.slice(start, i)); start = i + 1; }
  }
  return groups.filter(group => group.length).map(group => {
    const open = group.findIndex(token => token.value === '(');
    const names = (open < 0 ? group : group.slice(0, open)).filter(token => token.type === 'id');
    assert.ok(names.length, 'Unsupported attribute declaration.');
    const name = names.at(-1).value.replace(/Attribute$/, '');
    if (open >= 0) assert.equal(matching(group, open, '(', ')'), group.length - 1, 'Unsupported attribute arguments.');
    const args = open < 0 ? [] : group.slice(open + 1, -1);
    if (name === 'AcceptVerbs' || /^Http[A-Z]/.test(name) && !HTTP.has(name)) throw new Error(`Unsupported HTTP attribute ${name}.`);
    return { name, args };
  });
}

function readAttributes(list, start) {
  const attributes = []; let next = start;
  while (list[next]?.value === '[') {
    const end = matching(list, next, '[', ']');
    attributes.push(...splitAttributes(list.slice(next + 1, end))); next = end + 1;
  }
  return { attributes, next };
}

function routeValue(attribute, optional = false) {
  if (optional && attribute.args.length === 0) return '';
  assert.equal(attribute.args.length, 1, 'Route templates must be one literal string; computed/named templates need an explicit reader update.');
  assert.equal(attribute.args[0]?.type, 'string', 'Route template is not a string literal.');
  assert.equal(attribute.args[0].literal, true, 'Interpolated/raw route templates are unsupported.');
  return attribute.args[0].value;
}

function requestPath(template, controller, action, header) {
  let result = template.replace(/\[controller\]/gi, controller.replace(/Controller$/, '').toLowerCase()).replace(/\[action\]/gi, action);
  assert.ok(!/[\[\]]/.test(result), 'Unsupported route token.');
  result = result.replace(/\{([^}]+)\}/g, (_, parameter) => {
    const match = /^([A-Za-z_]\w*)(?::(guid|int|long|ulong))?$/.exec(parameter);
    assert.ok(match, `Unsupported route parameter ${parameter}.`);
    const [, name, constraint] = match;
    const position = header.findIndex(token => token.value === name);
    const parameterType = position > 0 ? header[position - 1].value : '';
    if (constraint === 'guid' || parameterType === 'Guid') return GUID;
    if (constraint || ['ulong', 'long', 'uint', 'int', 'byte'].includes(parameterType) || /^(?:question|attempt|analysis|analysisJob|node|edge|activityRun|sessionRun)Id$/i.test(name) || name === 'id' && controller === 'QuestionsController') return '1';
    return /Id$/i.test(name) ? GUID : 'fixture';
  });
  assert.ok(!/[{}?#]/.test(result) && !result.includes('..'), 'Unsupported route path.');
  result = '/' + result.replace(/^~?\//, '').replace(/\/+/g, '/');
  assert.ok(result.startsWith('/api/v1/'), 'Route outside the expected API prefix.');
  return result;
}

function parseController(source, filename = 'controller.cs') {
  const list = tokens(source); const routes = []; const controllers = [];
  let consumedHttp = 0;
  for (let index = 0; index < list.length; index++) {
    if (list[index].value !== 'class' || !list[index + 1]?.value.endsWith('Controller')) continue;
    const controller = list[index + 1].value; controllers.push(controller);
    let declaration = index - 1;
    while (declaration >= 0 && ['public', 'sealed', 'partial', 'abstract'].includes(list[declaration].value)) declaration--;
    const classAttributes = [];
    while (list[declaration]?.value === ']') {
      let start = declaration; let depth = 1;
      while (--start >= 0) {
        if (list[start].type === 'string') continue;
        if (list[start].value === ']') depth++;
        if (list[start].value === '[' && --depth === 0) break;
      }
      assert.ok(start >= 0); classAttributes.unshift(...splitAttributes(list.slice(start + 1, declaration))); declaration = start - 1;
    }
    const classRoutes = classAttributes.filter(a => a.name === 'Route').map(a => routeValue(a));
    assert.equal(classRoutes.length, 1, `${controller}: expected one explicit class Route.`);
    let body = index + 2;
    while (body < list.length && list[body].value !== '{') body++;
    const classEnd = matching(list, body, '{', '}');
    for (let member = body + 1; member < classEnd;) {
      if (list[member].value === ';') { member++; continue; }
      const { attributes, next } = readAttributes(list, member); member = next;
      const headerStart = member; let parameterStart = -1;
      while (member < classEnd) {
        const value = list[member].value;
        if (value === '(') {
          if (parameterStart < 0) parameterStart = member;
          member = matching(list, member, '(', ')') + 1; continue;
        }
        if (['{', ';', '=>'].includes(value)) break;
        member++;
      }
      assert.ok(member < classEnd, `${controller}: unparsed member.`);
      const header = list.slice(headerStart, member);
      const http = attributes.filter(a => HTTP.has(a.name)); consumedHttp += http.length;
      if (!http.length && header.some(t => t.value === 'public') && parameterStart >= 0 && list[parameterStart - 1].value !== controller)
        throw new Error(`${controller}: public action without an explicit supported HTTP verb.`);
      if (attributes.some(a => a.name === 'Route') && http.length === 0) throw new Error(`${controller}: method Route without explicit HTTP verb is unsupported.`);
      if (http.length) {
        assert.ok(header.some(t => t.value === 'public') && parameterStart >= 0, `${controller}: HTTP attribute is not attached to a public action.`);
        const action = list[parameterStart - 1].value;
        assert.ok(list[parameterStart - 1].type === 'id', 'Unsupported generic action name.');
        const methodRoutes = attributes.filter(a => a.name === 'Route').map(a => routeValue(a));
        assert.ok(methodRoutes.length === 0 || http.every(a => a.args.length === 0), 'Ambiguous Route/Http template combination.');
        const anonymous = [...classAttributes, ...attributes].some(a => a.name === 'AllowAnonymous');
        const authorized = !anonymous && [...classAttributes, ...attributes].some(a => a.name === 'Authorize');
        if (!authorized) assert.ok(PUBLIC_ACTIONS.has(`${controller}.${action}`), `Unreviewed anonymous endpoint ${controller}.${action}.`);
        for (const verb of http) for (const suffix of methodRoutes.length ? methodRoutes : [routeValue(verb, true)]) {
          const template = suffix.startsWith('/') || suffix.startsWith('~/') ? suffix : `${classRoutes[0]}${suffix ? '/' + suffix : ''}`;
          routes.push({ controller, action, method: HTTP.get(verb.name), template, path: requestPath(template, controller, action, header), protected: authorized });
        }
      }
      if (list[member].value === '{') { member = matching(list, member, '{', '}') + 1; }
      else if (list[member].value === '=>') {
        member++;
        while (member < classEnd && list[member].value !== ';') {
          if (list[member].value === '{') member = matching(list, member, '{', '}') + 1;
          else if (list[member].value === '(') member = matching(list, member, '(', ')') + 1;
          else member++;
        }
        assert.ok(member < classEnd, 'Unterminated expression-bodied member.'); member++;
      } else member++;
    }
    index = classEnd;
  }
  const declaredHttp = list.filter(token => token.type === 'id' && HTTP.has(token.value)).length;
  assert.equal(consumedHttp, declaredHttp, `${filename}: HTTP attribute was omitted from catalog.`);
  assert.ok(controllers.length > 0 && routes.length > 0, `${filename}: no controller/actions found.`);
  return { controllers, routes };
}

function catalog(root = path.resolve(__dirname, '../../..')) {
  const directory = path.join(root, 'src/EduTwin.API/Controllers');
  const files = fs.readdirSync(directory).filter(file => file.endsWith('.cs')).sort();
  assert.ok(files.length > 0, 'Controller source is required to derive route coverage.');
  const controllers = []; const routes = [];
  for (const filename of files) {
    const parsed = parseController(fs.readFileSync(path.join(directory, filename), 'utf8'), filename);
    controllers.push(...parsed.controllers); routes.push(...parsed.routes);
  }
  assert.equal(new Set(controllers).size, controllers.length, 'Duplicate controller declaration.');
  const routeKey = r => `${r.method} ${r.template.replace(/\[controller\]/gi, r.controller.replace(/Controller$/, '').toLowerCase()).replace(/\[action\]/gi, r.action)}`;
  assert.equal(new Set(routes.map(routeKey)).size, routes.length, 'Duplicate method/template needs explicit coverage review.');
  return { controllers, protected: routes.filter(r => r.protected), anonymous: routes.filter(r => !r.protected) };
}

function protectedRoutes(root) { return catalog(root).protected; }
module.exports = { tokens, parseController, requestPath, catalog, protectedRoutes };
