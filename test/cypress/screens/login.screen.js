const { routes } = require('./routes');

const loginScreen = {
  open: () => cy.visit(routes.login),
  centerCode: () => cy.get('[data-cy="login-center-code"]'),
  username: () => cy.get('[data-cy="login-username"]'),
  password: () => cy.get('[data-cy="login-password"]'),
  submit: () => cy.get('[data-cy="login-submit"]').click(),
  fill(account) {
    loginScreen.centerCode().clear().type(account.centerCode, { log: false });
    loginScreen.username().clear().type(account.username, { log: false });
    return loginScreen.password().clear().type(account.password, { log: false });
  },
};
module.exports = { loginScreen };
