const {
  runtimeValidationScreen: s
} = require('../../../screens/runtime-validation.screen');
const {
  academicScreen: a
} = require('../../../screens/academic-validation.screen');
describe('Notification recovery form in a seeded failed-dispatch state', () => {
  it('requires a nonblank reason before enabling redrive', () => {
    s.redrive();
    a.input('notification-redrive-reason', '   ');
    a.get('notification-redrive-confirm').should('be.disabled');
    a.input('notification-redrive-reason', 'Đã kiểm tra nguyên nhân');
    a.get('notification-redrive-confirm').should('not.be.disabled');
  });
  it('caps a typed recovery reason at 500 characters', () => {
    s.redrive();
    a.input('notification-redrive-reason', 'A'.repeat(501));
    a.get('notification-redrive-reason').should('have.value', 'A'.repeat(500));
  });
});
describe('Real placement mission activity form', () => {
  it('blocks an unanswered activity after starting placement through the UI', () => {
    s.placement();
    let submissions = 0;
    cy.intercept('POST', '**/students/me/activity-runs/*/submissions', req => {
      submissions++;
      req.continue();
    });
    a.get('mission-activity-form').find('button[type="submit"]').click();
    a.get('mission-activity-form').find(':invalid').should('have.length.at.least', 1);
    cy.then(() => expect(submissions, 'native validation must prevent activity submission').to.eq(0));
  });
});
