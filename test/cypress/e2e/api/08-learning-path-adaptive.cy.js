const { IDS, unique } = require('../../fixtures/builders');
const { expectProblem } = require('../../support/validation');

describe('Learning goals, calendar and route scope validation', () => {
  function scopedGoal(token, mutate, status) {
    return cy.api(token, 'GET', `/students/me/academic-context?subjectId=${IDS.math}`).then(context => {
      expect(context.status).to.eq(200);
      const classId = context.body.data.selectedClassId;
      expect(classId, 'a current seeded class is required').to.be.a('string').and.not.be.empty;
      return cy.api(token, 'GET', `/students/me/learning-paths/capability-map?subjectId=${IDS.math}&classId=${classId}`).then(map => {
        expect(map.status).to.eq(200);
        expect(map.body.skills, 'a scoped skill library is required').to.be.an('array').and.not.be.empty;
        const payload = { subjectId: IDS.math, classId, goalType: 'Foundation', title: unique('Mục tiêu'),
          targetSkillIds: [map.body.skills[0].skillId], availableMinutesPerDay: 15, availableDaysPerWeek: 5, preferredPace: 'Moderate', ...mutate };
        return cy.api(token, 'POST', '/students/me/learning-goals', payload).then(r => {
          if (status === 200) { expect(r.status).to.eq(200); expect(r.body.goalId).to.be.a('string').and.not.be.empty; }
          else expectProblem(r, status, null);
        });
      });
    });
  }
  it('accepts a valid class-scoped goal', () => cy.apiLogin('student').then(token => scopedGoal(token, {}, 200)));
  for (const [name, patch] of [
    ['score below0', { targetScore: -1 }], ['score above10', { targetScore: 11 }],
    ['unknown pace', { preferredPace: 'Unlimited' }], ['minutes4', { availableMinutesPerDay: 4 }],
    ['minutes301', { availableMinutesPerDay: 301 }], ['days0', { availableDaysPerWeek: 0 }], ['days8', { availableDaysPerWeek: 8 }],
    ['oversized title', { title: 'a'.repeat(201) }], ['oversized description', { description: 'a'.repeat(2001) }],
    ['foreign skills', { targetSkillIds: ['20000'] }], ['unknown goal type', { goalType: 'OverrideMastery' }],
    ['past deadline', { deadlineDate: '2020-01-01T00:00:00Z' }],
    ['invalid timezone', { schedule: { timeZoneId: '../../etc/passwd' } }],
    ['duplicate study days', { schedule: { studyDays: [1, 1, 2, 3, 4] } }],
    ['study day7', { schedule: { studyDays: [0, 1, 2, 3, 7] } }],
    ['reserve over40', { schedule: { reviewReservePercent: 41 } }],
  ]) {
    it(`rejects ${name}`, () => cy.apiLogin('student').then(token => scopedGoal(token, patch, 422)));
  }
});

describe('Adaptive practice validation and independent session ownership', () => {
  before(() => cy.task('isolated:assertAdaptiveSchema'));
  function clearActive(token) {
    return cy.api(token, 'GET', `/adaptive-practice/active-session?subjectId=${IDS.math}`).then(r => {
      // ASP.NET formats Ok(null) as204 when no active session exists.
      if (r.status === 204) {
        expect(r.body, 'no active session has no response payload').to.eq('');
        return;
      }
      expect(r.status).to.eq(200);
      expect(r.body?.sessionId, 'an existing session must have an ID').to.be.a('string').and.not.be.empty;
      return cy.api(token, 'POST', `/adaptive-practice/sessions/${r.body.sessionId}/close`).its('status').should('eq', 200);
    });
  }
  function withProposal(action) {
    return cy.apiLogin('student2').then(token => clearActive(token).then(() =>
      cy.api(token, 'GET', `/adaptive-practice/proposal?subjectId=${IDS.math}`).then(r => {
        expect(r.status).to.eq(200);
        expect(String(r.body.topicNodeId)).to.match(/^[1-9][0-9]*$/);
        return action(token, { subjectId: IDS.math, topicNodeId: r.body.topicNodeId, practiceMode: 'CustomTopic', timeBudgetMinutes: 15 });
      })));
  }
  it('creates a valid session and denies foreign ownership', () => withProposal((token, payload) =>
    cy.api(token, 'POST', '/adaptive-practice/sessions', payload).then(r => {
      expect(r.status).to.eq(201);
      const id = r.body.sessionId;
      expect(id).to.be.a('string').and.not.be.empty;
      cy.apiLogin('foreignStudent').then(foreign => cy.api(foreign, 'GET', `/adaptive-practice/sessions/${id}`).then(read => expectProblem(read, 404, 'SESSION_NOT_FOUND')));
    })));
  // Rejected input must return400 and leave session storage unchanged.
  for (const [name, patch] of [
    ['empty subject', { subjectId: IDS.zero }], ['zero topic', { topicNodeId: '0' }],
    ['invalid enum string', { practiceMode: 'Override' }], ['numeric enum', { practiceMode: 1 }],
    ['negative minutes', { timeBudgetMinutes: -1 }], ['zero minutes', { timeBudgetMinutes: 0 }],
    ['unoffered minutes', { timeBudgetMinutes: 11 }], ['minutes above20', { timeBudgetMinutes: 21 }],
    ['target below0', { customTargetMastery: -1 }], ['target above100', { customTargetMastery: 101 }],
    ['empty learning-path ID', { learningPathId: IDS.zero }], ['zero learning-path item ID', { learningPathItemId: '0' }],
  ]) {
    it(`rejects ${name} without saving a session`, () => withProposal((token, payload) =>
      cy.task('isolated:count', 'adaptiveSessions').then(before => {
        cy.api(token, 'POST', '/adaptive-practice/sessions', { ...payload, ...patch }).then(r => expectProblem(r, 400, null));
        cy.task('isolated:count', 'adaptiveSessions').should('eq', before);
      })));
  }
});
