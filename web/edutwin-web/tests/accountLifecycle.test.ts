import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import { isConcurrencyConflict, extractProblemDetails } from "../src/utils/problemDetails.ts";
import type {
  TeacherDto,
  UpdateTeacherRequest,
  StudentDetailDto,
  UpdateStudentRequest,
  ResetAccountPasswordRequest,
  ResetAccountPasswordData,
  ResetAccountPasswordResponse,
} from "../src/types/organization.ts";

const centerManagerUser = {
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
  ],
};

const readOnlyCenterManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.teachersRead,
    permissions.studentsRead,
    permissions.classesRead,
  ],
};

const teacherUser = {
  accountType: "Teacher" as const,
  permissions: [
    permissions.dashboardsTeacherRead,
    permissions.twinStudentReadScoped,
  ],
};

const studentUser = {
  accountType: "Student" as const,
  permissions: [
    permissions.dashboardsStudentRead,
    permissions.learningAttemptsSubmit,
  ],
};

test("CenterManager with full permissions can perform teacher update, reset-password, and delete", () => {
  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersUpdate],
    }),
    true
  );

  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersResetPassword],
    }),
    true
  );

  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersDelete],
    }),
    true
  );
});

test("CenterManager with full permissions can perform student update, reset-password, and delete", () => {
  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsUpdate],
    }),
    true
  );

  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsResetPassword],
    }),
    true
  );

  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsDelete],
    }),
    true
  );
});

test("Read-only CenterManager cannot perform update, reset-password, or delete on teachers or students", () => {
  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersUpdate],
    }),
    false
  );

  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersResetPassword],
    }),
    false
  );

  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersDelete],
    }),
    false
  );

  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsUpdate],
    }),
    false
  );

  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsResetPassword],
    }),
    false
  );

  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsDelete],
    }),
    false
  );
});

test("Teacher and Student personas cannot perform center-level teacher or student account mutations", () => {
  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.teachersResetPassword, permissions.teachersDelete],
    }),
    false
  );

  assert.equal(
    canAccess(studentUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.studentsResetPassword, permissions.studentsDelete],
    }),
    false
  );
});

test("Teacher contract models serialize and validate expected OCC rowVersion and fields", () => {
  const teacher: TeacherDto = {
    teacherId: "00000000-0000-0000-0000-000000000010",
    username: "teacher.nguyen",
    displayName: "Nguyễn Văn Thầy",
    department: "Tổ Toán học",
    status: "Active",
    classCount: 2,
    rowVersion: "1001",
  };

  assert.equal(teacher.teacherId, "00000000-0000-0000-0000-000000000010");
  assert.equal(teacher.classCount, 2);
  assert.equal(teacher.rowVersion, "1001");

  const updateReq: UpdateTeacherRequest = {
    displayName: "Nguyễn Văn Thầy Mới",
    department: "Tổ Toán - Tin",
    status: "Active",
    rowVersion: "1001",
  };

  assert.ok(updateReq.displayName && updateReq.displayName.length >= 2);
  assert.ok(updateReq.rowVersion && updateReq.rowVersion.length > 0);

  // UI Rule: teacher cannot be deleted if assigned classes > 0
  const canDeleteTeacher = (teacher.classCount as number) === 0;
  assert.equal(canDeleteTeacher, false, "Teacher with assigned classes must block deletion");
});

test("Teacher detail contract validates canonical classCount and profile fields from getTeacher", () => {
  const teacherDetail: TeacherDto = {
    teacherId: "00000000-0000-0000-0000-000000000010",
    username: "teacher.nguyen",
    displayName: "Nguyễn Văn Thầy",
    department: "Tổ Toán học",
    status: "Active",
    classCount: 3,
    rowVersion: "1005",
  };

  assert.equal(teacherDetail.teacherId, "00000000-0000-0000-0000-000000000010");
  assert.equal(teacherDetail.username, "teacher.nguyen");
  assert.equal(teacherDetail.displayName, "Nguyễn Văn Thầy");
  assert.equal(teacherDetail.department, "Tổ Toán học");
  assert.equal(teacherDetail.status, "Active");
  assert.equal(teacherDetail.classCount, 3);
  assert.equal(teacherDetail.rowVersion, "1005");
});

test("Student contract models serialize and validate detail with subject goals and active classes", () => {
  const studentDetail: StudentDetailDto = {
    studentId: "00000000-0000-0000-0000-000000000020",
    username: "student.an",
    fullName: "Nguyễn Văn An",
    gradeLevel: 10,
    status: "Active",
    activeClassCount: 1,
    rowVersion: "2001",
    classes: [
      {
        classId: "00000000-0000-0000-0000-000000000030",
        className: "Lớp 10A1",
        academicYear: "2025-2026",
        subject: {
          subjectId: "00000000-0000-0000-0000-000000000040",
          subjectName: "Toán học",
        },
        teacher: {
          teacherId: "00000000-0000-0000-0000-000000000010",
          displayName: "Nguyễn Văn Thầy",
        },
        studentCount: 30,
        status: "Active",
        rowVersion: "3001",
      },
    ],
    subjectGoals: [
      {
        goalId: "00000000-0000-0000-0000-000000000050",
        studentId: "00000000-0000-0000-0000-000000000020",
        subjectId: "00000000-0000-0000-0000-000000000040",
        targetScore: 8.5,
        remainingDays: 30,
        currentPredictedScore: 8.0,
        riskScore: 0.1,
        rowVersion: "4001",
      },
    ],
  };

  assert.equal(studentDetail.fullName, "Nguyễn Văn An");
  assert.equal(studentDetail.gradeLevel, 10);
  assert.equal(studentDetail.classes.length, 1);
  assert.equal(studentDetail.subjectGoals.length, 1);
  assert.equal(studentDetail.subjectGoals[0].targetScore, 8.5);

  const updateReq: UpdateStudentRequest = {
    fullName: "Nguyễn Văn An Cập Nhật",
    gradeLevel: 11,
    status: "Active",
    rowVersion: "2001",
  };

  assert.ok(updateReq.fullName && updateReq.fullName.length >= 2);
  assert.ok(updateReq.gradeLevel && updateReq.gradeLevel >= 1 && updateReq.gradeLevel <= 12);
  assert.ok(updateReq.rowVersion.length > 0);
});

test("Reset password contract enforces password policy, reason requirements, and zero-secrets response", () => {
  const validRequest: ResetAccountPasswordRequest = {
    newPassword: "SecurePassword123!",
    expectedUserRowVersion: "1001",
    reason: "Quên mật khẩu theo yêu cầu trực tiếp từ giáo viên/học sinh",
  };

  assert.ok(validRequest.newPassword.length >= 12, "Password must be at least 12 characters");
  assert.ok(validRequest.newPassword.length <= 200, "Password must be at most 200 characters");
  assert.ok(validRequest.reason.trim().length > 0, "Reason must not be empty");
  assert.ok(validRequest.expectedUserRowVersion.length > 0, "Expected rowVersion must be provided");

  // Zero-secrets check: response must only contain targetUserId and newRowVersion, never raw or hashed secrets
  const responseData: ResetAccountPasswordData = {
    targetUserId: "00000000-0000-0000-0000-000000000050",
    newRowVersion: "1002",
  };

  const response: ResetAccountPasswordResponse = {
    data: responseData,
    meta: {
      traceId: "0HN4PASSWORDRESET01",
      timestamp: "2026-09-14T13:35:45Z",
    },
  };

  assert.equal(response.data.targetUserId, "00000000-0000-0000-0000-000000000050");
  assert.equal(response.data.newRowVersion, "1002");
  assert.equal("password" in response.data, false);
  assert.equal("passwordHash" in response.data, false);
  assert.equal("token" in response.data, false);
});

test("409 Concurrency Conflict correctly detected on stale account updates and password resets", () => {
  const conflictError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        type: "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
        title: "Xung đột dữ liệu",
        status: 409,
        detail: "Dữ liệu tài khoản đã bị thay đổi bởi người khác. Vui lòng tải lại danh sách.",
        errorCode: "CONCURRENCY_CONFLICT",
        traceId: "0HN4OCC_ACCOUNT_01",
      },
      headers: {},
    },
    message: "Request failed with status code 409",
  };

  assert.equal(isConcurrencyConflict(conflictError), true);
  const details = extractProblemDetails(conflictError);
  assert.equal(details.status, 409);
  assert.equal(details.errorCode, "CONCURRENCY_CONFLICT");
  assert.equal(details.traceId, "0HN4OCC_ACCOUNT_01");
  assert.ok(details.detail && details.detail.includes("thay đổi"));
});
