const {
  academicScreen: a
} = require('./academic-validation.screen');
const notificationActionsScreen = {
  reply(f) {
    cy.uiLogin('student');
    cy.visit(`/hoc-tap/thong-bao?notification=${encodeURIComponent(f.deliveryId)}`);
    return a.get('notification-reply').should('be.visible');
  },
  sent(f) {
    cy.uiLogin('teacher');
    cy.visit(`/giao-vien/thong-bao?sent=${f.messageId}`);
    return a.get('notification-retract-reason').should('be.visible');
  },
  replySubmit: () => a.get('notification-reply-form').find('button[type="submit"]'),
  retractSubmit: () => a.get('notification-retract-form').find('button[type="submit"]')
};
module.exports = {
  notificationActionsScreen
};
