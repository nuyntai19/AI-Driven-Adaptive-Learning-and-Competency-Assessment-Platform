const {
  academicScreen: a
} = require('./academic-validation.screen');
const {
  studentValidationScreen: student
} = require('./student-validation.screen');
const playerScreen = {
  open() {
    cy.task('isolated:reasoningAssignment', null, {
      log: false,
      timeout: 60000
    }).then(f => {
      cy.uiLogin('student');
      cy.visit(`/hoc-tap/bai-tap/${f.assignmentId}`);
      a.get('student-assignment-start').click();
      playerScreen.answer().should('be.visible');
    });
  },
  answer: () => a.get('learning-answer-editor').find('textarea'),
  reasoning: () => a.get('learning-reasoning'),
  openProposal() {
    student.openGoal();
    a.get('goal-skip-placement').check();
    a.get('goal-select-skills').click();
    // A prerequisite acknowledgement is a real user choice, only shown for that scope.
    cy.get('[data-cy="goal-form"]').then($form => {
      const include = $form.find('input[name="prerequisite-choice"]').eq(1);
      if (include.length) cy.wrap(include).check();
    });
    a.input('goal-title-input', 'Mục tiêu validation UI');
    a.input('goal-score', '8');
    const futureDeadline = new Date(Date.now() + 90 * 86400000).toISOString().slice(0, 10);
    a.input('goal-deadline', futureDeadline);
    a.get('goal-form').contains('button', /^2h$/).click();
    a.get('goal-form').find('input[type="range"]').should('have.value', '120');
    a.get('goal-form').contains('label', /^\s*T7\s*$/).find('input[type="checkbox"]').check();
    a.get('goal-form').contains('label', /^\s*CN\s*$/).find('input[type="checkbox"]').check();
    student.goalSubmit().should('not.be.disabled').click();
    a.get('proposal-open-customize').should('be.visible').click();
    return a.get('proposal-customize-form').should('be.visible');
  },
  proposalCount: () => a.get('proposal-question-count').not('[disabled]').eq(0)
};
module.exports = {
  playerScreen
};
