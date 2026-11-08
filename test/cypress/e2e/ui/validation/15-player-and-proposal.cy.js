const {
  playerScreen: s
} = require('../../../screens/learning-player.screen');
const {
  academicScreen: a
} = require('../../../screens/academic-validation.screen');
describe('Real assignment player reasoning validation', () => {
  it('blocks final confirmation until required reasoning is actually entered', () => {
    s.open();
    s.answer().type('goes');
    a.get('learning-open-submit').click();
    cy.contains('Các câu đã có đáp án nhưng thiếu lập luận bắt buộc:').should('be.visible');
    a.get('learning-confirm-submit').should('be.disabled');
    a.get('learning-cancel-submit').click();
    s.reasoning().type('Chủ ngữ she dùng động từ goes ở thì hiện tại đơn.');
    a.get('learning-open-submit').click();
    a.get('learning-confirm-submit').should('not.be.disabled');
  });
});
describe('Real generated route proposal customization', () => {
  for (const [value, flag] of [['2', 'rangeUnderflow'], ['21', 'rangeOverflow']]) it(`rejects ${value} questions per set after opening a real proposal`, () => {
    s.openProposal();
    s.proposalCount().clear().type(value);
    a.get('proposal-customize-form').find('button[type="submit"]').click();
    s.proposalCount().should($el => expect($el[0].validity[flag]).to.eq(true));
  });
});
