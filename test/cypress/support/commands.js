const { expectProblem } = require('./validation');
const { loginScreen } = require('../screens/login.screen');
const { routes } = require('../screens/routes');

Cypress.Commands.add('apiLogin', role => cy.task('isolated:login', role, { log: false }));
Cypress.Commands.add('api', (token, method, route, body, options = {}) => {
  expect(route, 'relative API path').to.match(/^\/(?!\/)/);
  return cy.request({ method, url: `/api/v1${route}`, failOnStatusCode: false,
    headers: token ? { authorization: `Bearer ${token}` } : {},
    ...(body === undefined ? {} : { body }), ...options, log: false });
});
Cypress.Commands.add('expectRejected', (token, method, route, body, status = 400, errorCode = 'VALIDATION_FAILED') =>
  cy.api(token, method, route, body).then(response => expectProblem(response, status, errorCode)));
Cypress.Commands.add('uiLogin', role => {
  cy.task('isolated:credentials', role, { log: false }).then(account => {
    loginScreen.open();
    loginScreen.fill(account);
    loginScreen.submit();
    cy.location('pathname').should('not.eq', routes.login);
  });
});
