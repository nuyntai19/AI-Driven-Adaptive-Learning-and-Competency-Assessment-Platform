import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import { initialEnrollmentClasses } from "../src/utils/studentClassGrades.ts";

test("initial enrollment shows only active classes of the student's grade", () => {
  const classes = [{ classId: "old", gradeLevel: null, status: "Active" },
    { classId: "10", gradeLevel: 10, status: "Active" }, { classId: "11", gradeLevel: 11, status: "Active" },
    { classId: "closed", gradeLevel: 10, status: "Archived" }];
  assert.deepEqual(initialEnrollmentClasses(classes, 10).map(c => c.classId), ["10"]);
  assert.deepEqual(initialEnrollmentClasses(classes, 11).map(c => c.classId), ["11"]);
  assert.deepEqual(initialEnrollmentClasses(classes, 12), []);
});

test("both manager account forms remove selected classes after changing grade", () => {
  const source = fs.readFileSync(new URL("../src/pages/StudentListPage.tsx", import.meta.url), "utf8");
  assert.equal((source.match(/initialEnrollmentClasses\(classesData\?\.data \?\? \[\], createGradeLevel\)/g) ?? []).length, 2);
  assert.equal((source.match(/setCreateClassIds\(prev => prev\.filter\(id => initialClasses\.some/g) ?? []).length, 2);
  assert.equal((source.match(/initialClasses\.map\(\(c\) =>/g) ?? []).length, 2);
});
