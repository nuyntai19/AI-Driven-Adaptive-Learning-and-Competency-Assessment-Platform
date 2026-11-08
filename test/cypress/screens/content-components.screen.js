const {
  IDS
} = require('../fixtures/builders');
const {
  academicScreen: a
} = require('./academic-validation.screen');
const contentComponentsScreen = {
  draft(copy = false) {
    return cy.apiLogin('teacher').then(token => {
      const source = copy ? cy.api(token, 'GET', `/knowledge/graph/versions?subjectId=${IDS.math}&gradeLevel=10`).then(r => {
        expect(r.status).to.eq(200);
        const version = r.body.data.find(v => v.status === 'Published' && v.isCurrent && !v.isLegacy);
        expect(version, 'current approved graph fixture').to.exist;
        return version.graphVersionId;
      }) : cy.wrap(null, {
        log: false
      });
      return source.then(sourceGraphVersionId => cy.api(token, 'POST', '/knowledge/graph/versions', {
        subjectId: IDS.math,
        gradeLevel: 10,
        title: 'Component validation Draft',
        ...(sourceGraphVersionId ? {
          sourceGraphVersionId
        } : {})
      }).then(r => {
        expect(r.status).to.eq(200);
        a.open(`/giao-vien/do-thi-tri-thuc?subjectId=${IDS.math}&gradeLevel=10&graphVersionId=${r.body.data.graphVersionId}`);
        a.get('graph-version-title').should('be.visible');
      }));
    });
  },
  edge() {
    contentComponentsScreen.draft(true);
    a.get('graph-create-edge').click();
    return a.get('graph-edge-source').should('be.visible');
  },
  endpoints(same = false) {
    a.get('graph-edge-target').find('option').then($options => {
      const ids = [...$options].map(o => o.value).filter(Boolean);
      expect(ids.length, 'at least two authored topics in copied graph').to.be.at.least(2);
      a.get('graph-edge-source').select(ids[0]);
      a.get('graph-edge-target').select(same ? ids[0] : ids[1]);
    });
  },
  edgeSubmit: () => a.get('graph-create-edge-form').find('button[type="submit"]'),
  rubric() {
    a.question();
    a.get('question-type').select('Essay');
    a.get('rubric-add').click();
    return a.get('rubric-title').should('be.visible');
  },
  publishedCurriculum() {
    return cy.apiLogin('teacher').then(token => cy.api(token, 'GET', `/curriculums?subjectId=${IDS.math}&status=Published`).then(r => {
      expect(r.status).to.eq(200);
      const source = r.body.data.find(c => c.teacherId === IDS.teacher && c.reviewStatus === 'Published');
      expect(source, 'owned published curriculum fixture').to.exist;
      return cy.api(token, 'POST', `/curriculums/${source.curriculumId}/clone`, {
        title: `Component validation ${Date.now()}`
      }).then(cloned => {
        expect(cloned.status).to.eq(201);
        const draft = cloned.body.data;
        return cy.api(token, 'POST', `/curriculums/${draft.curriculumId}/publish`, {
          rowVersion: draft.rowVersion
        }).then(published => {
          expect(published.status).to.eq(200);
          a.open(`/giao-vien/giao-trinh/${draft.curriculumId}`);
          a.get('curriculum-open-clone').should('be.visible');
        });
      });
    }));
  },
  clone() {
    contentComponentsScreen.publishedCurriculum();
    a.get('curriculum-open-clone').click();
    return a.get('curriculum-clone-title').should('be.visible');
  },
  archive() {
    contentComponentsScreen.publishedCurriculum();
    a.get('curriculum-open-archive').click();
    cy.contains('Không còn lớp hoạt động đang áp dụng giáo trình này.').should('be.visible');
    return a.get('curriculum-archive-reason').should('not.be.disabled');
  }
};
module.exports = {
  contentComponentsScreen
};
