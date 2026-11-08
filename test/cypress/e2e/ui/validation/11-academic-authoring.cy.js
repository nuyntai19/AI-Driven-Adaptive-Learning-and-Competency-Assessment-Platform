const {
  academicScreen: s
} = require('../../../screens/academic-validation.screen');
describe('Visible question editor validation', () => {
  it('requires a real topic selection', () => {
    s.question();
    s.get('question-save').click();
    cy.contains('Vui lòng chọn chủ đề Cây Tri thức (Topic Node) cho câu hỏi.').should('be.visible');
  });
  it('rejects blank authored text after choosing a topic', () => {
    s.question();
    s.chooseTopic();
    s.get('question-save').click();
    cy.contains('Vui lòng nhập nội dung đề bài hoặc đính kèm ảnh chứa đề.').should('be.visible');
  });
  it('rejects zero max score entered on screen', () => {
    s.question();
    s.chooseTopic();
    s.text('teacher-question-text', 'Tính 1 + 1');
    s.input('question-max-score', '0');
    s.get('question-save').click();
    cy.contains('Điểm tối đa phải lớn hơn 0.').should('be.visible');
  });
  for (const type of ['ShortAnswer', 'Essay']) it(`${type} requires the reference answer`, () => {
    s.question();
    s.chooseTopic();
    s.get('question-type').select(type);
    s.text('teacher-question-text', 'Tính 1 + 1');
    s.get('question-save').click();
    cy.contains(type === 'Essay' ? 'Vui lòng nhập đáp án chuẩn hoặc hướng dẫn chấm chuẩn cho câu hỏi tự luận.' : 'Vui lòng nhập đáp án chuẩn cho câu hỏi.').should('be.visible');
  });
  it('requires text for each MCQ option', () => {
    s.question();
    s.chooseTopic();
    s.get('question-type').select('MultipleChoice');
    s.text('teacher-question-text', 'Chọn đáp án đúng');
    s.get('question-save').click();
    cy.contains('Vui lòng nhập đầy đủ nội dung cho phương án A.').should('be.visible');
  });
  it('rejects an SVG picked as a question image', () => {
    s.question();
    s.file('question-image-file', 'untrusted.svg', 'image/svg+xml', '<svg onload="alert(1)"/>');
    cy.get('[role="alert"]').should('be.visible').and('contain.text', 'PNG');
  });
});
describe('Visible curriculum and assignment validation', () => {
  it('curriculum rejects a blank title without a create request', () => {
    s.curriculum();
    let writes = 0;
    cy.intercept('POST', '**/api/v1/curriculums', req => {
      writes++;
      req.continue();
    });
    s.get('curriculum-save').click();
    cy.contains('Không thể lưu giáo trình. Vui lòng kiểm tra lại thông tin.').should('be.visible');
    cy.then(() => expect(writes).to.eq(0));
  });
  it('curriculum rejects an unselected grade after entering a title', () => {
    s.curriculum();
    s.input('curriculum-title', 'Giáo trình UI');
    let writes = 0;
    cy.intercept('POST', '**/api/v1/curriculums', req => {
      writes++;
      req.continue();
    });
    s.get('curriculum-save').click();
    cy.contains('Không thể lưu giáo trình. Vui lòng kiểm tra lại thông tin.').should('be.visible');
    cy.then(() => expect(writes).to.eq(0));
  });
  it('curriculum title cannot exceed 200 typed characters', () => {
    s.curriculum();
    s.input('curriculum-title', 'A'.repeat(201));
    s.get('curriculum-title').should('have.value', 'A'.repeat(200));
  });
  it('assignment wizard rejects a missing title', () => {
    s.assignment();
    s.selectAnyClass();
    s.get('assignment-next-questions').click();
    cy.contains(/Vui lòng nhập.*tiêu đề|Tiêu đề.*không được/).should('be.visible');
  });
  it('assignment wizard rejects a missing class', () => {
    s.assignment();
    s.input('assignment-title', 'Bài tập UI');
    s.get('assignment-next-questions').click();
    cy.contains(/Vui lòng chọn lớp/).should('be.visible');
  });
  it('assignment wizard rejects nonpositive time', () => {
    s.assignment();
    s.selectAnyClass();
    s.input('assignment-title', 'Bài tập UI');
    s.input('teacher-assignment-timelimit-input', '0');
    s.get('assignment-next-questions').click();
    cy.contains('Thời gian làm bài phải là số dương lớn hơn 0.').should('be.visible');
  });
  it('assignment wizard cannot progress without any question', () => {
    s.assignment();
    s.selectAnyClass();
    s.input('assignment-title', 'Bài tập UI');
    s.get('assignment-next-questions').click();
    s.get('assignment-next-audience').click();
    cy.contains('Vui lòng chọn ít nhất 1 câu hỏi cho bài tập.').should('be.visible');
  });
});
describe('Visible graph node input boundaries', () => {
  for (const [field, value, flag] of [['graph-node-name', '', 'valueMissing'], ['graph-node-code', '', 'valueMissing'], ['graph-node-importance', '-1', 'rangeUnderflow'], ['graph-node-importance', '101', 'rangeOverflow'], ['graph-node-minutes', '4', 'rangeUnderflow'], ['graph-node-minutes', '301', 'rangeOverflow']]) {
    it(`blocks ${field}=${JSON.stringify(value)} through the real node form`, () => {
      s.graph();
      s.input('graph-node-name', 'Chủ đề UI');
      s.input('graph-node-code', 'UI_NODE');
      s.input(field, value);
      s.graphSubmit();
      s.get(field).should($el => expect($el[0].validity[flag]).to.eq(true));
    });
  }
  it('node code caps at 50 real keystrokes', () => {
    s.graph();
    s.input('graph-node-code', 'A'.repeat(51));
    s.get('graph-node-code').should('have.value', 'A'.repeat(50));
  });
});
describe('Real import file picker validation', () => {
  for (const [source, file, mime] of [['excel', 'bad.exe', 'application/octet-stream'], ['docx', 'bad.csv', 'text/csv'], ['media', 'bad.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document']]) {
    it(`${source} rejects a file with the wrong extension`, () => {
      s.import();
      s.get('question-import-source').select(source);
      s.file('question-import-file', file, mime);
      cy.get('[role="alert"]').should('be.visible');
      s.get('question-import-preview').should('be.disabled');
    });
  }
});
describe('Real notification composition validation', () => {
  for (const role of ['teacher', 'manager', 'platform']) {
    it(`${role} cannot preview an empty draft or a draft without recipients`, () => {
      s.notification(role);
      s.notificationPreview().should('be.disabled');
      s.input('notification-title', 'Thông báo UI');
      s.input('notification-body', 'Nội dung UI');
      s.notificationPreview().should('be.disabled');
    });
    it(`${role} title is capped at 160 characters`, () => {
      s.notification(role);
      s.input('notification-title', 'A'.repeat(161));
      s.get('notification-title').should('have.value', 'A'.repeat(160));
    });
  }
  it('teacher sees the server content restriction after typing HTML', () => {
    s.notification('teacher');
    s.input('notification-title', 'Thông báo UI');
    s.input('notification-body', '<img src=x onerror=alert(1)>');
    s.get('notification-select-all').click();
    cy.intercept('POST', '**/api/v1/notifications/preview').as('contentRejected');
    s.notificationPreview().should('not.be.disabled').click();
    cy.wait('@contentRejected').then(({
      response
    }) => {
      expect(response.statusCode).to.eq(400);
      expect(response.body.errorCode).to.eq('NOTIFICATION_CONTENT_RESTRICTED');
    });
    cy.get('[role="alert"]').should('be.visible');
    cy.contains(/không.*(?:HTML|thẻ)|nội dung.*không|Không gửi/i).should('be.visible');
  });
});
