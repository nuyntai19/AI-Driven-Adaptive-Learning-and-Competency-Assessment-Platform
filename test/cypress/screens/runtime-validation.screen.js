const {
  academicScreen: a
} = require('./academic-validation.screen');
const {
  studentValidationScreen: student
} = require('./student-validation.screen');
const { IDS } = require('../fixtures/builders');
const runtimeValidationScreen = {
  redrive() {
    cy.task('isolated:deadLetterNotification', null, {
      log: false
    }).then(({
      eventId
    }) => {
      a.open('/quan-ly/thong-bao', 'manager');
      a.get('notification-open-operations').click();
      cy.contains('li', eventId).find('[data-cy="notification-open-redrive"]').click();
      a.get('notification-redrive-reason').should('be.visible');
    });
  },
  placement() {
    student.openGoal('student2', { placement: true });
    a.get('goal-skip-placement').should('not.be.checked');
    a.get('goal-clear-skills').click();
    a.get(`goal-skill-${IDS.mathTopic}`).check();
    a.get('goal-form').then($form => {
      const selfReport = $form.find('input[name="prerequisite-choice"]').eq(0);
      if (selfReport.length) cy.wrap(selfReport).check();
    });
    a.get('goal-title-input').should('not.exist');
    a.get('goal-score').should('not.exist');
    a.get('goal-deadline').should('not.exist');
    cy.intercept('POST', '**/students/me/learning-goals').as('placementGoal');
    cy.intercept('POST', '**/path-proposals/*/accept').as('placementAccepted');
    student.goalSubmit().should('not.be.disabled').click();
    cy.wait('@placementGoal').then(({ request, response }) => {
      expect(response.statusCode).to.eq(200);
      expect(request.body.startWithPlacement).to.eq(true);
      expect(request.body.clearDeadline).to.eq(true);
      expect(request.body.targetScore).to.eq(null);
      expect(request.body).not.to.have.property('deadlineDate');
    });
    cy.wait('@placementAccepted').then(({
      response
    }) => {
      expect(response.statusCode, JSON.stringify(response.body)).to.eq(200);
    });
    return a.get('mission-activity-form').should('be.visible');
  }
};
module.exports = {
  runtimeValidationScreen
};
