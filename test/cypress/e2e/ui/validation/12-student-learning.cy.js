const {
  studentValidationScreen: s
} = require('../../../screens/student-validation.screen');
const {
  academicScreen: a
} = require('../../../screens/academic-validation.screen');
const {
  noExecutableMarkup
} = require('../../../support/browser-safety');
const {
  XSS
} = require('../../../fixtures/builders');
describe('Visible class-scoped student goals', () => {
  it('cannot generate a route after clearing the skill selection', () => {
    s.openGoal();
    s.get('goal-clear-skills').click();
    s.goalSubmit().should('be.disabled');
    cy.contains('Chọn ít nhất một chủ đề hoặc kỹ năng để tiếp tục.').should('be.visible');
  });
  it('caps a typed goal title at 200 characters', () => {
    s.openGoal();
    a.input('goal-title-input', 'A'.repeat(201));
    s.get('goal-title-input').should('have.value', 'A'.repeat(200));
  });
  for (const [value, flag] of [['-1', 'rangeUnderflow'], ['11', 'rangeOverflow']]) it(`rejects a target score of ${value} in the actual goal form`, () => {
    s.openGoal();
    a.input('goal-score', value);
    s.get('goal-score').should($el => expect($el[0].validity[flag]).to.eq(true));
  });
  it('rejects a past deadline selected on screen', () => {
    s.openGoal();
    s.get('goal-deadline').type('2020-01-01');
    s.get('goal-deadline').should($el => expect($el[0].validity.rangeUnderflow).to.eq(true));
  });
});
describe('Manager subject-goal validation in the student drawer', () => {
  it('keeps legacy scoped goal editing hidden from the CenterManager account type', () => {
    cy.uiLogin('manager');
    cy.visit('/quan-ly/hoc-sinh');
    s.get('student-edit').should('have.length.at.least', 1);
    s.get('student-goals').should('not.exist');
  });
});
describe('Student Twin goal editor', () => {
  for (const [field, value] of [['twin-goal-score', '11'], ['twin-goal-score', '-1'], ['twin-goal-days', '0'], ['twin-goal-days', '3651']]) it(`rejects typed ${field}=${value}`, () => {
    s.openTwinGoal();
    a.input('twin-goal-score', '8');
    a.input('twin-goal-days', '60');
    a.input(field, value);
    s.get('twin-save-goal').click();
    cy.contains('Điểm mục tiêu phải từ 0 đến 10 và thời hạn phải từ 1 đến 3650 ngày.').should('be.visible');
  });
});
describe('Adaptive practice inputs and browser note persistence', () => {
  it('only offers the designed duration choices through the actual UI', () => {
    s.openAdaptive();
    s.get('adaptive-duration').should('have.length', 3).then($buttons => expect([...$buttons].map(b => b.textContent)).to.satisfy(texts => ['10 phút', '15 phút', '20 phút'].every(t => texts.some(x => x.includes(t)))));
  });
  it('a real started session blocks an empty answer and enables the selected answer control', () => {
    s.openAdaptive();
    cy.intercept('GET', '**/adaptive-practice/sessions/*/next-question').as('realAdaptiveQuestion');
    s.get('adaptive-duration').eq(0).click();
    s.get('adaptive-start').click();
    cy.location('pathname').should('match', /\/phien\/[a-f0-9-]+$/);
    s.get('adaptive-submit').should('be.disabled');
    cy.wait('@realAdaptiveQuestion').then(({
      response
    }) => {
      expect(response.statusCode).to.eq(200);
      if (response.body.questionType === 'MultipleChoice') s.get('adaptive-option').eq(0).click();else {
        a.input('text-ans', '   ');
        s.get('adaptive-submit').should('be.disabled');
        a.input('text-ans', '2');
      }
      s.get('adaptive-submit').should('not.be.disabled');
    });
  });
  it('teacher note text remains literal across a real reload', () => {
    s.openTeacherNotes();
    a.input('teacher-local-notes', `Ghi chú UI ${XSS}`);
    noExecutableMarkup();
    cy.reload();
    s.selectTeacherClass();
    s.get('teacher-student-open').eq(0).click();
    s.get('teacher-notes-tab').click();
    s.get('teacher-local-notes').should('have.value', `Ghi chú UI ${XSS}`);
    noExecutableMarkup();
  });
});
