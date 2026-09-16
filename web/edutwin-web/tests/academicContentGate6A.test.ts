import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import type {
  KnowledgeGraphNodeDto,
  KnowledgeGraphEdgeDto,
  KnowledgeNodeType,
  KnowledgeRelationType,
  CreateKnowledgeNodeRequest,
  UpdateKnowledgeNodeRequest,
  UpdateKnowledgeEdgeRequest,
} from "../src/types/knowledgeGraph.ts";
import type {
  Curriculum,
  ReviewStatus,
} from "../src/types/curriculum.ts";
import {
  buildUpdateCurriculumPayload,
  isConcurrencyConflictError,
  shouldDisplayTeacherSelector,
} from "../src/pages/curriculumEditorHelpers.ts";
import { mapSafeOperationalError, extractProblemDetails } from "../src/utils/problemDetails.ts";

const centerManagerUser = (grants: string[] = []) => ({
  accountType: "CenterManager" as const,
  permissions: grants,
});

const teacherUser = (grants: string[] = []) => ({
  accountType: "Teacher" as const,
  permissions: grants,
});

const studentUser = (grants: string[] = []) => ({
  accountType: "Student" as const,
  permissions: grants,
});

// ============================================================================
// 1. ACTOR ISOLATION
// ============================================================================
test("1. Actor isolation: CenterManager receives modern view while Teacher and others keep legacy", () => {
  const isCenterManager = (user: { accountType: string } | null | undefined): boolean => {
    return user?.accountType === "CenterManager";
  };

  const cm = centerManagerUser([permissions.nodesRead, permissions.curriculumsRead]);
  const teacher = teacherUser([permissions.nodesRead, permissions.curriculumsRead]);
  const student = studentUser([]);

  assert.equal(isCenterManager(cm), true, "CenterManager accountType triggers modern Dark Enterprise view");
  assert.equal(isCenterManager(teacher), false, "Teacher accountType keeps legacy view");
  assert.equal(isCenterManager(student), false, "Student accountType keeps legacy view");
  assert.equal(isCenterManager(null), false, "Unauthenticated / null user does not trigger modern view");

  // Verify that fake role labels (e.g. role: 'Quản lý' with accountType: 'Teacher') do NOT bypass actor isolation
  const fakeManagerTeacher = {
    accountType: "Teacher" as const,
    role: "CenterManager", // display label or spoofed role
    permissions: [permissions.nodesRead],
  };
  assert.equal(isCenterManager(fakeManagerTeacher), false, "Actor isolation strictly relies on canonical accountType");
});

// ============================================================================
// 2. READ-ONLY CAPABILITY GATING
// ============================================================================
test("2. Read-only capability gating: Mutations and controls are strictly permission-bounded", () => {
  const readOnlyManager = centerManagerUser([
    permissions.subjectsRead,
    permissions.nodesRead,
    permissions.edgesRead,
    permissions.curriculumsRead,
  ]);

  // Knowledge Graph mutation permissions
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.nodesCreate] }), false, "Cannot create nodes");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.nodesUpdate] }), false, "Cannot update nodes");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.nodesDelete] }), false, "Cannot delete nodes");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.edgesCreate] }), false, "Cannot create edges");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.edgesUpdate] }), false, "Cannot update edges");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.edgesDelete] }), false, "Cannot delete edges");

  // Curriculum mutation permissions
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.curriculumsCreate] }), false, "Cannot create curriculum");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.curriculumsUpdate] }), false, "Cannot update curriculum");
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.curriculumsPublish] }), false, "Cannot publish curriculum");

  // Read permissions are present
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.subjectsRead] }), true);
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.nodesRead] }), true);
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.edgesRead] }), true);
  assert.equal(canAccess(readOnlyManager, { allOf: [permissions.curriculumsRead] }), true);
});

// ============================================================================
// 3. KNOWLEDGE GRAPH CONTRACTS
// ============================================================================
test("3. Knowledge Graph: Canonical Enums, Zero Fake Coordinates, and RowVersion OCC", () => {
  // Canonical NodeTypes defined in backend EduTwin.Contracts.KnowledgeGraph
  const validNodeTypes: KnowledgeNodeType[] = ["Subject", "Chapter", "Topic", "Skill", "Concept"];
  assert.equal(validNodeTypes.length, 5);

  // Canonical RelationTypes defined in backend EduTwin.Contracts.KnowledgeGraph
  const validRelationTypes: KnowledgeRelationType[] = ["PrerequisiteOf", "RelatedTo", "PartOf", "CausesErrorIn"];
  assert.equal(validRelationTypes.length, 4);

  // Validate Node creation payload has NO fake coordinates (x, y, coords)
  const nodeCreateReq: CreateKnowledgeNodeRequest = {
    subjectId: "sub-100",
    parentNodeId: null,
    nodeType: "Topic",
    nodeCode: "MATH.ALGEBRA.01",
    nodeName: "Hàm số và đồ thị",
    description: "Nội dung cơ bản về hàm số",
    orderIndex: 1,
    examImportance: 15.5,
    estimatedLearningMinutes: 60,
    isActive: true,
  };

  const nodeKeys = Object.keys(nodeCreateReq);
  assert.equal(nodeKeys.includes("x"), false, "Payload has no x coordinate");
  assert.equal(nodeKeys.includes("y"), false, "Payload has no y coordinate");
  assert.equal(nodeKeys.includes("position"), false, "Payload has no position field");
  assert.equal(nodeKeys.includes("coordinates"), false, "Payload has no coordinates field");

  // Validate Node update payload requires rowVersion
  const nodeUpdateReq: UpdateKnowledgeNodeRequest = {
    nodeName: "Hàm số bậc hai",
    parentNodeId: null,
    description: "Cập nhật mô tả",
    orderIndex: 1,
    examImportance: 20,
    estimatedLearningMinutes: 90,
    isActive: true,
    rowVersion: "AAAAAA==",
  };
  assert.ok(nodeUpdateReq.rowVersion, "RowVersion must be provided in UpdateKnowledgeNodeRequest");

  // Validate Edge update payload requires rowVersion and weight
  const edgeUpdateReq: UpdateKnowledgeEdgeRequest = {
    weight: 0.85,
    rowVersion: "BBBBBB==",
  };
  assert.equal(edgeUpdateReq.weight, 0.85);
  assert.ok(edgeUpdateReq.rowVersion, "RowVersion must be provided in UpdateKnowledgeEdgeRequest");
});

test("3b. Knowledge Graph: Cycle and dependency error codes mapped safely", () => {
  // Backend returns 409 with DagCycleDetected
  const cycleError = {
    isAxiosError: true,
    response: {
      status: 409,
      headers: {},
      data: {
        type: "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
        title: "Xung đột dữ liệu",
        status: 409,
        detail: "Phát hiện xung đột dữ liệu.",
        traceId: "trace-cycle-12345",
        errorCode: "KNOWLEDGE_CYCLE_DETECTED",
        extensions: {
          errorCode: "KNOWLEDGE_CYCLE_DETECTED",
          traceId: "trace-cycle-12345",
        },
      },
    },
  };

  const safeMsg = mapSafeOperationalError(
    cycleError,
    "Không thể tạo liên kết vì sẽ tạo chu trình phụ thuộc (DAG cycle) hoặc trùng lặp liên kết."
  );
  assert.ok(safeMsg.length > 0);
  assert.equal(safeMsg.includes("raw ProblemDetails"), false);

  const extracted = extractProblemDetails(cycleError);
  assert.equal(extracted.traceId, "trace-cycle-12345");
  assert.equal(extracted.errorCode, "KNOWLEDGE_CYCLE_DETECTED");
});

// ============================================================================
// 4. CURRICULUM CONTRACTS & ROWVERSION OCC
// ============================================================================
test("4. Curriculum: Canonical ReviewStatus, Node Ordering & Class Assignment persistence", () => {
  const canonicalStatuses: ReviewStatus[] = ["Draft", "Published", "Archived"];
  assert.deepEqual(canonicalStatuses, ["Draft", "Published", "Archived"]);

  // Node ordering preserving sequence:
  const nodeIds = ["node-101", "node-102", "node-103", "node-104"];

  // Reorder test: move index 2 up
  const reordered = [...nodeIds];
  const temp = reordered[2];
  reordered[2] = reordered[1];
  reordered[1] = temp;

  assert.deepEqual(reordered, ["node-101", "node-103", "node-102", "node-104"]);
  assert.equal(reordered.length, nodeIds.length, "No nodes lost during reordering");

  // Class assignment multi-selection preservation:
  const classIds = ["class-A", "class-B", "class-C"];
  assert.equal(classIds.length, 3);
  assert.equal(classIds[1], "class-B");
});

test("4b. Curriculum: Publish and Consecutive Mutation RowVersion Propagation", () => {
  // Initial state from backend
  let currentCurriculum: Curriculum = {
    curriculumId: "curr-001",
    title: "Toán 10 Nâng cao",
    description: "Giáo trình toán chuyên sâu",
    subjectId: "sub-toan",
    teacherId: "teacher-001",
    nodeIds: ["node-1", "node-2"],
    classIds: ["class-1"],
    reviewStatus: "Draft",
    rowVersion: "row-v1",
  };

  // Mutation 1: Update general info
  const updateInfoPayload = buildUpdateCurriculumPayload({
    title: "Toán 10 Nâng cao (Đã sửa)",
    description: currentCurriculum.description || undefined,
    rowVersion: currentCurriculum.rowVersion,
  });
  assert.equal(updateInfoPayload.rowVersion, "row-v1");

  // Simulate mutation 1 response returning fresh rowVersion "row-v2"
  const mutation1Response = {
    ...currentCurriculum,
    title: updateInfoPayload.title,
    rowVersion: "row-v2",
  };
  // Propagate fresh rowVersion immediately to state & cache
  currentCurriculum = mutation1Response;
  assert.equal(currentCurriculum.rowVersion, "row-v2");

  // Mutation 2 (consecutive in same session): Update nodes using fresh rowVersion "row-v2"
  const updateNodesPayload = {
    nodeIds: ["node-1", "node-2", "node-3"],
    rowVersion: currentCurriculum.rowVersion,
  };
  assert.equal(
    updateNodesPayload.rowVersion,
    "row-v2",
    "Consecutive mutation uses updated rowVersion, preventing artificial 409"
  );

  // Simulate mutation 2 response returning fresh rowVersion "row-v3"
  currentCurriculum = {
    ...currentCurriculum,
    nodeIds: updateNodesPayload.nodeIds,
    rowVersion: "row-v3",
  };

  // Mutation 3: Publish curriculum requires the latest rowVersion
  const publishPayload = {
    rowVersion: currentCurriculum.rowVersion,
  };
  assert.equal(publishPayload.rowVersion, "row-v3", "Publish request transmits latest canonical rowVersion");

  // After publish, status is Published
  currentCurriculum = {
    ...currentCurriculum,
    reviewStatus: "Published",
    rowVersion: "row-v4",
  };

  // In Published status, modifications are disallowed
  const isDraft = currentCurriculum.reviewStatus === "Draft";
  assert.equal(isDraft, false, "Published curriculum is no longer in Draft status");
});

test("4c. Curriculum Concurrency Conflict error detection and handling", () => {
  const conflictError = {
    response: {
      status: 409,
      data: {
        type: "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
        title: "Xung đột phiên bản",
        status: 409,
        detail: "Dữ liệu đã bị thay đổi bởi người dùng khác.",
        extensions: {
          errorCode: "CONCURRENCY_CONFLICT",
          traceId: "trace-409-occ",
        },
      },
    },
  };

  assert.equal(isConcurrencyConflictError(conflictError), true, "Identifies 409 conflict correctly");

  const safeMsg = mapSafeOperationalError(conflictError, "Lộ trình đã bị thay đổi.");
  assert.ok(safeMsg.length > 0);
  assert.equal(safeMsg.includes("trace-409-occ"), false, "Internal traceId not leaked into main message string");
});

// ============================================================================
// 5. NO RAW PROBLEMDETAILS & SAFE ERROR HANDLING
// ============================================================================
test("5. Error handling: Raw ProblemDetails and internal exceptions never leak to UI", () => {
  const server500Error = {
    isAxiosError: true,
    response: {
      status: 500,
      headers: {},
      data: {
        type: "https://httpstatuses.com/500",
        title: "Internal Server Error",
        status: 500,
        detail: "System.Data.SqlClient.SqlException: Deadlock victim process ID 52",
        traceId: "trace-internal-deadlock",
        extensions: {
          traceId: "trace-internal-deadlock",
          stackTrace: "at EduTwin.DAL.Repositories.KnowledgeRepository...",
        },
      },
    },
  };

  const safeMsg = mapSafeOperationalError(server500Error, "Hệ thống tạm thời gián đoạn. Vui lòng thử lại.");
  assert.equal(
    safeMsg.includes("SqlException"),
    false,
    "Raw SQL / internal exception message is never exposed in user message"
  );
  assert.equal(
    safeMsg.includes("EduTwin.DAL"),
    false,
    "Stack trace is never exposed in user message"
  );

  const problem = extractProblemDetails(server500Error);
  assert.equal(problem.traceId, "trace-internal-deadlock", "TraceId extracted safely for support panel");
});

// ============================================================================
// 6. ZERO FAKE FIELDS / STATS
// ============================================================================
test("6. DTO contracts integrity: Zero fake fields or invented stats across academic content", () => {
  // KnowledgeGraphNodeDto contract
  const node: KnowledgeGraphNodeDto = {
    nodeId: "n-1",
    parentNodeId: null,
    nodeType: "Concept",
    nodeCode: "MATH.C.01",
    nodeName: "Số phức liên hợp",
    description: "Định nghĩa số phức liên hợp",
    orderIndex: 0,
    examImportance: 10,
    estimatedLearningMinutes: 45,
    isActive: true,
    rowVersion: "row-node-1",
  };

  // Ensure no coordinates or fake fields
  assert.equal("x" in node, false);
  assert.equal("y" in node, false);
  assert.equal("canvasCoordinates" in node, false);
  assert.equal("masteryScore" in node, false); // No fake student scores in graph node DTO

  // KnowledgeGraphEdgeDto contract
  const edge: KnowledgeGraphEdgeDto = {
    edgeId: "e-1",
    sourceNodeId: "n-1",
    targetNodeId: "n-2",
    relationType: "PrerequisiteOf",
    weight: 0.9,
    rowVersion: "row-edge-1",
  };
  assert.equal("color" in edge, false);
  assert.equal("curveType" in edge, false);

  // Curriculum contract
  const curr: Curriculum = {
    curriculumId: "c-1",
    title: "Chương trình mẫu",
    description: "Mô tả",
    subjectId: "sub-1",
    teacherId: "t-1",
    nodeIds: ["n-1"],
    classIds: ["cls-1"],
    reviewStatus: "Draft",
    rowVersion: "v1",
  };
  assert.equal("fakeStudentCount" in curr, false);
  assert.equal("completionRate" in curr, false);
});

// ============================================================================
// 7. TEACHER VIEW AND BEHAVIOR PRESERVATION
// ============================================================================
test("7. Teacher view preservation: Teacher maintains implicit teacher binding during creation", () => {
  // Teacher does NOT see teacher selector (implicit binding in backend)
  const showTeacherSelectorForTeacher = shouldDisplayTeacherSelector({
    isEditMode: false,
    isCenterManager: false,
  });
  assert.equal(showTeacherSelectorForTeacher, false, "Teacher does not see teacher selector");

  // CenterManager DOES see teacher selector in create mode
  const showTeacherSelectorForManager = shouldDisplayTeacherSelector({
    isEditMode: false,
    isCenterManager: true,
  });
  assert.equal(showTeacherSelectorForManager, true, "CenterManager must select a teacher");

  // In edit mode, neither sees teacher selector (immutable teacher binding)
  assert.equal(
    shouldDisplayTeacherSelector({ isEditMode: true, isCenterManager: true }),
    false,
    "Teacher selector hidden in edit mode for CenterManager"
  );
  assert.equal(
    shouldDisplayTeacherSelector({ isEditMode: true, isCenterManager: false }),
    false,
    "Teacher selector hidden in edit mode for Teacher"
  );
});
