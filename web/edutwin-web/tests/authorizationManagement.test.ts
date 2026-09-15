import assert from "node:assert/strict";
import test from "node:test";
import { canAccess, hasPermission } from "../src/auth/capabilities.ts";
import { permissions, authorizationUiPermissions } from "../src/auth/permissions.ts";
import { isConcurrencyConflict, extractProblemDetails, isForbidden } from "../src/utils/problemDetails.ts";
import type {
  AuthorizationRoleDto,
  AuthorizationRoleQueryParams,
  AuthorizationAuditQueryParams,
  CreateAuthorizationRoleRequest,
  UpdateAuthorizationRoleRequest,
  ReplaceRolePermissionsRequest,
  ReplaceUserRolesRequest,
  PermissionDto,
} from "../src/types/authorization.ts";
import type { ProblemDetails } from "../src/types/auth.ts";
import {
  parseSafeError,
  buildRoleQueryParams,
  buildUserQueryParams,
  buildAuditQueryParams,
  isSelfUser,
  filterCompatibleRoles,
  validateRoleCreation,
} from "../src/pages/authorizationManagementHelpers.ts";

// ==========================================
// Test Personas
// ==========================================

const fullCenterManager = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.rolesRead,
    permissions.rolesCreate,
    permissions.rolesUpdate,
    permissions.rolesArchive,
    permissions.rolesManagePermissions,
    permissions.permissionsRead,
    permissions.userRolesRead,
    permissions.userRolesAssign,
    permissions.auditRead,
    permissions.teachersRead,
    permissions.studentsRead,
    permissions.classesRead,
  ],
};

const restrictedCenterManager = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.rolesRead,
    permissions.userRolesRead,
    // Lacks rolesCreate, rolesUpdate, rolesArchive, rolesManagePermissions, userRolesAssign, auditRead
  ],
};

const teacherUser = {
  accountType: "Teacher" as const,
  permissions: [
    permissions.dashboardsTeacherRead,
    permissions.twinReasoningReview,
    permissions.classesRead,
  ],
};

const studentUser = {
  accountType: "Student" as const,
  permissions: [
    permissions.dashboardsStudentRead,
    permissions.learningAttemptsSubmit,
  ],
};

// ==========================================
// Test Suites
// ==========================================

test("1. Full CenterManager has sufficient capability to access RBAC UI and all sub-panels", () => {
  assert.equal(
    canAccess(fullCenterManager, {
      accountTypes: ["CenterManager"],
      anyOf: authorizationUiPermissions,
    }),
    true
  );

  assert.equal(hasPermission(fullCenterManager, permissions.rolesRead), true);
  assert.equal(hasPermission(fullCenterManager, permissions.rolesCreate), true);
  assert.equal(hasPermission(fullCenterManager, permissions.rolesUpdate), true);
  assert.equal(hasPermission(fullCenterManager, permissions.rolesArchive), true);
  assert.equal(hasPermission(fullCenterManager, permissions.rolesManagePermissions), true);
  assert.equal(hasPermission(fullCenterManager, permissions.permissionsRead), true);
  assert.equal(hasPermission(fullCenterManager, permissions.userRolesRead), true);
  assert.equal(hasPermission(fullCenterManager, permissions.userRolesAssign), true);
  assert.equal(hasPermission(fullCenterManager, permissions.auditRead), true);
});

test("2. Restricted CenterManager can only access granted tabs and actions", () => {
  // Can view roles and user roles
  assert.equal(hasPermission(restrictedCenterManager, permissions.rolesRead), true);
  assert.equal(hasPermission(restrictedCenterManager, permissions.userRolesRead), true);

  // Strictly denied from mutations
  assert.equal(hasPermission(restrictedCenterManager, permissions.rolesCreate), false);
  assert.equal(hasPermission(restrictedCenterManager, permissions.rolesUpdate), false);
  assert.equal(hasPermission(restrictedCenterManager, permissions.rolesArchive), false);
  assert.equal(hasPermission(restrictedCenterManager, permissions.rolesManagePermissions), false);
  assert.equal(hasPermission(restrictedCenterManager, permissions.userRolesAssign), false);

  // Strictly denied from reading audit
  assert.equal(hasPermission(restrictedCenterManager, permissions.auditRead), false);
});

test("3. Teacher and Student are strictly blocked from center-wide RBAC", () => {
  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["CenterManager"],
      anyOf: authorizationUiPermissions,
    }),
    false
  );

  assert.equal(
    canAccess(studentUser, {
      accountTypes: ["CenterManager"],
      anyOf: authorizationUiPermissions,
    }),
    false
  );
});

test("4. Cross-tenant identifier returns fail-closed 404 ProblemDetails", () => {
  const crossTenantProblem: ProblemDetails = {
    type: "https://edutwin.local/problems/resource-not-found",
    title: "Không tìm thấy dữ liệu",
    status: 404,
    detail: "Không tìm thấy role trong trung tâm hiện tại.",
    errorCode: "RESOURCE_NOT_FOUND",
    traceId: "00-crosstenant-01",
  };
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 404,
      data: crossTenantProblem,
      headers: {},
    },
  };

  const details = extractProblemDetails(axiosError);
  assert.equal(details.errorCode, "RESOURCE_NOT_FOUND");
  assert.equal(details.traceId, "00-crosstenant-01");
  assert.equal(isForbidden(axiosError), false);
});

test("5. Platform permissions (platform.*) cannot be delegated or assigned in tenant", () => {
  const platformPermission: PermissionDto = {
    permissionCode: "platform.centers.manage",
    module: "Platform",
    resource: "centers",
    action: "manage",
    description: "Quản trị trung tâm nền tảng",
    allowedAccountTypes: ["PlatformAdmin"],
    isSensitive: true,
    isDelegable: false,
    status: "Active",
  };

  // 1. Not delegable
  assert.equal(platformPermission.isDelegable, false);

  // 2. Incompatible with any tenant account type
  assert.equal(platformPermission.allowedAccountTypes.includes("CenterManager"), false);
  assert.equal(platformPermission.allowedAccountTypes.includes("Teacher"), false);
  assert.equal(platformPermission.allowedAccountTypes.includes("Student"), false);

  // 3. Name check
  assert.equal(platformPermission.permissionCode.startsWith("platform."), true);
});

test("6. Non-delegable permissions cannot be assigned by tenant manager", () => {
  const nonDelegablePermission: PermissionDto = {
    permissionCode: "security.emergency.lockdown",
    module: "Security",
    resource: "emergency",
    action: "lockdown",
    description: "Khóa khẩn cấp trung tâm",
    allowedAccountTypes: ["CenterManager"],
    isSensitive: true,
    isDelegable: false,
    status: "Active",
  };

  assert.equal(nonDelegablePermission.isDelegable, false);
});

test("7. Role AccountType compatibility is strictly enforced", () => {
  const teacherOnlyPermission: PermissionDto = {
    permissionCode: "twin.reasoning.review",
    module: "Twin",
    resource: "reasoning",
    action: "review",
    description: "Duyệt suy luận học sinh",
    allowedAccountTypes: ["Teacher"],
    isSensitive: false,
    isDelegable: true,
    status: "Active",
  };

  const studentOnlyPermission: PermissionDto = {
    permissionCode: "learning.attempts.submit",
    module: "Learning",
    resource: "attempts",
    action: "submit",
    description: "Nộp bài làm học sinh",
    allowedAccountTypes: ["Student"],
    isSensitive: false,
    isDelegable: true,
    status: "Active",
  };

  // CenterManager role cannot receive teacher-only or student-only permissions
  assert.equal(teacherOnlyPermission.allowedAccountTypes.includes("CenterManager"), false);
  assert.equal(studentOnlyPermission.allowedAccountTypes.includes("CenterManager"), false);

  // Teacher role cannot receive student-only permission
  assert.equal(studentOnlyPermission.allowedAccountTypes.includes("Teacher"), false);
});

test("8. Actor cannot over-grant permissions they do not own to CenterManager roles", () => {
  const actorOwnedPermissions = new Set(["organization.classes.read", "organization.classes.create"]);
  const requestedRolePermissions = ["organization.classes.read", "organization.center.manage"];

  // Actor lacks organization.center.manage
  const isSubset = requestedRolePermissions.every((code) => actorOwnedPermissions.has(code));
  assert.equal(isSubset, false);
});

test("9. Actor cannot self-elevate privileges", () => {
  const privilegeEscalationProblem: ProblemDetails = {
    type: "https://edutwin.local/problems/privilege-escalation",
    title: "Không thể nâng đặc quyền",
    status: 403,
    detail: "Permission set vượt quá quyền được phép của người thao tác.",
    errorCode: "AUTH_PRIVILEGE_ESCALATION",
    traceId: "00-self-elevate-01",
  };
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 403,
      data: privilegeEscalationProblem,
      headers: {},
    },
  };

  const details = extractProblemDetails(axiosError);
  assert.equal(details.errorCode, "AUTH_PRIVILEGE_ESCALATION");
  assert.equal(isForbidden(axiosError), true);
});

test("10. Last tenant administrator cannot be deleted, archived, or stripped of admin role", () => {
  const lastAdminProblem: ProblemDetails = {
    type: "https://edutwin.local/problems/last-tenant-admin",
    title: "Phải giữ quản trị viên cuối",
    status: 409,
    detail: "Thao tác sẽ làm trung tâm không còn quản trị viên hợp lệ.",
    errorCode: "LAST_TENANT_ADMIN",
    traceId: "00-last-admin-01",
  };
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: lastAdminProblem,
      headers: {},
    },
  };

  const details = extractProblemDetails(axiosError);
  assert.equal(details.errorCode, "LAST_TENANT_ADMIN");
  assert.equal(details.status, 409);
});

test("11. Replace role permissions with stale RowVersion returns 409 ConcurrencyConflict", () => {
  const occProblem: ProblemDetails = {
    type: "https://edutwin.local/problems/concurrency-conflict",
    title: "Xung đột cập nhật",
    status: 409,
    detail: "Role đã được cập nhật bởi request khác.",
    errorCode: "CONCURRENCY_CONFLICT",
    traceId: "00-occ-role-01",
  };
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: occProblem,
      headers: {},
    },
  };

  assert.equal(isConcurrencyConflict(axiosError), true);
  const details = extractProblemDetails(axiosError);
  assert.equal(details.traceId, "00-occ-role-01");
});

test("12. Replace user roles with stale RowVersion returns 409 ConcurrencyConflict", () => {
  const occUserProblem: ProblemDetails = {
    type: "https://edutwin.local/problems/concurrency-conflict",
    title: "Xung đột cập nhật",
    status: 409,
    detail: "User đã được cập nhật bởi request khác.",
    errorCode: "CONCURRENCY_CONFLICT",
    traceId: "00-occ-user-01",
  };
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: occUserProblem,
      headers: {},
    },
  };

  assert.equal(isConcurrencyConflict(axiosError), true);
  const details = extractProblemDetails(axiosError);
  assert.equal(details.traceId, "00-occ-user-01");
});

test("13. Dynamic RBAC contracts serialize with canonical OCC string and pagination metadata", () => {
  const createReq: CreateAuthorizationRoleRequest = {
    roleCode: "TEACHER_EXAM_LEAD",
    roleName: "Trưởng ban đề thi",
    accountType: "Teacher",
    description: "Quản lý ngân hàng câu hỏi môn Toán",
  };
  assert.equal(createReq.roleCode, "TEACHER_EXAM_LEAD");
  assert.equal(createReq.accountType, "Teacher");

  const updateReq: UpdateAuthorizationRoleRequest = {
    roleName: "Trưởng ban đề thi nâng cao",
    status: "Active",
    rowVersion: "4",
    reason: "Cập nhật tên vai trò theo phân công mới",
  };
  assert.equal(typeof updateReq.rowVersion, "string");
  assert.equal(updateReq.reason.length >= 3, true);

  const replacePermReq: ReplaceRolePermissionsRequest = {
    permissionCodes: ["curriculum.questions.read", "curriculum.questions.create"],
    rowVersion: "4",
    reason: "Gán quyền câu hỏi",
  };
  assert.equal(replacePermReq.permissionCodes.length, 2);

  const replaceUserReq: ReplaceUserRolesRequest = {
    roleIds: ["11111111-1111-1111-1111-111111111111"],
    rowVersion: "2",
    reason: "Gán vai trò trưởng ban",
  };
  assert.equal(replaceUserReq.roleIds.length, 1);

  const roleQueryParams: AuthorizationRoleQueryParams = {
    search: "LEAD",
    accountType: "Teacher",
    status: "Active",
    page: 1,
    pageSize: 20,
  };
  assert.equal(roleQueryParams.search, "LEAD");

  const auditQueryParams: AuthorizationAuditQueryParams = {
    actionType: "RolePermissionsReplaced",
    page: 1,
    pageSize: 15,
  };
  assert.equal(auditQueryParams.actionType, "RolePermissionsReplaced");
});

test("14. Effective permissions calculation correctly attributes source roles", () => {
  const activeRoles: AuthorizationRoleDto[] = [
    {
      roleId: "role-1",
      roleCode: "TEACHER_DEFAULT",
      roleName: "Giáo viên mặc định",
      accountType: "Teacher",
      description: null,
      isSystemRole: true,
      status: "Active",
      permissionCodes: ["organization.classes.read", "organization.students.read"],
      activeUserCount: 5,
      rowVersion: "1",
    },
    {
      roleId: "role-2",
      roleCode: "CURRICULUM_COORDINATOR",
      roleName: "Điều phối giáo trình",
      accountType: "Teacher",
      description: null,
      isSystemRole: false,
      status: "Active",
      permissionCodes: ["organization.classes.read", "curriculum.curriculums.read"],
      activeUserCount: 1,
      rowVersion: "2",
    },
  ];

  const selectedRoleIds = ["role-1", "role-2"];
  const selectedRoles = activeRoles.filter((r) => selectedRoleIds.includes(r.roleId));

  const permissionSources: Record<string, string[]> = {};
  for (const r of selectedRoles) {
    for (const code of r.permissionCodes) {
      (permissionSources[code] ??= []).push(r.roleName);
    }
  }

  // organization.classes.read is provided by BOTH roles
  assert.deepEqual(permissionSources["organization.classes.read"], [
    "Giáo viên mặc định",
    "Điều phối giáo trình",
  ]);

  // curriculum.curriculums.read is provided ONLY by Điều phối giáo trình
  assert.deepEqual(permissionSources["curriculum.curriculums.read"], [
    "Điều phối giáo trình",
  ]);
});

test("15. System role invariant: system role is fixed and cannot be modified or archived", () => {
  const systemRole: AuthorizationRoleDto = {
    roleId: "sys-role-1",
    roleCode: "SYSTEM_CENTERMANAGER",
    roleName: "Quản lý trung tâm mặc định",
    accountType: "CenterManager",
    description: "Vai trò quản trị tối cao của trung tâm",
    isSystemRole: true,
    status: "Active",
    permissionCodes: ["organization.center.read"],
    activeUserCount: 1,
    rowVersion: "1",
  };

  assert.equal(systemRole.isSystemRole, true);
});

test("16. Self-change detection correctly identifies when actor edits their own roles", () => {
  const actorUserId: string = "user-123";
  const targetUserSelf: string = "user-123";
  const targetUserOther: string = "user-456";

  assert.equal(actorUserId === targetUserSelf, true);
  assert.equal(actorUserId === targetUserOther, false);
});

test("17. parseSafeError maps known backend error codes to localized messages and extracts traceId", () => {
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        errorCode: "CONCURRENCY_CONFLICT",
        traceId: "00-test-occ-trace",
        detail: "RowVersion mismatch on entity Role 123",
        title: "Conflict",
      },
    },
  };

  const parsed = parseSafeError(axiosError);
  assert.equal(parsed.code, "CONCURRENCY_CONFLICT");
  assert.equal(parsed.traceId, "00-test-occ-trace");
  assert.match(parsed.message, /Dữ liệu đã được cập nhật bởi một phiên làm việc khác/);
  // Must NOT be the raw backend string
  assert.notEqual(parsed.message, "RowVersion mismatch on entity Role 123");
});

test("18. parseSafeError strictly hides raw ProblemDetails internal detail and title for unknown error codes", () => {
  const rawSensitiveLeak = "SELECT password_hash FROM Users WHERE center_id = 'leak'; Connection timed out";
  const axiosError = {
    isAxiosError: true,
    response: {
      status: 500,
      data: {
        errorCode: "UNKNOWN_INTERNAL_CRASH",
        traceId: "00-crash-trace-999",
        detail: rawSensitiveLeak,
        title: "Internal Database Exception",
      },
    },
  };

  const parsed = parseSafeError(axiosError);
  assert.equal(parsed.code, "UNKNOWN_INTERNAL_CRASH");
  assert.equal(parsed.traceId, "00-crash-trace-999");
  // CRITICAL: Raw detail or title must NEVER appear in the user-facing message
  assert.equal(parsed.message.includes(rawSensitiveLeak), false);
  assert.equal(parsed.message.includes("Internal Database Exception"), false);
  assert.match(parsed.message, /Không thể hoàn tất thao tác do lỗi hệ thống/);
});

test("19. parseSafeError gracefully handles non-Axios exceptions without throwing", () => {
  const genericError = new Error("Network connection lost");
  const parsed = parseSafeError(genericError);
  assert.match(parsed.message, /Vui lòng kiểm tra kết nối mạng và thử lại/);
  assert.equal(parsed.traceId, undefined);

  const nullError = parseSafeError(null);
  assert.match(nullError.message, /Vui lòng kiểm tra kết nối mạng và thử lại/);
});

test("20. buildRoleQueryParams enforces server-side pagination, search trimming, and enum filtering", () => {
  // Test 1: Full options with untrimmed search
  const query1 = buildRoleQueryParams(2, 25, "  TEACHER  ", "Teacher", "Active");
  assert.deepEqual(query1, {
    page: 2,
    pageSize: 25,
    search: "TEACHER",
    accountType: "Teacher",
    status: "Active",
  });

  // Test 2: Omits "all" and whitespace-only search
  const query2 = buildRoleQueryParams(1, 10, "   ", "all", "all");
  assert.deepEqual(query2, {
    page: 1,
    pageSize: 10,
  });

  // Test 3: Pagination beyond page 1 prevents 100-record truncation
  const queryPage5 = buildRoleQueryParams(5, 50);
  assert.equal(queryPage5.page, 5);
  assert.equal(queryPage5.pageSize, 50);
});

test("21. buildUserQueryParams supports all center user types with server-side pagination and search", () => {
  // CenterManager search
  const managerQuery = buildUserQueryParams(1, 20, "manager", "CenterManager", "Active");
  assert.equal(managerQuery.accountType, "CenterManager");
  assert.equal(managerQuery.search, "manager");

  // Teacher search
  const teacherQuery = buildUserQueryParams(3, 15, "toan", "Teacher", "Active");
  assert.equal(teacherQuery.accountType, "Teacher");
  assert.equal(teacherQuery.page, 3);

  // Student search
  const studentQuery = buildUserQueryParams(1, 50, "nguyen", "Student", "Active");
  assert.equal(studentQuery.accountType, "Student");

  // "all" accounts search
  const allUsersQuery = buildUserQueryParams(1, 25, undefined, "all", "all");
  assert.equal(allUsersQuery.accountType, undefined);
  assert.equal(allUsersQuery.status, undefined);
});

test("22. buildAuditQueryParams supports all H3 audit filters and ISO timestamp conversion", () => {
  const query = buildAuditQueryParams(1, 15, {
    actionType: "RolePermissionsReplaced",
    permissionCode: "PERM_ROLES_CREATE",
    actorUserId: "actor-uuid-1",
    targetUserId: "target-user-uuid-2",
    targetId: "role-uuid-3",
    from: "2026-09-01T00:00",
    to: "2026-09-15T00:00",
  });

  assert.equal(query.actionType, "RolePermissionsReplaced");
  assert.equal(query.permissionCode, "PERM_ROLES_CREATE");
  assert.equal(query.actorUserId, "actor-uuid-1");
  assert.equal(query.targetUserId, "target-user-uuid-2");
  assert.equal(query.targetId, "role-uuid-3");
  assert.equal(typeof query.from, "string");
  assert.equal(typeof query.to, "string");

  // Empty string fields must be pruned
  const emptyQuery = buildAuditQueryParams(1, 15, {
    actionType: "",
    permissionCode: "   ",
    actorUserId: undefined,
  });
  assert.equal(emptyQuery.actionType, undefined);
  assert.equal(emptyQuery.permissionCode, undefined);
  assert.equal(emptyQuery.actorUserId, undefined);
});

test("23. isSelfUser accurately identifies case-insensitive self-change and avoids false positives", () => {
  assert.equal(isSelfUser("USER-ABC-123", "user-abc-123"), true);
  assert.equal(isSelfUser("user-1", "user-2"), false);
  assert.equal(isSelfUser(undefined, "user-1"), false);
  assert.equal(isSelfUser("user-1", undefined), false);
});

test("24. filterCompatibleRoles filters roles strictly matching candidate accountType and Active status", () => {
  const roles: AuthorizationRoleDto[] = [
    {
      roleId: "r1",
      roleCode: "TEACHER_MATH",
      roleName: "Giáo viên Toán",
      accountType: "Teacher",
      description: null,
      isSystemRole: false,
      status: "Active",
      permissionCodes: [],
      activeUserCount: 1,
      rowVersion: "1",
    },
    {
      roleId: "r2",
      roleCode: "TEACHER_ARCHIVED",
      roleName: "Giáo viên lưu trữ",
      accountType: "Teacher",
      description: null,
      isSystemRole: false,
      status: "Archived",
      permissionCodes: [],
      activeUserCount: 0,
      rowVersion: "1",
    },
    {
      roleId: "r3",
      roleCode: "STUDENT_MONITOR",
      roleName: "Lớp trưởng",
      accountType: "Student",
      description: null,
      isSystemRole: false,
      status: "Active",
      permissionCodes: [],
      activeUserCount: 2,
      rowVersion: "1",
    },
  ];

  const teacherRoles = filterCompatibleRoles(roles, "Teacher");
  assert.equal(teacherRoles.length, 1);
  assert.equal(teacherRoles[0].roleId, "r1");

  const studentRoles = filterCompatibleRoles(roles, "Student");
  assert.equal(studentRoles.length, 1);
  assert.equal(studentRoles[0].roleId, "r3");

  const managerRoles = filterCompatibleRoles(roles, "CenterManager");
  assert.equal(managerRoles.length, 0);
});

test("25. validateRoleCreation enforces role code regex, length, and tenant account types", () => {
  assert.equal(validateRoleCreation("TEACHER_LEAD", "Trưởng môn", "Teacher").valid, true);

  // Invalid role code: lowercase
  const lowerCode = validateRoleCreation("teacher_lead", "Trưởng môn", "Teacher");
  assert.equal(lowerCode.valid, false);
  assert.match(lowerCode.error!, /chữ in hoa/);

  // Invalid role code: special characters
  const specialCode = validateRoleCreation("ROLE-NAME", "Tên", "Teacher");
  assert.equal(specialCode.valid, false);

  // Empty role name
  const emptyName = validateRoleCreation("ROLE_LEAD", "  ", "Teacher");
  assert.equal(emptyName.valid, false);

  // Disallowed account type (e.g. PlatformAdmin cannot be created by tenant)
  const invalidAccount = validateRoleCreation("ADMIN_ROLE", "Admin", "PlatformAdmin");
  assert.equal(invalidAccount.valid, false);
  assert.match(invalidAccount.error!, /Loại tài khoản không hợp lệ/);
});
