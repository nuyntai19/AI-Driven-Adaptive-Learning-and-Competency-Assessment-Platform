const { routes } = require('./routes');

const teacherQuestionScreen = {
  open: id => cy.visit(routes.teacherQuestion(id)),
  questionText: () => cy.get('[data-cy="teacher-question-text"]'),
  // Rendering the valid fraction remains a real KaTeX assertion alongside XSS checks.
  renderedQuestionMath: () => teacherQuestionScreen.questionText().find('.katex'),
};
module.exports = { teacherQuestionScreen };
