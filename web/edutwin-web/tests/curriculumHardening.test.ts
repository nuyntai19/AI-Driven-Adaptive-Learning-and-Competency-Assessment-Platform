import assert from "node:assert/strict";
import test from "node:test";
import {
  shouldDisplayTeacherSelector,
  validateCurriculumForm,
  buildCreateCurriculumPayload,
  buildUpdateCurriculumPayload,
  isConcurrencyConflictError
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
    { title: "Physics Course", subjectId: "sub-phys", teacherId: "" },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultNoTeacher.isValid, false);
  assert.equal(resultNoTeacher.errorMessage, "Vui lòng chọn giáo viên phụ trách lộ trình.");

  const resultWithTeacher = validateCurriculumForm(
    { title: "Physics Course", subjectId: "sub-phys", teacherId: "teacher-guid-123" },
    { isEditMode: false, isCenterManager: true }
  );
  assert.equal(resultWithTeacher.isValid, true);
  assert.equal(resultWithTeacher.errorMessage, undefined);
});

test("validateCurriculumForm allows Teacher to create curriculum without explicit teacherId", () => {
  const resultTeacherCreate = validateCurriculumForm(
    { title: "Chemistry Course", subjectId: "sub-chem", teacherId: "" },
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
      teacherId: "  tea-lead-456  ",
      nodeIds: ["101", "102"]
    },
    true
  );
  assert.equal(managerPayload.title, "Advanced Biology");
  assert.equal(managerPayload.teacherId, "tea-lead-456");
  assert.equal(managerPayload.subjectId, "sub-bio");
  assert.deepEqual(managerPayload.nodeIds, ["101", "102"]);

  const teacherPayload = buildCreateCurriculumPayload(
    {
      title: "  Basic Biology  ",
      subjectId: "sub-bio",
      teacherId: "should-not-be-sent",
      nodeIds: []
    },
    false
  );
  assert.equal(teacherPayload.title, "Basic Biology");
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
