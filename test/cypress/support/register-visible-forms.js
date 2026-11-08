const {
  formsScreen
} = require('../screens/forms.screen');
const {
  nativeForms,
  password
} = require('../fixtures/ui-validation-cases');
function registerVisibleForms(ids) {
  for (const id of ids) if (!nativeForms.some(flow => flow.id === id)) throw new Error(`Unknown UI form ${id}`);
  describe('Visible governance forms: real typing and browser validation', () => {
    for (const flow of nativeForms.filter(f => ids.includes(f.id))) {
      describe(flow.id, () => {
        for (const key of flow.required || []) it(`blocks empty ${key} before sending a write request`, () => {
          formsScreen.open(flow.id);
          formsScreen.fill(flow.id, flow.base);
          let writes = 0;
          cy.intercept({
            url: '**/api/v1/**',
            method: '+(POST|PUT|PATCH|DELETE)'
          }, req => {
            writes++;
            req.continue();
          });
          formsScreen.fill(flow.id, {
            [key]: ''
          });
          if (formsScreen.flow(flow.id).disabledWhenBlank) {
            formsScreen.submitButton(flow.id).should('be.disabled');
          } else {
            formsScreen.submit(flow.id);
          }
          formsScreen.field(flow.id, key).should($el => expect($el[0].validity.valueMissing, 'native required validation').to.eq(true));
          cy.then(() => expect(writes, 'invalid UI must not submit a write').to.eq(0));
        });
        for (const [key, length] of Object.entries(flow.short || {})) it(`blocks too short ${key} after actual typing`, () => {
          formsScreen.open(flow.id);
          formsScreen.fill(flow.id, {
            ...flow.base,
            [key]: 'a'.repeat(length)
          });
          const serverResource = {
            teacherCreate: 'teachers',
            studentCreate: 'students'
          }[flow.id];
          if (serverResource) cy.intercept('POST', `**/api/v1/${serverResource}`).as('shortFieldRejected');
          formsScreen.submit(flow.id);
          if (serverResource) cy.wait('@shortFieldRejected').then(({
            response
          }) => {
            expect(response.statusCode, 'validation rejection, never a server failure').to.eq(400);
            expect(response.body.errorCode).to.eq('VALIDATION_FAILED');
          });
          formsScreen.field(flow.id, key).should('have.value', 'a'.repeat(length));
          // Controlled React values do not consistently retain the browser's tooShort
          // flag after a render. Assert the real rejection shown by this workflow.
          cy.contains(flow.shortErrors[key]).should('be.visible');
        });
        for (const [key, limit] of Object.entries(flow.limits || {})) it(`limits typed ${key} to ${limit} characters`, () => {
          formsScreen.open(flow.id);
          formsScreen.fill(flow.id, {
            [key]: 'A'.repeat(limit + 1)
          });
          formsScreen.field(flow.id, key).should('have.value', 'A'.repeat(limit));
        });
      });
    }
    for (const id of ['teacherReset', 'studentReset', 'securityStudent', 'securityPlatform'].filter(id => ids.includes(id))) it(`${id} shows a password-confirmation mismatch on screen`, () => {
      const base = nativeForms.find(f => f.id === id).base;
      formsScreen.open(id);
      formsScreen.fill(id, {
        ...base,
        password,
        confirm: 'Different-Disposable-Password!'
      });
      formsScreen.submit(id);
      cy.contains('Xác nhận mật khẩu').should('be.visible');
      cy.contains(/không (?:trùng|khớp)/).should('be.visible');
    });
  });
}
module.exports = {
  registerVisibleForms
};
