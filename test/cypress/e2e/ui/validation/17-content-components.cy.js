const {
  contentComponentsScreen: s
} = require('../../../screens/content-components.screen');
const {
  academicScreen: a
} = require('../../../screens/academic-validation.screen');
describe('Graph relationship and version controls', () => {
  it('keeps link creation disabled without source and target topics', () => {
    s.edge();
    a.get('graph-edge-source').select('');
    a.get('graph-edge-target').select('');
    s.edgeSubmit().should('be.disabled');
  });
  it('rejects selecting the same topic at both ends of a relationship', () => {
    s.edge();
    s.endpoints(true);
    cy.contains('Điểm tri thức nguồn và đích không được trùng nhau.').should('be.visible');
    s.edgeSubmit().should('be.disabled');
  });
  for (const [value, flag] of [['-0.05', 'rangeUnderflow'], ['1.05', 'rangeOverflow']]) {
    it(`blocks a typed relationship weight of ${value}`, () => {
      s.edge();
      s.endpoints();
      a.input('graph-edge-weight', value);
      let writes = 0;
      cy.intercept('POST', '**/knowledge/edges', req => {
        writes++;
        req.continue();
      });
      s.edgeSubmit().click();
      a.get('graph-edge-weight').should($el => expect($el[0].validity[flag]).to.eq(true));
      cy.then(() => expect(writes).to.eq(0));
    });
  }
  it('caps a typed graph-version title at 200 characters', () => {
    s.draft();
    a.input('graph-version-title', 'A'.repeat(201));
    a.get('graph-version-title').should('have.value', 'A'.repeat(200));
  });
  it('cannot publish an empty Draft even after acknowledging review', () => {
    s.draft();
    a.get('graph-version-reviewed').check();
    a.get('graph-version-publish').should('be.disabled');
  });
});
describe('Rubric fields inside the real question editor', () => {
  const definitionError = 'Mỗi tiêu chí cần tên, mã riêng và điểm tối đa dương (tối đa 2 chữ số thập phân).';
  it('shows an error for a new criterion without a name', () => {
    s.rubric();
    cy.contains(definitionError).should('be.visible');
  });
  it('rejects a criterion with zero maximum points', () => {
    s.rubric();
    a.input('rubric-title', 'Phương pháp');
    a.input('rubric-score', '0');
    cy.contains(definitionError).should('be.visible');
  });
  it('requires criterion totals to match the question maximum', () => {
    s.rubric();
    a.input('rubric-title', 'Phương pháp');
    a.input('rubric-score', '0.5');
    a.input('question-max-score', '2');
    cy.contains('Tổng điểm tối đa của các tiêu chí phải bằng điểm tối đa câu hỏi.').should('be.visible');
  });
  it('rejects duplicate visual-evidence requirements entered on separate lines', () => {
    s.rubric();
    a.input('rubric-title', 'Hình minh họa');
    a.input('rubric-visual-requirements', 'Có nhãn A\nCó nhãn A');
    cy.contains('Yêu cầu ảnh nháp: tối đa 12 mục mỗi tiêu chí, không để dòng trống/trùng, mỗi mục tối đa 500 ký tự.').should('be.visible');
  });
});
describe('Published curriculum action inputs', () => {
  it('disables clone confirmation for a blank title', () => {
    s.clone();
    a.input('curriculum-clone-title', '   ');
    a.get('curriculum-confirm-clone').should('be.disabled');
  });
  it('caps the clone title at 200 real keystrokes', () => {
    s.clone();
    a.input('curriculum-clone-title', 'A'.repeat(201));
    a.get('curriculum-clone-title').should('have.value', 'A'.repeat(200));
  });
  it('requires a nonblank reason before archiving an unused curriculum', () => {
    s.archive();
    a.input('curriculum-archive-reason', '   ');
    a.get('curriculum-archive-confirm').should('be.disabled');
    a.input('curriculum-archive-reason', 'Thay phiên bản mới');
    a.get('curriculum-archive-confirm').should('not.be.disabled');
  });
  it('caps the archive reason at 500 characters', () => {
    s.archive();
    a.input('curriculum-archive-reason', 'A'.repeat(501));
    a.get('curriculum-archive-reason').should('have.value', 'A'.repeat(500));
  });
  it('caps the application change reason at 500 characters', () => {
    s.publishedCurriculum();
    a.input('curriculum-application-reason', 'A'.repeat(501));
    a.get('curriculum-application-reason').should('have.value', 'A'.repeat(500));
  });
});
