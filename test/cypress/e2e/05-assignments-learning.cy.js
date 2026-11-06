const { IDS, assignment, answer, expectProblem } = require('../support/validation');

describe('Assignment and student submission validation', () => {
  it('accepts a teacher draft with valid class/question scope', () => cy.apiLogin('teacher').then(token =>
    cy.api(token, 'POST', '/assignments', assignment()).its('status').should('eq', 201)));
  for (const [name, patch] of [
    ['blank title', { title: ' ' }], ['title length251', { title: 'a'.repeat(251) }],
    ['empty class', { classId: IDS.zero }], ['past deadline', { dueAt: '2020-01-01T00:00:00Z' }],
    ['zero time limit', { timeLimitMinutes: 0 }], ['negative time limit', { timeLimitMinutes: -1 }],
    ['unknown target mode', { targetMode: 'AllTenants' }],
    ['selected mode without students', { targetMode: 'SelectedStudents', studentIds: [] }],
    ['malformed question ID', { questionIds: ['1;DROP TABLE questions'] }],
    ['duplicate question IDs', { questionIds: ['10000', '10000'] }],
    ['exam without time', { kind: 'Exam', examAt: null }],
  ]) {
    it(`rejects ${name}`, () => cy.apiLogin('teacher').then(token => cy.expectRejected(token, 'POST', '/assignments', { ...assignment(), ...patch })));
  }
  const submission = () => ({ ...answer(), clientSubmissionId: crypto.randomUUID() });
  for (const [name, patch] of [
    ['empty submission nonce', { clientSubmissionId: IDS.zero }], ['empty answer', { finalAnswer: ' ' }],
    ['negative confidence', { confidence: -1 }], ['confidence101', { confidence: 101 }],
    ['non-decimal question ID', { questionId: '10000 OR 1=1' }], ['question ID overflow', { questionId: '18446744073709551616' }],
    ['negative time', { timeSpentSeconds: -1 }], ['negative change count', { answerChanges: -1 }],
    ['empty assignment UUID', { assignmentId: IDS.zero }],
  ]) {
    it(`attempt rejects ${name} before persistence`, () => {
      cy.task('isolated:count', 'attempts').then(before => cy.apiLogin('student').then(token => {
        cy.expectRejected(token, 'POST', '/learning/attempts', { ...submission(), ...patch });
        cy.task('isolated:count', 'attempts').should('eq', before);
      }));
    });
  }
  it('persists a versioned draft, rejects stale writes and duplicate IDs, and submits once', () => {
    cy.task('isolated:assignment').then(fixture => cy.apiLogin('student').then(token => {
      const route = `/students/me/assignments/${fixture.assignmentId}`;
      cy.api(token, 'POST', `${route}/start`, {}).its('status').should('eq', 200);
      cy.api(token, 'PUT', `${route}/draft`, { answers: [answer()], draftVersion: 1 }).its('status').should('eq', 200);
      cy.expectRejected(token, 'PUT', `${route}/draft`, { answers: [{ ...answer(), finalAnswer: 'changed' }], draftVersion: 1 }, 409, 'CONCURRENCY_CONFLICT');
      cy.api(token, 'GET', route).then(r => {
        expect(r.status).to.eq(200);
        expect(r.body.data.draftAnswers[0].finalAnswer).to.eq(answer().finalAnswer);
      });
      cy.api(token, 'POST', `${route}/submit`, { answers: [answer(), { ...answer(), questionId: '10006', finalAnswer: '5' }] }).then(r => {
        expect(r.status).to.eq(200);
        expect(r.body.isCompleted).to.eq(true);
        const job = r.body.lastAnalysisJobId;
        expect(job).to.exist;
        // Replay the same endpoint: the submitted attempt count must not increase.
        cy.task('isolated:count', 'attempts').then(before => {
          cy.api(token, 'POST', `${route}/submit`, { answers: [answer()] }).its('status').should('eq', 200);
          cy.task('isolated:count', 'attempts').should('eq', before);
        });
        const poll = (remaining = 35) => cy.api(token, 'GET', `/learning/analysis-jobs/${job}`).then(state => {
          expect(state.status).to.eq(200);
          const status = state.body.data.status;
          if (['Completed', 'FallbackCompleted'].includes(status)) {
            expect(state.body.data.terminal).to.eq(true);
            expect(state.body.data.feedbackUrl).to.be.a('string').and.not.be.empty;
            return cy.api(token, 'GET', state.body.data.feedbackUrl.replace('/api/v1', '')).its('status').should('eq', 200);
          }
          expect(status, 'worker must finish, loading is not success').not.to.eq('FailedTerminal');
          expect(remaining, 'bounded worker completion').to.be.greaterThan(0);
          return cy.wait(1000, { log: false }).then(() => poll(remaining - 1));
        });
        poll();
      });
    }));
  });
  it('another student and another tenant cannot read assignment/feedback ownership', () => {
    cy.task('isolated:assignment').then(fixture => {
      const route = `/students/me/assignments/${fixture.assignmentId}`;
      cy.apiLogin('student').then(token => cy.api(token, 'GET', route).its('status').should('eq', 200));
      for (const role of ['student2', 'foreignStudent']) {
        cy.apiLogin(role).then(token => cy.api(token, 'GET', route).then(r => expectProblem(r, 404, null)));
      }
    });
  });
  it('rejects malformed analysis and attachment resource IDs', () => cy.apiLogin('student').then(token => {
    cy.expectRejected(token, 'GET', '/learning/analysis-jobs/not-a-number', undefined);
    cy.api(token, 'GET', '/learning/attempts/18446744073709551616/attachment').then(r => expectProblem(r, 400, null));
  }));
});
