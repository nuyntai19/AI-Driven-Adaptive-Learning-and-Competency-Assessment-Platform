const {
  reviewScreen: s
} = require('../../../screens/review-governance.screen');
const {
  academicScreen: a
} = require('../../../screens/academic-validation.screen');
const {
  formsScreen
} = require('../../../screens/forms.screen');
describe('Real grading controls with a genuinely submitted assignment', () => {
  for (const score of ['-1', '11']) it(`rejects a typed teacher score of ${score}`, () => {
    s.open();
    a.input('grading-score', score);
    s.get('grading-save').click();
    cy.contains('Điểm phải nằm trong khoảng 0 đến điểm tối đa của câu hỏi.').should('be.visible');
  });
  it('requires visible teacher feedback before saving', () => {
    s.open();
    s.get('grading-correct').click();
    a.input('grading-feedback', '');
    s.get('grading-save').click();
    cy.contains('Vui lòng nhập lời nhận xét của giáo viên gửi học sinh trước khi lưu.').should('be.visible');
  });
  it('requires an adjustment reason for an already auto-graded answer', () => {
    s.open();
    s.get('grading-correct').click();
    a.input('grading-feedback', 'Nhận xét UI');
    a.input('grading-reason', '');
    s.get('grading-save').click();
    cy.contains('Vui lòng nhập lý do điều chỉnh điểm số (bắt buộc theo quy định kiểm tra).').should('be.visible');
  });
});
describe('Actual custom role information and permission controls', () => {
  it('rejects a role code starting with a number through the browser form', () => {
    formsScreen.open('roleCreate');
    formsScreen.fill('roleCreate', {
      code: '1INVALID',
      name: 'Vai trò UI'
    });
    formsScreen.submit('roleCreate');
    formsScreen.field('roleCreate', 'code').should($el => expect($el[0].validity.patternMismatch).to.eq(true));
  });
  it('cannot save role edits without a sufficiently long reason', () => {
    s.role();
    a.input('role-edit-name', 'Tên vai trò đã sửa');
    a.input('role-info-reason', 'aa');
    s.get('btn-save-role-info').should('be.disabled');
  });
  it('cannot replace permissions with a two-character reason', () => {
    s.role();
    s.get('role-permission-toggle').not('[disabled]').eq(0).check();
    a.input('role-permissions-reason', 'aa');
    s.get('btn-replace-role-permissions').should('be.disabled');
  });
});
describe('Platform account and center action reasons', () => {
  it('cannot change a center status with a short reason', () => {
    cy.uiLogin('platform');
    cy.visit('/quan-tri-nen-tang/trung-tam');
    s.get('platform-open-status').eq(0).click();
    a.input('platform-status-reason', 'abcd');
    s.get('platform-confirm-status').should('be.disabled');
  });
  it('cannot reset a manager password to a short value', () => {
    s.openManagerAction('manager-open-password');
    a.input('manager-reset-password', 'short');
    a.input('manager-reset-reason', 'Kiểm tra validation');
    s.get('manager-confirm-password').should('be.disabled');
  });
});
