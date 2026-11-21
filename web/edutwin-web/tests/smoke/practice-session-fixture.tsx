import { createRoot } from "react-dom/client";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AdaptivePracticeSessionPage } from "../../src/pages/AdaptivePracticeSessionPage";
import { AdaptivePracticeOverviewPage } from "../../src/pages/AdaptivePracticeOverviewPage";
import { useAuthStore } from "../../src/stores/authStore";
import { httpClient } from "../../src/api/httpClient";
import type { AdaptivePracticeSession, AdaptivePracticeQuestion } from "../../src/types/adaptivePractice";
import "../../src/index.css";

// Stateful, isolated server simulation. No requests leave this adapter.
const params = new URLSearchParams(location.search);
const session: AdaptivePracticeSession = {
  sessionId: "pause-fixture", centerId: "fixture-center", studentId: "fixture-student", classId: "class-a",
  subjectId: "math", subjectName: "Toán", subjectCode: "MATH", topicNodeId: "quadratic",
  topicName: "Hàm số và phương trình bậc hai", practiceMode: "Weakness", startingMastery: 74,
  currentMastery: 74, targetMastery: 89, timeBudgetMinutes: 15, minimumQuestions: 5, maximumQuestions: 9,
  questionCount: 1, status: "Paused", selectionReason: "Củng cố kỹ năng", elapsedSeconds: 34,
  pausedAt: new Date().toISOString(), isDiagnosticMode: false, currentDifficulty: 2,
};
const question: AdaptivePracticeQuestion = {
  sessionItemId: "item-2", questionId: "q-2", topicNodeId: "quadratic", topicName: session.topicName,
  difficulty: 2, questionText: "Giải bất phương trình x² − x − 6 < 0. Viết khoảng nghiệm theo dạng (a;b).",
  questionType: params.has("mcq") ? "MultipleChoice" : "ShortAnswer", answerEvaluationMode: "TextExact",
  options: params.has("mcq") ? [{ optionId: "a", label: "A", text: "(-2;3)", orderIndex: 1 }, { optionId: "b", label: "B", text: "(2;3)", orderIndex: 2 }] : [],
  hasImage: false, maxScore: 1, estimatedTimeSeconds: 60, selectionReasonCode: "GAP_RECOVERY",
  selectionReasonText: "Luyện tập củng cố kỹ năng trọng tâm.", sequenceNumber: 2,
  totalQuestionsEstimatedMin: 5, totalQuestionsEstimatedMax: 9, availableHintsCount: 3, hintsUsedCount: 0, isPrerequisiteCheck: false,
};
const controls = { failPause: false, failResume: false, failQuestion: false, emptyQuestion: false,
  pauses: 0, resumes: 0, questions: 0, requestsWhilePaused: 0, submits: 0, lastSubmission: null as unknown };
Object.assign(window, { practiceSessionFixture: controls });
const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));
httpClient.defaults.adapter = async config => {
  const url = config.url ?? "";
  let data: unknown;
  if (url.endsWith("/pause")) {
    controls.pauses++;
    await delay(80);
    if (controls.failPause) throw new Error("Pause unavailable");
    session.status = "Paused";
    session.pausedAt = new Date().toISOString();
    data = { ...session };
  } else if (url.endsWith("/resume")) {
    controls.resumes++;
    await delay(80);
    if (controls.failResume) throw new Error("Resume unavailable");
    session.status = "InProgress";
    session.pausedAt = null;
    data = { ...session };
  } else if (url.endsWith("/next-question")) {
    controls.questions++;
    const paused = session.status === "Paused";
    if (paused) controls.requestsWhilePaused++;
    // A paused request can finish after resume. Caching this empty response caused the blank screen.
    await delay(paused ? 700 : 50);
    if (controls.failQuestion) throw new Error("Question unavailable");
    data = paused || controls.emptyQuestion ? null : { ...question };
  } else if (url.endsWith("/sessions/pause-fixture")) {
    const snapshot = { ...session };
    await delay(40);
    data = snapshot;
  } else if (url.endsWith("/submit")) {
    controls.submits++;
    controls.lastSubmission = JSON.parse(config.data);
    data = { isCorrect: true, awardedScore: 1, correctAnswer: "(-2;3)", solutionExplanation: "Nghiệm là (-2;3).",
      hintsUsed: 0, nextActionRecommendation: "Tiếp tục", isSessionClosed: false, updatedEstimatedMastery: 76 };
  } else if (url.endsWith("/academic-context")) data = { data: { selectedClassId: "class-a", isHistory: false,
    classes: [{ classId: "class-a", subjectId: "math", className: "Toán 10", isHistorical: false }],
    curriculums: [{ curriculumId: "curriculum", classId: "class-a", applicationRole: "Primary" }] } };
  else if (url === "/adaptive-practice/active-session") data = { ...session };
  else if (url.endsWith("/history") || url.endsWith("/question-bookmarks")) data = [];
  else if (url === "/adaptive-practice/proposal") data = { subjectId: "math", subjectName: "Toán", topicNodeId: "quadratic", topicName: session.topicName,
    currentMastery: 74, targetMastery: 89, selectionReason: "Củng cố chủ đề", recommendedTimeMinutes: 15,
    isDiagnosticRecommended: false, defaultMode: "Weakness", availableQuestionCount: 9, availableTopics: [] };
  else throw new Error(`Unexpected fixture request ${url}`);
  return { data, status: 200, statusText: "OK", headers: {}, config };
};
useAuthStore.setState({ sessionStatus: "authenticated", user: { userId: "fixture-student", centerId: "fixture-center", centerName: "Kiểm thử", username: "fixture", displayName: "Học sinh", accountType: "Student", role: "Student", roles: [], authorizationVersion: 1,
  permissions: ["learning.attempts.submit", "recommendations.student.read_own", "recommendations.student.update_own"] } });
const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
// Deliberately stale caches reproduce returning from the overview after a second pause.
queryClient.setQueryData(["adaptive-practice", "session", session.sessionId], { ...session, status: "InProgress" });
queryClient.setQueryData(["adaptive-practice", "question", session.sessionId], null);
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={queryClient}><MemoryRouter initialEntries={["/hoc-tap/luyen-tap/phien/pause-fixture"]}><Routes>
  <Route path="/hoc-tap/luyen-tap/phien/:sessionId" element={<AdaptivePracticeSessionPage />} />
  <Route path="/hoc-tap/luyen-tap" element={<AdaptivePracticeOverviewPage />} />
</Routes></MemoryRouter></QueryClientProvider>);
