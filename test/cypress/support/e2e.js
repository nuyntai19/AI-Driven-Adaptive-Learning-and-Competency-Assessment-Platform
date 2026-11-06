require('./commands');
before(() => cy.task('isolated:verify', null, { log: false }));
// Do not globally suppress uncaught exceptions: UI XSS and rendering failures must fail tests.
