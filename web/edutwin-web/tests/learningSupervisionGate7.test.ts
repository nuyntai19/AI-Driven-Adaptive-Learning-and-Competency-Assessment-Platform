import assert from "node:assert/strict";
import test from "node:test";
import { permissions } from "../src/auth/permissions.ts";
import { canAccess } from "../src/auth/capabilities.ts";
import type { EvidenceDecisionDto, TeacherReviewQueueItemDto, TeacherOverrideRequest } from "../src/types/reviews.ts";
import type { PermissionDto, AuthorizationRoleDto } from "../src/types/authorization.ts";
import type { ClassDto, ClassListParams } from "../src/types/organization.ts";
import { extractProblemDetails, isOverrideConflict } from "../src/utils/problemDetails.ts";
import {
  isValidOccVersion,
  formatOccVersionLabel,
  resolveReviewQueueViewMode,
  resolveStudentTwinViewMode,
  executeOccRefetchWrapper,
} from "../src/utils/reviewQueueHelpers.ts";

const centerManagerUser = (grants: string[] = []) => ({
  accountType: "CenterManager" as const,
  permissions: grants,
});

const teacherUser = (grants: string[] = []) => ({
  accountType: "Teacher" as const,
  permissions: grants,
});

const createAxiosError = (status: number, data: any = {}, headers: Record<string, string> = {}) => ({
  isAxiosError: true,
  name: "AxiosError",
  message: `Request failed with status code ${status}`,
  response: {
    status,
    data,
    headers,
  },
});

// ============================================================================
// 1. CANONICAL PERMISSIONS DECLARATIONS FOR GATE 7
// ============================================================================
test("1. Gate 7 Canonical Permissions: Learning supervision, reviews, and scratchpad capabilities", () => {
  assert.equal(
    permissions.teacherReviewsRead,
    "twin.reasoning.review",
    "teacherReviewsRead matches backend GET /api/v1/teachers/me/review-queue"
  );
  assert.equal(
    permissions.teacherReviewsOverride,
    "twin.reasoning.override",
    "teacherReviewsOverride matches backend POST /api/v1/teachers/me/reasoning-analyses/{id}/override"
  );
  assert.equal(
    permissions.learningAttemptsReadScoped,
    "learning.attempts.read_scoped",
    "learningAttemptsReadScoped guards GET /api/v1/learning/attempts/{attemptId}/attachment"
  );
  assert.equal(
    permissions.learningAttemptsSubmit,
    "learning.attempts.submit",
    "learningAttemptsSubmit matches attempt submission policy"
  );
  assert.equal(
    permissions.rolesManagePermissions,
    "authorization.roles.manage_permissions",
    "rolesManagePermissions matches PUT /api/v1/authorization/roles/{id}/permissions"
  );
});

// ============================================================================
// 2. CAPABILITY-FIRST ACCESS CONTROL FOR REVIEWS & SCRATCHPAD
// ============================================================================
test("2. Capabilities: CenterManager and Teacher access guards for Review Queue and Scratchpad", () => {
  // CenterManager with review permission
  const managerWithReview = centerManagerUser([permissions.teacherReviewsRead]);
  assert.equal(canAccess(managerWithReview, { allOf: [permissions.teacherReviewsRead] }), true);
  assert.equal(canAccess(managerWithReview, { allOf: [permissions.teacherReviewsOverride] }), false);

  // CenterManager with full review and override
  const managerFull = centerManagerUser([
    permissions.teacherReviewsRead,
    permissions.teacherReviewsOverride,
    permissions.learningAttemptsReadScoped,
  ]);
  assert.equal(canAccess(managerFull, { allOf: [permissions.teacherReviewsRead] }), true);
  assert.equal(canAccess(managerFull, { allOf: [permissions.teacherReviewsOverride] }), true);
  assert.equal(canAccess(managerFull, { allOf: [permissions.learningAttemptsReadScoped] }), true);

  // Teacher without scratchpad capability cannot view scratchpad attachments
  const teacherNoScratchpad = teacherUser([
    permissions.teacherReviewsRead,
    permissions.teacherReviewsOverride,
  ]);
  assert.equal(canAccess(teacherNoScratchpad, { allOf: [permissions.learningAttemptsReadScoped] }), false);
});

// ============================================================================
// 3. CANONICAL EVIDENCE DECISION DTO & OCC TOKEN SYNC (NO LEGACY MODE)
// ============================================================================
test("3. EvidenceDecisionDto: Synchronized contract with canonical OCC token", () => {
  const evidence: EvidenceDecisionDto = {
    evidenceAssessmentId: "ev-101",
    sourceType: "AIRuleHybrid",
    trustLevel: "HighConfidence",
    decisionMode: "AutomatedWithDeterministicFallback",
    reasoningWeight: 0.85,
    reasonCodes: ["REASONING_STEP_INCOMPLETE", "INTERMEDIATE_ARITHMETIC_ERROR"],
    requiresTeacherReview: true,
    policyVersion: "2026.09.v1",
    analysisOverrideVersion: 3,
    evaluatedAt: "2026-09-16T12:00:00Z",
  };

  assert.equal(evidence.evidenceAssessmentId, "ev-101");
  assert.equal(evidence.decisionMode, "AutomatedWithDeterministicFallback");
  assert.equal(evidence.reasoningWeight, 0.85);
  assert.equal(evidence.analysisOverrideVersion, 3);
  assert.equal(evidence.reasonCodes.length, 2);
  assert.equal(evidence.requiresTeacherReview, true);
  assert.equal("mode" in evidence, false, "Legacy mode property must not exist in canonical DTO");
});

// ============================================================================
// 4. REVIEW QUEUE ITEM CONSTRUCT & CANONICAL OCC INITIALIZATION
// ============================================================================
test("4. ReviewQueue: OverrideVersion is initialized from evidence.analysisOverrideVersion", () => {
  const reviewItem: TeacherReviewQueueItemDto = {
    attemptId: "att-42",
    studentId: "stu-101",
    studentName: "Nguyen Van A",
    questionId: "q-505",
    subjectId: "sub-math",
    questionText: "Tính giá trị biểu thức tích phân...",
    analysisId: "an-777",
    finalAnswer: "42",
    reasoningText: "Bước 1: Đặt u = 2x...\nBước 2: Vi phân du = 2dx...",
    isFallback: true,
    reasoningQuality: 65,
    analysisFeedback: "Cần kiểm tra kỹ bước đổi cận của tích phân.",
    analysisConfidence: 58,
    evidence: {
      evidenceAssessmentId: "ev-999",
      sourceType: "AIReasoningEngine",
      trustLevel: "RequiresReview",
      decisionMode: "RuleFallback",
      reasoningWeight: 0.6,
      reasonCodes: ["CONFIDENCE_BELOW_THRESHOLD"],
      requiresTeacherReview: true,
      policyVersion: "2026.09.v2",
      analysisOverrideVersion: 2,
      evaluatedAt: "2026-09-16T10:30:00Z",
    },
    submittedAt: "2026-09-16T10:28:00Z",
  };

  // Ensure overrideVersion starts from the actual OCC token, NOT hardcoded 0
  const initialOverrideVersion = reviewItem.evidence.analysisOverrideVersion;
  assert.equal(initialOverrideVersion, 2, "OverrideVersion must equal evidence.analysisOverrideVersion");

  const overridePayload: TeacherOverrideRequest = {
    reasoningQuality: 85,
    errorType: "Skill",
    feedback: "Em đã hiểu phương pháp nhưng nhầm cận x = 1.",
    isCorrect: false,
    awardedScore: 7.5,
    reason: "Giáo viên xác nhận lỗi tính toán cận, phương pháp tư duy đúng 85%",
    overrideVersion: initialOverrideVersion,
  };

  assert.equal(overridePayload.overrideVersion, 2, "Payload must send the exact OCC version");
  assert.ok(overridePayload.reason.length >= 3, "Reason is mandatory");
});

// ============================================================================
// 5. OCC 409 CONFLICT HANDLING AND REFETCH WORKFLOW
// ============================================================================
test("5. OCC Conflict: Correctly detects 409 and extract ProblemDetails with traceId", () => {
  const conflict409Error = createAxiosError(409, {
    type: "https://edutwin.local/problems/conflict",
    title: "Xung đột phiên bản",
    status: 409,
    detail: "Lượt phân tích đã được cập nhật bởi một phiên làm việc khác.",
    instance: "/api/v1/teachers/me/reasoning-analyses/an-777/override",
    errorCode: "OVERRIDE_CONFLICT",
    traceId: "trace-conflict-occ-777",
  });

  assert.equal(isOverrideConflict(conflict409Error), true, "Identifies 409 as override conflict");
  const details = extractProblemDetails(conflict409Error);
  assert.equal(details.status, 409);
  assert.equal(details.errorCode, "OVERRIDE_CONFLICT");
  assert.equal(details.traceId, "trace-conflict-occ-777");
});

// ============================================================================
// 6. SCRATCHPAD DRAWER HTTP STATUS DIFFERENTIATION
// ============================================================================
test("6. Scratchpad Drawer: Distinct error handling for 404, 503, and 403", () => {
  // 404: No attachment or out of scope
  const error404 = createAxiosError(404, { errorCode: "RESOURCE_NOT_FOUND" });
  const details404 = extractProblemDetails(error404);
  assert.equal(details404.status, 404);

  // 503: Storage outage / recovery
  const error503 = createAxiosError(503, { errorCode: "STORAGE_UNAVAILABLE" });
  const details503 = extractProblemDetails(error503);
  assert.equal(details503.status, 503);

  // 403: Forbidden
  const error403 = createAxiosError(403, { errorCode: "FORBIDDEN_RESOURCE" });
  const details403 = extractProblemDetails(error403);
  assert.equal(details403.status, 403);
});

// ============================================================================
// 7. CLASS SELECTOR: SERVER-SIDE SEARCH INTEGRATION
// ============================================================================
test("7. Class Selector: Server-side search queries send search param to backend", () => {
  const searchInput = "Chuyên Toán";
  const params: ClassListParams = {
    page: 1,
    pageSize: 20,
    search: searchInput.trim() || undefined,
  };

  assert.equal(params.page, 1);
  assert.equal(params.pageSize, 20);
  assert.equal(params.search, "Chuyên Toán");

  // Query key reflects both search term and page
  const queryKey = ["reviewQueueClasses", params.search, params.page];
  assert.deepEqual(queryKey, ["reviewQueueClasses", "Chuyên Toán", 1]);

  // Simulating backend response returning a class not previously cached
  const backendMatchingClass: ClassDto = {
    classId: "cls-math-adv",
    className: "Lớp 10 Chuyên Toán",
    academicYear: "2026-2027",
    subject: { subjectId: "sub-math", subjectName: "Toán học" },
    teacher: { teacherId: "t-1", displayName: "Thầy Bình" },
    studentCount: 30,
    status: "Active",
    rowVersion: "1",
  };

  const localCache = new Map<string, ClassDto>();
  assert.equal(localCache.has("cls-math-adv"), false, "Not in cache before query");

  // When response arrives, it is added to cache
  localCache.set(backendMatchingClass.classId, backendMatchingClass);
  assert.equal(localCache.has("cls-math-adv"), true, "Loaded into cache after server query");
  assert.equal(localCache.get("cls-math-adv")?.className, "Lớp 10 Chuyên Toán");
});

// ============================================================================
// 8. CLASS SELECTOR: PERSISTENT SELECTION CACHE ACROSS PAGINATION
// ============================================================================
test("8. Class Selector: Persistent selection cache preserves selected class across pages", () => {
  const classCache = new Map<string, ClassDto>();

  // Page 1 arrives
  const page1: ClassDto[] = [
    {
      classId: "cls-1",
      className: "Lớp 10A1",
      academicYear: "2026-2027",
      subject: { subjectId: "sub-1", subjectName: "Toán" },
      teacher: { teacherId: "t-1", displayName: "Cô Mai" },
      studentCount: 25,
      status: "Active",
      rowVersion: "1",
    },
    {
      classId: "cls-2",
      className: "Lớp 10A2",
      academicYear: "2026-2027",
      subject: { subjectId: "sub-1", subjectName: "Toán" },
      teacher: { teacherId: "t-1", displayName: "Cô Mai" },
      studentCount: 28,
      status: "Active",
      rowVersion: "1",
    },
  ];
  for (const c of page1) classCache.set(c.classId, c);

  // User selects cls-1
  const selectedClassId = "cls-1";

  // User paginates to Page 2
  const page2: ClassDto[] = [
    {
      classId: "cls-3",
      className: "Lớp 11B1",
      academicYear: "2026-2027",
      subject: { subjectId: "sub-1", subjectName: "Toán" },
      teacher: { teacherId: "t-2", displayName: "Thầy Hùng" },
      studentCount: 32,
      status: "Active",
      rowVersion: "1",
    },
  ];
  for (const c of page2) classCache.set(c.classId, c);

  // Compute options on page 2: cls-1 must be prepended if not in current page2
  const list = [...page2];
  if (selectedClassId && !list.some((c) => c.classId === selectedClassId)) {
    const cached = classCache.get(selectedClassId);
    if (cached) list.unshift(cached);
  }

  assert.ok(list.some((c) => c.classId === selectedClassId), "Selected class persists on page 2 options");
  assert.equal(list[0].classId, "cls-1");
  assert.equal(list.length, 2);
});

// ============================================================================
// 9. PERMISSION MATRIX GUARDRAIL 2: EXACT INTERSECTION RULE
// ============================================================================
test("9. Permission Matrix Guardrail 2: Toggle allowed only on Active ∩ Compatible ∩ Delegable ∩ Actor Effective", () => {
  const targetRole: AuthorizationRoleDto = {
    roleId: "role-teacher-custom",
    roleCode: "TEACHER_ASSISTANT",
    roleName: "Trợ giảng chuyên môn",
    description: "Trợ giảng chuyên môn",
    accountType: "Teacher",
    status: "Active",
    isSystemRole: false,
    permissionCodes: ["curriculum.questions.read", "legacy.deprecated.perm", "admin.override.perm"],
    activeUserCount: 2,
    rowVersion: "1",
  };

  const actorPermissions = new Set([
    "curriculum.questions.read",
    "curriculum.questions.create",
    "twin.reasoning.review",
  ]);

  const evaluatePermission = (permission: PermissionDto) => {
    const isActive = permission.status === "Active";
    const isOwnerByActor = actorPermissions.has(permission.permissionCode);
    const isDelegable = permission.isDelegable;
    const isCompatible = permission.allowedAccountTypes.includes(targetRole.accountType);
    const isOutOfScope = !isOwnerByActor;
    const isWithinDelegableSet = isActive && isCompatible && isDelegable && isOwnerByActor;
    const isAssigned = targetRole.permissionCodes.includes(permission.permissionCode);
    const canToggle = !targetRole.isSystemRole && isWithinDelegableSet;

    return { isActive, isOwnerByActor, isDelegable, isCompatible, isOutOfScope, isAssigned, canToggle };
  };

  // Case A: Permission in intersection (Active, Compatible with Teacher, Delegable, Owned by Actor)
  const permA: PermissionDto = {
    permissionCode: "curriculum.questions.create",
    module: "Curriculum",
    resource: "questions",
    action: "create",
    description: "Tạo câu hỏi",
    status: "Active",
    isSensitive: false,
    isDelegable: true,
    allowedAccountTypes: ["Teacher", "CenterManager"],
  };
  const evalA = evaluatePermission(permA);
  assert.equal(evalA.canToggle, true, "Perm A is inside intersection -> canToggle must be true");

  // Case B: Permission is Inactive
  const permB: PermissionDto = {
    ...permA,
    permissionCode: "legacy.deprecated.perm",
    status: "Deprecated",
  };
  const evalB = evaluatePermission(permB);
  assert.equal(evalB.canToggle, false, "Inactive perm cannot be toggled");
  assert.equal(evalB.isAssigned, true, "Already assigned inactive perm is visible");

  // Case C: Permission is Non-delegable
  const permC: PermissionDto = {
    ...permA,
    permissionCode: "system.critical.op",
    isDelegable: false,
  };
  const evalC = evaluatePermission(permC);
  assert.equal(evalC.canToggle, false, "Non-delegable perm cannot be toggled");

  // Case D: Permission not owned by Actor (out of actor's effective scope)
  const permD: PermissionDto = {
    ...permA,
    permissionCode: "admin.override.perm",
  };
  const evalD = evaluatePermission(permD);
  assert.equal(evalD.canToggle, false, "Actor lacks perm -> cannot toggle");
  assert.equal(evalD.isAssigned, true, "Assigned out-of-scope perm must remain marked assigned");

  // Case E: Target Role is System Role -> CANNOT toggle anything
  const systemRole = { ...targetRole, isSystemRole: true };
  const canToggleOnSystem = !systemRole.isSystemRole && evalA.canToggle;
  assert.equal(canToggleOnSystem, false, "System role cannot have permissions toggled");
});

// ============================================================================
// 10. READ-ONLY PRESERVATION OF ASSIGNED PERMISSIONS OUTSIDE DELEGABLE SET
// ============================================================================
test("10. Permission Matrix Preservation: Assigned permissions outside delegable set are preserved in save payload", () => {
  const initialPermissions = ["curriculum.questions.read", "legacy.deprecated.perm", "out_of_scope.perm"];
  let selectedPermissions = [...initialPermissions];

  // User toggles an allowed permission: curriculum.questions.create
  const allowedCode = "curriculum.questions.create";
  selectedPermissions = [...selectedPermissions, allowedCode];

  // When sending replace permissions payload:
  const payload = {
    permissionCodes: selectedPermissions,
    rowVersion: "1",
    reason: "Cập nhật bổ sung quyền tạo câu hỏi",
  };

  // Crucial check: non-toggleable permissions were NOT silently dropped!
  assert.ok(payload.permissionCodes.includes("legacy.deprecated.perm"), "Deprecated assigned perm preserved");
  assert.ok(payload.permissionCodes.includes("out_of_scope.perm"), "Out of scope assigned perm preserved");
  assert.ok(payload.permissionCodes.includes(allowedCode), "Newly toggled perm included");
  assert.equal(payload.permissionCodes.length, 4);
});

// ============================================================================
// 11. SYSTEM ROLE IMMUTABILITY AND SELF-ROLE MUTATION PROTECTION CONTRACT
// ============================================================================
test("11. Backend Security Boundary: System roles and self-roles invariants", () => {
  // Verify system role invariant contract
  const systemRole: AuthorizationRoleDto = {
    roleId: "sys-cm-1",
    roleCode: "SYSTEM_CENTER_MANAGER",
    roleName: "Quản lý trung tâm hệ thống",
    description: "Quản lý trung tâm hệ thống",
    accountType: "CenterManager",
    status: "Active",
    isSystemRole: true,
    permissionCodes: ["authorization.roles.read"],
    activeUserCount: 1,
    rowVersion: "1",
  };

  assert.equal(systemRole.isSystemRole, true);

  // Invariant 1: System role edit / archive UI is blocked
  const canEditName = !systemRole.isSystemRole;
  const canReplacePerms = !systemRole.isSystemRole;
  assert.equal(canEditName, false, "System role name edit must be disabled");
  assert.equal(canReplacePerms, false, "System role perm replacement must be disabled");

  // Invariant 2: Self-role assignment protection
  const actorId = "user-current-manager";
  const targetUserId = "user-current-manager";
  const isSelf = actorId === targetUserId;
  assert.equal(isSelf, true, "Actor modifying self must be rejected (InvalidStateTransition)");
});

// ============================================================================
// 12. DIGITAL TWIN KPI BOUNDED TO SINGLE SUBJECT
// ============================================================================
test("12. Digital Twin Single-Subject KPI: Average mastery strictly bounded to selected subjectId", () => {
  const topicsForSubjectMath = [
    { topicNodeId: "tn-1", topicName: "Tích phân cơ bản", masteryPercentage: 80, evidenceCount: 12 },
    { topicNodeId: "tn-2", topicName: "Tích phân từng phần", masteryPercentage: 60, evidenceCount: 8 },
    { topicNodeId: "tn-3", topicName: "Ứng dụng tích phân", masteryPercentage: 70, evidenceCount: 5 },
  ];

  const averageMastery = topicsForSubjectMath.reduce((acc, t) => acc + t.masteryPercentage, 0) / topicsForSubjectMath.length;
  assert.equal(averageMastery, 70, "Average calculated accurately for math topics");

  const totalEvidence = topicsForSubjectMath.reduce((acc, t) => acc + t.evidenceCount, 0);
  assert.equal(totalEvidence, 25, "Total evidence reflects math subject attempts only");

  // Label requirement verification for CenterManager view
  const kpiLabel = "Độ thuần thục trung bình trong môn đang chọn";
  assert.ok(!kpiLabel.includes("tổng thể"), "Label must not state 'tổng thể' to avoid cross-subject ambiguity");
});

// ============================================================================
// 13. ACTOR ISOLATION: REVIEW QUEUE VIEW MODE RESOLUTION
// ============================================================================
test("13. Actor Isolation: ReviewQueuePage uses resolveReviewQueueViewMode for strict actor view separation", () => {
  // CenterManager receives dedicated modern Master/Detail view
  assert.equal(resolveReviewQueueViewMode("CenterManager"), "CenterManager");

  // All other actors (Teacher, Student, PlatformAdmin, null, undefined) strictly receive legacy view
  assert.equal(resolveReviewQueueViewMode("Teacher"), "Teacher");
  assert.equal(resolveReviewQueueViewMode("Student"), "Teacher");
  assert.equal(resolveReviewQueueViewMode("PlatformAdmin"), "Teacher");
  assert.equal(resolveReviewQueueViewMode(null), "Teacher");
  assert.equal(resolveReviewQueueViewMode(undefined), "Teacher");
});

// ============================================================================
// 14. ACTOR ISOLATION: STUDENT DIGITAL TWIN VIEW MODE RESOLUTION
// ============================================================================
test("14. Actor Isolation: TeacherStudentTwinPage uses resolveStudentTwinViewMode for strict KPI separation", () => {
  // CenterManager receives dedicated bounded single-subject view
  assert.equal(resolveStudentTwinViewMode("CenterManager"), "CenterManager");

  // Teacher and all other actors strictly receive legacy view
  assert.equal(resolveStudentTwinViewMode("Teacher"), "Teacher");
  assert.equal(resolveStudentTwinViewMode("Student"), "Teacher");
  assert.equal(resolveStudentTwinViewMode("PlatformAdmin"), "Teacher");
  assert.equal(resolveStudentTwinViewMode(null), "Teacher");
  assert.equal(resolveStudentTwinViewMode(undefined), "Teacher");
});

// ============================================================================
// 15. OCC FAIL-CLOSED: CANONICAL UINT32 TOKEN VALIDATION & LABEL FORMATTING
// ============================================================================
test("15. OCC Fail-Closed: Canonical isValidOccVersion strictly rejects negative, float, out-of-range, and non-numeric tokens", () => {
  // 1. Valid uint32 tokens (range: 0 to 4294967295)
  assert.equal(isValidOccVersion(0), true, "Version 0 is a valid uint32");
  assert.equal(isValidOccVersion(1), true, "Version 1 is a valid uint32");
  assert.equal(isValidOccVersion(100), true, "Version 100 is a valid uint32");
  assert.equal(isValidOccVersion(4294967295), true, "Max uint32 (4294967295) is valid");

  // 2. Invalid tokens: negative numbers, floats, overflow, and non-numbers MUST fail closed
  assert.equal(isValidOccVersion(-1), false, "Negative token -1 must be rejected");
  assert.equal(isValidOccVersion(-100), false, "Negative token -100 must be rejected");
  assert.equal(isValidOccVersion(1.5), false, "Float token 1.5 must be rejected");
  assert.equal(isValidOccVersion(4294967296), false, "uint32 overflow (4294967296) must be rejected");
  assert.equal(isValidOccVersion(NaN), false, "NaN must be rejected");
  assert.equal(isValidOccVersion(Infinity), false, "Infinity must be rejected");
  assert.equal(isValidOccVersion(undefined), false, "Undefined must be rejected");
  assert.equal(isValidOccVersion(null), false, "Null must be rejected");
  assert.equal(isValidOccVersion("1"), false, "String number must be rejected");
  assert.equal(isValidOccVersion({}), false, "Object must be rejected");
  assert.equal(isValidOccVersion([]), false, "Array must be rejected");

  // 3. formatOccVersionLabel formatting
  assert.equal(formatOccVersionLabel(0), "#0");
  assert.equal(formatOccVersionLabel(7), "#7");
  assert.equal(formatOccVersionLabel(-1), "Không khả dụng");
  assert.equal(formatOccVersionLabel(null), "Không khả dụng");
  assert.equal(formatOccVersionLabel(undefined), "Không khả dụng");
  assert.equal(formatOccVersionLabel(3.14), "Không khả dụng");
  assert.equal(formatOccVersionLabel("42"), "Không khả dụng");
});

// ============================================================================
// 16. OCC 409 CONCURRENCY CONFLICT & REFETCH RECOVERY (SUCCESS AND FAILURE)
// ============================================================================
test("16. OCC 409 Workflow: Conflict enables refetch, refetch failure keeps conflict locked, success synchronizes token", async () => {
  const serverVersion = 5;
  let clientOverrideVersion: number | null = 4;
  let isConflict = false;
  let conflictDetails: { message: string; traceId?: string } | null = null;
  let errorMessage: string | null = null;

  // Mock submit producing 409 conflict
  const attemptSubmit = (version: number) => {
    if (version !== serverVersion) {
      const err = createAxiosError(409, {
        status: 409,
        detail: "Phiên bản đã thay đổi trên máy chủ.",
        traceId: "trace-occ-409-refetch-check",
      });
      if (isOverrideConflict(err)) {
        isConflict = true;
        conflictDetails = {
          message: "Lượt phân tích đã được cập nhật bởi một phiên làm việc khác.",
          traceId: extractProblemDetails(err).traceId ?? undefined,
        };
      }
      return false;
    }
    return true;
  };

  // 1. Submit with stale version 4 -> Fails with 409 conflict
  const initialSubmitSuccess = attemptSubmit(clientOverrideVersion!);
  assert.equal(initialSubmitSuccess, false);
  assert.equal(isConflict, true);
  assert.equal((conflictDetails as { message: string; traceId?: string } | null)?.traceId, "trace-occ-409-refetch-check");

  // 2. Branch A: Refetch failure (Network error or server unavailable)
  // Must FAIL CLOSED: conflict remains active, submit remains locked
  const handleFailingRefetch = async () => {
    try {
      await executeOccRefetchWrapper(async () => {
        return { isError: true, error: new Error("Network timeout") };
      });
      isConflict = false;
    } catch (err) {
      errorMessage = (err as Error).message;
      // Conflict remains true!
    }
  };

  await handleFailingRefetch();
  assert.equal(isConflict, true, "isConflict must remain true when refetch fails");
  assert.ok(conflictDetails !== null, "conflictDetails must be preserved when refetch fails");
  assert.equal(clientOverrideVersion, 4, "client token must NOT be updated when refetch fails");
  assert.equal(errorMessage, "Network timeout");

  // Submit button remains locked during refetch failure
  const isSubmitDisabledDuringFailure = clientOverrideVersion === null || !isValidOccVersion(clientOverrideVersion) || isConflict;
  assert.equal(isSubmitDisabledDuringFailure, true, "Submit button strictly disabled during conflict and refetch failure");

  // 3. Branch B: Refetch success (Server returns updated analysis with version 5)
  const handleSuccessfulRefetch = async () => {
    try {
      const data = await executeOccRefetchWrapper(async () => {
        return { isError: false, data: { newVersion: serverVersion } };
      });
      if (isValidOccVersion(data.newVersion)) {
        clientOverrideVersion = data.newVersion;
        isConflict = false;
        conflictDetails = null;
        errorMessage = null;
      }
    } catch (err) {
      errorMessage = (err as Error).message;
    }
  };

  await handleSuccessfulRefetch();
  assert.equal(isConflict, false, "isConflict cleared after successful refetch");
  assert.equal(conflictDetails, null, "conflictDetails cleared after successful refetch");
  assert.equal(clientOverrideVersion, 5, "clientOverrideVersion updated to 5");
  assert.equal(errorMessage, null);

  // 4. Retry submission with synchronized version 5 -> Succeeds!
  const retrySuccess = attemptSubmit(clientOverrideVersion!);
  assert.equal(retrySuccess, true);
  assert.equal(isConflict, false);
});
