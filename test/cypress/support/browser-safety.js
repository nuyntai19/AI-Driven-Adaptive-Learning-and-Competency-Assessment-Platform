// These security assertions deliberately inspect executable DOM, not layout hooks.
function noExecutableMarkup() {
  cy.window().its('__edutwinXss').should('be.undefined');
  cy.get('img[src="cypress-xss"], script[data-cypress-xss], [href^="javascript:"]').should('not.exist');
}
module.exports = { noExecutableMarkup };
