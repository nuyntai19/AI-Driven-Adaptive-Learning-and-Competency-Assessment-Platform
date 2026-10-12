import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { studentWorkspaceLabels } from "../src/utils/studentWorkspaceSummary.ts";
import type { StudentWorkspaceSummaryDto } from "../src/types/dashboards.ts";

const summary = { assignmentCount: 42, dailyStreak: 2, studiedToday: true, timezone: "Asia/Ho_Chi_Minh" } as StudentWorkspaceSummaryDto;
test("workspace badges use actual totals, including zero and larger totals", () => {
  assert.equal(studentWorkspaceLabels(summary).assignmentCount, "42");
  assert.equal(studentWorkspaceLabels(summary).streak, "Chuỗi 2 ngày");
  assert.equal(studentWorkspaceLabels({ ...summary, assignmentCount: 0, dailyStreak: 0 }).assignmentCount, "0");
  assert.equal(studentWorkspaceLabels({ ...summary, dailyStreak: 0 }).shortStreak, "0 ngày");
});
test("loading/network failure does not invent activity or reuse a stale number", () => {
  assert.equal(studentWorkspaceLabels().assignmentCount, "—");
  assert.equal(studentWorkspaceLabels(undefined, true).shortStreak, "— ngày");
  assert.equal(studentWorkspaceLabels(summary, true).assignmentCount, "—");
  assert.match(studentWorkspaceLabels(summary, true).streakTitle, /Không tải được/);
});
test("student layout uses the same live indicators in desktop and mobile variants", () => {
  const source = fs.readFileSync(new URL("../src/layouts/StudentLayout.tsx", import.meta.url), "utf8");
  assert.equal((source.match(/workspaceLabels.assignmentCount/g) ?? []).length, 2);
  assert.equal((source.match(/workspaceLabels.streak\}/g) ?? []).length, 1); // shared context row for all widths
  assert.equal((source.match(/workspaceLabels.shortStreak\}/g) ?? []).length, 1);
  assert.doesNotMatch(source, /Chuỗi 7 ngày|>3<\/span>/);
  assert.match(source, /user\?\.centerId, user\?\.userId, activeSubjectId/);
});
