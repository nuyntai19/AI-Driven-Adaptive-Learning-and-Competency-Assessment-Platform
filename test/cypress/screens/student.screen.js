const { routes } = require('./routes');

const studentScreen = {
  workspace: () => cy.get('[data-cy="student-workspace"]'),
  openAssignments: () => cy.visit(routes.studentAssignments),
  assignmentTitle: title => cy.contains('[data-cy="student-assignment-title"]', title),
  openOverview: (scope = {}) => cy.visit(`${routes.studentOverview}?${new URLSearchParams(scope)}`),
};
module.exports = { studentScreen };
