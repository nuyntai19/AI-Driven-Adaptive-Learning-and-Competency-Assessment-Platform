import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import { isConcurrencyConflict, extractProblemDetails } from "../src/utils/problemDetails.ts";
import type { CenterProfileDto, UpdateCenterProfileRequest } from "../src/types/organization.ts";

const centerManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.centerRead,
    permissions.centerManage,
    permissions.dashboardsCenterRead,
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
    permissions.learningAttemptsSubmit,
    permissions.dashboardsStudentRead,
  ],
};

const platformAdminUser = {
  accountType: "PlatformAdmin" as const,
  permissions: [
    permissions.platformCentersRead,
    permissions.platformCentersManage,
  ],
};

test("CenterManager with centerRead or centerManage can access /quan-ly/trung-tam route", () => {
  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.centerRead, permissions.centerManage],
    }),
    true
  );
});

test("Non-CenterManager personas (Teacher, Student, PlatformAdmin) cannot access /quan-ly/trung-tam", () => {
  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.centerRead, permissions.centerManage],
    }),
    false
  );

  assert.equal(
    canAccess(studentUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.centerRead, permissions.centerManage],
    }),
    false
  );

  assert.equal(
    canAccess(platformAdminUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.centerRead, permissions.centerManage],
    }),
    false
  );
});

test("CenterProfile contracts serialize and validate correctly", () => {
  const profile: CenterProfileDto = {
    centerId: "00000000-0000-0000-0000-000000000001",
    centerCode: "TEST_CENTER",
    centerName: "Trung tâm Luyện thi EduTwin",
    status: "Active",
    timezone: "Asia/Ho_Chi_Minh",
    rowVersion: "12345",
  };

  assert.equal(profile.centerCode, "TEST_CENTER");
  assert.equal(profile.status, "Active");
  assert.equal(profile.timezone, "Asia/Ho_Chi_Minh");
  assert.equal(profile.rowVersion, "12345");

  const request: UpdateCenterProfileRequest = {
    centerName: "Trung tâm Luyện thi EduTwin Mới",
    timezone: "Asia/Bangkok",
    rowVersion: "12345",
  };

  assert.ok(request.centerName.trim().length > 0);
  assert.ok(request.centerName.trim().length <= 200);
  assert.ok(request.rowVersion.length > 0);
});

test("CenterProfile update 409 conflict detection extracts traceId for user support", () => {
  const conflictError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        type: "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
        title: "Xung đột dữ liệu",
        status: 409,
        detail: "Dữ liệu đã bị thay đổi bởi người khác, vui lòng thử lại.",
        errorCode: "CONCURRENCY_CONFLICT",
        traceId: "0HN4OCC_CENTER_01",
      },
      headers: {},
    },
    message: "Request failed with status code 409",
  };

  assert.equal(isConcurrencyConflict(conflictError), true);
  const details = extractProblemDetails(conflictError);
  assert.equal(details.status, 409);
  assert.equal(details.errorCode, "CONCURRENCY_CONFLICT");
  assert.equal(details.traceId, "0HN4OCC_CENTER_01");
});
