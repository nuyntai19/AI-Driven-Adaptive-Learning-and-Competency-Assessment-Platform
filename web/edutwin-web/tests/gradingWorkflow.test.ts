import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import { createRequire } from "node:module";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import * as helpers from "../src/utils/gradingWorkspaceHelpers.ts";
import * as feedbackPresentation from "../src/utils/attemptFeedbackPresentation.ts";
import * as aiTextFormatting from "../src/utils/aiTextFormatting.ts";
import * as rubricHelpers from "../src/utils/rubric.ts";
import type { TeacherReviewQueueItemDto } from "../src/types/reviews.ts";
import type { AssignmentProgressItemDto } from "../src/types/assignments.ts";

const require = createRequire(import.meta.url);
const source = fs.readFileSync(new URL("../src/components/reviews/AssignmentGradingWorkspace.tsx", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020, esModuleInterop: true } }).outputText;
const question = (extra = {}) => ({ analysisId: "1", attemptId: "1", questionId: "1", attemptStatus: "Completed", maxScore: 10, awardedScore: 10, isCorrect: true, finalAnswer: "x", questionText: "Question", reasoningQuality: 80, evidence: { analysisOverrideVersion: 0 }, ...extra }) as TeacherReviewQueueItemDto;
const student = (extra = {}) => ({ studentId: "student", fullName: "Student", teacherFinalReviewStatus: "Pending", finalReviewVersion: 0, finalReviewEligibility: { canApprove: true, missingQuestionCount: 0, pendingReviewQuestionCount: 0, processingQuestionCount: 0, failedQuestionCount: 0 }, ...extra }) as AssignmentProgressItemDto;

function harness(q: TeacherReviewQueueItemDto | TeacherReviewQueueItemDto[] = question(), s = student(), canGrade = true, fresh = async () => ({ data: [s] })) {
  let currentQuestions = Array.isArray(q) ? q : [q];
  let currentStudent = s;
  const mutationArgs: any[] = [];
  let params = new URLSearchParams("assignmentId=assignment&studentId=student");
  let confirmations = 0;
  const states: unknown[] = [];
  const refs: any[] = [];
  const dependencies: any[] = [];
  let effects: (() => void)[] = [];
  let cursor = 0;
  let refCursor = 0;
  let effectCursor = 0;
  let approvals = 0;
  let overridePayload: any = null;
  let reopenPayload: any = null;
  const mutations: any[] = [];
  const fakeReact = { ...React,
    useState: (initial: unknown) => { const index = cursor++; if (!(index in states)) states[index] = initial; return [states[index], (value: any) => { states[index] = typeof value === "function" ? value(states[index]) : value; }]; },
    useMemo: (fn: () => unknown) => fn(),
    useCallback: (fn: () => unknown) => fn,
    useEffect: (fn: () => void, deps: any[]) => { const index = effectCursor++; if (!dependencies[index] || deps.some((d, i) => d !== dependencies[index][i])) { dependencies[index] = deps; effects.push(fn); } },
    useRef: (initial: unknown) => { const index = refCursor++; return refs[index] ??= { current: initial }; } };
  const exports = {};
  vm.runInNewContext(compiled, { exports, URLSearchParams, setTimeout: () => 0, window: { addEventListener: () => {}, removeEventListener: () => {}, confirm: () => { confirmations++; return true; } }, require: (name: string) => {
    if (name === "react") return fakeReact;
    if (name === "react/jsx-runtime") return require(name);
    if (name === "react-router-dom") return { useSearchParams: () => [params, (next: URLSearchParams) => { params = next; }] };
    if (name === "@tanstack/react-query") return {
      useQuery: ({ queryKey, enabled }: any) => ({ data: { data: enabled === false ? [] : queryKey[0] === "gradingAssignmentsList" ? [{ assignmentId: "assignment", title: "Assignment" }, { assignmentId: "assignment2", title: "Test 2" }] : queryKey[0] === "gradingAssignmentProgress" ? [currentStudent] : queryKey[0] === "gradingStudentQuestions" ? currentQuestions : [] }, refetch: async () => {} }),
      useMutation: (config: any) => { const index = mutations.length; mutations.push(config); return { isPending: false, mutate: (args: any) => { mutationArgs[index] = args; } }; },
      useQueryClient: () => ({ invalidateQueries: () => {} }) };
    if (name.endsWith("gradingWorkspaceHelpers")) return helpers;
    if (name.endsWith("utils/rubric")) return rubricHelpers;
    if (name.endsWith("RubricGradeView")) return { RubricGradeView: () => null };
    if (name.endsWith("attemptFeedbackPresentation")) return feedbackPresentation;
    if (name.endsWith("aiTextFormatting")) return aiTextFormatting;
    if (name.endsWith("authStore")) return { useAuthStore: (selector: any) => selector({ hasPermission: () => canGrade }) };
    if (name.endsWith("permissions")) return { permissions: { twinReasoningOverride: "grade" } };
    if (name.endsWith("assignmentsApi")) return { getAssignmentProgress: fresh };
    if (name.endsWith("teacherReviewsApi")) return { approveAssignmentResult: () => { approvals++; },
      overrideReasoningAnalysis: (_id: string, payload: any) => { overridePayload = payload; },
      reopenAssignmentResult: (_id: string, payload: any) => { reopenPayload = payload; } };
    if (name.endsWith("RichMathText")) return { RichMathText: ({ text }: any) => React.createElement("span", null, text) };
    if (name.endsWith("ScratchpadAttachmentDrawer")) return { ScratchpadAttachmentDrawer: () => null };
    if (name.endsWith("problemDetails")) return { extractProblemDetails: () => ({}) };
    return {};
  }});
  const component = (exports as any).AssignmentGradingWorkspace;
  const render = () => { cursor = 0; refCursor = 0; effectCursor = 0; effects = []; mutations.length = 0; const result = component({ actor: "Teacher" }); for (const effect of effects) effect(); return result; };
  render(); // Mount effects initialise form values just as in the actual client.
  const tree = render();
  return { tree, render, mutations, mutationArgs, replaceStudent: (next: AssignmentProgressItemDto) => { currentStudent = next; }, replaceQuestion: (next: TeacherReviewQueueItemDto) => { currentQuestions = [next]; },
    replaceQuestions: (next: TeacherReviewQueueItemDto[]) => { currentQuestions = next; },
    navigate: (next: string) => { params = new URLSearchParams(next); }, params: () => params.toString(),
    confirmations: () => confirmations, approvals: () => approvals, overridePayload: () => overridePayload,
    reopenPayload: () => reopenPayload, html: () => renderToStaticMarkup(render()) };
}

function nodes(tree: any): any[] {
  if (Array.isArray(tree)) return tree.flatMap(nodes);
  if (!tree || typeof tree !== "object") return [];
  return [tree, ...nodes(tree.props?.children)];
}
function label(node: any): string { return renderToStaticMarkup(node).replace(/<[^>]+>/g, ""); }
function button(tree: any, text: string) { return nodes(tree).find(n => n.type === "button" && label(n).includes(text)); }

test("actual workspace does not label a legacy ungraded correct essay as student wrong", () => {
  const q = question({ questionType: "Essay", attemptStatus: "NeedsTeacherReview", isCorrect: null,
    awardedScore: null, suggestedScore: null, answerAssessment: "Correct", isFallback: false,
    reasoningQuality: 100, analysisFeedback: "Lập luận đúng và hợp lệ." });
  const html = harness(q).html();
  assert.match(html, /AI đánh giá đúng · Chờ giáo viên duyệt/);
  assert.match(html, /Chưa chấm/);
  assert.doesNotMatch(html, /Học sinh làm sai/);
  assert.equal(q.awardedScore, null);
  assert.equal(q.isCorrect, null);
});

test("actual workspace separates AI incorrect opinion, unknown result and authoritative wrong verdict", () => {
  for (const extra of [
    { isCorrect: null, answerAssessment: "Incorrect", isFallback: false, expected: "AI đánh giá chưa đúng · Chờ giáo viên duyệt" },
    { isCorrect: null, answerAssessment: "Uncertain", isFallback: false, expected: "Chưa có kết luận · Chờ giáo viên duyệt" },
    { isCorrect: null, answerAssessment: "Incorrect", isFallback: true, expected: "Chưa có kết luận · Chờ giáo viên duyệt" },
    { isCorrect: undefined, answerAssessment: undefined, isFallback: false, expected: "Chưa có kết luận · Chờ giáo viên duyệt" },
  ]) {
    const html = harness(question({ ...extra, attemptStatus: "NeedsTeacherReview", awardedScore: null })).html();
    assert.ok(html.includes(extra.expected));
    assert.doesNotMatch(html, /Học sinh làm sai/);
    assert.match(html, /dark:text-amber-200/);
  }
  assert.match(harness(question({ isCorrect: false, answerAssessment: "Correct", isFallback: false })).html(), /✗ Học sinh làm sai/);
  assert.match(harness(question({ isCorrect: true, answerAssessment: "Incorrect", isFallback: false })).html(), /✓ Học sinh làm đúng/);
});

test("the shared teacher/student rubric view renders the same /10 breakdown without editable controls", () => {
  const code = ts.transpileModule(fs.readFileSync(new URL("../src/components/reviews/RubricGradeView.tsx", import.meta.url), "utf8"),
    { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020 } }).outputText;
  const exports = {};
  vm.runInNewContext(code, { exports, require });
  const View = (exports as any).RubricGradeView;
  const html = renderToStaticMarkup(React.createElement(View, { grade: { maxScore: 2, awardedScore: 1,
    criteria: [{ criterionId: "method", title: "Phương pháp", maxScore: 0.5, awardedScore: 0.5, comment: "Cách giải hợp lệ" },
      { criterionId: "result", title: "Kết quả", maxScore: 1.5, awardedScore: 0.5 }] } }));
  assert.match(html, /2.5 \/ 2.5/);
  assert.match(html, /2.5 \/ 7.5/);
  assert.match(html, /Tổng: 5 \/ 10/);
  assert.match(html, /Cách giải hợp lệ/);
  assert.doesNotMatch(html, /<(?:input|textarea|button)/);
});

test("actual workspace shows confirm OR expanded manual grading, never both main question actions", () => {
  const h = harness();
  assert.ok(button(h.tree, "Xác nhận kết quả câu này"));
  assert.equal(button(h.tree, "Lưu đánh giá câu này"), undefined);
  button(h.tree, "Điều chỉnh điểm").props.onClick();
  const edited = h.render();
  assert.equal(button(edited, "Xác nhận kết quả câu này"), undefined);
  assert.ok(button(edited, "Lưu đánh giá câu này"));
});

test("scored rubric cannot quick approve; explicit criterion grades produce native server inputs and a read-only total", async () => {
  const q = question({ maxScore: 2, awardedScore: 2, gradingCriteria: { schemaVersion: "2.0", criteria: [
    { criterionId: "method", title: "Phương pháp", description: "Cách giải hợp lệ", maxScore: 0.5 },
    { criterionId: "result", title: "Kết quả", description: "Kết quả đúng", maxScore: 1.5 },
  ] } });
  const h = harness(q);
  assert.equal(button(h.tree, "Xác nhận kết quả câu này"), undefined);
  assert.ok(button(h.tree, "Lưu đánh giá câu này"));
  assert.match(h.html(), /Chưa chấm đủ/);
  const change = (aria: string, value: string) => nodes(h.render()).find(n => n.props?.["aria-label"] === aria).props.onChange({ target: { value } });
  change("Điểm tiêu chí Phương pháp", "2.5");
  change("Điểm tiêu chí Kết quả", "7.5");
  change("Nhận xét tiêu chí Phương pháp", "Cách giải khác vẫn hợp lệ");
  let tree = h.render();
  const total = nodes(tree).find(n => n.type === "input" && n.props.type === "number" && n.props.disabled);
  assert.equal(total.props.value, 10);
  assert.match(renderToStaticMarkup(tree), /Tổng điểm: 10/);
  nodes(tree).find(n => n.type === "textarea" && n.props.placeholder).props.onChange({ target: { value: "Lập luận và kết quả đúng" } });
  tree = h.render();
  assert.equal(button(tree, "Chốt kết quả toàn bài").props.disabled, true);
  button(tree, "Lưu đánh giá câu này").props.onClick();
  const args = h.mutationArgs[1];
  assert.ok(args);
  await h.mutations[1].mutationFn(args);
  const payload = h.overridePayload();
  assert.equal(payload.awardedScore, 2);
  assert.equal(payload.rubricScores[0].awardedScore, 0.5);
  assert.equal(payload.rubricScores[1].awardedScore, 1.5);
  assert.equal(payload.rubricScores[0].comment, "Cách giải khác vẫn hợp lệ");
});

test("ungraded manual and failed analysis work opens teacher form without quick approve", () => {
  for (const status of ["NeedsTeacherReview", "AnalysisFailed"]) {
    const h = harness(question({ attemptStatus: status, isCorrect: null, awardedScore: null }));
    assert.equal(button(h.tree, "Xác nhận kết quả câu này"), undefined);
    assert.ok(button(h.tree, "Lưu đánh giá câu này"));
    assert.equal(helpers.resolveQuestionDefaultFormValues(question({ isCorrect: null, awardedScore: null, evidence: { trustLevel: "Trusted" } })).awardedScore, 0);
  }
});

test("reviewed question hides redundant confirm; a new student appeal can be resolved", () => {
  const q = question({ reviewDecision: "Approved" });
  assert.equal(helpers.questionGradingActions(q).needsReview, false);
  assert.equal(button(harness(q).tree, "Xác nhận kết quả câu này"), undefined);
  const appeal = question({ reviewDecision: "Approved", hasStudentReviewRequest: true, attemptStatus: "NeedsTeacherReview" });
  assert.equal(helpers.questionGradingActions(appeal).needsReview, true);
  assert.ok(button(harness(appeal).tree, "Xác nhận sau khi xem xét yêu cầu"));
});

test("processing or unknown state never enables confirm/edit; no grading permission renders read-only", () => {
  for (const attemptStatus of ["Processing", "PendingAnalysis", "Unknown", undefined]) {
    assert.equal(helpers.questionGradingActions(question({ attemptStatus })).canEdit, false);
    assert.equal(helpers.questionGradingActions(question({ attemptStatus })).canConfirm, false);
  }
  const h = harness(question(), student(), false);
  assert.equal(button(h.tree, "Xác nhận kết quả câu này"), undefined);
  assert.equal(button(h.tree, "Lưu đánh giá câu này"), undefined);
  assert.match(h.html(), /không có quyền chấm/);
});

test("actual workspace uses authoritative eligibility and disables final approval until ready", () => {
  const pending = student({ finalReviewEligibility: { canApprove: false, pendingReviewQuestionCount: 2, blockReason: "Còn 2 câu cần xử lý." } });
  const h = harness(question(), pending);
  assert.equal(button(h.tree, "Chốt kết quả toàn bài").props.disabled, true);
  assert.match(h.html(), /Còn 2 câu cần xử lý/);
  assert.equal(button(harness().tree, "Chốt kết quả toàn bài").props.disabled, false);
  assert.equal(button(harness(question(), student({ teacherFinalReviewStatus: "Approved" })).tree, "Đã chốt kết quả toàn bài").props.disabled, true);
});

test("dirty form and busy operations block final approval; missing metadata fails closed", () => {
  const baseline = helpers.resolveQuestionDefaultFormValues(question());
  assert.equal(helpers.gradingFormIsDirty(baseline, baseline), false);
  for (const key of Object.keys(baseline)) {
    assert.equal(helpers.gradingFormIsDirty({ ...baseline, [key]: "changed" } as any, baseline), true);
  }
  assert.match(helpers.finalApprovalBlockReason(student(), true, false)!, /chưa lưu/);
  assert.match(helpers.finalApprovalBlockReason(student(), false, true)!, /Đang lưu/);
  assert.match(helpers.finalApprovalBlockReason(student({ finalReviewEligibility: undefined }), false, false)!, /server/);
});

test("final mutation never uses stale version after refresh failure or newly pending review", async () => {
  const failed = harness(question(), student(), true, async () => { throw new Error("network failure"); });
  await assert.rejects(failed.mutations[2].mutationFn, /network failure/);
  assert.equal(failed.approvals(), 0);
  const changed = harness(question(), student(), true, async () => ({ data: [student({ finalReviewEligibility: { canApprove: false, blockReason: "Mới có yêu cầu xem xét." } })] }));
  await assert.rejects(changed.mutations[2].mutationFn, /Mới có yêu cầu/);
  assert.equal(changed.approvals(), 0);
});

test("teacher references, detected method and fallback uncertainty are visible without fabricated confidence", () => {
  const h = harness(question({ teacherSolution: "Teacher reference", expectedReasoning: "Criteria", methodDetected: "Alternative method", isFallback: true, analysisConfidence: 100 }));
  assert.match(h.html(), /Teacher reference/);
  assert.match(h.html(), /Alternative method/);
  assert.match(h.html(), /Criteria/);
  assert.match(h.html(), /Không có phân tích AI/);
  assert.doesNotMatch(h.html(), /100% \(Quy tắc\)/);
});

test("same-version refetch keeps an unsaved form; concurrent newer version cannot silently replace or overwrite it", () => {
  const original = question({ overrideVersion: 1 });
  const h = harness(original);
  button(h.tree, "Điều chỉnh điểm").props.onClick();
  let tree = h.render();
  nodes(tree).find(n => n.type === "textarea" && n.props.placeholder)?.props.onChange({ target: { value: "Unsaved teacher feedback" } });
  h.replaceQuestion({ ...original });
  h.render();
  tree = h.render();
  assert.equal(nodes(tree).find(n => n.type === "textarea" && n.props.placeholder)?.props.value, "Unsaved teacher feedback");
  assert.equal(button(tree, "Chốt kết quả toàn bài").props.disabled, true);
  h.replaceQuestion(question({ overrideVersion: 2, teacherFeedback: "Other teacher update" }));
  h.render();
  tree = h.render();
  assert.equal(nodes(tree).find(n => n.type === "textarea" && n.props.placeholder)?.props.value, "Unsaved teacher feedback");
  assert.equal(button(tree, "Lưu đánh giá câu này").props.disabled, true);
  assert.match(renderToStaticMarkup(tree), /không thể lưu đè/);
  button(tree, "Hủy thay đổi").props.onClick();
  h.render();
  assert.equal(button(h.render(), "Chốt kết quả toàn bài").props.disabled, false);
});

test("AI quality display remains the original baseline after teacher quality override", () => {
  const h = harness(question({ hasTeacherOverride: true, reasoningQuality: 0, originalReasoningQuality: 82 }));
  assert.match(h.html(), /82\/100/);
  button(h.tree, "Chỉnh sửa đánh giá đã lưu").props.onClick();
  assert.equal(nodes(h.render()).find(n => n.type === "input" && n.props.type === "range")?.props.value, 0);
});

test("one question status icon prioritizes required review over correctness", () => {
  const h = harness(question({ attemptStatus: "NeedsTeacherReview", isCorrect: true, hasTeacherOverride: true, hasStudentReviewRequest: true }));
  const tab = button(h.tree, "Câu 1");
  assert.equal(label(tab), "Câu 1⚠️");
  assert.match(tab.props.title, /Cần giáo viên/);
});

test("confirming a wrong answer does not turn its question tab into a correct-answer checkmark", () => {
  const h = harness(question({ reviewDecision: "Approved", isCorrect: false }));
  assert.equal(label(button(h.tree, "Câu 1")), "Câu 1✗");
});

test("cancel is a high-contrast button and opening an unchanged form is not dirty", () => {
  const h = harness();
  button(h.tree, "Điều chỉnh điểm").props.onClick();
  const tree = h.render();
  const cancel = button(tree, "Hủy thay đổi");
  assert.match(cancel.props.className, /dark:bg-slate-700/);
  assert.match(cancel.props.className, /dark:text-slate-100/);
  assert.equal(button(tree, "Chốt kết quả toàn bài").props.disabled, false);
  cancel.props.onClick();
  assert.equal(h.confirmations(), 0);
});

test("teacher feedback uses the shared source label and displays saved AI solution with normalized line breaks", () => {
  const h = harness(question({ feedbackOrigin: "LegacySystem", analysisFeedback: "Canonical system notice", aiSolution: "Step 1\\n\\nStep 2 $\\neq 0$" }));
  const html = h.html();
  assert.match(html, /Thông báo hệ thống \(dữ liệu cũ\)/);
  assert.match(html, /Canonical system notice/);
  assert.match(html, /Step 1\n\nStep 2/);
  assert.match(html, /\\neq 0/);
  assert.doesNotMatch(html, /Step 1\\n/);
  assert.match(harness().html(), /Chưa có lời giải AI được lưu/);
});

test("approval refresh preserves published numbering and selected question identity", async () => {
  const q1 = question({ questionOrderIndex: 1 });
  const q2 = question({ attemptId: "2", analysisId: "2", questionId: "2", questionOrderIndex: 2, questionText: "Second question" });
  const h = harness([q1, q2]);
  button(h.tree, "Câu 2").props.onClick();
  h.render();
  h.render();
  const onSuccess = h.mutations[0].onSuccess;
  h.replaceQuestions([{ ...q2, reviewDecision: "Approved", evidence: { ...q2.evidence, evaluatedAt: "later" } }, q1]);
  await onSuccess();
  h.render();
  const tree = h.render();
  assert.match(button(tree, "Câu 2").props.className, /bg-indigo-600/);
  assert.doesNotMatch(button(tree, "Câu 1").props.className, /bg-indigo-600/);
  assert.match(renderToStaticMarkup(tree), /Second question/);
  assert.equal(h.confirmations(), 0);
});

test("final approval and sidebar navigation do not leak dirty form state into another assignment", async () => {
  const h = harness(question({ overrideVersion: 1, teacherFeedback: "Saved feedback", overrideReason: "Saved reason", hasTeacherOverride: true }));
  button(h.tree, "Chỉnh sửa đánh giá đã lưu").props.onClick();
  h.render();
  await h.mutations[2].onSuccess();
  h.render();
  h.navigate(""); // sidebar to queue
  h.render();
  h.render();
  h.navigate("assignmentId=assignment2&studentId=student");
  h.replaceQuestion(question({ attemptId: "7", analysisId: "7", questionId: "7", questionText: "Test 2 question", teacherFeedback: "" }));
  h.render();
  const tree = h.render();
  assert.equal(button(tree, "Chốt kết quả toàn bài").props.disabled, false);
  assert.equal(button(tree, "Lưu đánh giá câu này"), undefined);
  button(tree, "Quay lại danh sách học sinh").props.onClick();
  assert.equal(h.confirmations(), 0);
  assert.equal(h.params(), "assignmentId=assignment2");
});

test("actual manual form edits warn once, discarding them cannot contaminate the next student", () => {
  const h = harness(question({ attemptStatus: "NeedsTeacherReview", isCorrect: null, awardedScore: null }));
  nodes(h.tree).find(n => n.type === "textarea" && n.props.placeholder)?.props.onChange({ target: { value: "Actual edit" } });
  const tree = h.render();
  button(tree, "Quay lại danh sách học sinh").props.onClick();
  assert.equal(h.confirmations(), 1);
  h.render();
  h.navigate("assignmentId=assignment2&studentId=student");
  h.replaceQuestion(question({ analysisId: "3", attemptId: "3" }));
  h.render();
  button(h.render(), "Quay lại danh sách học sinh").props.onClick();
  assert.equal(h.confirmations(), 1);
});

test("finalized assignment is read-only until explicit reopen; reason required and current version refreshed", async () => {
  const h = harness(question({ hasTeacherOverride: true }), student({ teacherFinalReviewStatus: "Approved", finalReviewVersion: 3 }));
  assert.equal(button(h.tree, "Chỉnh sửa đánh giá đã lưu"), undefined);
  assert.equal(button(h.tree, "Hủy câu hỏi do lỗi đề"), undefined);
  assert.equal(button(h.tree, "Lưu đánh giá câu này"), undefined);
  assert.ok(button(h.tree, "Mở lại để điều chỉnh"));
  await assert.rejects(h.mutations[4].mutationFn, /Lý do mở lại/);
  button(h.tree, "Mở lại để điều chỉnh").props.onClick();
  let tree = h.render();
  assert.equal(button(tree, "Xác nhận mở lại").props.disabled, true);
  nodes(tree).find(n => n.type === "textarea" && n.props.maxLength === 1000)?.props.onChange({ target: { value: "Correct grading error" } });
  tree = h.render();
  await h.mutations[4].mutationFn();
  assert.equal(h.reopenPayload().finalReviewVersion, 3);
  assert.equal(h.reopenPayload().reason, "Correct grading error");
  await h.mutations[4].onSuccess();
  h.replaceStudent(student({ finalReviewVersion: 4 }));
  h.render();
  const reopened = h.render();
  assert.equal(button(reopened, "Mở lại để điều chỉnh"), undefined);
  assert.ok(button(reopened, "Chỉnh sửa đánh giá đã lưu"));
  assert.ok(button(reopened, "Hủy câu hỏi do lỗi đề"));
});

test("teacher grade matches student ten-point display, with separate assignment contribution", () => {
  const h = harness(question({ maxScore: 100, awardedScore: 50, assignmentQuestionCount: 2 }));
  assert.match(h.html(), /Điểm câu hỏi \(thang 10\): 5 \/ 10/);
  assert.match(h.html(), /Đóng góp vào tổng bài \(thang 10\): 2.5 \/ 5/);
  button(h.tree, "Điều chỉnh điểm").props.onClick();
  const tree = h.render();
  const score = nodes(tree).find(n => n.type === "input" && n.props.type === "number");
  assert.equal(score.props.max, 10);
  assert.equal(score.props.value, 5);
  assert.match(renderToStaticMarkup(tree), /thang 100, không cộng/);
});

test("void modal requires explicit acknowledgment of all students and reopened final results", () => {
  const h = harness();
  button(h.tree, "Hủy câu hỏi do lỗi đề").props.onClick();
  let tree = h.render();
  assert.equal(button(tree, "Xác nhận hủy câu hỏi").props.disabled, true);
  const acknowledgement = nodes(tree).find(n => n.type === "input" && n.props.type === "checkbox" && n.props.checked === false);
  acknowledgement.props.onChange({ target: { checked: true } });
  tree = h.render();
  assert.equal(button(tree, "Xác nhận hủy câu hỏi").props.disabled, false);
  assert.match(renderToStaticMarkup(tree), /TẤT CẢ học sinh/);
});

test("actual save action converts the teacher's ten-point input back to the raw question scale", async () => {
  const h = harness(question({ maxScore: 100, awardedScore: 50, teacherFeedback: "Saved feedback", overrideReason: "Saved reason" }));
  button(h.tree, "Điều chỉnh điểm").props.onClick();
  let tree = h.render();
  nodes(tree).find(n => n.type === "input" && n.props.type === "number").props.onChange({ target: { value: "7.5" } });
  tree = h.render();
  button(tree, "Lưu đánh giá câu này").props.onClick();
  assert.equal(h.mutationArgs[1].payload.awardedScore, 75);
  await h.mutations[1].mutationFn(h.mutationArgs[1]);
  assert.equal(h.overridePayload().awardedScore, 75);
  assert.equal(h.overridePayload().reasoningQuality, 80);
});
