Cypress.Commands.add('login', (username, password) => {
  const user = username || Cypress.env('username');
  const pass = password || Cypress.env('password');

  cy.session([user, pass], () => {
    cy.visit('/Account/Login');
    cy.get('#UserName').should('be.visible').clear().type(user);
    cy.get('#Password').clear().type(pass, { log: false });
    cy.get('button[type="submit"]').contains('Sign in').click();
    cy.url({ timeout: 20000 }).should('not.include', '/Account/Login');
  });
});
