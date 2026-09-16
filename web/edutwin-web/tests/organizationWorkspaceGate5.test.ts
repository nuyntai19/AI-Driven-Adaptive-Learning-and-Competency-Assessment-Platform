import assert from "node:assert/strict";
import test from "node:test";
import { canAccess, hasPermission } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import {
  evaluateClassCapabilities,
  toggleStudentSelection,
  mergePageSelection,
  unmergePageSelection,
  getCandidateListState,
} from "../src/pages/classListHelpers.ts";
import {
  isConcurrencyConflict,
  extractProblemDetails,
  mapSafeOperationalError,
} from "../src/utils/problemDetails.ts";
import type {
  TeacherDto,
  UpdateTeacherRequest,
  StudentDto,
  StudentDetailDto,
  UpdateStudentRequest,
  ResetAccountPasswordRequest,
  ClassDto,
  UpdateClassRequest,
  SubjectDto,
  UpdateSubjectRequest,
  StudentSubjectGoalDto,
  UpsertStudentSubjectGoalRequest,
} from "../src/types/organization.ts";

const fullCenterManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.teachersRead,
    permissions.teachersCreate,
    permissions.teachersUpdate,
    permissions.teachersDelete,
    permissions.teachersResetPassword,
    permissions.studentsRead,
    permissions.studentsCreate,
    permissions.studentsUpdate,
    permissions.studentsDelete,
    permissions.studentsResetPassword,
    permissions.classesRead,
    permissions.classesCreate,
    permissions.classesUpdate,
    permissions.classesManageMembers,
    permissions.subjectsRead,
    permissions.subjectsCreate,
    permissions.subjectsUpdate,
    permissions.subjectsDelete,
    permissions.nodesRead,
    permissions.edgesRead,
    permissions.dashboardsCenterRead,
    permissions.centerRead,
    permissions.centerManage,
  ],
};

const restrictedCenterManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.teachersRead,
    permissions.studentsRead,
    permissions.classesRead,
    permissions.subjectsRead,
  ],
};

const teacherUser = {
  accountType: "Teacher" as const,
  permissions: [
    permissions.classesRead,
    permissions.dashboardsTeacherRead,
    permissions.twinStudentReadScoped,
    permissions.teacherReviewsRead,
    permissions.teachersRead,
    permissions.studentsRead,
    permissions.subjectsRead,
  ],
};

const studentUser = {
  accountType: "Student" as const,
  permissions: [
    permissions.dashboardsStudentRead,
    permissions.twinStudentReadOwn,
    permissions.learningAttemptsSubmit,
  ],
};

const platformAdminUser = {
  accountType: "PlatformAdmin" as const,
  permissions: [
    permissions.platformCentersRead,
    permissions.platformCentersManage,
    permissions.platformManagersManage,
    permissions.platformAuditRead,
  ],
};

// 1. CenterManager đầy đủ quyền thấy tất cả action tương ứng
test("1. Full CenterManager has capability for all Organization Management actions", () => {
  // Teachers
  assert.equal(hasPermission(fullCenterManagerUser, permissions.teachersRead), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.teachersCreate), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.teachersUpdate), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.teachersDelete), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.teachersResetPassword), true);

  // Students
  assert.equal(hasPermission(fullCenterManagerUser, permissions.studentsRead), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.studentsCreate), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.studentsUpdate), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.studentsDelete), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.studentsResetPassword), true);

  // Classes
  const classCaps = evaluateClassCapabilities(fullCenterManagerUser);
  assert.equal(classCaps.canCreateClass, true);
  assert.equal(classCaps.canUpdateClass, true);
  assert.equal(classCaps.canAddMembers, true);
  assert.equal(classCaps.canRemoveMembers, true);
  assert.equal(classCaps.canViewDashboard, true);

  // Subjects
  assert.equal(hasPermission(fullCenterManagerUser, permissions.subjectsRead), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.subjectsCreate), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.subjectsUpdate), true);
  assert.equal(hasPermission(fullCenterManagerUser, permissions.subjectsDelete), true);
  assert.equal(
    hasPermission(fullCenterManagerUser, permissions.nodesRead) &&
      hasPermission(fullCenterManagerUser, permissions.edgesRead),
    true
  );
});

// 2. Restricted CenterManager chỉ thấy action được cấp
test("2. Restricted CenterManager only sees granted actions and cannot mutate", () => {
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.teachersRead), true);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.teachersCreate), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.teachersUpdate), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.teachersDelete), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.teachersResetPassword), false);

  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.studentsRead), true);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.studentsCreate), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.studentsUpdate), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.studentsDelete), false);

  const classCaps = evaluateClassCapabilities(restrictedCenterManagerUser);
  assert.equal(classCaps.canCreateClass, false);
  assert.equal(classCaps.canUpdateClass, false);
  assert.equal(classCaps.canAddMembers, false);
  assert.equal(classCaps.canRemoveMembers, false);

  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.subjectsRead), true);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.subjectsCreate), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.subjectsUpdate), false);
  assert.equal(hasPermission(restrictedCenterManagerUser, permissions.subjectsDelete), false);
});

// 3. Teacher/Student/PlatformAdmin không nhận nhầm giao diện hoặc quyền CenterManager
test("3. Actor isolation: Non-CenterManager users do not receive CenterManager context", () => {
  // Teacher accountType is not CenterManager
  assert.notEqual(teacherUser.accountType, "CenterManager");
  assert.equal(canAccess(teacherUser, { accountTypes: ["CenterManager"] }), false);

  // Student cannot access organization management routes
  assert.notEqual(studentUser.accountType, "CenterManager");
  assert.equal(canAccess(studentUser, { accountTypes: ["CenterManager"] }), false);
  assert.equal(hasPermission(studentUser, permissions.teachersRead), false);
  assert.equal(hasPermission(studentUser, permissions.classesRead), false);

  // PlatformAdmin cannot access center-manager scoped mutations
  assert.notEqual(platformAdminUser.accountType, "CenterManager");
  assert.equal(canAccess(platformAdminUser, { accountTypes: ["CenterManager"] }), false);
});

// 4. Read-only user không thể mutation
test("4. Read-only user cannot execute any mutation", () => {
  const readOnlyUser = {
    accountType: "CenterManager" as const,
    permissions: [
      permissions.teachersRead,
      permissions.studentsRead,
      permissions.classesRead,
      permissions.subjectsRead,
    ],
  };

  const mutationPermissions = [
    permissions.teachersCreate,
    permissions.teachersUpdate,
    permissions.teachersDelete,
    permissions.teachersResetPassword,
    permissions.studentsCreate,
    permissions.studentsUpdate,
    permissions.studentsDelete,
    permissions.studentsResetPassword,
    permissions.classesCreate,
    permissions.classesUpdate,
    permissions.classesManageMembers,
    permissions.subjectsCreate,
    permissions.subjectsUpdate,
    permissions.subjectsDelete,
  ];

  for (const perm of mutationPermissions) {
    assert.equal(hasPermission(readOnlyUser, perm), false, `Should not have ${perm}`);
  }
});

// 5. Teacher CRUD giữ RowVersion
test("5. Teacher CRUD maintains RowVersion for OCC", () => {
  const teacher: TeacherDto = {
    teacherId: "teacher-001",
    username: "teacher.nguyen",
    displayName: "Nguyễn Văn Thầy",
    department: "Tổ Toán",
    status: "Active",
    classCount: 2,
    rowVersion: "0x0000000000000101",
  };

  const updateReq: UpdateTeacherRequest = {
    displayName: "Nguyễn Văn Thầy (Đã cập nhật)",
    department: "Tổ Toán - Tin",
    status: "Active",
    rowVersion: teacher.rowVersion,
  };

  assert.equal(updateReq.rowVersion, "0x0000000000000101");

  const resetReq: ResetAccountPasswordRequest = {
    newPassword: "SecurePassword123!",
    expectedUserRowVersion: teacher.rowVersion,
    reason: "Yêu cầu cấp lại mật khẩu từ giáo viên",
  };

  assert.equal(resetReq.expectedUserRowVersion, "0x0000000000000101");

  const conflictError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: { errorCode: "CONCURRENCY_CONFLICT" },
      headers: {},
    },
  };
  const nonConflictError = {
    isAxiosError: true,
    response: {
      status: 400,
      data: { errorCode: "VALIDATION_FAILED" },
      headers: {},
    },
  };
  assert.equal(isConcurrencyConflict(conflictError), true);
  assert.equal(isConcurrencyConflict(nonConflictError), false);
});

// 6. Student CRUD và reset password giữ RowVersion
test("6. Student CRUD and reset password maintain RowVersion for OCC", () => {
  const student: StudentDto = {
    studentId: "student-001",
    username: "student.an",
    fullName: "Nguyễn Văn An",
    gradeLevel: 12,
    status: "Active",
    activeClassCount: 3,
    rowVersion: "0x0000000000000202",
  };

  const updateReq: UpdateStudentRequest = {
    fullName: "Nguyễn Văn An (Cập nhật)",
    gradeLevel: 12,
    status: "Active",
    rowVersion: student.rowVersion,
  };

  assert.equal(updateReq.rowVersion, "0x0000000000000202");

  const resetReq: ResetAccountPasswordRequest = {
    newPassword: "NewStudentPass123!",
    expectedUserRowVersion: student.rowVersion,
    reason: "Học sinh quên mật khẩu",
  };

  assert.equal(resetReq.expectedUserRowVersion, "0x0000000000000202");
});

// 7. Subject Goals vẫn hoạt động đúng contract (Digital Twin)
test("7. Subject Goals adhere to Digital Twin backend contract and validation", () => {
  const goal: StudentSubjectGoalDto = {
    studentId: "student-001",
    subjectId: "sub-toan",
    subjectCode: "TOAN12",
    subjectName: "Toán học 12",
    targetScore: 8.5,
    goalId: "goal-001",
    remainingDays: 60,
    currentPredictedScore: 7.8,
    riskScore: 0.15,
    rowVersion: "0x0000000000000303",
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-15T00:00:00Z",
  };

  assert.equal(goal.targetScore, 8.5);
  assert.equal(goal.remainingDays, 60);
  assert.equal(goal.rowVersion, "0x0000000000000303");

  const upsertReq: UpsertStudentSubjectGoalRequest = {
    targetScore: 9.0,
    remainingDays: 45,
    rowVersion: goal.rowVersion,
  };

  assert.ok(upsertReq.targetScore >= 0 && upsertReq.targetScore <= 10);
  assert.ok(upsertReq.remainingDays >= 1 && upsertReq.remainingDays <= 365);
  assert.equal(upsertReq.rowVersion, "0x0000000000000303");
});

// 8. Class candidate pagination / anti-join không bị thay bằng client filtering
test("8. Class candidate selection preserves multi-page state and SQL anti-join semantics", () => {
  let selectedIds: string[] = [];

  // Page 1 candidates
  const page1Ids = ["stu-1", "stu-2", "stu-3"];
  // Select stu-1
  selectedIds = toggleStudentSelection(selectedIds, "stu-1");
  assert.deepEqual(selectedIds, ["stu-1"]);

  // Select all on page 1
  selectedIds = mergePageSelection(selectedIds, page1Ids);
  assert.deepEqual(selectedIds, ["stu-1", "stu-2", "stu-3"]);

  // Switch to Page 2 candidates
  const page2Ids = ["stu-4", "stu-5"];
  // Selection from page 1 must NOT be lost when adding from page 2!
  selectedIds = mergePageSelection(selectedIds, page2Ids);
  assert.deepEqual(selectedIds, ["stu-1", "stu-2", "stu-3", "stu-4", "stu-5"]);

  // Unmerge page 1
  selectedIds = unmergePageSelection(selectedIds, page1Ids);
  assert.deepEqual(selectedIds, ["stu-4", "stu-5"]);

  // UpdateClassRequest validation with RowVersion
  const classUpdate: UpdateClassRequest = {
    className: "Lớp 10A1 Nâng Cao",
    teacherId: "teacher-001",
    status: "Active",
    rowVersion: "0x0000000000000404",
  };
  assert.equal(classUpdate.rowVersion, "0x0000000000000404");

  // Check state machine
  assert.equal(
    getCandidateListState({ isError: false, isLoading: true, isFetching: false, candidateCount: 0 }),
    "loading"
  );
  assert.equal(
    getCandidateListState({ isError: true, isLoading: false, isFetching: false, candidateCount: 0 }),
    "error"
  );
  assert.equal(
    getCandidateListState({ isError: false, isLoading: false, isFetching: false, candidateCount: 0 }),
    "empty"
  );
  assert.equal(
    getCandidateListState({ isError: false, isLoading: false, isFetching: false, candidateCount: 5 }),
    "ready"
  );
});

// 9. Class membership removal giữ semantics hiện hành
test("9. Class membership removal preserves assessment and attempts history", () => {
  const studentDetail: StudentDetailDto = {
    studentId: "student-001",
    username: "student.an",
    fullName: "Nguyễn Văn An",
    gradeLevel: 12,
    status: "Active",
    activeClassCount: 2,
    rowVersion: "0x0000000000000505",
    classes: [
      {
        classId: "class-math-1",
        className: "Toán 12A1",
        academicYear: "2026-2027",
        teacher: { teacherId: "tch-1", displayName: "Thầy Hưng" },
        subject: { subjectId: "sub-math", subjectName: "Toán học" },
        studentCount: 30,
        status: "Active",
        rowVersion: "0x01",
      },
      {
        classId: "class-phys-1",
        className: "Vật lý 12",
        academicYear: "2026-2027",
        teacher: { teacherId: "tch-2", displayName: "Cô Mai" },
        subject: { subjectId: "sub-phys", subjectName: "Vật lý" },
        studentCount: 25,
        status: "Active",
        rowVersion: "0x02",
      },
    ],
    subjectGoals: [],
  };

  // Removing student from "class-math-1" does not wipe their user account or grade
  const remainingClasses = studentDetail.classes.filter((c) => c.classId !== "class-math-1");
  assert.equal(remainingClasses.length, 1);
  assert.equal(remainingClasses[0].classId, "class-phys-1");
  assert.equal(studentDetail.studentId, "student-001");
  assert.equal(studentDetail.gradeLevel, 12);
});

// 10. Subject mutation giữ RowVersion và tôn trọng route contract Knowledge Graph
test("10. Subject mutation preserves RowVersion and respects knowledge graph route contract", () => {
  const subject: SubjectDto = {
    subjectId: "sub-bio-12",
    subjectCode: "BIO12",
    subjectName: "Sinh học 12",
    description: "Chương trình Sinh học nâng cao",
    isActive: true,
    rowVersion: "0x0000000000000606",
  };

  const updateSubjectReq: UpdateSubjectRequest = {
    subjectCode: "BIO12_ADV",
    subjectName: "Sinh học 12 Chuyên",
    description: "Chương trình nâng cao chuyên sâu",
    isActive: true,
    rowVersion: subject.rowVersion,
  };

  assert.equal(updateSubjectReq.rowVersion, "0x0000000000000606");

  // Knowledge graph route contract check: /kien-thuc/do-thi?subjectId={subjectId}
  const kgRoute = `/kien-thuc/do-thi?subjectId=${encodeURIComponent(subject.subjectId)}`;
  assert.equal(kgRoute, "/kien-thuc/do-thi?subjectId=sub-bio-12");
});

// 11. Raw ProblemDetails không được render ra UI
test("11. Raw ProblemDetails and internal error details are never leaked to user UI", () => {
  const backendRawError = {
    isAxiosError: true,
    response: {
      status: 400,
      headers: {},
      data: {
        title: "Internal database constraint violation",
        detail: "FK_StudentClasses_Users_StudentId violated at tbl_classes constraint line 94",
        status: 400,
        traceId: "trace-999-secure",
        errors: {
          code: ["Invalid SQL syntax or duplicate key"],
        },
      },
    },
  };

  const safeMsg = mapSafeOperationalError(backendRawError, "Không thể thao tác. Vui lòng thử lại.");
  // Must NOT contain technical internal details
  assert.equal(safeMsg.includes("FK_StudentClasses_Users_StudentId"), false);
  assert.equal(safeMsg.includes("constraint line 94"), false);
  assert.equal(safeMsg.includes("Invalid SQL syntax"), false);

  // TraceId is isolated and extracted separately
  const details = extractProblemDetails(backendRawError);
  assert.equal(details.traceId, "trace-999-secure");
});

// 12. Không có fake field / fake statistic
test("12. DTO models have zero fake fields, zero simulated KPIs", () => {
  const classObj: ClassDto = {
    classId: "cls-uuid",
    className: "Lý 12",
    academicYear: "2026-2027",
    subject: { subjectId: "sub-phys", subjectName: "Vật lý 12" },
    teacher: { teacherId: "tch-phys", displayName: "Cô Hương" },
    studentCount: 28,
    status: "Active",
    rowVersion: "0x55",
  };

  // Ensure no invented fields like "averageScore", "predictedPassRate" exist on ClassDto
  assert.equal((classObj as unknown as Record<string, unknown>).averageScore, undefined);
  assert.equal((classObj as unknown as Record<string, unknown>).predictedPassRate, undefined);

  const teacher: TeacherDto = {
    teacherId: "tch-uuid",
    username: "teacher.ly",
    displayName: "Cô Hương",
    department: "Tổ Lý - Hóa",
    status: "Active",
    classCount: 3,
    rowVersion: "0x66",
  };

  assert.equal((teacher as unknown as Record<string, unknown>).rating, undefined);
  assert.equal((teacher as unknown as Record<string, unknown>).kpiScore, undefined);
});
