const { IDS, unique, expectProblem } = require('../support/validation');

describe('Account type, tenant and privilege boundaries', () => {
  for (const route of ['/teachers', '/students', '/classes', '/subjects', '/assignments', '/authorization/roles', '/notifications', '/platform/centers']) {
    it(`requires authentication for ${route}`, () => cy.api(null, 'GET', route).then(r => expectProblem(r, 401, null)));
  }
  for (const [role, method, route, body] of [
    ['student', 'GET', '/teachers/me/review-queue'],
    ['student', 'GET', '/platform/centers'],
    ['student', 'GET', '/teacher-student-notes/my-notes'],
    ['platform', 'GET', '/teacher-student-notes/my-notes'],
    ['student', 'POST', '/teacher-student-notes', { studentId: IDS.student, content: 'Unauthorized note' }],
    ['teacher', 'GET', '/authorization/roles'],
    ['manager', 'POST', '/questions', {}],
    ['manager', 'POST', '/curriculums', {}],
    ['teacher', 'POST', '/learning/attempts', {}],
    ['platform', 'GET', '/students'],
    ['platform', 'GET', '/questions'],
    ['platform', 'GET', '/classes'],
  ]) {
    it(`${role} is denied ${method} ${route}`, () => cy.apiLogin(role).then(token =>
      cy.api(token, method, route, body).then(r => expectProblem(r, 403, null))));
  }
  const roleBody = () => ({ roleCode: unique('CYP_ROLE'), roleName: 'Vai trò kiểm thử', accountType: 'Teacher', description: 'Cypress' });
  for (const [name, patch] of [
    ['lowercase role code', { roleCode: 'teacher_lower' }], ['role-code markup', { roleCode: '<SCRIPT>' }],
    ['role-code length65', { roleCode: 'A'.repeat(65) }], ['blank role name', { roleName: '' }],
    ['role-name length151', { roleName: 'A'.repeat(151) }], ['description length501', { description: 'A'.repeat(501) }],
    ['unknown account type', { accountType: 'SuperAdmin' }], ['numeric enum', { accountType: 1 }],
  ]) {
    it(`rejects ${name}`, () => cy.apiLogin('manager').then(token => cy.expectRejected(token, 'POST', '/authorization/roles', { ...roleBody(), ...patch })));
  }
  it('accepts a compatible new role then blocks an invalid concurrency token', () => cy.apiLogin('manager').then(token => {
    cy.api(token, 'POST', '/authorization/roles', roleBody()).then(r => {
      expect(r.status).to.eq(201);
      const id = r.body.data.roleId;
      expect(id).to.be.a('string').and.not.be.empty;
      cy.expectRejected(token, 'PUT', `/authorization/roles/${id}/permissions`, {
        permissionCodes: [], rowVersion: "1 OR 1=1", reason: 'Cypress invalid row version',
      });
    });
  }));
  it('never lets a tenant create a PlatformAdmin role', () => cy.apiLogin('manager').then(token =>
    cy.api(token, 'POST', '/authorization/roles', { ...roleBody(), accountType: 'PlatformAdmin' }).then(r => expectProblem(r, 403, null))));
  it('allows platform metadata listing but rejects an empty status-change reason', () => cy.apiLogin('platform').then(token => {
    cy.api(token, 'GET', '/platform/centers').then(r => {
      expect(r.status).to.eq(200);
      const center = r.body.data.items.find(c => c.centerId !== '00000000-0000-0000-0000-000000000001');
      expect(center, 'a seeded non-root center is required').to.be.an('object');
      cy.expectRejected(token, 'PATCH', `/platform/centers/${center.centerId}/status`, {
        status: 'Suspended', rowVersion: center.rowVersion, reason: '',
      });
    });
  }));
  it('direct student navigation to platform/teacher administration is denied in the browser', () => {
    cy.uiLogin('student');
    cy.visit('/quan-tri-nen-tang/trung-tam');
    cy.location('pathname').should('eq', '/khong-co-quyen');
    cy.visit('/giao-vien/cham-bai');
    cy.location('pathname').should('eq', '/khong-co-quyen');
  });
});
