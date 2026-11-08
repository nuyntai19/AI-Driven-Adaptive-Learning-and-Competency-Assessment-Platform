import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

test("teacher pedagogical notes have migrated away from localStorage to MySQL backend API", () => {
  const viewCode = readFileSync(
    new URL("../src/pages/teacher/TeacherStudentManagementView.tsx", import.meta.url),
    "utf8"
  );

  // Must NOT read or write pedagogical notes to localStorage
  assert.doesNotMatch(
    viewCode,
    /localStorage\.setItem\(notesKey/,
    "TeacherStudentManagementView must not save pedagogical notes to localStorage"
  );
  assert.doesNotMatch(
    viewCode,
    /readTeacherNotes/,
    "TeacherStudentManagementView must not use legacy readTeacherNotes"
  );
  assert.doesNotMatch(
    viewCode,
    /teacherNotesKey/,
    "TeacherStudentManagementView must not use legacy teacherNotesKey"
  );

  // Must call persistent teacherNotesApi
  assert.match(
    viewCode,
    /teacherNotesApi\.getMyNotes/,
    "TeacherStudentManagementView must query persistent notes via teacherNotesApi.getMyNotes"
  );
  assert.match(
    viewCode,
    /teacherNotesApi\.upsertNoteForStudent/,
    "TeacherStudentManagementView must persist notes via teacherNotesApi.upsertNoteForStudent"
  );
});

test("teacherNotesApi provides complete CRUD operations against /teacher-student-notes", () => {
  // Read as text: the module's extensionless imports do not resolve under node --test.
  const apiCode = readFileSync(new URL("../src/api/teacherNotesApi.ts", import.meta.url), "utf8");
  for (const method of ["listNotes", "getMyNotes", "getNote", "getNoteForStudent", "createNote", "updateNote", "upsertNoteForStudent", "deleteNote"]) {
    assert.match(apiCode, new RegExp(`\\b${method}: async`), `teacherNotesApi must expose ${method}`);
  }
  assert.match(apiCode, /"\/teacher-student-notes"/);
});
