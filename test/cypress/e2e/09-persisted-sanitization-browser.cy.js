const { IDS, XSS, klass, question, unique, noExecutableMarkup } = require('../support/validation');

describe('Persisted untrusted content rendered by real browser pages', () => {
  it('hydrates stored HTML and untrusted LaTeX safely in the real teacher math editor', () => {
    const marker = unique('CYP_MATH_XSS');
    const authored = `${marker} ${XSS} $\\frac{1}{2}$ $\\href{javascript:window.__edutwinXss=1}{bấm}$`;
    cy.apiLogin('teacher').then(token => cy.api(token, 'POST', '/questions', {
      ...question(), questionText: authored, solution: `Một cộng một bằng hai. ${authored}`,
    }).then(response => {
      expect(response.status).to.eq(201);
      const id = response.body.data.questionId;
      expect(id).to.be.a('string').and.not.be.empty;
      cy.api(token, 'GET', `/questions/${id}`).then(stored => {
        expect(stored.status).to.eq(200);
        expect(stored.body.data.questionText).to.eq(authored);
      });
      cy.uiLogin('teacher');
      cy.visit(`/giao-vien/cau-hoi/${id}`);
      const editor = '[role="textbox"][contenteditable="true"]';
      cy.get(editor).first().should('contain.text', marker).and('contain.text', XSS);
      cy.get(editor).first().find('.katex').should('exist');
      noExecutableMarkup();
      cy.reload();
      cy.get(editor).first().should('contain.text', marker).and('contain.text', XSS);
      cy.get(editor).first().find('.katex').should('exist');
      noExecutableMarkup();
    }));
  });

  it('renders an API-authored class name as inert text on the manager class page', () => {
    const name = `000_${unique('CYP_XSS')} ${XSS}`;
    cy.apiLogin('manager').then(token => {
      cy.api(token, 'POST', '/classes', { ...klass(), className: name }).then(r => {
        expect(r.status).to.eq(201);
        expect(r.body.data.className).to.eq(name);
      });
    });
    cy.uiLogin('manager');
    // Real requests still reach the API. Increase only the legitimate pagination parameter to find this new fixture.
    cy.intercept('GET', '**/api/v1/classes?*', req => {
      const url = new URL(req.url); url.searchParams.set('pageSize', '100'); req.url = url.toString(); req.continue();
    });
    cy.visit('/quan-ly/lop-hoc');
    cy.contains('td', name).should('be.visible');
    noExecutableMarkup();
    cy.reload();
    cy.contains('td', name).should('be.visible');
    noExecutableMarkup();
  });
  it('renders a stored assignment title as inert text in the student assignment list', () => {
    const title = `${unique('CYP_STORED')} ${XSS}`;
    cy.task('isolated:assignment', { title }).then(fixture => {
      cy.uiLogin('student');
      cy.visit('/hoc-tap/bai-tap');
      cy.contains(fixture.fullTitle).should('be.visible');
      noExecutableMarkup();
      cy.reload();
      cy.contains(fixture.fullTitle).should('be.visible');
      noExecutableMarkup();
    });
  });
  it('keeps malicious URL input from turning a navigation parameter into an executable URL', () => {
    cy.uiLogin('student');
    cy.visit(`/hoc-tap/tong-quan?subjectId=${encodeURIComponent('javascript:window.__edutwinXss=1')}&classId=${IDS.zero}`);
    cy.get('body').should('be.visible');
    noExecutableMarkup();
    cy.location('origin').should('eq', 'http://127.0.0.1:3002');
  });
});
