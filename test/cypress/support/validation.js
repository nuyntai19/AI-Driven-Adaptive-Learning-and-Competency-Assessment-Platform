function expectProblem(response, status = 400, errorCode = 'VALIDATION_FAILED') {
  expect(response.status, 'HTTP rejection').to.eq(status);
  // ASP.NET's JwtBearer challenge is allowed to be a bodyless401; domain validation still requires ProblemDetails.
  if (status === 401 && errorCode === null && (response.body === '' || response.body === null)) return;
  expect(response.body, 'Problem Details').to.be.an('object');
  if (errorCode) expect(response.body.errorCode, 'error code').to.eq(errorCode);
  expect(JSON.stringify(response.body), 'no infrastructure/secret disclosure')
    .not.to.match(/(?:at Microsoft\.|MySqlException|stackTrace|PasswordHash|SigningKey|Server=mysql)/i);
}
module.exports = { expectProblem };
