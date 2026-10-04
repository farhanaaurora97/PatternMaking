describe('Style Sheet', () => {
  beforeEach(() => {
    cy.login();
    cy.visit('/StyleSheet');
  });

  it('loads PLM register with pagination', () => {
    cy.contains('Style Sheet').should('be.visible');
    cy.get('.tbl-wrap--primary').should('exist');
    cy.get('#ss-lifecycle-guide').should('exist');
    cy.get('#ss-tbody').scrollIntoView().should('exist');
    cy.get('#ss-pagination').scrollIntoView().should('exist');
    cy.get('#ss-page-size').should('exist');
    cy.get('#ss-search').scrollIntoView().should('exist');
    cy.get('#ss-count').invoke('text').should('match', /\d+/);
  });

  it('filters by lifecycle tab', () => {
    cy.get('#ss-lifecycle-tabs [data-lifecycle="Bulk"]').then(($tab) => {
      if ($tab.length === 0) return;
      cy.wrap($tab).click();
      cy.get('#ss-clear-lifecycle').should('be.visible');
      cy.get('#ss-tbody tr:visible').each(($row) => {
        expect($row.attr('data-lifecycle')).to.equal('Bulk');
      });
    });
  });

  it('changes page size', () => {
    cy.get('#ss-page-size').select('20');
    cy.get('#ss-count').invoke('text').should('match', /of/);
  });
});
