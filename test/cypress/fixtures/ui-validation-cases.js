const {
  IDS
} = require('./builders');
const password = 'Disposable-New-Password-2026!';
const nativeForms = [{
  id: 'teacherCreate',
  base: {
    username: 'ui_teacher',
    password,
    name: 'Giáo viên UI',
    department: 'Toán'
  },
  required: ['username', 'password', 'name'],
  limits: {
    username: 100,
    name: 200,
    department: 150
  },
  short: {
    password: 11
  },
  shortErrors: {
    password: 'Một hoặc nhiều trường không hợp lệ.'
  }
}, {
  id: 'teacherEdit',
  base: {
    name: 'Giáo viên UI',
    department: 'Toán'
  },
  required: ['name'],
  limits: {
    name: 200,
    department: 150
  }
}, {
  id: 'teacherReset',
  base: {
    password,
    confirm: password,
    reason: 'Kiểm tra validation UI'
  },
  required: ['password', 'confirm', 'reason'],
  short: {
    password: 11,
    reason: 4
  },
  shortErrors: {
    password: 'Mật khẩu mới phải từ 12 đến 200 ký tự.',
    reason: 'Lý do đặt lại mật khẩu là bắt buộc'
  }
}, {
  id: 'studentCreate',
  base: {
    username: 'ui_student',
    password,
    name: 'Học sinh UI'
  },
  required: ['username', 'password', 'name'],
  limits: {
    username: 100,
    name: 200
  },
  short: {
    password: 11
  },
  shortErrors: {
    password: 'Dữ liệu nhập vào chưa hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc.'
  }
}, {
  id: 'studentEdit',
  base: {
    name: 'Học sinh UI'
  },
  required: ['name'],
  limits: {
    name: 200
  }
}, {
  id: 'studentReset',
  base: {
    password,
    confirm: password,
    reason: 'Kiểm tra validation UI'
  },
  required: ['password', 'confirm', 'reason'],
  short: {
    password: 11,
    reason: 4
  },
  shortErrors: {
    password: 'Mật khẩu mới phải từ 12 đến 200 ký tự.',
    reason: 'Lý do đặt lại mật khẩu là bắt buộc'
  }
}, {
  id: 'subjectCreate',
  base: {
    code: 'UI_SUBJECT',
    name: 'Môn học UI',
    description: 'Mô tả môn'
  },
  required: ['code', 'name'],
  limits: {
    code: 32,
    name: 100,
    description: 500
  }
}, {
  id: 'subjectEdit',
  base: {
    code: 'UI_SUBJECT',
    name: 'Môn học UI',
    description: 'Mô tả môn'
  },
  required: ['code', 'name'],
  limits: {
    code: 32,
    name: 100,
    description: 500
  }
}, {
  id: 'classCreate',
  base: {
    name: 'Lớp UI',
    year: '2026-2027',
    subject: IDS.math,
    teacher: IDS.teacher,
    grade: '10'
  },
  required: ['name', 'year', 'subject', 'teacher'],
  limits: {
    name: 150,
    year: 20
  }
}, {
  id: 'classEdit',
  base: {
    name: 'Lớp UI'
  },
  required: ['name'],
  limits: {
    name: 150
  }
}, {
  id: 'centerProfile',
  base: {
    name: 'Trung tâm UI'
  },
  required: ['name'],
  limits: {
    name: 200
  }
}, {
  id: 'roleCreate',
  base: {
    code: 'UI_VALIDATION',
    name: 'Vai trò UI',
    description: 'Mô tả quyền'
  },
  required: ['code', 'name'],
  limits: {
    code: 64,
    name: 150,
    description: 500
  }
}, {
  id: 'platformCreate',
  base: {
    code: 'UI_CENTER',
    name: 'Trung tâm UI',
    username: 'ui_manager',
    displayName: 'Quản lý UI',
    password
  },
  required: ['code', 'name', 'username', 'displayName', 'password']
}, {
  id: 'platformEdit',
  base: {
    name: 'Trung tâm UI',
    reason: 'Kiểm tra UI'
  },
  required: ['name', 'reason'],
  short: {
    reason: 2
  },
  shortErrors: {
    reason: 'Lý do phải có ít nhất 3 ký tự.'
  }
}, {
  id: 'managerCreate',
  base: {
    username: 'ui_manager_extra',
    name: 'Quản lý UI',
    password,
    reason: 'Kiểm tra validation'
  },
  required: ['username', 'name', 'password', 'reason']
}, {
  id: 'securityStudent',
  base: {
    current: 'Disposable-Current-Only!',
    password,
    confirm: password
  },
  required: ['current', 'password', 'confirm'],
  short: {
    password: 11
  },
  shortErrors: {
    password: 'Mật khẩu mới phải có ít nhất 12 ký tự.'
  }
}, {
  id: 'securityPlatform',
  base: {
    current: 'Disposable-Current-Only!',
    password,
    confirm: password
  },
  required: ['current', 'password', 'confirm'],
  short: {
    password: 11
  },
  shortErrors: {
    password: 'Mật khẩu mới phải có ít nhất 12 ký tự.'
  }
}];
module.exports = {
  nativeForms,
  password
};
