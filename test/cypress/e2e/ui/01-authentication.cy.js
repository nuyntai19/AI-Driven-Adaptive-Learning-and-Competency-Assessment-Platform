const { loginScreen } = require('../../screens/login.screen');
const { studentScreen } = require('../../screens/student.screen');
const { routes } = require('../../screens/routes');

describe('Authentication browser behavior', () => {
  it('the browser blocks empty required fields before issuing a login request', () => {
    let requests = 0;
    cy.intercept('POST', '**/api/v1/auth/login', () => { requests++; });
    loginScreen.open();
    loginScreen.submit();
    loginScreen.centerCode().then($input => expect($input[0].validity.valid).to.eq(false));
    cy.then(() => expect(requests).to.eq(0));
    cy.location('pathname').should('eq', routes.login);
  });
  it('a real browser login reaches the student workspace', () => {
    cy.uiLogin('student');
    studentScreen.workspace().should('be.visible');
    cy.location('pathname').should('not.eq', routes.login);
  });
});
