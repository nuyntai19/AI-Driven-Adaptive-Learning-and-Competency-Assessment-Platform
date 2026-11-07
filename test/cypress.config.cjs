const { defineConfig } = require('cypress');
const { register } = require('./cypress/plugins/runtime.cjs');

module.exports = defineConfig({
  video: false,
  screenshotOnRunFailure: true,
  screenshotsFolder: 'results/cypress/screenshots',
  downloadsFolder: 'results/cypress/downloads',
  trashAssetsBeforeRuns: false,
  retries: 0,
  viewportWidth: 1440,
  viewportHeight: 1000,
  e2e: {
    baseUrl: 'http://127.0.0.1:3002',
    specPattern: 'cypress/e2e/**/*.cy.js',
    supportFile: 'cypress/support/e2e.js',
    defaultCommandTimeout: 15000,
    requestTimeout: 15000,
    responseTimeout: 30000,
    testIsolation: true,
    setupNodeEvents: register,
  },
});
