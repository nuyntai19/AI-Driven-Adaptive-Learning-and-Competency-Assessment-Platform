import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import ts from "typescript";
import { evaluateClassCapabilities } from "../src/pages/classListHelpers.ts";
import { permissions } from "../src/auth/permissions.ts";
import { academicLifecycleError } from "../src/utils/academicLifecycleError.ts";

const source = (path: string) => readFileSync(new URL(`../src/${path}`, import.meta.url), "utf8");

test("Manager class report is a real governance route, not a teacher redirect", () => {
  const text=source("App.tsx");
  const ast=ts.createSourceFile("App.tsx",text,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);
  let matches=0;
  function visit(node: ts.Node, governance=false) {
    if (ts.isJsxElement(node)) governance ||= node.openingElement.attributes.getText(ast).includes("CenterManagerLayoutBoundary");
    if (ts.isJsxSelfClosingElement(node) && node.attributes.getText(ast).includes('path="/quan-ly/lop-hoc/:classId/tong-quan"')) {
      matches++;assert.equal(governance,true);assert.match(node.getText(ast),/CenterClassReportPage/);assert.doesNotMatch(node.getText(ast),/Redirect/);
    }
    ts.forEachChild(node,child=>visit(child,governance));
  }
  visit(ast);assert.equal(matches,1);
  assert.match(text,/allOf=\{\[permissions.classesRead, permissions.studentsRead\]\} accountTypes=\{\["CenterManager"\]\}/);
});

test("Class report capability checks the actor and both API read permissions", () => {
  const manager={accountType:"CenterManager" as const,permissions:[permissions.classesRead,permissions.studentsRead]};
  assert.equal(evaluateClassCapabilities(manager).canViewDashboard,true);
  assert.equal(evaluateClassCapabilities({...manager,permissions:[permissions.classesRead]}).canViewDashboard,false);
  assert.equal(evaluateClassCapabilities({...manager,accountType:"Teacher"}).canViewDashboard,false);
});

test("Manager report is read-only and uses server score summaries on scale ten", () => {
  const text=source("pages/CenterClassReportPage.tsx");
  assert.match(text,/academic-report/);assert.match(text,/actor\?\.centerId, actor\?\.userId, classId/);
  assert.match(text,/ClassHistoryPanel/);assert.match(text,/reportScoreText\(record\)/);assert.match(text,/Điểm thang 10/);
  assert.doesNotMatch(text,/httpClient\.(post|put|patch|delete)|useMutation|TeacherClassOverview|AssignmentGradingWorkspace/);
});

test("Archive dialog fails closed while checking dependencies and requires a reason", () => {
  const text=source("components/teacher/CurriculumArchiveDialog.tsx");
  assert.match(text,/curriculumApi.lifecycleUsage/);
  assert.match(text,/usage.isFetching \|\| !usage.data \|\| usage.isError \|\| blocked \|\| !reason.trim\(\)/);
  assert.match(text,/kể cả lớp của giáo viên khác/);assert.match(text,/maxLength=\{500\}/);
  assert.match(text,/onConfirm\(reason.trim\(\)\)/);
});

test("Dependency messages keep controlled business detail but suppress unknown internal errors", () => {
  const error={response:{status:409,data:{errorCode:"INVALID_STATE_TRANSITION",detail:"Đang được dùng bởi Lớp Toán 12."}}};
  assert.equal(academicLifecycleError(error,"fallback"),error.response.data.detail);
  assert.equal(academicLifecycleError({response:{status:500,data:{errorCode:"INTERNAL_ERROR",detail:"secret SQL stack"}}},"An toàn"),"An toàn");
  assert.doesNotMatch(academicLifecycleError({response:{status:403,data:{detail:"private class names"}}},"fallback"),/private/);
});

test("Graph dependency failures are not reported as stale versions or bypassed with deactivate", () => {
  const text=source("pages/teacher/TeacherKnowledgeGraphView.tsx");
  assert.match(text,/details.errorCode === "CONCURRENCY_CONFLICT"/);
  assert.doesNotMatch(text,/Nên tắt hoạt động|Hãy tắt hoạt động.*thay vì/);
});
