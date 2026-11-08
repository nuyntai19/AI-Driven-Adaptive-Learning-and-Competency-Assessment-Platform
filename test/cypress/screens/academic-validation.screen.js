const {
  IDS
} = require('../fixtures/builders');
const get = hook => cy.get(`[data-cy="${hook}"]`);
const academicScreen = {
  get,
  open(route, role = 'teacher') {
    cy.uiLogin(role);
    return cy.visit(route);
  },
  question() {
    academicScreen.open('/giao-vien/cau-hoi/tao-moi');
    get('question-subject').select(IDS.math);
    get('question-grade').select('10');
  },
  chooseTopic() {
    get('question-topic').should('not.be.disabled').click();
    return get('question-topic-option').should('have.length.at.least', 1).eq(0).click();
  },
  text(hook, value) {
    get(hook).click().type('{selectall}{backspace}');
    if (value) return get(hook).type(value, {
      delay: 0,
      parseSpecialCharSequences: false
    });
  },
  input(hook, value) {
    return get(hook).then($input => {
      // Number-controlled inputs may restore 0 on clear; replace the selected value
      // with real keystrokes so 4 is not accidentally entered as 40.
      if ($input.attr('type') === 'number' && value !== '') return get(hook).click().type('{selectall}').type(String(value), {
        delay: 0,
        parseSpecialCharSequences: false
      }).should('have.value', String(value));
      get(hook).clear();
      if (value !== '') return get(hook).type(String(value), {
        delay: 0,
        parseSpecialCharSequences: false
      });
    });
  },
  curriculum() {
    return academicScreen.open('/giao-vien/giao-trinh/tao-moi');
  },
  assignment() {
    academicScreen.open('/giao-vien/bai-tap/tao-moi');
    return get('assignment-class-select').should('not.be.disabled');
  },
  selectAnyClass() {
    return get('assignment-class-select').find('option').then($options => {
      const selected = [...$options].find(o => o.value);
      expect(selected, 'assigned class fixture').to.exist;
      return get('assignment-class-select').select(selected.value);
    });
  },
  graph() {
    cy.apiLogin('teacher').then(token => cy.api(token, 'POST', '/knowledge/graph/versions', {
      subjectId: IDS.math,
      gradeLevel: 10,
      title: 'UI validation Draft'
    }).then(r => {
      expect(r.status).to.eq(200);
      expect(r.body.data.graphVersionId).to.be.a('string');
      academicScreen.open(`/giao-vien/do-thi-tri-thuc?subjectId=${IDS.math}&gradeLevel=10&graphVersionId=${r.body.data.graphVersionId}`);
      get('graph-create-node').click();
      get('graph-node-name').should('be.visible');
    }));
  },
  graphSubmit() {
    return get('graph-create-node-form').find('button[type="submit"]').click();
  },
  import() {
    academicScreen.open('/giao-vien/cau-hoi');
    get('question-open-import').click();
    return get('question-import-source').should('be.visible');
  },
  file(hook, name, mimeType, content = 'invalid fixture') {
    return get(hook).selectFile({
      contents: Cypress.Buffer.from(content),
      fileName: name,
      mimeType
    }, {
      force: true
    });
  },
  notification(role) {
    const prefix = {
      teacher: '/giao-vien',
      manager: '/quan-ly',
      platform: '/quan-tri-nen-tang'
    }[role];
    academicScreen.open(prefix + '/thong-bao', role);
    get('notification-open-compose').click();
    return get('notification-title').should('be.visible');
  },
  notificationPreview() {
    return get('notification-compose-form').find('button[type="submit"]');
  }
};
module.exports = {
  academicScreen
};
