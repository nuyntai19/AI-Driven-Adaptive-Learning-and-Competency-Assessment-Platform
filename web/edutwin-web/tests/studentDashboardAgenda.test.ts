import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import vm from "node:vm";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { studentScopeUrl } from "../src/utils/studentAcademicNavigation.ts";

const require = createRequire(import.meta.url);
const source = readFileSync(new URL("../src/pages/StudentDashboardPage.tsx", import.meta.url), "utf8");
const dashboard = { student: { fullName: "Học sinh kiểm thử" }, subject: { subjectName: "Toán" },
  goal: { hasGoal: false, currentPredictedScore: 1.5, targetScore: 8, riskScore: 55 }, masteryRadar: [], progressLine: [] };
const mission = { title: "Tìm hiểu: Hàm số", objective: "Học cách xét hàm số", canStart: true,
  estimatedDurationMinutes: 5, status: "InProgress", activeRunId: 8 };

function render({ assignment = false, pending = false, history = false, all = false, error = false, inProgress = true } = {}) {
  const params = new URLSearchParams(all ? "" : `subjectId=math&classId=class-10${history ? "&history=true" : ""}`);
  const queries: any[] = []; const calls: any[] = [];
  const exports: any = {};
  const dependencies: Record<string, any> = {
    "react": { ...React, useState: (value: any) => [value, () => {}], useMemo: (fn: any) => fn() },
    "react/jsx-runtime": require("react/jsx-runtime"),
    "react-router-dom": { useSearchParams: () => [params], Link: ({ to, children, ...rest }: any) => React.createElement("a", { href: to, ...rest }, children) },
    "@tanstack/react-query": { useQuery: (query: any) => {
      queries.push(query);
      if (queries.length === 1) return { data: dashboard };
      if (queries.length === 2) return { data: { data: assignment ? [{ assignmentId: "homework", title: "Bài giáo viên giao", progress: { status: "NotStarted" } }] : [] } };
      if (query.enabled === false) return { isPending: true };
      return { isPending: pending, isError: error, data: pending || error ? undefined : {
        ongoingRuns: inProgress ? [mission] : [], primaryMission: { ...mission, status: "NotStarted" }, agendaMessage: "Kế hoạch học hôm nay" } };
    } },
    "../api/dashboardsApi": { getStudentDashboard() {} },
    "../api/assignmentsApi": { getStudentAssignments() {} },
    "../api/learningPathApi": { getLearningAgenda: (...args: any[]) => calls.push(args) },
    "../components/student/subjectTheme": { getSubjectTheme: () => ({}) },
    "../utils/studentAcademicNavigation": { studentScopeUrl },
    "../utils/academicDisplay": { goalGapLabel: () => "" },
  };
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS,
    jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true, target: ts.ScriptTarget.ES2022 } }).outputText,
    { exports, require: (name: string) => {
      if (dependencies[name]) return dependencies[name];
      if (name.startsWith("../components/student/")) return new Proxy({}, { get: () => () => null });
      throw new Error(`Unexpected dependency ${name}`);
    } });
  const html = renderToStaticMarkup(React.createElement(exports.StudentDashboardPage));
  return { html, queries, calls };
}

test("dashboard uses the current agenda and keeps subject/class context when continuing a mission", async () => {
  const h = render();
  await h.queries[2].queryFn();
  assert.deepEqual(h.calls[0], ["math", 15, "class-10"]);
  assert.match(h.html, /Tìm hiểu: Hàm số/);
  assert.match(h.html, /Tiếp tục học ngay/);
  assert.match(h.html, /href="\/hoc-tap\/lo-trinh-hoc-tap\?subjectId=math&amp;classId=class-10"/);
  assert.doesNotMatch(h.html, /href="\/hoc-tap\/luyen-tap/);
});

test("an unfinished assignment remains the primary action; a ready agenda mission is shown next", () => {
  const h = render({ assignment: true, inProgress: false });
  assert.match(h.html, /href="\/hoc-tap\/bai-tap\/homework\?subjectId=math&amp;classId=class-10"/);
  assert.match(h.html, /Mở nhiệm vụ trong lộ trình/);
});

test("all-subject and historical dashboards do not fetch a live single-subject agenda", () => {
  const all = render({ all: true });
  assert.equal(all.queries[2].enabled, false);
  assert.match(all.html, /Chọn môn và nhiệm vụ học tập/);
  const history = render({ history: true });
  assert.equal(history.queries[2].enabled, false);
  assert.doesNotMatch(history.html, /Học theo lộ trình|Tiếp tục học ngay/);
});

test("an agenda load failure offers the learning path instead of inventing a topic or claiming completion", () => {
  const h = render({ error: true });
  assert.match(h.html, /Chưa tải được kế hoạch hôm nay/);
  assert.match(h.html, /Xem lộ trình học tập/);
  assert.doesNotMatch(h.html, /Hàm số &amp; Khảo sát hàm|đã hoàn thành các câu hỏi/);
});
