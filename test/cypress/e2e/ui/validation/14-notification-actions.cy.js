const {
  notificationActionsScreen: s
} = require('../../../screens/notification-actions.screen');
const {
  academicScreen: a
} = require('../../../screens/academic-validation.screen');
describe('Visible reply and sent-message action validation', () => {
  let fixture;
  before(() => cy.task('isolated:replyNotification', null, {
    log: false,
    timeout: 120000
  }).then(f => {
    fixture = f;
  }));
  it('requires a nonblank reply before sending', () => {
    s.reply(fixture);
    s.replySubmit().should('be.disabled');
    a.input('notification-reply', '   ');
    s.replySubmit().should('be.disabled');
  });
  it('caps a typed private reply at 2000 characters', () => {
    s.reply(fixture);
    a.input('notification-reply', 'A'.repeat(2001));
    a.get('notification-reply').should('have.value', 'A'.repeat(2000));
  });
  it('shows content rejection when HTML is actually typed and submitted', () => {
    s.reply(fixture);
    cy.intercept('POST', '**/notifications/*/reply').as('realReply');
    a.input('notification-reply', '<img src=x onerror=alert(1)>');
    s.replySubmit().click();
    cy.wait('@realReply').then(({
      response
    }) => {
      expect(response.statusCode).to.eq(400);
      expect(response.body.errorCode).to.eq('NOTIFICATION_CONTENT_RESTRICTED');
    });
    cy.get('[role="alert"]').should('be.visible');
  });
  it('keeps retract disabled for a blank reason', () => {
    s.sent(fixture);
    s.retractSubmit().should('be.disabled');
    a.input('notification-retract-reason', '   ');
    s.retractSubmit().should('be.disabled');
  });
  it('caps a typed retract reason at 500 characters', () => {
    s.sent(fixture);
    a.input('notification-retract-reason', 'A'.repeat(501));
    a.get('notification-retract-reason').should('have.value', 'A'.repeat(500));
  });
});
