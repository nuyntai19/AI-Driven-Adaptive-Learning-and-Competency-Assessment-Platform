import assert from "node:assert/strict";
import test from "node:test";
import { notebookAnswers, notebookFocusSkills, notebookPracticeUrl, reviewableBookmarks, emptyRecallSession, recallSessionReducer } from "../src/utils/learningNotebook.ts";
import { readPracticePreferences } from "../src/utils/adaptivePracticePreferences.ts";
import type { LearningInsightsDto } from "../src/types/learningPath.ts";
import type { QuestionBookmarkDto } from "../src/api/questionBookmarksApi.ts";

const insight: LearningInsightsDto = {
  windowDays: 30, answerCount: 7, assignmentCount: 1, gradedQuestionCount: 3, pendingCount: 1, summary: "", suggestedSkillIds: [],
  skills: [{ skillId: "derivative", skillName: "Đạo hàm", questionCount: 2, correctCount: 1, wrongCount: 1, observation: "", confirmedMisconceptions: [], recentAnswers: [
    { attemptId: "wrong", questionText: "Tính đạo hàm", isCorrect: false, submittedAt: "2026-11-18T08:00:00Z", guided: false },
    { attemptId: "pending", questionText: "Đang chờ", isCorrect: null, submittedAt: "2026-11-18T09:00:00Z", guided: true },
    { attemptId: "right", questionText: "Đã làm đúng", isCorrect: true, submittedAt: "2026-11-17T08:00:00Z", guided: true },
  ] }],
};

test("notebook keeps unknown judgments out of the incorrect queue and preserves guided provenance", () => {
  assert.deepEqual(notebookAnswers(insight, "incorrect", "dao ham").map(a => a.attemptId), ["wrong"]);
  const pending = notebookAnswers(insight, "pending", "");
  assert.equal(pending[0].attemptId, "pending");
  assert.equal(pending[0].guided, true);
  assert.equal(notebookAnswers(insight, "correct", "")[0].attemptId, "right");
  assert.equal(notebookAnswers(insight, "all", "không có").length, 0);
});

test("notebook prioritizes observed error rate without inventing weakness for unassessed skills", () => {
  const data = { ...insight, skills: [...insight.skills,
    { ...insight.skills[0], skillId: "unknown", wrongCount: 0, questionCount: 0 },
    { ...insight.skills[0], skillId: "priority", wrongCount: 3, questionCount: 4 },
  ] };
  assert.deepEqual(notebookFocusSkills(data).map(s => s.skillId), ["priority", "derivative"]);
  assert.equal(insight.skills[0].skillId, "derivative");
});

test("recall excludes another subject, unanswered questions and answers withheld by the server", () => {
  const base: QuestionBookmarkDto = { questionId: "ok", subjectId: "math", answered: true, questionText: "A question", topicName: "Topic", correctAnswer: "Answer", solution: null, createdAt: "2026-11-18" };
  const result = reviewableBookmarks([base, { ...base, questionId: "other", subjectId: "english" }, { ...base, questionId: "unanswered", answered: false }, { ...base, questionId: "withheld", correctAnswer: null }, { ...base, questionId: "blank", correctAnswer: " " }], "math");
  assert.deepEqual(result.map(c => c.questionId), ["ok"]);
});

test("recall cannot claim remembered before reveal; repeat moves to tail and keeps progress honest", () => {
  let state = recallSessionReducer(emptyRecallSession, { type: "start", ids: ["a", "b", "a"] });
  assert.equal(state.total, 2);
  assert.equal(recallSessionReducer(state, { type: "remember" }).remembered, 0);
  state = recallSessionReducer(state, { type: "reveal" });
  state = recallSessionReducer(state, { type: "again" });
  assert.deepEqual(state.queue, ["b", "a"]);
  assert.equal(state.remembered, 0);
  assert.equal(state.revealed, false);
  state = recallSessionReducer(state, { type: "skip" });
  state = recallSessionReducer(state, { type: "reveal" });
  state = recallSessionReducer(state, { type: "remember" });
  assert.deepEqual(state.queue, []);
  assert.equal(state.remembered, 1);
  assert.equal(state.skipped, 1);
  assert.equal(recallSessionReducer(state, { type: "remember" }).remembered, 1);
});

test("recall sessions stay bounded and a new start resets the previous review", () => {
  const started = recallSessionReducer({ ...emptyRecallSession, remembered: 5 }, { type: "start", ids: Array.from({length: 20}, (_, i) => String(i)) });
  assert.equal(started.total, 10);
  assert.equal(started.remembered, 0);
  assert.deepEqual(recallSessionReducer(started, { type: "start", ids: [] }), emptyRecallSession);
});

test("a notebook practice link preserves academic context, encodes the topic and really prefills ten minutes", () => {
  const params = new URLSearchParams("subjectId=math&classId=class-a&history=true&sessionId=old");
  const link = new URL(notebookPracticeUrl(params, "topic/a & b", 10), "https://local.test");
  assert.equal(link.searchParams.get("classId"), "class-a");
  assert.equal(link.searchParams.get("history"), "true");
  assert.equal(link.searchParams.has("sessionId"), false);
  assert.deepEqual(readPracticePreferences(link.searchParams), { mode: "CustomTopic", topicId: "topic/a & b", minutes: 10 });
});

test("invalid practice preferences fall back safely and a non-custom mode drops a stale topic", () => {
  assert.deepEqual(readPracticePreferences(new URLSearchParams("mode=INVALID&minutes=1000")), { mode: "Weakness", topicId: "", minutes: 15 });
  assert.deepEqual(readPracticePreferences(new URLSearchParams("mode=SpacedReview&topicId=stale&minutes=20")), { mode: "SpacedReview", topicId: "", minutes: 20 });
});
import { studentScopeChangeUrl } from "../src/utils/studentAcademicNavigation.ts";

test("switching academic scope drops practice hints that belong to the old subject or class", () => {
  const url = studentScopeChangeUrl("/hoc-tap/luyen-tap", new URLSearchParams("subjectId=english&classId=class-b&topicId=old-math&minutes=10&mode=CustomTopic"));
  assert.equal(url, "/hoc-tap/luyen-tap?subjectId=english&classId=class-b");
});
import { canStartPractice } from "../src/utils/adaptivePracticePreferences.ts";

test("practice creation waits for both academic context and a successful active-session check", () => {
  const ready = { classResolved: true, proposalReady: true, proposalUnavailable: false, activeSessionChecked: true, hasActiveSession: false };
  assert.equal(canStartPractice(ready), true);
  assert.equal(canStartPractice({ ...ready, activeSessionChecked: false }), false);
  assert.equal(canStartPractice({ ...ready, classResolved: false }), false);
  assert.equal(canStartPractice({ ...ready, proposalReady: false }), false);
  assert.equal(canStartPractice({ ...ready, proposalUnavailable: true }), false);
  assert.equal(canStartPractice({ ...ready, hasActiveSession: true }), false);
});
