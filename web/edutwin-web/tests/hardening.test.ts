import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import { useAuthStore } from "../src/stores/authStore.ts";
import {
  extractProblemDetails,
  isOverrideConflict,
  isConcurrencyConflict,
  isRateLimit,
  isForbidden,
  mapSafeLoginError,
} from "../src/utils/problemDetails.ts";
import {
  isTerminalStatus,
  isSuccessfulTerminalStatus,
  shouldContinuePolling,
} from "../src/utils/polling.ts";
import {
  isChartDataEmpty,
  toAccessibleTableData,
} from "../src/utils/chartFallbacks.ts";

test("direct-URL capability enforcement rejects unauthorized personas", () => {
  const studentUser = {
    userId: "s-1",
    centerId: "c-1",
    centerName: "Center",
    username: "student",
    displayName: "Student",
    accountType: "Student" as const,
    role: "Student" as const,
    roles: [],
    permissions: [
      permissions.dashboardsStudentRead,
      permissions.twinStudentReadOwn,
      permissions.learningAttemptsSubmit,
    ],
    authorizationVersion: 1,
  };

  const teacherUser = {
    userId: "t-1",
    centerId: "c-1",
    centerName: "Center",
    username: "teacher",
    displayName: "Teacher",
    accountType: "Teacher" as const,
    role: "Teacher" as const,
    roles: [],
    permissions: [
      permissions.dashboardsTeacherRead,
      permissions.twinStudentReadScoped,
      permissions.teacherReviewsRead,
      permissions.teacherReviewsOverride,
    ],
    authorizationVersion: 1,
  };

  const centerManagerUser = {
    userId: "m-1",
    centerId: "c-1",
    centerName: "Center",
    username: "manager",
    displayName: "Manager",
    accountType: "CenterManager" as const,
    role: "CenterManager" as const,
    roles: [],
    permissions: [
      permissions.dashboardsCenterRead,
    ],
    authorizationVersion: 1,
  };

  // Student direct-URL navigation checks
  assert.equal(canAccess(studentUser, { allOf: [permissions.dashboardsTeacherRead] }), false, "Student cannot access teacher class dashboard");
  assert.equal(canAccess(studentUser, { allOf: [permissions.dashboardsCenterRead] }), false, "Student cannot access center dashboard");
  assert.equal(canAccess(studentUser, { anyOf: [permissions.teacherReviewsRead, permissions.teacherReviewsOverride] }), false, "Student cannot access review queue");

  // Teacher direct-URL navigation checks
  assert.equal(canAccess(teacherUser, { allOf: [permissions.dashboardsCenterRead] }), false, "Teacher cannot access center dashboard");
  assert.equal(canAccess(teacherUser, { anyOf: [permissions.dashboardsTeacherRead, permissions.dashboardsCenterRead] }), true, "Teacher can access class dashboard via composite policy");

  // CenterManager direct-URL navigation checks
  assert.equal(canAccess(centerManagerUser, { allOf: [permissions.dashboardsCenterRead] }), true, "Manager can access center dashboard");
  assert.equal(canAccess(centerManagerUser, { anyOf: [permissions.dashboardsTeacherRead, permissions.dashboardsCenterRead] }), true, "Manager can access class dashboard via composite policy");
  assert.equal(canAccess(centerManagerUser, { allOf: [permissions.dashboardsStudentRead] }), false, "Manager cannot access student individual dashboard");
});

test("409 Override Conflict detection and traceId extraction from ProblemDetails", () => {
  const conflictAxiosError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        type: "https://httpstatuses.com/409",
        title: "Conflict",
        status: 409,
        detail: "The analysis has already been overridden with higher version.",
        errorCode: "OVERRIDE_CONFLICT",
        traceId: "0HN4TRACETEST01",
      },
      headers: {},
    },
    message: "Request failed with status code 409",
  };

  assert.equal(isOverrideConflict(conflictAxiosError), true);

  const details = extractProblemDetails(conflictAxiosError);
  assert.equal(details.status, 409);
  assert.equal(details.errorCode, "OVERRIDE_CONFLICT");
  assert.equal(details.traceId, "0HN4TRACETEST01");
  assert.match(details.message, /0HN4TRACETEST01/);
});

test("403 Forbidden ProblemDetails extracts traceId for support and audit", () => {
  const forbiddenError = {
    isAxiosError: true,
    response: {
      status: 403,
      data: {
        title: "Forbidden",
        status: 403,
        detail: "User does not own this class.",
        traceId: "TRACE-403-CLASS-DENIED",
      },
      headers: {},
    },
    message: "Forbidden",
  };

  const details = extractProblemDetails(forbiddenError);
  assert.equal(details.status, 403);
  assert.equal(details.traceId, "TRACE-403-CLASS-DENIED");
  assert.match(details.message, /User does not own this class/);
  assert.match(details.message, /TRACE-403-CLASS-DENIED/);
});

test("terminal-based polling recognizes completed, failed, and in-progress jobs", () => {
  // Terminal statuses
  assert.equal(isTerminalStatus("completed"), true);
  assert.equal(isTerminalStatus("COMPLETED"), true);
  assert.equal(isTerminalStatus("FallbackCompleted"), true);
  assert.equal(isTerminalStatus("FailedTerminal"), true);
  assert.equal(isSuccessfulTerminalStatus("Completed"), true);
  assert.equal(isSuccessfulTerminalStatus("FallbackCompleted"), true);
  assert.equal(isSuccessfulTerminalStatus("FailedTerminal"), false);

  // Non-terminal statuses
  assert.equal(isTerminalStatus("pending"), false);
  assert.equal(isTerminalStatus("processing"), false);
  assert.equal(isTerminalStatus("analyzing"), false);
  assert.equal(isTerminalStatus(null), false);
  assert.equal(isTerminalStatus(undefined), false);

  // Polling continuation logic
  assert.equal(shouldContinuePolling("processing", 10, 60), true);
  assert.equal(shouldContinuePolling("completed", 10, 60), false);
  assert.equal(shouldContinuePolling("processing", 60, 60), false); // Max attempts reached
});

test("logout clears session and resets auth store to anonymous state", () => {
  const testUser = {
    userId: "u-test",
    centerId: "c-test",
    centerName: "Center",
    username: "user",
    displayName: "User",
    accountType: "Student" as const,
    role: "Student" as const,
    roles: [],
    permissions: [permissions.dashboardsStudentRead],
    authorizationVersion: 1,
  };

  // Set session
  useAuthStore.getState().setSession("test-token-123", testUser);
  assert.equal(useAuthStore.getState().accessToken, "test-token-123");
  assert.equal(useAuthStore.getState().sessionStatus, "authenticated");
  assert.notEqual(useAuthStore.getState().user, null);

  // Clear session on logout
  useAuthStore.getState().clearSession();
  assert.equal(useAuthStore.getState().accessToken, null);
  assert.equal(useAuthStore.getState().user, null);
  assert.equal(useAuthStore.getState().sessionStatus, "anonymous");
});

test("chart text and table fallbacks handle empty and populated series", () => {
  // Empty data fallback
  assert.equal(isChartDataEmpty([]), true);
  assert.equal(isChartDataEmpty(null), true);
  assert.equal(isChartDataEmpty(undefined), true);
  assert.equal(isChartDataEmpty([{ x: 1 }]), false);

  const columns = [
    { key: "topicName" as const, label: "Chuyên đề" },
    { key: "mastery" as const, label: "Độ thuần thục (%)", format: (v: unknown) => `${v}%` },
  ];

  const emptyResult = toAccessibleTableData<{ topicName: string; mastery: number }>([], columns);
  assert.deepEqual(emptyResult.headers, ["Chuyên đề", "Độ thuần thục (%)"]);
  assert.deepEqual(emptyResult.rows, []);

  const populatedData = [
    { topicName: "Đại số 10", mastery: 75.5 },
    { topicName: "Hình học 10", mastery: 45.0 },
  ];

  const populatedResult = toAccessibleTableData(populatedData, columns);
  assert.equal(populatedResult.rows.length, 2);
  assert.deepEqual(populatedResult.rows[0], ["Đại số 10", "75.5%"]);
  assert.deepEqual(populatedResult.rows[1], ["Hình học 10", "45%"]);
});

test("isConcurrencyConflict detects 409 and CONCURRENCY_CONFLICT", () => {
  const err409 = {
    isAxiosError: true,
    response: {
      status: 409,
      data: { errorCode: "CONCURRENCY_CONFLICT" },
      headers: {},
    },
  };
  assert.equal(isConcurrencyConflict(err409), true);
  assert.equal(isConcurrencyConflict({ isAxiosError: true, response: { status: 400 } }), false);
  assert.equal(isConcurrencyConflict(new Error("Generic")), false);
});

test("isRateLimit detects 429 status code and TOO_MANY_REQUESTS", () => {
  const err429 = {
    isAxiosError: true,
    response: {
      status: 429,
      data: { errorCode: "TOO_MANY_REQUESTS" },
      headers: {},
    },
  };
  assert.equal(isRateLimit(err429), true);
  assert.equal(isRateLimit({ isAxiosError: true, response: { status: 500 } }), false);
});

test("isForbidden detects 403 status code and FORBIDDEN_RESOURCE", () => {
  const err403 = {
    isAxiosError: true,
    response: {
      status: 403,
      data: { errorCode: "FORBIDDEN_RESOURCE" },
      headers: {},
    },
  };
  assert.equal(isForbidden(err403), true);
  assert.equal(isForbidden({ isAxiosError: true, response: { status: 200 } }), false);
});

test("mapSafeLoginError sanitizes credentials error and does not disclose account existence", () => {
  const invalidCreds = {
    isAxiosError: true,
    response: {
      status: 401,
      data: { errorCode: "AUTH_INVALID_CREDENTIALS", detail: "Internal raw exception" },
      headers: {},
    },
  };
  const msg = mapSafeLoginError(invalidCreds);
  assert.equal(msg, "Mã trung tâm, tên đăng nhập hoặc mật khẩu không chính xác.");

  const rateLimitErr = {
    isAxiosError: true,
    response: {
      status: 429,
      data: { errorCode: "TOO_MANY_REQUESTS" },
      headers: {},
    },
  };
  assert.equal(mapSafeLoginError(rateLimitErr), "Hệ thống ghi nhận quá nhiều lượt thử. Vui lòng đợi và thử lại sau ít phút.");

  const disabledErr = {
    isAxiosError: true,
    response: {
      status: 403,
      data: { errorCode: "AUTH_USER_DISABLED" },
      headers: {},
    },
  };
  assert.equal(mapSafeLoginError(disabledErr), "Tài khoản hoặc trung tâm hiện không khả dụng.");

  const networkErr = new Error("Network down");
  assert.equal(mapSafeLoginError(networkErr), "Không thể hoàn tất đăng nhập. Vui lòng thử lại sau.");
});

test("CenterManager is strictly denied from Platform Admin routes (/quan-tri-nen-tang/*)", () => {
  const centerManagerUser = {
    userId: "m-1",
    centerId: "c-1",
    centerName: "Center",
    username: "manager",
    displayName: "Manager",
    accountType: "CenterManager" as const,
    role: "CenterManager" as const,
    roles: [],
    permissions: [
      permissions.dashboardsCenterRead,
      permissions.teachersRead,
      permissions.studentsRead,
      permissions.classesRead,
    ],
    authorizationVersion: 1,
  };

  assert.equal(
    canAccess(centerManagerUser, {
      anyOf: [
        permissions.platformCentersRead,
        permissions.platformCentersManage,
        permissions.platformAuditRead,
      ],
      accountTypes: ["PlatformAdmin"],
    }),
    false,
    "CenterManager cannot access platform admin route layout"
  );
  assert.equal(
    canAccess(centerManagerUser, {
      allOf: [permissions.platformAuditRead],
      accountTypes: ["PlatformAdmin"],
    }),
    false,
    "CenterManager cannot access platform audit logs"
  );
});

test("Teacher is strictly denied from Center-wide management routes", () => {
  const teacherUser = {
    userId: "t-1",
    centerId: "c-1",
    centerName: "Center",
    username: "teacher",
    displayName: "Teacher",
    accountType: "Teacher" as const,
    role: "Teacher" as const,
    roles: [],
    permissions: [
      permissions.dashboardsTeacherRead,
      permissions.twinStudentReadScoped,
    ],
    authorizationVersion: 1,
  };

  assert.equal(
    canAccess(teacherUser, { allOf: [permissions.centerManage] }),
    false,
    "Teacher cannot access center profile edit"
  );
  assert.equal(
    canAccess(teacherUser, {
      anyOf: [permissions.rolesRead, permissions.permissionsRead, permissions.userRolesRead, permissions.auditRead],
      accountTypes: ["CenterManager"],
    }),
    false,
    "Teacher cannot access authorization management"
  );
  assert.equal(
    canAccess(teacherUser, { allOf: [permissions.teachersCreate] }),
    false,
    "Teacher cannot create other teachers"
  );
});

test("Restricted CenterManager role is constrained by effective permissions", () => {
  const restrictedManager = {
    userId: "m-2",
    centerId: "c-1",
    centerName: "Center",
    username: "restricted_mgr",
    displayName: "Restricted Manager",
    accountType: "CenterManager" as const,
    role: "CenterManager" as const,
    roles: ["LimitedRole"],
    permissions: [
      permissions.dashboardsCenterRead,
      permissions.teachersRead,
      permissions.studentsRead,
    ],
    authorizationVersion: 1,
  };

  assert.equal(canAccess(restrictedManager, { allOf: [permissions.teachersRead] }), true);
  assert.equal(canAccess(restrictedManager, { allOf: [permissions.teachersCreate] }), false);
  assert.equal(canAccess(restrictedManager, { allOf: [permissions.teachersDelete] }), false);
  assert.equal(canAccess(restrictedManager, { allOf: [permissions.centerManage] }), false);
});
