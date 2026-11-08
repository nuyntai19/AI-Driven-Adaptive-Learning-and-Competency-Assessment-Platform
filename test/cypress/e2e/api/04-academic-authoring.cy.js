const { IDS, question, unique } = require('../../fixtures/builders');
const { expectProblem } = require('../../support/validation');

describe('Question, curriculum, subject and graph validation', () => {
  it('accepts a valid teacher-authored question and persists the native answer', () => cy.apiLogin('teacher').then(token => {
    cy.api(token, 'POST', '/questions', question()).then(r => {
      expect(r.status).to.eq(201);
      expect(r.body.data.questionId).to.be.a('string');
      cy.api(token, 'GET', `/questions/${r.body.data.questionId}`).then(read => {
        expect(read.status).to.eq(200);
        expect(read.body.data.correctAnswer).to.eq('2');
      });
    });
  }));
  for (const [name, patch] of [
    ['difficulty0', { difficulty: 0 }], ['difficulty6', { difficulty: 6 }], ['grade9', { gradeLevel: 9 }],
    ['grade13', { gradeLevel: 13 }], ['empty question', { questionText: ' ' }], ['empty answer', { correctAnswer: '' }],
    ['empty solution', { solution: '' }], ['zero score', { maxScore: 0 }], ['negative score', { maxScore: -1 }],
    ['three-decimal score', { maxScore: 1.001 }], ['zero estimated time', { estimatedTimeSeconds: 0 }],
    ['unknown language', { languageCode: 'xx' }], ['unknown type', { questionType: 'Unknown' }],
    ['numeric type', { questionType: '1' }], ['unknown evaluation mode', { answerEvaluationMode: 'JavascriptEval' }],
    ['numeric node injection', { primaryTopicNodeId: '10000 OR 1=1' }],
    ['node bigint overflow', { primaryTopicNodeId: '18446744073709551616' }],
    ['forged teacher ownership', { teacherId: IDS.teacher }], ['invalid visibility', { visibility: 'Public' }],
    ['script image URI', { imageDataUrl: 'data:image/svg+xml,<svg onload="alert(1)"/>' }],
    ['MCQ with numeric grading', { questionType: 'MultipleChoice', answerEvaluationMode: 'NumericRational' }],
    ['Essay with exact grading', { questionType: 'Essay', answerEvaluationMode: 'TextExact' }],
    ['duplicate rubric IDs', { gradingCriteria: { criteria: [
      { criterionId: 'same', title: 'A', description: 'A', maxScore: .5 }, { criterionId: 'same', title: 'B', description: 'B', maxScore: .5 },
    ] } }],
  ]) {
    it(`rejects ${name}`, () => cy.apiLogin('teacher').then(token => cy.expectRejected(token, 'POST', '/questions', { ...question(), ...patch })));
  }
  for (const [name, patch] of [
    ['empty code', { subjectCode: '' }], ['code length33', { subjectCode: 'A'.repeat(33) }],
    ['name length101', { subjectName: 'a'.repeat(101) }], ['description length501', { description: 'a'.repeat(501) }],
  ]) {
    it(`subject rejects ${name}`, () => cy.apiLogin('manager').then(token => cy.expectRejected(token, 'POST', '/subjects', {
      subjectCode: unique('CYP'), subjectName: 'Môn học Cypress', ...patch,
    })));
  }
  for (const [name, patch] of [
    ['blank title', { title: '' }], ['title length251', { title: 'a'.repeat(251) }],
    ['grade9', { gradeLevel: 9 }], ['grade13', { gradeLevel: 13 }], ['null node list', { nodeIds: null }],
  ]) {
    it(`curriculum rejects ${name}`, () => cy.apiLogin('teacher').then(token => cy.expectRejected(token, 'POST', '/curriculums', {
      subjectId: IDS.math, title: unique('Giáo trình'), gradeLevel: 10, visibility: 'Private', nodeIds: [], ...patch,
    })));
  }
  for (const [name, patch] of [
    ['empty code', { nodeCode: '' }], ['empty name', { nodeName: '' }],
    ['unknown node type', { nodeType: 'Script' }], ['negative importance', { examImportance: -1 }],
    ['zero minutes', { estimatedLearningMinutes: 0 }],
  ]) {
    it(`knowledge node rejects ${name}`, () => cy.apiLogin('teacher').then(token => cy.expectRejected(token, 'POST', '/knowledge/nodes', {
      subjectId: IDS.math, nodeType: 'Topic', nodeCode: unique('CYP'), nodeName: 'Kiến thức Cypress',
      examImportance: 1, estimatedLearningMinutes: 30, isActive: true, ...patch,
    })));
  }
  it('rejects a prerequisite self-loop', () => cy.apiLogin('teacher').then(token => {
    cy.api(token, 'POST', '/knowledge/edges', { subjectId: IDS.math, sourceNodeId: '10000', targetNodeId: '10000', relationType: 'PrerequisiteOf', weight: 1 })
      .then(r => expectProblem(r, 400, 'VALIDATION_FAILED'));
  }));
  it('invalid authoring does not increase persisted question count', () => {
    cy.task('isolated:count', 'questions').then(before => cy.apiLogin('teacher').then(token => {
      cy.expectRejected(token, 'POST', '/questions', { ...question(), difficulty: 0 });
      cy.task('isolated:count', 'questions').should('eq', before);
    }));
  });
});
