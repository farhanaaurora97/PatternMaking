describe('Authentication', () => {
  it('redirects unauthenticated users from dashboard to login', () => {
    cy.visit('/', { failOnStatusCode: false });
    cy.url().should('include', '/Account/Login');
    cy.contains('Sign in').should('be.visible');
  });

  it('logs in with admin credentials', () => {
    cy.login();
    cy.visit('/');
    cy.get('.patternpro-dashboard').should('be.visible');
    cy.contains('All Patterns').should('be.visible');
  });
});
