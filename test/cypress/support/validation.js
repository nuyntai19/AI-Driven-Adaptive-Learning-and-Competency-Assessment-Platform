const IDS = Object.freeze({
  zero: '00000000-0000-0000-0000-000000000000',
  math: '30000000-0000-0000-0000-000000000003',
  teacher: 'd0000000-0000-0000-0001-000000000002',
  student: 'd0000000-0000-0000-0001-000000000004',
  mathClass: '51000000-0000-0000-0000-000000000010',
  centerB: '20000000-0000-0000-0000-000000000002',
});
const XSS = '<img src="cypress-xss" onerror="window.__edutwinXss=1">';
function unique(prefix = 'CYP') { return `${prefix}_${Date.now()}_${Cypress._.random(10000, 99999)}`; }
function expectProblem(response, status = 400, errorCode = 'VALIDATION_FAILED') {
  expect(response.status, 'HTTP rejection').to.eq(status);
  // ASP.NET's JwtBearer challenge is allowed to be a bodyless401; domain validation still requires ProblemDetails.
  if (status === 401 && errorCode === null && (response.body === '' || response.body === null)) return;
  expect(response.body, 'Problem Details').to.be.an('object');
  if (errorCode) expect(response.body.errorCode, 'error code').to.eq(errorCode);
  expect(JSON.stringify(response.body), 'no infrastructure/secret disclosure')
    .not.to.match(/(?:at Microsoft\.|MySqlException|stackTrace|PasswordHash|SigningKey|Server=mysql)/i);
}
function teacher() { return { username: unique('teacher'), temporaryPassword: 'Disposable-Test-2026!', displayName: 'Giáo viên Cypress', department: 'Toán' }; }
function student() { return { username: unique('student'), temporaryPassword: 'Disposable-Test-2026!', fullName: 'Học sinh Cypress', gradeLevel: 10, classIds: [] }; }
function klass() { return { className: unique('Cypress'), academicYear: '2026-2027', subjectId: IDS.math, teacherId: IDS.teacher, gradeLevel: 10 }; }
function question() { return { subjectId: IDS.math, primaryTopicNodeId: '10000', gradeLevel: 10, visibility: 'Private',
  questionType: 'ShortAnswer', difficulty: 1, questionText: 'Tính 1 + 1', correctAnswer: '2', solution: 'Một cộng một bằng hai.',
  maxScore: 1, estimatedTimeSeconds: 60, reasoningRequired: false, languageCode: 'vi', answerEvaluationMode: 'NumericRational', options: [], knowledgeMappings: [] }; }
function assignment() { return { classId: IDS.mathClass, title: unique('Cypress'), instructions: 'Đọc kỹ câu hỏi.',
  dueAt: new Date(Date.now() + 48 * 3600000).toISOString(), timeLimitMinutes: 60, questionIds: ['10000'], targetMode: 'WholeClass' }; }
function answer() { return { questionId: '10000', finalAnswer: '10000', reasoningText: 'Thay x bằng 3 thì y bằng 7.', confidence: 80, timeSpentSeconds: 12, answerChanges: 0 }; }
function noExecutableMarkup() {
  cy.window().its('__edutwinXss').should('be.undefined');
  cy.get('img[src="cypress-xss"], script[data-cypress-xss], [href^="javascript:"]').should('not.exist');
}
module.exports = { IDS, XSS, unique, expectProblem, teacher, student, klass, question, assignment, answer, noExecutableMarkup };
