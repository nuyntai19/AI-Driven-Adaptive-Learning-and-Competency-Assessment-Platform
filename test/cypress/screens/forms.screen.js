const {
  IDS
} = require('../fixtures/builders');

// Open buttons and controls are UI adapters; rules/assertions live in fixtures/specs.
const flows = {
  teacherCreate: {
    role: 'manager',
    route: '/quan-ly/giao-vien',
    open: 'btn-create-teacher',
    fields: {
      username: 'create-username',
      password: 'create-password',
      name: 'create-display-name',
      department: 'create-department'
    }
  },
  teacherEdit: {
    role: 'manager',
    route: '/quan-ly/giao-vien',
    listOpen: 'teacher-edit',
    fields: {
      name: 'edit-display-name',
      department: 'edit-department'
    }
  },
  teacherReset: {
    role: 'manager',
    route: '/quan-ly/giao-vien',
    listOpen: 'teacher-reset',
    fields: {
      password: 'reset-new-password',
      confirm: 'reset-confirm-password',
      reason: 'reset-reason'
    }
  },
  studentCreate: {
    role: 'manager',
    route: '/quan-ly/hoc-sinh',
    open: 'btn-create-student',
    fields: {
      username: 'student-username',
      password: 'student-password',
      name: 'student-fullname',
      grade: 'student-grade'
    }
  },
  studentEdit: {
    role: 'manager',
    route: '/quan-ly/hoc-sinh',
    listOpen: 'student-edit',
    fields: {
      name: 'edit-student-fullname'
    }
  },
  studentReset: {
    role: 'manager',
    route: '/quan-ly/hoc-sinh',
    listOpen: 'student-reset',
    fields: {
      password: 'reset-student-new-password',
      confirm: 'reset-student-confirm-password',
      reason: 'reset-student-reason'
    }
  },
  subjectCreate: {
    role: 'manager',
    route: '/quan-ly/mon-hoc',
    open: 'btn-open-create-subject',
    fields: {
      code: 'input-create-subject-code',
      name: 'input-create-subject-name',
      description: 'input-create-subject-desc'
    }
  },
  subjectEdit: {
    role: 'manager',
    route: '/quan-ly/mon-hoc',
    listOpen: 'subject-edit',
    fields: {
      code: 'input-edit-subject-code',
      name: 'input-edit-subject-name',
      description: 'input-edit-subject-desc'
    }
  },
  classCreate: {
    role: 'manager',
    route: '/quan-ly/lop-hoc',
    open: 'btn-open-create-class',
    fields: {
      name: 'input-class-name',
      year: 'input-academic-year',
      subject: 'select-subject',
      teacher: 'select-teacher',
      grade: 'select-grade-level'
    },
    selects: ['subject', 'teacher', 'grade']
  },
  classEdit: {
    role: 'manager',
    route: '/quan-ly/lop-hoc',
    listOpen: 'class-edit',
    fields: {
      name: 'input-edit-class-name'
    }
  },
  centerProfile: {
    role: 'manager',
    route: '/quan-ly/trung-tam',
    fields: {
      name: 'center-name'
    }
  },
  roleCreate: {
    role: 'manager',
    route: '/quan-ly/phan-quyen',
    open: 'btn-open-create-role',
    fields: {
      code: 'role-code',
      name: 'role-name',
      description: 'role-description'
    },
    disabledWhenBlank: true
  },
  platformCreate: {
    role: 'platform',
    route: '/quan-tri-nen-tang/trung-tam',
    open: 'platform-create-center',
    fields: {
      code: 'platform-center-code',
      name: 'platform-center-name',
      username: 'platform-manager-username',
      displayName: 'platform-manager-display-name',
      password: 'platform-manager-password'
    }
  },
  platformEdit: {
    role: 'platform',
    route: '/quan-tri-nen-tang/trung-tam',
    listOpen: 'platform-edit-center',
    fields: {
      name: 'platform-edit-center-name',
      reason: 'platform-edit-reason'
    }
  },
  managerCreate: {
    role: 'platform',
    route: '/quan-tri-nen-tang/trung-tam',
    listOpen: 'platform-managers',
    thenOpen: 'manager-create-tab',
    fields: {
      username: 'center-manager-username',
      name: 'center-manager-name',
      password: 'center-manager-password',
      reason: 'center-manager-reason'
    }
  },
  securityStudent: {
    role: 'student',
    route: '/hoc-tap/tong-quan',
    security: true,
    fields: {
      current: 'security-current-password',
      password: 'security-new-password',
      confirm: 'security-confirm-password'
    }
  },
  securityPlatform: {
    role: 'platform',
    route: '/quan-tri-nen-tang/trung-tam',
    security: true,
    fields: {
      current: 'security-current-password',
      password: 'security-new-password',
      confirm: 'security-confirm-password'
    }
  }
};
const get = hook => cy.get(`[data-cy="${hook}"]`);
const formsScreen = {
  flow: id => flows[id],
  open(id) {
    const f = flows[id];
    cy.uiLogin(f.role);
    cy.visit(f.route);
    if (f.open) get(f.open).click();
    // These are list actions: choose any actual record, without assuming its name/order.
    if (f.listOpen) get(f.listOpen).eq(0).click();
    if (f.thenOpen) get(f.thenOpen).click();
    if (f.security) {
      if (f.role === 'student') get('student-account-menu').click();
      get('account-security-open').click();
    }
    return formsScreen.field(id, Object.keys(f.fields)[0]).should('be.visible');
  },
  field(id, key) {
    return get(flows[id].fields[key]);
  },
  fill(id, values) {
    for (const [key, value] of Object.entries(values)) {
      const field = formsScreen.field(id, key);
      if (flows[id].selects?.includes(key)) {
        field.select(String(value));
      } else {
        field.clear();
        if (value !== '') formsScreen.field(id, key).type(String(value), {
          delay: 0,
          parseSpecialCharSequences: false,
          log: !/password|confirm|current/.test(key)
        });
      }
    }
  },
  submitButton(id) {
    return formsScreen.field(id, Object.keys(flows[id].fields)[0]).closest('form').find('button[type="submit"]');
  },
  submit(id) {
    return formsScreen.submitButton(id).click();
  },
  get,
  ids: IDS
};
module.exports = {
  formsScreen
};
