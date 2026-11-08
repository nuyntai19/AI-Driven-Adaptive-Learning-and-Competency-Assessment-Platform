const { expectProblem } = require('../../support/validation');

describe('Authentication API validation and input safety', () => {
  const valid = { centerCode: 'EDUTWIN_A', username: 'student01', password: 'Disposable-wrong-password!' };
  for (const [name, patch] of [
    ['missing center', { centerCode: '' }], ['whitespace center', { centerCode: '   ' }],
    ['oversized center', { centerCode: 'A'.repeat(33) }], ['missing username', { username: '' }],
    ['whitespace username', { username: '   ' }], ['oversized username', { username: 'a'.repeat(101) }],
    ['missing password', { password: '' }],
  ]) {
    it(`rejects ${name} through the real API`, () => cy.expectRejected(null, 'POST', '/auth/login', { ...valid, ...patch }));
  }
  for (const value of ["' OR 1=1 --", '<script>window.__edutwinXss=1</script>', '../student01']) {
    it(`does not authenticate injected identity ${value.slice(0, 24)}`, () => {
      cy.api(null, 'POST', '/auth/login', { ...valid, username: value }).then(r => {
        expectProblem(r, 401, null);
        expect(r.body).not.to.have.property('accessToken');
      });
    });
  }
  it('trims center and username while preserving a valid password', () => {
    cy.task('isolated:credentials', 'student', { log: false }).then(a => {
      cy.api(null, 'POST', '/auth/login', { ...a, centerCode: ` ${a.centerCode} `, username: ` ${a.username} ` })
        .its('status').should('eq', 200);
    });
  });
});
