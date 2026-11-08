const { routes } = require('./routes');

const managerClassesScreen = {
  open: () => cy.visit(routes.managerClasses),
  className: name => cy.contains('[data-cy="manager-class-name"]', name),
};
module.exports = { managerClassesScreen };
