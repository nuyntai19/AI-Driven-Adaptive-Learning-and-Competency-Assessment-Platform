const { IDS, expectProblem } = require('../support/validation');

describe('Notification content and preview/send integrity', () => {
  // Five preview POSTs maximum for this actor/endpoint: the production limit is 5/minute.
  // Keep these together; do not turn 429 into an accepted validation rejection.
  const draft = { title: 'Lịch sinh hoạt Cypress', body: 'Lớp vui lòng xem lại lịch sinh hoạt tuần tới.',
    audienceKind: 'SelectedStudents', targetIds: [IDS.student], priority: 'Normal', category: 'Information' };
  for (const [name, patch, code] of [
    ['HTML markup', { body: '<img src=x onerror=alert(1)>' }, 'NOTIFICATION_CONTENT_RESTRICTED'],
    ['obfuscated script URI', { body: 'ja\u200bvascript:alert(1)' }, 'NOTIFICATION_CONTENT_RESTRICTED'],
    ['credential text', { body: 'password=DisposableSecretOnly' }, 'NOTIFICATION_CONTENT_RESTRICTED'],
    ['oversized title', { title: 'a'.repeat(161) }, 'VALIDATION_FAILED'],
  ]) {
    it(`rejects ${name} at preview`, () => cy.apiLogin('teacher').then(token =>
      cy.expectRejected(token, 'POST', '/notifications/preview', { ...draft, ...patch }, 400, code)));
  }
  it('valid preview binds the exact message; changed body and forged token cannot send', () => cy.apiLogin('teacher').then(token => {
    cy.api(token, 'POST', '/notifications/preview', draft).then(r => {
      expect(r.status).to.eq(200);
      expect(r.body.data.recipientCount).to.be.greaterThan(0);
      const payload = { ...draft, previewToken: r.body.data.token, clientMessageId: crypto.randomUUID() };
      cy.api(token, 'POST', '/notifications/send', { ...payload, body: 'Nội dung đã bị sửa sau khi duyệt.' }).then(changed => expectProblem(changed, 400, 'VALIDATION_FAILED'));
      cy.api(token, 'POST', '/notifications/send', { ...payload, previewToken: 'forged-preview-token' }).then(forged => expectProblem(forged, 400, 'VALIDATION_FAILED'));
      cy.api(token, 'POST', '/notifications/send', payload).then(sent => {
        expect(sent.status).to.eq(200);
        expect(sent.body.data.messageId).to.be.a('string').and.not.be.empty;
        expect(sent.body.data.body).to.eq(draft.body);
      });
    });
  }));
  it('refuses extra audience/tenant parameters instead of silently mass-assigning them', () => cy.apiLogin('manager').then(token =>
    cy.expectRejected(token, 'POST', '/notifications/preview', { ...draft, centerId: IDS.centerB })));
  it('student cannot invoke message composition', () => cy.apiLogin('student').then(token =>
    cy.api(token, 'GET', '/notifications/compose-options').then(r => expectProblem(r, 403, null))));
});
