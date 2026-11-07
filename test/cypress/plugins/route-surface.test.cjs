const { test } = require('node:test');
const assert = require('node:assert/strict');
const { parseController, requestPath, catalog } = require('./route-surface.cjs');

const source = members => `
using Microsoft.AspNetCore.Mvc;
[ApiController, Route("api/v1/[controller]"), Authorize]
public sealed class SamplesController(object dependency) : ControllerBase {
  public SamplesController() { }
  ${members}
}`;

test('derives inline/composite attributes, token substitution and constrained parameters', () => {
  const parsed = parseController(source(`
    [HttpGet("{id:guid}"), Authorize(Policy = "sample.read")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Ok();
    [HttpPost, Authorize(Policy = "sample.create")]
    public Task<IActionResult> Create([FromBody] object request) { return null; }
    [HttpGet("/api/v1/special/{attemptId:long}")]
    public IActionResult Absolute(long attemptId) => Ok();
  `));
  assert.equal(parsed.routes.length, 3);
  assert.equal(parsed.routes[0].path, '/api/v1/samples/11111111-1111-4111-8111-111111111111');
  assert.equal(parsed.routes[1].method, 'POST');
  assert.equal(parsed.routes[1].path, '/api/v1/samples');
  assert.equal(parsed.routes[2].path, '/api/v1/special/1');
  assert.ok(parsed.routes.every(r => r.protected));
});

test('uses method authorization when the class has no authorization attribute', () => {
  const text = '[Route("api/v1/auth")] public class AuthController : ControllerBase { [HttpGet("me"), Authorize] public IActionResult GetCurrentUser() => Ok(); }';
  assert.equal(parseController(text).routes[0].protected, true);
});

test('records explicit public auth/health routes separately', () => {
  const text = '[Route("api/v1/auth"), Authorize] public class AuthController : ControllerBase { [HttpPost("login"), AllowAnonymous] public IActionResult Login() => Ok(); }';
  assert.equal(parseController(text).routes[0].protected, false);
});

test('comments, body strings and response dictionaries cannot create fake endpoints', () => {
  const parsed = parseController(source(`
    // [HttpDelete("fake")]
    /* [HttpPatch("also-fake")] */
    [HttpGet]
    public IActionResult List() {
      var text = "[HttpDelete(\\\"fake\\\")]";
      var interpolation = $"value {new { Name = "body" }}";
      var raw = """[HttpPost("raw")]""";
      return Ok(new Dictionary<string,string> { ["field"] = text });
    }
  `));
  assert.equal(parsed.routes.length, 1);
  assert.equal(parsed.routes[0].action, 'List');
});

test('supports a separate method Route with a verb and the absolute ~/ form', () => {
  const parsed = parseController(source('[HttpPut, Route("~/api/v1/moved/{nodeId}")] public IActionResult Update(ulong nodeId) => Ok();'));
  assert.equal(parsed.routes[0].path, '/api/v1/moved/1');
});

for (const [name, member] of [
  ['computed route', '[HttpGet(Routes.Dynamic)] public IActionResult List() => Ok();'],
  ['interpolated route', '[HttpGet($"dynamic")] public IActionResult List() => Ok();'],
  ['unknown verb', '[HttpTrace("x")] public IActionResult List() => Ok();'],
  ['AcceptVerbs', '[AcceptVerbs("GET")] public IActionResult List() => Ok();'],
  ['unsupported optional constraint', '[HttpGet("{id:guid?}")] public IActionResult Detail(Guid id) => Ok();'],
  ['verb-less public action', 'public IActionResult List() => Ok();'],
  ['unknown public endpoint', '[HttpGet, AllowAnonymous] public IActionResult List() => Ok();'],
]) {
  test(`fails closed on ${name}`, () => assert.throws(() => parseController(source(member))));
}

test('fails closed on an unconsumed HTTP attribute outside a controller', () => {
  assert.throws(() => parseController(source('[HttpGet] public IActionResult List() => Ok();') + '\n[HttpPost] public class Helper { }'));
});

test('dummy parameter values respect Guid and numeric route contracts', () => {
  assert.equal(requestPath('api/v1/learning/{questionId}', 'LearningController', 'Get', [{ value: 'string' }, { value: 'questionId' }]), '/api/v1/learning/1');
  assert.equal(requestPath('api/v1/questions/{id}', 'QuestionsController', 'Get', [{ value: 'string' }, { value: 'id' }]), '/api/v1/questions/1');
  assert.throws(() => requestPath('https://foreign.invalid/path', 'SamplesController', 'Get', []));
});

test('the entire current source surface is represented and only five endpoints are public', () => {
  const result = catalog();
  assert.equal(result.controllers.length, 32, 'Review and update this baseline when adding a controller.');
  assert.equal(new Set(result.protected.map(r => r.controller)).size, 31);
  assert.ok(result.protected.length >= 200, 'Unexpectedly small route surface indicates a parser omission.');
  assert.deepEqual(result.anonymous.map(r => `${r.controller}.${r.action}`).sort(), [
    'AuthController.Login', 'AuthController.Logout', 'AuthController.Refresh', 'HealthController.Live', 'HealthController.Ready',
  ]);
  for (const route of result.protected) {
    assert.match(route.path, /^\/api\/v1\//);
    assert.doesNotMatch(route.path, /[{}[\]]/);
    assert.ok(['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'].includes(route.method));
  }
});
