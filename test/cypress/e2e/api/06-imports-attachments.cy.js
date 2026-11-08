const { upload, importFields, csv } = require('../../support/uploads');
const { expectProblem } = require('../../support/validation');

describe('Import and attachment validation/sanitization', () => {
  it('accepts a valid real PNG and returns a scoped expiring upload grant', () => cy.apiLogin('student').then(token =>
    cy.task('isolated:png', null, { log: false }).then(base64 => upload(token, '/learning/attempts/attachments/prepare-upload', {
      name: 'cypress-drawing.png', type: 'image/png', base64,
    }).then(r => {
      expect(r.status).to.eq(200);
      expect(r.body.data.drawingUploadToken).to.be.a('string').and.not.be.empty;
      expect(Date.parse(r.body.data.expiresAtUtc)).to.be.greaterThan(Date.now());
    }))));
  it('previews real CSV then binds confirmation to actor/revision and imports only once', () => {
    cy.apiLogin('teacher').then(token => upload(token, '/questions/import/preview', {
      name: 'cypress-valid.csv', type: 'text/csv', text: csv, fields: importFields,
    }).then(preview => {
      expect(preview.status).to.eq(200);
      expect(preview.body.data.validCount).to.eq(1);
      const grant = preview.body.data.previewToken;
      expect(grant).to.be.a('string').and.not.be.empty;
      const confirm = { previewToken: grant, revision: 1, importMode: 'AllValid', targetStatus: 'Draft', selectedQuestionKeys: [] };
      cy.apiLogin('foreignTeacher').then(foreign => cy.expectRejected(foreign, 'POST', '/questions/import/confirm', confirm));
      cy.expectRejected(token, 'POST', '/questions/import/confirm', { ...confirm, revision: 2 });
      cy.expectRejected(token, 'POST', '/questions/import/confirm', { ...confirm, targetStatus: 'Active' });
      cy.task('isolated:count', 'questions').then(before => {
        cy.api(token, 'POST', '/questions/import/confirm', confirm).then(r => {
          expect(r.status).to.eq(200);
          expect(r.body.data.importedCount).to.eq(1);
        });
        cy.task('isolated:count', 'questions').should('eq', before + 1);
        cy.expectRejected(token, 'POST', '/questions/import/confirm', confirm);
        cy.task('isolated:count', 'questions').should('eq', before + 1);
      });
    }));
  });
  for (const file of [
    { name: 'empty.csv', text: '' },
    { name: 'script.exe', text: csv },
    { name: 'corrupt.xlsx', text: 'not an OpenXML archive' },
    { name: 'missing-headers.csv', text: 'unexpected\nvalue\n' },
  ]) {
    it(`rejects ${file.name} without importing records`, () => cy.apiLogin('teacher').then(token => {
      cy.task('isolated:count', 'questions').then(before => {
        upload(token, '/questions/import/preview', { ...file, fields: importFields }).then(r => expectProblem(r, 400, null));
        cy.task('isolated:count', 'questions').should('eq', before);
      });
    }));
  }
  it('rejects a corrupt DOCX package in V2', () => cy.apiLogin('teacher').then(token =>
    upload(token, '/questions/import-v2/preview', { name: 'corrupt.docx', text: 'PK-not-a-zip',
      fields: { ...importFields, templateVersion: '2.0', sourceProfile: 'docx-1.0' } }).then(r => expectProblem(r, 400, null))));
  for (const file of [
    { name: 'empty.png', type: 'image/png', text: '' },
    { name: 'fake.png', type: 'image/png', text: '<script>window.__edutwinXss=1</script>' },
    { name: 'vector.svg', type: 'image/svg+xml', text: '<svg onload="alert(1)"/>' },
    { name: '../../escape.png', type: 'image/png', text: 'not a real PNG' },
  ]) {
    it(`refuses invalid drawing content ${file.name}`, () => cy.apiLogin('student').then(token =>
      upload(token, '/learning/attempts/attachments/prepare-upload', file).then(r => expectProblem(r, 400, null))));
  }
  it('rejects non-multipart upload requests', () => cy.apiLogin('student').then(token =>
    cy.expectRejected(token, 'POST', '/learning/attempts/attachments/prepare-upload', { file: 'fake.png' })));
  it('rejects drawing content over the 5 MiB application limit', () => cy.apiLogin('student').then(token =>
    upload(token, '/learning/attempts/attachments/prepare-upload', { name: 'oversized.png', type: 'image/png', text: 'x'.repeat(5 * 1024 * 1024 + 1) })
      .then(r => expectProblem(r, 413, null))));
  it('does not allow a manager to upload a student drawing', () => cy.apiLogin('manager').then(token =>
    cy.api(token, 'POST', '/learning/attempts/attachments/prepare-upload', {}).then(r => expectProblem(r, 403, null))));
});
