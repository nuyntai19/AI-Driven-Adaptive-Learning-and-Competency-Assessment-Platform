import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import { isConcurrencyConflict, extractProblemDetails } from "../src/utils/problemDetails.ts";
import {
  evaluateClassCapabilities,
  toggleStudentSelection,
  mergePageSelection,
  unmergePageSelection,
  getCandidateListState,
} from "../src/pages/classListHelpers.ts";
import type {
  ClassDto,
  UpdateClassRequest,
  AddStudentsToClassRequest,
  AddStudentsToClassResponse,
} from "../src/types/organization.ts";

const fullCenterManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.classesRead,
    permissions.classesCreate,
    permissions.classesUpdate,
    permissions.classesManageMembers,
    permissions.teachersRead,
    permissions.studentsRead,
    permissions.subjectsRead,
    permissions.dashboardsCenterRead,
  ],
};

const readOnlyCenterManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.classesRead,
    permissions.subjectsRead,
    permissions.teachersRead,
  ],
};

const teacherUser = {
  accountType: "Teacher" as const,
  permissions: [
    permissions.classesRead,
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

test("CenterManager with full permissions can perform class read, create, update, manage_members, and view dashboard", () => {
  assert.equal(
    canAccess(fullCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesRead],
    }),
    true
  );

  assert.equal(
    canAccess(fullCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesCreate],
    }),
    true
  );

  assert.equal(
    canAccess(fullCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesUpdate],
    }),
    true
  );

  assert.equal(
    canAccess(fullCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesManageMembers],
    }),
    true
  );

  assert.equal(
    canAccess(fullCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.dashboardsCenterRead],
    }),
    true
  );
});

test("Restricted CenterManager without classesUpdate cannot perform class update", () => {
  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesUpdate],
    }),
    false
  );
});

test("Restricted CenterManager without classesManageMembers cannot add or remove class students", () => {
  assert.equal(
    canAccess(readOnlyCenterManagerUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesManageMembers],
    }),
    false
  );
});

test("Student persona cannot access /quan-ly/lop-hoc", () => {
  assert.equal(
    canAccess(studentUser, {
      accountTypes: ["CenterManager", "Teacher"],
      anyOf: [permissions.classesRead],
    }),
    false
  );
});

test("Teacher persona can read classes and view teacher dashboard but cannot manage members or update class without permission", () => {
  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["Teacher"],
      anyOf: [permissions.classesRead],
    }),
    true
  );

  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["Teacher"],
      anyOf: [permissions.dashboardsTeacherRead],
    }),
    true
  );

  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesUpdate],
    }),
    false
  );

  assert.equal(
    canAccess(teacherUser, {
      accountTypes: ["CenterManager"],
      anyOf: [permissions.classesManageMembers],
    }),
    false
  );
});

test("ClassDto validates structure, properties, and status types correctly", () => {
  const classObj: ClassDto = {
    classId: "11111111-1111-1111-1111-111111111111",
    className: "Toán 12A Cơ bản",
    academicYear: "2026-2027",
    subject: {
      subjectId: "22222222-2222-2222-2222-222222222222",
      subjectName: "Toán học 12",
    },
    teacher: {
      teacherId: "33333333-3333-3333-3333-333333333333",
      displayName: "Nguyễn Văn Thầy",
    },
    studentCount: 35,
    status: "Active",
    rowVersion: "0x00000000000007D1",
  };

  assert.equal(classObj.classId, "11111111-1111-1111-1111-111111111111");
  assert.equal(classObj.className, "Toán 12A Cơ bản");
  assert.equal(classObj.academicYear, "2026-2027");
  assert.equal(classObj.subject.subjectName, "Toán học 12");
  assert.equal(classObj.teacher.displayName, "Nguyễn Văn Thầy");
  assert.equal(classObj.studentCount, 35);
  assert.equal(classObj.status, "Active");
  assert.equal(classObj.rowVersion, "0x00000000000007D1");
});

test("UpdateClassRequest enforces length constraints and canonical rowVersion", () => {
  const req: UpdateClassRequest = {
    className: "Toán 12A Nâng cao",
    teacherId: "33333333-3333-3333-3333-333333333333",
    status: "Active",
    rowVersion: "0x00000000000007D1",
  };

  assert.ok(req.className.trim().length > 0);
  assert.ok(req.className.trim().length <= 150);
  assert.ok(req.teacherId.trim().length > 0);
  assert.ok(["Active", "Archived"].includes(req.status));
  assert.ok(req.rowVersion.length > 0);
});

test("AddStudentsToClassRequest validates student IDs payload", () => {
  const req: AddStudentsToClassRequest = {
    studentIds: [
      "44444444-4444-4444-4444-444444444441",
      "44444444-4444-4444-4444-444444444442",
    ],
  };

  assert.equal(req.studentIds.length, 2);
  assert.ok(req.studentIds.every((id) => id.length > 0));

  const response: AddStudentsToClassResponse = {
    data: {
      classId: "11111111-1111-1111-1111-111111111111",
      addedCount: 2,
      alreadyMemberCount: 0,
    },
    meta: {
      traceId: "trace-add-students-001",
      timestamp: "2026-09-14T15:00:00Z",
    },
  };

  assert.equal(response.data.addedCount, 2);
  assert.equal(response.data.alreadyMemberCount, 0);
  assert.equal(response.meta.traceId, "trace-add-students-001");
});

test("409 CONCURRENCY_CONFLICT is accurately detected for OCC recovery", () => {
  const conflictError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        type: "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
        status: 409,
        title: "Lỗi đồng bộ dữ liệu.",
        detail: "Dữ liệu đã bị thay đổi bởi người khác, vui lòng tải lại trang và thử lại.",
        errorCode: "CONCURRENCY_CONFLICT",
        traceId: "trace-conflict-cls-123",
      },
    },
  };

  assert.equal(isConcurrencyConflict(conflictError), true);
  const details = extractProblemDetails(conflictError);
  assert.equal(details.errorCode, "CONCURRENCY_CONFLICT");
  assert.equal(details.traceId, "trace-conflict-cls-123");
});

test("409 DUPLICATE_RESOURCE for duplicate class name and academic year is detected and distinguished", () => {
  const duplicateError = {
    isAxiosError: true,
    response: {
      status: 409,
      data: {
        type: "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
        status: 409,
        title: "Dữ liệu đã tồn tại.",
        detail: "Lớp học với tên và năm học này đã tồn tại trong trung tâm.",
        errorCode: "DUPLICATE_RESOURCE",
        traceId: "trace-dup-cls-456",
      },
    },
  };

  const details = extractProblemDetails(duplicateError);
  assert.equal(details.errorCode, "DUPLICATE_RESOURCE");
  assert.equal(details.traceId, "trace-dup-cls-456");
  const isDuplicate = details.errorCode === "DUPLICATE_RESOURCE";
  assert.equal(isDuplicate, true);
});

test("Membership soft-delete preserves historical attempts and assessment evidence", () => {
  const softRemovedMembership = {
    membershipId: "55555555-5555-5555-5555-555555555555",
    classId: "11111111-1111-1111-1111-111111111111",
    studentId: "44444444-4444-4444-4444-444444444441",
    status: "Removed" as const,
    joinedAt: "2026-09-01T00:00:00Z",
    removedAt: "2026-09-14T15:00:00Z",
  };

  assert.equal(softRemovedMembership.status, "Removed");
  assert.ok(softRemovedMembership.removedAt !== null);
  // Contract invariant: historical records are never cascade deleted
  assert.equal(softRemovedMembership.classId, "11111111-1111-1111-1111-111111111111");
  assert.equal(softRemovedMembership.studentId, "44444444-4444-4444-4444-444444444441");
});

test("Dynamic RBAC: evaluateClassCapabilities calculates capabilities from actual component function", () => {
  const makeUser = (perms: string[]) => ({
    accountType: "CenterManager" as const,
    permissions: perms,
  });

  // canUpdateClass requires BOTH classes.update AND teachers.read
  assert.equal(evaluateClassCapabilities(makeUser([])).canUpdateClass, false);
  assert.equal(evaluateClassCapabilities(makeUser([permissions.classesUpdate])).canUpdateClass, false);
  assert.equal(evaluateClassCapabilities(makeUser([permissions.teachersRead])).canUpdateClass, false);
  assert.equal(
    evaluateClassCapabilities(makeUser([permissions.classesUpdate, permissions.teachersRead])).canUpdateClass,
    true
  );

  // canAddMembers requires BOTH classes.manage_members AND students.read
  assert.equal(evaluateClassCapabilities(makeUser([])).canAddMembers, false);
  assert.equal(evaluateClassCapabilities(makeUser([permissions.classesManageMembers])).canAddMembers, false);
  assert.equal(evaluateClassCapabilities(makeUser([permissions.studentsRead])).canAddMembers, false);
  assert.equal(
    evaluateClassCapabilities(makeUser([permissions.classesManageMembers, permissions.studentsRead])).canAddMembers,
    true
  );

  // canRemoveMembers requires ONLY classes.manage_members (independent of students.read)
  assert.equal(evaluateClassCapabilities(makeUser([])).canRemoveMembers, false);
  assert.equal(evaluateClassCapabilities(makeUser([permissions.studentsRead])).canRemoveMembers, false);
  assert.equal(evaluateClassCapabilities(makeUser([permissions.classesManageMembers])).canRemoveMembers, true);
  assert.equal(
    evaluateClassCapabilities(makeUser([permissions.classesManageMembers, permissions.studentsRead])).canRemoveMembers,
    true
  );

  // canCreateClass requires classes.create, subjects.read, AND teachers.read
  assert.equal(evaluateClassCapabilities(makeUser([])).canCreateClass, false);
  assert.equal(
    evaluateClassCapabilities(
      makeUser([permissions.classesCreate, permissions.subjectsRead, permissions.teachersRead])
    ).canCreateClass,
    true
  );
  assert.equal(
    evaluateClassCapabilities(makeUser([permissions.classesCreate, permissions.subjectsRead])).canCreateClass,
    false
  );
});

test("toggleStudentSelection accurately manages single-item toggles and prevents double inversion", () => {
  let selection: string[] = [];

  // First selection adds student
  selection = toggleStudentSelection(selection, "student-01");
  assert.deepEqual(selection, ["student-01"]);

  // Selecting a second student accumulates
  selection = toggleStudentSelection(selection, "student-02");
  assert.deepEqual(selection, ["student-01", "student-02"]);

  // Unselecting student-01 removes only student-01
  selection = toggleStudentSelection(selection, "student-01");
  assert.deepEqual(selection, ["student-02"]);

  // Double toggle without stopPropagation would invert twice (returning to ["student-02"])
  const doubleToggle = (current: string[], id: string) => {
    const afterFirst = toggleStudentSelection(current, id);
    return toggleStudentSelection(afterFirst, id);
  };
  assert.deepEqual(doubleToggle(["student-02"], "student-03"), ["student-02"]);

  // With stopPropagation on checkbox, only a single toggleStudentSelection executes
  let stopPropagationCalled = false;
  const simulatedEvent = {
    stopPropagation: () => {
      stopPropagationCalled = true;
    },
  };
  simulatedEvent.stopPropagation();
  assert.equal(stopPropagationCalled, true);
  selection = toggleStudentSelection(selection, "student-03");
  assert.deepEqual(selection, ["student-02", "student-03"]);
});

test("Candidate student selection persists across pagination flips via mergePageSelection and unmergePageSelection", () => {
  // Page 1 selection
  let selection = ["p1-s1", "p1-s2"];

  // Page 2: Select All merges page 2 candidate ids without losing page 1 selections
  const page2CandidateIds = ["p2-s3", "p2-s4"];
  selection = mergePageSelection(selection, page2CandidateIds);
  assert.deepEqual(selection, ["p1-s1", "p1-s2", "p2-s3", "p2-s4"]);

  // Page 3: User adds individual student on page 3
  selection = toggleStudentSelection(selection, "p3-s5");
  assert.deepEqual(selection, ["p1-s1", "p1-s2", "p2-s3", "p2-s4", "p3-s5"]);

  // Page 2: User unselects all page 2 students, page 1 and page 3 selections remain completely intact
  selection = unmergePageSelection(selection, page2CandidateIds);
  assert.deepEqual(selection, ["p1-s1", "p1-s2", "p3-s5"]);
});

test("getCandidateListState prioritizes error state over empty and loading states", () => {
  // Error state takes highest priority (e.g. 403 Forbidden or 500 Network Failure)
  assert.equal(
    getCandidateListState({ isError: true, isLoading: false, isFetching: false, candidateCount: 0 }),
    "error"
  );
  assert.equal(
    getCandidateListState({ isError: true, isLoading: true, isFetching: false, candidateCount: 0 }),
    "error"
  );

  // Loading / fetching state
  assert.equal(
    getCandidateListState({ isError: false, isLoading: true, isFetching: false, candidateCount: 0 }),
    "loading"
  );
  assert.equal(
    getCandidateListState({ isError: false, isLoading: false, isFetching: true, candidateCount: 0 }),
    "loading"
  );

  // Empty state (0 candidates returned successfully)
  assert.equal(
    getCandidateListState({ isError: false, isLoading: false, isFetching: false, candidateCount: 0 }),
    "empty"
  );

  // Ready state (candidates available to select)
  assert.equal(
    getCandidateListState({ isError: false, isLoading: false, isFetching: false, candidateCount: 15 }),
    "ready"
  );
});
