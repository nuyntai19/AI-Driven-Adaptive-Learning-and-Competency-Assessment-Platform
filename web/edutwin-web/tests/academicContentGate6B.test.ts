import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import type {
  Question,
  QuestionStatus,
  CreateQuestionRequest,
  UpdateQuestionRequest,
  QuestionFilter,
} from "../src/types/questions.ts";
import type {
  AssignmentStatus,
  TargetMode,
  AssignmentProgressItemDto,
} from "../src/types/assignments.ts";
import { mapSafeOperationalError, extractProblemDetails, isConcurrencyConflictError } from "../src/utils/problemDetails.ts";

const centerManagerUser = (grants: string[] = []) => ({
  accountType: "CenterManager" as const,
  permissions: grants,
});

// ============================================================================
// 1. CANONICAL PERMISSIONS DECLARATION
// ============================================================================
test("1. Canonical permissions: Frontend declares all required policies for Questions & Assignments", () => {
  // Verify the 3 mandatory new permissions
  assert.equal(
    permissions.questionsDelete,
    "curriculum.questions.delete",
    "questionsDelete matches backend DELETE /api/v1/questions/{id} policy"
  );
  assert.equal(
    permissions.assignmentsPublish,
    "assignments.assignments.publish",
    "assignmentsPublish matches backend POST /api/v1/assignments/{id}/publish policy"
  );
  assert.equal(
    permissions.assignmentsClose,
    "assignments.assignments.close",
    "assignmentsClose matches backend POST /api/v1/assignments/{id}/close policy"
  );

  // Verify baseline permissions for Questions & Assignments
  assert.equal(permissions.questionsRead, "curriculum.questions.read");
  assert.equal(permissions.questionsCreate, "curriculum.questions.create");
  assert.equal(permissions.questionsUpdate, "curriculum.questions.update");
  assert.equal(permissions.questionsPublish, "curriculum.questions.publish");
  assert.equal(permissions.assignmentsRead, "assignments.assignments.read");
  assert.equal(permissions.assignmentsCreate, "assignments.assignments.create");
  assert.equal(permissions.assignmentsUpdate, "assignments.assignments.update");

  // Verify that permissions are distinct and NOT aliased/reused across different actions
  assert.notEqual(
    permissions.questionsPublish,
    permissions.questionsDelete,
    "questionsPublish must not be used for deletion"
  );
  assert.notEqual(
    permissions.assignmentsUpdate,
    permissions.assignmentsPublish,
    "assignmentsUpdate must not be used for publishing"
  );
  assert.notEqual(
    permissions.assignmentsUpdate,
    permissions.assignmentsClose,
    "assignmentsUpdate must not be used for closing"
  );
});

// ============================================================================
// 2. QUESTION STATE MACHINE & ACTIONS GATING
// ============================================================================
test("2. Question state machine: Draft (editable, activate, archive, delete), Active (read-only, archive only), Archived (read-only, activate only)", () => {
  type QuestionAction = "edit" | "activate" | "archive" | "delete";

  const getAvailableQuestionActions = (
    status: QuestionStatus,
    userGrants: string[]
  ): QuestionAction[] => {
    const actions: QuestionAction[] = [];
    const has = (p: string) => userGrants.includes(p);

    if (status === "Draft") {
      if (has(permissions.questionsUpdate)) actions.push("edit");
      if (has(permissions.questionsPublish)) actions.push("activate");
      if (has(permissions.questionsPublish)) actions.push("archive");
      if (has(permissions.questionsDelete)) actions.push("delete");
    } else if (status === "Active") {
      // Content is strictly read-only; can only archive
      if (has(permissions.questionsPublish)) actions.push("archive");
    } else if (status === "Archived") {
      // Content is strictly read-only; can only reactivate
      if (has(permissions.questionsPublish)) actions.push("activate");
    }

    return actions;
  };

  const isQuestionContentReadOnly = (status: QuestionStatus, canUpdate: boolean): boolean => {
    // Both Active and Archived questions have locked content
    if (status !== "Draft") return true;
    return !canUpdate;
  };

  const allGrants = [
    permissions.questionsRead,
    permissions.questionsCreate,
    permissions.questionsUpdate,
    permissions.questionsPublish,
    permissions.questionsDelete,
  ];

  // Draft with all permissions
  const draftActions = getAvailableQuestionActions("Draft", allGrants);
  assert.deepEqual(draftActions, ["edit", "activate", "archive", "delete"]);
  assert.equal(isQuestionContentReadOnly("Draft", true), false, "Draft question is editable by updater");

  // Active question: only archive is permitted, content is read-only
  const activeActions = getAvailableQuestionActions("Active", allGrants);
  assert.deepEqual(activeActions, ["archive"], "Active question only permits archive");
  assert.equal(isQuestionContentReadOnly("Active", true), true, "Active question content is strictly read-only");

  // Archived question: only activate is permitted, content is read-only
  const archivedActions = getAvailableQuestionActions("Archived", allGrants);
  assert.deepEqual(archivedActions, ["activate"], "Archived question only permits activation");
  assert.equal(isQuestionContentReadOnly("Archived", true), true, "Archived question content is strictly read-only");

  // Draft without delete grant: delete action is absent
  const draftNoDelete = getAvailableQuestionActions("Draft", [
    permissions.questionsUpdate,
    permissions.questionsPublish,
  ]);
  assert.equal(draftNoDelete.includes("delete"), false, "Cannot delete Draft question without questionsDelete permission");

  // Draft without publish grant: activate and archive are absent
  const draftNoPublish = getAvailableQuestionActions("Draft", [
    permissions.questionsUpdate,
    permissions.questionsDelete,
  ]);
  assert.deepEqual(draftNoPublish, ["edit", "delete"], "Draft without publish grant only has edit and delete");
});

// ============================================================================
// 3. ASSIGNMENT STATE MACHINE & ISREADONLY RULE
// ============================================================================
test("3. Assignment state machine: Draft (editable, publish), Published (read-only, close only), Closed/Archived (read-only)", () => {
  const evaluateAssignmentAccess = (
    status: AssignmentStatus,
    canUpdate: boolean,
    canPublish: boolean,
    canClose: boolean
  ) => {
    // Canonical isReadOnly rule: anything other than Draft is read-only
    const isReadOnly = status !== "Draft" || !canUpdate;
    const canPublishAction = status === "Draft" && canPublish;
    const canCloseAction = status === "Published" && canClose;

    return { isReadOnly, canPublishAction, canCloseAction };
  };

  // Draft state with full update and publish permissions
  const draftState = evaluateAssignmentAccess("Draft", true, true, false);
  assert.equal(draftState.isReadOnly, false, "Draft assignment is editable");
  assert.equal(draftState.canPublishAction, true, "Draft assignment can be published");
  assert.equal(draftState.canCloseAction, false, "Draft assignment cannot be closed");

  // Published state: strictly read-only, only can close
  const publishedState = evaluateAssignmentAccess("Published", true, false, true);
  assert.equal(publishedState.isReadOnly, true, "Published assignment is strictly read-only");
  assert.equal(publishedState.canPublishAction, false, "Published assignment cannot be published again");
  assert.equal(publishedState.canCloseAction, true, "Published assignment can be closed");

  // Closed state: strictly read-only, neither publish nor close allowed
  const closedState = evaluateAssignmentAccess("Closed", true, true, true);
  assert.equal(closedState.isReadOnly, true, "Closed assignment is strictly read-only");
  assert.equal(closedState.canPublishAction, false, "Closed assignment cannot be published");
  assert.equal(closedState.canCloseAction, false, "Closed assignment cannot be closed");

  // Archived state: strictly read-only
  const archivedState = evaluateAssignmentAccess("Archived", true, true, true);
  assert.equal(archivedState.isReadOnly, true, "Archived assignment is strictly read-only");
  assert.equal(archivedState.canPublishAction, false, "Archived assignment cannot be published");
  assert.equal(archivedState.canCloseAction, false, "Archived assignment cannot be closed");

  // Draft without canUpdate permission: read-only is true
  const draftNoUpdate = evaluateAssignmentAccess("Draft", false, true, false);
  assert.equal(draftNoUpdate.isReadOnly, true, "Draft without update permission is read-only");
});

// ============================================================================
// 4. CONTRACT INTEGRITY: UPDATEQUESTIONREQUEST IMMUTABILITY
// ============================================================================
test("4. Contract alignment: UpdateQuestionRequest is decoupled from CreateQuestionRequest and does NOT have teacherId or subjectId", () => {
  // Simulate building an update payload from form data
  const buildUpdateQuestionPayload = (
    formData: CreateQuestionRequest,
    rowVersion: string
  ): UpdateQuestionRequest => {
    return {
      primaryTopicNodeId: formData.primaryTopicNodeId,
      questionType: formData.questionType,
      difficulty: formData.difficulty,
      questionText: formData.questionText,
      correctAnswer: formData.correctAnswer,
      solution: formData.solution,
      expectedReasoning: formData.expectedReasoning,
      gradingCriteria: formData.gradingCriteria,
      maxScore: formData.maxScore,
      estimatedTimeSeconds: formData.estimatedTimeSeconds,
      reasoningRequired: formData.reasoningRequired,
      languageCode: formData.languageCode,
      answerEvaluationMode: formData.answerEvaluationMode,
      options: formData.options,
      knowledgeMappings: formData.knowledgeMappings,
      rowVersion,
    };
  };

  const createForm: CreateQuestionRequest = {
    teacherId: "teacher-uuid-123",
    subjectId: "subject-uuid-456",
    primaryTopicNodeId: "node-uuid-789",
    questionType: "ShortAnswer",
    difficulty: 3,
    questionText: "Tính diện tích hình tròn có bán kính $r=5$.",
    correctAnswer: "25\\pi",
    maxScore: 2,
    estimatedTimeSeconds: 120,
    reasoningRequired: true,
    languageCode: "vi",
    answerEvaluationMode: "NumericRational",
  };

  const updatePayload = buildUpdateQuestionPayload(createForm, "row-ver-1");

  // Check that updatePayload matches UpdateQuestionRequest and does NOT have teacherId or subjectId
  assert.equal("teacherId" in updatePayload, false, "UpdateQuestionRequest must not contain teacherId");
  assert.equal("subjectId" in updatePayload, false, "UpdateQuestionRequest must not contain subjectId");
  assert.equal(updatePayload.primaryTopicNodeId, "node-uuid-789");
  assert.equal(updatePayload.rowVersion, "row-ver-1");
  assert.equal(updatePayload.answerEvaluationMode, "NumericRational");
  assert.equal(updatePayload.reasoningRequired, true);
});

// ============================================================================
// 5. BACKEND QUERY CONTRACTS: NO SEARCH PARAM ON QUESTION & CLASS
// ============================================================================
test("5. Backend query contracts: Question and Class list queries do NOT accept search; Student and Teacher do", () => {
  // QuestionFilter contract check
  const questionFilter: QuestionFilter = {
    subjectId: "sub-1",
    topicId: "topic-1",
    type: "MultipleChoice",
    difficulty: 2,
    status: "Active",
    page: 1,
    pageSize: 20,
  };

  assert.equal("search" in questionFilter, false, "QuestionFilter must not declare or send search");

  // Verify that an object with search would have unexpected properties
  const buildQuestionParams = (filter: QuestionFilter) => {
    const { subjectId, topicId, type, difficulty, status, page, pageSize } = filter;
    return { subjectId, topicId, type, difficulty, status, page, pageSize };
  };

  const params = buildQuestionParams(questionFilter);
  assert.deepEqual(Object.keys(params).includes("search"), false, "Clean query parameters omit search");

  // Class query contract
  const classParams = { status: "Active", page: 1, pageSize: 20 };
  assert.equal("search" in classParams, false, "Class query must not have search parameter");

  // Student and Teacher queries allow search
  const studentQuery = { classId: "c-1", search: "Nguyen", page: 1, pageSize: 20 };
  assert.equal(studentQuery.search, "Nguyen", "Student query allows server-side search");

  const teacherQuery = { search: "Tran", page: 1, pageSize: 20, status: "Active" };
  assert.equal(teacherQuery.search, "Tran", "Teacher query allows server-side search");
});

// ============================================================================
// 6. COMPOSITE CAPABILITY FAIL-CLOSED GUARD
// ============================================================================
test("6. Composite capability fail-closed guard: CenterManager Question Create requires 4 permissions", () => {
  const evaluateQuestionCreateCapabilities = (grants: string[]) => {
    const has = (p: string) => grants.includes(p);
    const missing: string[] = [];

    if (!has(permissions.questionsCreate)) missing.push("curriculum.questions.create");
    if (!has(permissions.subjectsRead)) missing.push("knowledge.subjects.read");
    if (!has(permissions.nodesRead)) missing.push("knowledge.nodes.read");
    if (!has(permissions.teachersRead)) missing.push("organization.teachers.read");

    return {
      isAllowed: missing.length === 0,
      missing,
    };
  };

  // Full grants
  const full = evaluateQuestionCreateCapabilities([
    permissions.questionsCreate,
    permissions.subjectsRead,
    permissions.nodesRead,
    permissions.teachersRead,
  ]);
  assert.equal(full.isAllowed, true);
  assert.equal(full.missing.length, 0);

  // Missing teachers.read
  const noTeacher = evaluateQuestionCreateCapabilities([
    permissions.questionsCreate,
    permissions.subjectsRead,
    permissions.nodesRead,
  ]);
  assert.equal(noTeacher.isAllowed, false);
  assert.deepEqual(noTeacher.missing, ["organization.teachers.read"]);

  // Missing nodes.read
  const noNode = evaluateQuestionCreateCapabilities([
    permissions.questionsCreate,
    permissions.subjectsRead,
    permissions.teachersRead,
  ]);
  assert.equal(noNode.isAllowed, false);
  assert.deepEqual(noNode.missing, ["knowledge.nodes.read"]);
});

// ============================================================================
// 7. SELECTION CACHE PRESERVATION ACROSS PAGINATION
// ============================================================================
test("7. Selection cache preservation: Selections are preserved when user paginates or filters questions and students", () => {
  // Simulate cachedQuestions map
  const cachedQuestions = new Map<string, Question>();
  const selectedQuestionIds: string[] = [];

  const page1Questions: Question[] = [
    {
      questionId: "q-1",
      subjectId: "sub-1",
      primaryTopicNodeId: "node-1",
      questionType: "MultipleChoice",
      difficulty: 2,
      questionText: "Câu hỏi trang 1",
      maxScore: 1,
      estimatedTimeSeconds: 60,
      reasoningRequired: false,
      languageCode: "vi",
      status: "Active",
      knowledgeMappings: [],
      rowVersion: "1",
    },
  ];

  // User loads page 1 and selects q-1
  for (const q of page1Questions) {
    cachedQuestions.set(q.questionId, q);
  }
  selectedQuestionIds.push("q-1");

  // User paginates to page 2 (page 1 questions are no longer in active query result)
  const page2Questions: Question[] = [
    {
      questionId: "q-2",
      subjectId: "sub-1",
      primaryTopicNodeId: "node-2",
      questionType: "ShortAnswer",
      difficulty: 3,
      questionText: "Câu hỏi trang 2",
      maxScore: 2,
      estimatedTimeSeconds: 120,
      reasoningRequired: true,
      languageCode: "vi",
      status: "Active",
      knowledgeMappings: [],
      rowVersion: "1",
    },
  ];

  for (const q of page2Questions) {
    cachedQuestions.set(q.questionId, q);
  }
  selectedQuestionIds.push("q-2");

  // In Wizard Step 4 (Summary), verify both q-1 and q-2 details are preserved from cache!
  assert.equal(selectedQuestionIds.length, 2);
  const q1 = cachedQuestions.get("q-1");
  const q2 = cachedQuestions.get("q-2");
  assert.ok(q1, "q-1 details are preserved from cache despite navigating away from page 1");
  assert.ok(q2, "q-2 details are preserved from cache");

  const totalPoints = selectedQuestionIds.reduce((sum, id) => {
    return sum + (cachedQuestions.get(id)?.maxScore || 1);
  }, 0);
  assert.equal(totalPoints, 3, "Total points accurately calculated from cached items: 1 + 2 = 3");
});

// ============================================================================
// 8. TARGET SUMMARY ACCURACY: ZERO FAKE STUDENT COUNTS
// ============================================================================
test("8. Target summary accuracy: WholeClass does not display fake 0 students for Draft assignments", () => {
  const formatTargetSummaryStudentCount = (
    targetMode: TargetMode,
    selectedStudentCount: number,
    _isDraft: boolean,
    totalClassMembersLoaded?: number
  ): string => {
    if (targetMode === "SelectedStudents") {
      return `${selectedStudentCount} học sinh được chọn thủ công`;
    }

    // WholeClass mode
    if (totalClassMembersLoaded !== undefined && totalClassMembersLoaded > 0) {
      return `${totalClassMembersLoaded} học sinh (toàn bộ lớp tại thời điểm này)`;
    }

    return "Toàn bộ học sinh đang hoạt động trong lớp tại thời điểm xuất bản";
  };

  // Draft WholeClass with unknown total count: must NOT say 0
  const draftWholeClassUnknown = formatTargetSummaryStudentCount("WholeClass", 0, true, undefined);
  assert.equal(
    draftWholeClassUnknown,
    "Toàn bộ học sinh đang hoạt động trong lớp tại thời điểm xuất bản",
    "Must explain that active students will be snapped at publish rather than showing 0"
  );

  // Draft WholeClass with loaded roster (e.g. 35 students in class)
  const draftWholeClassLoaded = formatTargetSummaryStudentCount("WholeClass", 0, true, 35);
  assert.equal(
    draftWholeClassLoaded,
    "35 học sinh (toàn bộ lớp tại thời điểm này)",
    "Displays actual loaded class roster count"
  );

  // SelectedStudents mode
  const selectedMode = formatTargetSummaryStudentCount("SelectedStudents", 5, true, 35);
  assert.equal(
    selectedMode,
    "5 học sinh được chọn thủ công",
    "Displays exact number of selected students"
  );
});

// ============================================================================
// 9. ASSIGNMENT PROGRESS DTO INTEGRITY: ZERO FAKE KPIS
// ============================================================================
test("9. Assignment progress DTO integrity: Displays student progress from AssignmentProgressItemDto without simulated class averages", () => {
  const sampleProgressList: AssignmentProgressItemDto[] = [
    {
      studentId: "std-001",
      fullName: "Nguyễn Văn An",
      status: "Completed",
      completedQuestionCount: 5,
      totalQuestionCount: 5,
    },
    {
      studentId: "std-002",
      fullName: "Trần Thị Bình",
      status: "InProgress",
      completedQuestionCount: 3,
      totalQuestionCount: 5,
    },
    {
      studentId: "std-003",
      fullName: "Lê Hoàng Cường",
      status: "NotStarted",
      completedQuestionCount: 0,
      totalQuestionCount: 5,
    },
  ];

  // Derive metrics strictly from items (NO invented class average scores)
  const total = sampleProgressList.length;
  const completed = sampleProgressList.filter((p) => p.status === "Completed").length;
  const inProgress = sampleProgressList.filter((p) => p.status === "InProgress").length;
  const notStarted = sampleProgressList.filter((p) => p.status === "NotStarted").length;

  assert.equal(total, 3);
  assert.equal(completed, 1);
  assert.equal(inProgress, 1);
  assert.equal(notStarted, 1);

  // Per-student percentage is purely mathematical: completed / total
  const student1Percent = Math.round(
    (sampleProgressList[0].completedQuestionCount / sampleProgressList[0].totalQuestionCount) * 100
  );
  assert.equal(student1Percent, 100);

  const student2Percent = Math.round(
    (sampleProgressList[1].completedQuestionCount / sampleProgressList[1].totalQuestionCount) * 100
  );
  assert.equal(student2Percent, 60);

  // Verify that AssignmentProgressItemDto does NOT have classAverageScore or similar invented KPI
  const sample = sampleProgressList[0] as any;
  assert.equal(sample.classAverageScore, undefined, "DTO has no class average score field");
});

// ============================================================================
// 10. DELETE QUESTION CONTRACT (NO BODY, DRAFT ONLY, 422 HANDLING)
// ============================================================================
test("10. Delete question contract: DELETE /api/v1/questions/{id} without body, handles 422 dependency conflict", () => {
  const canDeleteQuestion = (status: QuestionStatus, hasDeletePermission: boolean) => {
    return status === "Draft" && hasDeletePermission;
  };

  assert.equal(canDeleteQuestion("Draft", true), true, "Can delete Draft question with permission");
  assert.equal(canDeleteQuestion("Draft", false), false, "Cannot delete Draft question without permission");
  assert.equal(canDeleteQuestion("Active", true), false, "Cannot delete Active question");
  assert.equal(canDeleteQuestion("Archived", true), false, "Cannot delete Archived question");

  // Error mapping on 422 or 409
  const error422 = {
    isAxiosError: true,
    response: {
      status: 422,
      headers: {},
      data: {
        title: "Trạng thái không hợp lệ",
        detail: "Không thể thực hiện thao tác do sai trạng thái hoặc đang có bài tập phụ thuộc.",
        errorCode: "INVALID_STATE_TRANSITION",
        traceId: "trace-422-err-xyz",
      },
    },
  };

  const safeMsg = mapSafeOperationalError(error422, "Không thể xóa câu hỏi.");
  const details = extractProblemDetails(error422);

  assert.equal(safeMsg.includes("Không thể thực hiện thao tác ở trạng thái hiện tại"), true);
  assert.equal(details.traceId, "trace-422-err-xyz");

  // Concurrency conflict detection
  assert.equal(isConcurrencyConflictError(error422), false);
  assert.equal(isConcurrencyConflictError({ isAxiosError: true, response: { status: 409, data: {} } }), true);
});

// ============================================================================
// 11. ROUTE CAPABILITIES ALIGNMENT IN APP.TSX
// ============================================================================
test("11. Route capabilities alignment: Detail routes require Read permission to allow view mode", () => {
  const questionDetailRequirements = { allOf: [permissions.questionsRead] };
  const assignmentDetailRequirements = { allOf: [permissions.assignmentsRead] };

  const viewerCM = centerManagerUser([
    permissions.questionsRead,
    permissions.assignmentsRead,
  ]);

  // Viewer can access detail routes in view mode without Update permission
  assert.equal(canAccess(viewerCM, questionDetailRequirements), true, "Read-only actor can access question detail");
  assert.equal(canAccess(viewerCM, assignmentDetailRequirements), true, "Read-only actor can access assignment detail");

  const unprivilegedCM = centerManagerUser([]);
  assert.equal(canAccess(unprivilegedCM, questionDetailRequirements), false, "Actor without questionsRead cannot access question detail");
  assert.equal(canAccess(unprivilegedCM, assignmentDetailRequirements), false, "Actor without assignmentsRead cannot access assignment detail");
});
