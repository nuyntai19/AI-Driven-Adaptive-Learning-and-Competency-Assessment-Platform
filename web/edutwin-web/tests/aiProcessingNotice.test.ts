import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import { areSubmittedAnalysesTerminal, getAIProcessingNotice, isAIProcessingPending } from "../src/utils/aiProcessingNotice.ts";
import type { AttemptFeedbackDataDto } from "../src/types/learning.ts";

const data = (patch: Partial<AttemptFeedbackDataDto> = {}): AttemptFeedbackDataDto => ({
  attemptId: "1", questionId: "2", status: "NeedsTeacherReview", grading: { maxScore: 10, source: "PendingTeacher" },
  analysis: { analysisId: "a", schemaVersion: "1", feedback: "Chưa thể phân tích.", missingSteps: [], rootCauseNodes: [],
    isFallback: true, needsTeacherReview: true, hasTeacherOverride: false }, ...patch,
});

test("invalid model output is not described as seven exhausted keys or unavailable AI", () => {
  const notice = getAIProcessingNotice(data({ aiProcessing: { status: "FallbackCompleted", reason: "ResponseInvalid" } }))!;
  assert.equal(notice.title, "Kết quả AI chưa đạt kiểm tra hợp lệ");
  assert.doesNotMatch(notice.message, /quota|7 key|không khả dụng/);
  assert.match(notice.message, /không phải kết luận của AI/);
});
test("a valid AI proposal waiting for teacher approval has no failure banner", () => {
  const proposal = data(); proposal.analysis = { ...proposal.analysis!, isFallback: false, suggestedScore: 8 };
  assert.equal(getAIProcessingNotice(proposal), null);
});
test("pending repair and quota wait explain different operations without requesting resubmission", () => {
  const repair = getAIProcessingNotice(data({ aiProcessing: { status: "Pending", reason: "ResponseInvalid" } }))!;
  assert.match(repair.title, /kiểm tra lại/); assert.match(repair.message, /riêng câu này/);
  const quota = getAIProcessingNotice(data({ aiProcessing: { status: "Pending", reason: "QuotaWait" } }))!;
  assert.match(quota.title, /quota/); assert.match(quota.message, /không cần nộp lại/);
});
test("provider capacity wait does not claim all keys were tested or exhausted", () => {
  const notice = getAIProcessingNotice(data({ aiProcessing: { status: "Pending", reason: "CapacityWait" } }))!;
  assert.match(notice.title, /lượt xử lý/); assert.doesNotMatch(notice.message, /hết quota|7 key/);
});
test("job status is authoritative during retry even if a previous fallback is still displayed", () => {
  assert.equal(isAIProcessingPending(data({ aiProcessing: { status: "Processing" } })), true);
  assert.equal(isAIProcessingPending(data({ status: "PendingAnalysis", aiProcessing: { status: "Completed" } })), false);
});
test("old fallback without diagnostic metadata reports an unknown analysis failure, not quota", () => {
  assert.equal(getAIProcessingNotice(data())!.title, "Chưa có kết quả phân tích AI hợp lệ");
});
test("timeout, attachment failure and score-only view have accurate notices", () => {
  assert.match(getAIProcessingNotice(data({ aiProcessing: { status: "FallbackCompleted", reason: "Timeout" } }))!.title, /quá thời gian/);
  assert.match(getAIProcessingNotice(data({ aiProcessing: { status: "FallbackCompleted", reason: "AttachmentUnavailable" } }))!.title, /ảnh nháp/);
  assert.doesNotMatch(getAIProcessingNotice(data(), true)!.message, /lời giải giáo viên/);
});
test("all AI terminal states clear waiting even if a fallback or teacher review remains", () => {
  assert.equal(areSubmittedAnalysesTerminal([{ attemptStatus: "Completed" }, { attemptStatus: "NeedsTeacherReview" }, { attemptStatus: "AnalysisFailed" }]), true);
});
test("one finished selected question cannot clear the banner for an unfinished assignment", () => {
  assert.equal(areSubmittedAnalysesTerminal([{ attemptStatus: "Completed" }, { latestAttempt: { status: "Processing" } }]), false);
  assert.equal(areSubmittedAnalysesTerminal([{ latestAttempt: { status: "PendingAnalysis" }, attemptStatus: "Completed" }]), false);
});
test("unhydrated questions fail closed; voided questions do not hold up the banner", () => {
  assert.equal(areSubmittedAnalysesTerminal([]), false);
  assert.equal(areSubmittedAnalysesTerminal([{}]), false);
  assert.equal(areSubmittedAnalysesTerminal([{ isVoided: true }, { attemptStatus: "Completed" }]), true);
});
test("actual page connects terminal checks and bounded assignment refresh, not just the last job", () => {
  const page = fs.readFileSync(new URL("../src/pages/LearningPlayerPage.tsx", import.meta.url), "utf8");
  assert.match(page, /isAssignmentSubmitted && areSubmittedAnalysesTerminal\(assignmentQuestions\)/);
  assert.match(page, /current\?\.kind === "waiting" \? null : current/);
  assert.match(page, /failures < 5/);
  const hierarchy = fs.readFileSync(new URL("../src/components/student/AttemptFeedbackHierarchy.tsx", import.meta.url), "utf8");
  assert.match(hierarchy, /getAIProcessingNotice\(feedbackData, scoreAndFeedbackOnly\)/);
  assert.doesNotMatch(hierarchy, /AI hiện đang tạm thời không khả dụng/);
});
