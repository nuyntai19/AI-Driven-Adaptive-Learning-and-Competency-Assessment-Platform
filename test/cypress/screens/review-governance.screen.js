const {
  academicScreen: a
} = require('./academic-validation.screen');
const get = a.get;
const reviewScreen = {
  open() {
    cy.task('isolated:submittedAssignment', null, {
      log: false,
      timeout: 60000
    }).then(f => {
      cy.uiLogin('teacher');
      cy.visit(`/giao-vien/cham-bai?assignmentId=${f.assignmentId}&studentId=${f.studentId}`);
      get('grading-open-form').should('not.be.disabled').click();
      get('grading-score').should('be.visible');
    });
  },
  get,
  role() {
    const code = `UI_RULE_${Date.now()}`;
    cy.apiLogin('manager').then(token => cy.api(token, 'POST', '/authorization/roles', {
      roleCode: code,
      roleName: 'Vai trò validation UI',
      accountType: 'Teacher',
      description: 'UI fixture'
    }).then(r => {
      expect(r.status).to.eq(201);
      cy.uiLogin('manager');
      cy.visit('/quan-ly/phan-quyen');
      a.input('role-search', code);
      cy.contains('[data-cy="role-select"]', code).click();
      get('role-edit-name').should('not.be.disabled');
    }));
  },
  openManagerAction(hook) {
    cy.uiLogin('platform');
    cy.visit('/quan-tri-nen-tang/trung-tam');
    get('platform-managers').eq(0).click();
    get(hook).eq(0).click();
  }
};
module.exports = {
  reviewScreen
};
