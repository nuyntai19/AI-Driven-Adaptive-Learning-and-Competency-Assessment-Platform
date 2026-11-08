const {
  IDS
} = require('../fixtures/builders');
const {
  academicScreen
} = require('./academic-validation.screen');
const get = academicScreen.get;
const studentValidationScreen = {
  get,
  openGoal(role = 'student', { placement = false } = {}) {
    cy.apiLogin(role).then(token => cy.api(token, 'GET', `/students/me/academic-context?subjectId=${IDS.math}`).then(r => {
      expect(r.status).to.eq(200);
      expect(r.body.data.selectedClassId).to.be.a('string').and.not.be.empty;
      cy.uiLogin(role);
      cy.visit(`/hoc-tap/lo-trinh-hoc-tap?subjectId=${IDS.math}&classId=${r.body.data.selectedClassId}`);
      cy.get('[data-cy="goal-open-start"], [data-cy="goal-open-update"]').should('have.length.at.least', 1).eq(0).should('not.be.disabled').then($button => {
        const starting = $button.attr('data-cy') === 'goal-open-start';
        cy.wrap($button).click();
        get('goal-form').should('be.visible');
        if (starting && !placement) get('goal-skip-placement').check();
        if (!placement) get('goal-title-input').should('be.visible');
      });
    }));
  },
  goalSubmit() {
    return get('goal-form').find('button[type="submit"]');
  },
  openTwinGoal() {
    cy.apiLogin('student').then(token => cy.api(token, 'GET', `/students/me/academic-context?subjectId=${IDS.math}`).then(r => {
      expect(r.status).to.eq(200);
      cy.uiLogin('student');
      cy.visit(`/hoc-tap/ho-so-nang-luc?subjectId=${IDS.math}&classId=${r.body.data.selectedClassId}`);
      get('twin-open-goal').click();
      get('twin-goal-score').should('be.visible');
    }));
  },
  openAdaptive() {
    cy.uiLogin('student2');
    cy.visit(`/hoc-tap/luyen-tap?subjectId=${IDS.math}`);
    return get('adaptive-start').should('not.be.disabled');
  },
  selectTeacherClass() {
    return get('teacher-student-class').select(IDS.mathClass);
  },
  openTeacherNotes() {
    cy.uiLogin('teacher');
    cy.visit('/giao-vien/hoc-sinh');
    studentValidationScreen.selectTeacherClass();
    get('teacher-student-open').eq(0).click();
    get('teacher-notes-tab').click();
    return get('teacher-local-notes').should('be.visible');
  }
};
module.exports = {
  studentValidationScreen
};
