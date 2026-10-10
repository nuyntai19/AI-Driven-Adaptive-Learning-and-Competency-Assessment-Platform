import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

test("teacher header wraps the toolbar instead of shrinking the title to a narrow column", () => {
  const source = readFileSync(new URL("../src/components/teacher/TeacherPrimitives.tsx", import.meta.url), "utf8");
  const header = source.slice(source.indexOf("export function TeacherPageHeader"), source.indexOf("interface TeacherMetricCardProps"));
  assert.match(header, /flex flex-wrap items-end justify-between gap-4/);
  assert.match(header, /min-w-0 flex-\[1_1_28rem\]/);
  assert.match(header, /min-w-0 max-w-full flex flex-\[0_1_auto\]/);
  assert.doesNotMatch(header, /shrink-0/);
});

test("student management toolbar and metric cards fit a phone viewport", () => {
  const source = readFileSync(new URL("../src/pages/teacher/TeacherStudentManagementView.tsx", import.meta.url), "utf8");
  assert.match(source, /th-select text-xs w-full min-w-0 max-w-full sm:w-auto/);
  assert.match(source, /grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4/);
});
