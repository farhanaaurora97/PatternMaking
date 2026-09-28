const { defineConfig } = require('cypress');

module.exports = defineConfig({
  e2e: {
    baseUrl: process.env.CYPRESS_BASE_URL || 'http://localhost:5001',
    specPattern: 'cypress/e2e/**/*.cy.js',
    supportFile: 'cypress/support/e2e.js',
    defaultCommandTimeout: 15000,
    pageLoadTimeout: 60000,
    video: false,
    screenshotOnRunFailure: true,
  },
  env: {
    username: 'admin',
    password: 'Admin@123',
  },
});
