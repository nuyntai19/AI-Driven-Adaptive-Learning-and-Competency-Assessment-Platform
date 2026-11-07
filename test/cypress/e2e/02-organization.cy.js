const { IDS, teacher, student, klass, unique } = require('../support/validation');

describe('Organization request boundaries', () => {
  it('reads teacher notes from the freshly migrated database', () => cy.apiLogin('teacher').then(token =>
    cy.api(token, 'GET', '/teacher-student-notes/my-notes').then(response => {
      expect(response.status).to.eq(200);
      expect(response.body.data).to.be.an('array');
      expect(response.body.meta.totalItems).to.eq(0);
    })));
  const resources = [
    { route: '/teachers', build: teacher, mutations: [
      ['empty username', { username: ' ' }], ['username length101', { username: 'a'.repeat(101) }],
      ['password length11', { temporaryPassword: 'a'.repeat(11) }], ['password length201', { temporaryPassword: 'a'.repeat(201) }],
      ['blank display name', { displayName: ' ' }], ['display length201', { displayName: 'a'.repeat(201) }],
      ['department length151', { department: 'a'.repeat(151) }],
    ] },
    { route: '/students', build: student, mutations: [
      ['blank fullname', { fullName: ' ' }], ['fullname length201', { fullName: 'a'.repeat(201) }],
      ['grade9', { gradeLevel: 9 }], ['grade13', { gradeLevel: 13 }],
      ['null memberships', { classIds: null }], ['empty membership ID', { classIds: [IDS.zero] }],
      ['duplicate memberships', { classIds: [IDS.mathClass, IDS.mathClass] }],
      ['password length11', { temporaryPassword: 'a'.repeat(11) }],
    ] },
    { route: '/classes', build: klass, mutations: [
      ['blank class name', { className: ' ' }], ['class name length151', { className: 'a'.repeat(151) }],
      ['year length21', { academicYear: 'a'.repeat(21) }], ['missing grade', { gradeLevel: null }],
      ['grade9', { gradeLevel: 9 }], ['grade13', { gradeLevel: 13 }],
      ['empty subject', { subjectId: IDS.zero }], ['empty teacher', { teacherId: IDS.zero }],
    ] },
  ];
  for (const resource of resources) {
    describe(resource.route, () => {
      it('accepts a valid positive control', () => cy.apiLogin('manager').then(token =>
        cy.api(token, 'POST', resource.route, resource.build()).its('status').should('eq', 201)));
      for (const [name, patch] of resource.mutations) {
        it(`rejects ${name}`, () => cy.apiLogin('manager').then(token =>
          cy.expectRejected(token, 'POST', resource.route, { ...resource.build(), ...patch })));
      }
    });
  }
  it('normalizes teacher names and does not accept a forged tenant discriminator', () => {
    const payload = { ...teacher(), displayName: '  Giáo viên chuẩn hóa  ', centerId: IDS.centerB };
    cy.apiLogin('manager').then(token => {
      cy.api(token, 'POST', '/teachers', payload).then(r => {
        expect(r.status).to.eq(201);
        expect(r.body.displayName).to.eq('Giáo viên chuẩn hóa');
        const id = r.body.teacherId;
        expect(id).to.be.a('string').and.not.be.empty;
        cy.apiLogin('foreignTeacher').then(foreign => cy.api(foreign, 'GET', `/teachers/${id}`).its('status').should('eq', 404));
      });
    });
  });
  it('rejects a duplicate username without a second teacher record', () => {
    const payload = { ...teacher(), username: unique('duplicate') };
    cy.apiLogin('manager').then(token => {
      cy.api(token, 'POST', '/teachers', payload).its('status').should('eq', 201);
      cy.expectRejected(token, 'POST', '/teachers', payload, 409, 'DUPLICATE_RESOURCE');
    });
  });
});
