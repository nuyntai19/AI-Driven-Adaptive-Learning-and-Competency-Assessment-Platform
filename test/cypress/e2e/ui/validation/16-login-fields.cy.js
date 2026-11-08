const {
  loginScreen
} = require('../../../screens/login.screen');
describe('Actual login text-field required validation', () => {
  for (const key of ['centerCode', 'username', 'password']) it(`blocks an empty ${key} after filling the other fields`, () => {
    cy.task('isolated:credentials', 'student', {
      log: false
    }).then(account => {
      loginScreen.open();
      loginScreen.fill(account);
      loginScreen[key]().clear();
      let requests = 0;
      cy.intercept('POST', '**/api/v1/auth/login', req => {
        requests++;
        req.continue();
      });
      loginScreen.submit();
      loginScreen[key]().should($el => expect($el[0].validity.valueMissing).to.eq(true));
      cy.then(() => expect(requests, 'the browser must block the incomplete form').to.eq(0));
    });
  });
});
