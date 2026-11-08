const { navigationScreen } = require('../../screens/navigation.screen');
const { routes } = require('../../screens/routes');

describe('Account type browser navigation boundaries', () => {
  it('direct student navigation to platform/teacher administration is denied in the browser', () => {
    cy.uiLogin('student');
    navigationScreen.open(routes.platformCenters);
    cy.location('pathname').should('eq', routes.accessDenied);
    navigationScreen.open(routes.teacherReview);
    cy.location('pathname').should('eq', routes.accessDenied);
  });
});
