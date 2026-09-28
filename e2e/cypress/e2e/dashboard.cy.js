describe('Dashboard', () => {
  beforeEach(() => {
    cy.login();
    cy.visit('/');
  });

  it('shows table-first layout with pagination', () => {
    cy.get('#patterns-tbody').scrollIntoView().should('exist');
    cy.get('#tbl-pagination').scrollIntoView().should('exist');
    cy.get('#tbl-page-size').should('exist');
    cy.get('#tbl-search-input').scrollIntoView().should('exist');
    cy.get('#tbl-count').invoke('text').should('match', /\d+/);
  });

  it('filters patterns via search', () => {
    cy.get('#patterns-tbody tr').then(($rows) => {
      if ($rows.length === 0) {
        cy.log('No patterns — skip search filter assertion');
        return;
      }
      cy.get('#tbl-search-input').type('DN-');
      cy.wait(400);
      cy.get('#tbl-count').invoke('text').should('match', /of/);
    });
  });

  it('expands analytics section and loads charts', () => {
    cy.get('#dash-analytics').should('exist');
    cy.get('#dash-analytics').then(($el) => {
      if (!$el[0].open) {
        cy.get('#dash-analytics summary').click();
      }
    });
    cy.get('#chart-status').scrollIntoView().should('exist');
    cy.get('#pant-board-grid').should('exist');
  });

  it('paginates when more than one page of rows', () => {
    cy.get('#tbl-page-size').select('10');
    cy.get('#patterns-tbody tr').then(($rows) => {
      if ($rows.length <= 10) {
        cy.log('Not enough rows for pagination — skip');
        return;
      }
      cy.get('#tbl-page-next').should('not.be.disabled').click();
      cy.get('#tbl-page-info').invoke('text').should('match', /Page 2/);
    });
  });
});
