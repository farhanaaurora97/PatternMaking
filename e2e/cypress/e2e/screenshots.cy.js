/**
 * Captures reference screenshots for docs / QA.
 * Output: e2e/cypress/screenshots/screenshots.cy.js/
 * Run: npm run cy:screenshots (Pattern.Web on http://localhost:5001)
 */
describe('Capture screenshots', () => {
  const pages = [
    { name: '01-login', path: '/Account/Login', auth: false },
    { name: '02-dashboard', path: '/', auth: true },
    { name: '03-style-sheet', path: '/StyleSheet', auth: true },
    { name: '04-library', path: '/Library', auth: true },
    { name: '05-canvas', path: '/Canvas', auth: true },
    { name: '06-pieces', path: '/Pieces', auth: true },
    { name: '07-size-chart', path: '/SizeChart', auth: true },
    { name: '08-export', path: '/Export', auth: true },
  ];

  pages.forEach(({ name, path, auth }) => {
    it(`captures ${name}`, () => {
      if (auth) cy.login();
      cy.visit(path, { failOnStatusCode: false });
      cy.wait(800);
      cy.document().then((doc) => {
        if (doc.body) cy.get('body').should('be.visible');
      });
      cy.screenshot(name, { capture: 'fullPage' });
    });
  });
});
