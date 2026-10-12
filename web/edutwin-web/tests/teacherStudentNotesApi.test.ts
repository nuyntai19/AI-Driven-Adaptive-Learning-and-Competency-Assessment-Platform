import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { teacherNotesApi } from "../src/api/teacherNotesApi.ts";

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
  assert.equal(typeof teacherNotesApi.listNotes, "function");
  assert.equal(typeof teacherNotesApi.getMyNotes, "function");
  assert.equal(typeof teacherNotesApi.getNote, "function");
  assert.equal(typeof teacherNotesApi.getNoteForStudent, "function");
  assert.equal(typeof teacherNotesApi.createNote, "function");
  assert.equal(typeof teacherNotesApi.updateNote, "function");
  assert.equal(typeof teacherNotesApi.upsertNoteForStudent, "function");
  assert.equal(typeof teacherNotesApi.deleteNote, "function");
});
