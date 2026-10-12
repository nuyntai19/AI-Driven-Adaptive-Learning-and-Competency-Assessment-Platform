import assert from "node:assert/strict";
import test from "node:test";
import {
  shouldDisplayTeacherSelector,
  validateCurriculumForm,
  buildCreateCurriculumPayload,
  buildUpdateCurriculumPayload,
  isConcurrencyConflictError,
  formatCurriculumSaveError
} from "../src/pages/curriculumEditorHelpers.ts";

test("shouldDisplayTeacherSelector returns true only for CenterManager in creation mode", () => {
  assert.equal(
    shouldDisplayTeacherSelector({ isEditMode: false, isCenterManager: true }),
    true,
    "CenterManager in create mode must see teacher selector"
  );

  assert.equal(
    shouldDisplayTeacherSelector({ isEditMode: false, isCenterManager: false }),
    false,
    "Teacher in create mode must NOT see teacher selector"
  );

  assert.equal(
    shouldDisplayTeacherSelector({ isEditMode: true, isCenterManager: true }),
    false,
    "Edit mode must not show teacher selector even for CenterManager"
  );

  assert.equal(
    shouldDisplayTeacherSelector({ isEditMode: true, isCenterManager: false }),
    false,
    "Edit mode must not show teacher selector for Teacher"
  );
});

test("validateCurriculumForm enforces title across all modes", () => {
  const resultEmpty = validateCurriculumForm(
    { title: "", subjectId: "sub-1", teacherId: "tea-1" },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultEmpty.isValid, false);
  assert.equal(resultEmpty.errorMessage, "Vui lòng nhập tiêu đề lộ trình.");

  const resultWhitespace = validateCurriculumForm(
    { title: "   ", subjectId: "sub-1" },
    { isEditMode: true, isCenterManager: false }
  );
  assert.equal(resultWhitespace.isValid, false);
  assert.equal(resultWhitespace.errorMessage, "Vui lòng nhập tiêu đề lộ trình.");
});

test("validateCurriculumForm enforces subjectId for creation mode", () => {
  const resultNoSubject = validateCurriculumForm(
    { title: "Math Course", subjectId: "", teacherId: "tea-1" },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultNoSubject.isValid, false);
  assert.equal(resultNoSubject.errorMessage, "Vui lòng chọn môn học.");
});

test("validateCurriculumForm enforces teacherId for CenterManager in creation mode", () => {
  const resultNoTeacher = validateCurriculumForm(
    { title: "Physics Course", subjectId: "sub-phys", teacherId: "", gradeLevel: 10 },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultNoTeacher.isValid, false);
  assert.equal(resultNoTeacher.errorMessage, "Vui lòng chọn giáo viên phụ trách lộ trình.");

  const resultWithTeacher = validateCurriculumForm(
    { title: "Physics Course", subjectId: "sub-phys", teacherId: "teacher-guid-123", gradeLevel: 10 },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultWithTeacher.isValid, true);
  assert.equal(resultWithTeacher.errorMessage, undefined);
});

test("validateCurriculumForm enforces required GradeLevel (10, 11, 12) in creation mode", () => {
  const resultNoGrade = validateCurriculumForm(
    { title: "Course", subjectId: "sub-1", teacherId: "tea-1", gradeLevel: null },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultNoGrade.isValid, false);
  assert.equal(resultNoGrade.errorMessage, "Vui lòng chọn khối học áp dụng (Khối 10, 11 hoặc 12) cho giáo trình mới.");

  const resultInvalidGrade = validateCurriculumForm(
    { title: "Course", subjectId: "sub-1", teacherId: "tea-1", gradeLevel: 9 },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultInvalidGrade.isValid, false);

  const resultValidGrade = validateCurriculumForm(
    { title: "Course", subjectId: "sub-1", teacherId: "tea-1", gradeLevel: 12 },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultValidGrade.isValid, true);
});

test("validateCurriculumForm allows Teacher to create curriculum without explicit teacherId", () => {
  const resultTeacherCreate = validateCurriculumForm(
    { title: "Chemistry Course", subjectId: "sub-chem", teacherId: "", gradeLevel: 11 },
    { isEditMode: false, isCenterManager: false }
  );
  assert.equal(resultTeacherCreate.isValid, true, "Teacher creates curriculum with implicit teacher binding");
});

test("validateCurriculumForm in edit mode does not re-validate subject or teacher", () => {
  const resultEdit = validateCurriculumForm(
    { title: "Updated Title" },
    { isEditMode: true, isCenterManager: true }
  );
  assert.equal(resultEdit.isValid, true);
});

test("buildCreateCurriculumPayload attaches teacherId only when isCenterManager", () => {
  const managerPayload = buildCreateCurriculumPayload(
    {
      title: "  Advanced Biology  ",
      description: "Comprehensive biology curriculum",
      subjectId: "sub-bio",
      gradeLevel: 10,
      teacherId: "  tea-lead-456  ",
      nodeIds: ["101", "102"]
    },
    true
  );
  assert.equal(managerPayload.title, "Advanced Biology");
  assert.equal(managerPayload.teacherId, "tea-lead-456");
  assert.equal(managerPayload.subjectId, "sub-bio");
  assert.equal(managerPayload.gradeLevel, 10);
  assert.deepEqual(managerPayload.nodeIds, ["101", "102"]);

  const teacherPayload = buildCreateCurriculumPayload(
    {
      title: "  Basic Biology  ",
      subjectId: "sub-bio",
      gradeLevel: 11,
      teacherId: "should-not-be-sent",
      nodeIds: []
    },
    false
  );
  assert.equal(teacherPayload.title, "Basic Biology");
  assert.equal(teacherPayload.gradeLevel, 11);
  assert.equal(teacherPayload.teacherId, undefined, "Teacher created payload must omit explicit teacherId");
});

test("buildUpdateCurriculumPayload trims title and preserves rowVersion", () => {
  const updatePayload = buildUpdateCurriculumPayload({
    title: "  New Title  ",
    description: "New Desc",
    rowVersion: "3"
  });
  assert.equal(updatePayload.title, "New Title");
  assert.equal(updatePayload.description, "New Desc");
  assert.equal(updatePayload.rowVersion, "3");
});

test("isConcurrencyConflictError accurately identifies HTTP 409 conflict", () => {
  assert.equal(isConcurrencyConflictError({ response: { status: 409 } }), true);
  assert.equal(isConcurrencyConflictError({ status: 409 }), true);
  assert.equal(isConcurrencyConflictError({ response: { status: 400 } }), false);
  assert.equal(isConcurrencyConflictError({ response: { status: 404 } }), false);
  assert.equal(isConcurrencyConflictError({ response: { status: 500 } }), false);
  assert.equal(isConcurrencyConflictError(null), false);
  assert.equal(isConcurrencyConflictError(undefined), false);
});

test("formatCurriculumSaveError formats HTTP 409 cross-grade error for Axios-shaped error", () => {
  const axiosError = {
    response: {
      status: 409,
      data: {
        title: "Trạng thái không hợp lệ",
        detail: "Không thể thực hiện hành động do sai trạng thái.",
        errorCode: "INVALID_STATE_TRANSITION"
      }
    }
  };

  const message = formatCurriculumSaveError(axiosError, {
    classes: [
      { classId: "cls-11", name: "Lớp 11A", gradeLevel: 11 },
      { classId: "cls-10", name: "Lớp 10A", gradeLevel: 10 }
    ],
    selectedClassIds: ["cls-11"],
    curriculumGradeLevel: 10
  });

  assert.equal(message, "Không thể gán lớp Khối 11 vào giáo trình Khối 10.");
});

test("formatCurriculumSaveError formats HTTP 409 for mapped Error instance without leaking generic Axios status", () => {
  const mappedError = new Error("Request failed with status code 409");
  (mappedError as any).status = 409;

  const message = formatCurriculumSaveError(mappedError, {
    classes: [
      { classId: "cls-11", name: "UIACC-GRADE11-CLASS", gradeLevel: 11 }
    ],
    selectedClassIds: ["cls-11"],
    curriculumGradeLevel: 10
  });

  assert.equal(message, "Không thể gán lớp Khối 11 vào giáo trình Khối 10.");
});

test("formatCurriculumSaveError falls back to business detail or Vietnamese conflict message when context is absent", () => {
  const axiosWithDetail = {
    response: {
      status: 409,
      data: {
        title: "Xung đột dữ liệu",
        detail: "Giáo trình đã được sửa đổi ở phiên khác.",
        errorCode: "CONCURRENCY_CONFLICT"
      }
    }
  };
  assert.equal(
    formatCurriculumSaveError(axiosWithDetail),
    "Lỗi xung đột (HTTP 409): Giáo trình đã được sửa đổi ở phiên khác."
  );

  const generic409Error = new Error("Request failed with status code 409");
  assert.equal(
    formatCurriculumSaveError(generic409Error),
    "Không thể gán lớp học khác khối vào giáo trình."
  );
});
