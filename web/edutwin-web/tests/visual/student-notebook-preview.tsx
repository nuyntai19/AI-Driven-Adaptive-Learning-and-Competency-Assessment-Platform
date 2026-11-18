import { createRoot } from "react-dom/client";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StudentLayout } from "../../src/layouts/StudentLayout";
import { StudentNotebookPage } from "../../src/pages/StudentNotebookPage";
import { AdaptivePracticeOverviewPage } from "../../src/pages/AdaptivePracticeOverviewPage";
import { useAuthStore } from "../../src/stores/authStore";
import { httpClient } from "../../src/api/httpClient";
import type { LearningInsightsDto } from "../../src/types/learningPath";
import "../../src/index.css";

// Isolated visual fixture: no request reaches the backend and no production entry imports this file.
const scenario = new URLSearchParams(window.location.search);
const date = new Date().toISOString();
const context = { selectedClassId: "class-a", isHistory: false, message: null,
  classes: [{ classId: "class-a", className: "12A · Toán nâng cao", subjectId: "math", gradeLevel: 12, isHistorical: false }],
  curriculums: [{ curriculumId: "demo", classId: "class-a", title: "Toán 12", applicationRole: "Primary" }] };
const insights: LearningInsightsDto = { windowDays: 30, answerCount: 34, assignmentCount: 4, gradedQuestionCount: 24, pendingCount: 2,
  summary: "Nên ưu tiên Đạo hàm và Tích phân. Đề xuất dựa trên câu đã làm; bạn có thể chọn phần muốn luyện thêm.", suggestedSkillIds: ["derivative"], skills: [
    { skillId: "derivative", skillName: "Đạo hàm", questionCount: 10, correctCount: 6, wrongCount: 4, observation: "Đúng 6/10 câu gần nhất; 4 câu cần xem lại.", confirmedMisconceptions: [], recentAnswers: [
      { attemptId: "a1", questionText: "Tìm đạo hàm của hàm số $f(x) = x^3 - 3x + 1$.", isCorrect: false, submittedAt: date, guided: false },
      { attemptId: "a2", questionText: "Giải thích quy tắc đạo hàm của một tích.", isCorrect: null, submittedAt: date, guided: true },
      { attemptId: "a3", questionText: "Tính đạo hàm của hàm số $y = x^2$.", isCorrect: true, submittedAt: date, guided: false },
    ] },
    { skillId: "integral", skillName: "Tích phân", questionCount: 8, correctCount: 6, wrongCount: 2, observation: "Đúng 6/8 câu; cần xem lại cách đặt biến.", confirmedMisconceptions: [], recentAnswers: [{ attemptId: "a4", questionText: "Tính tích phân $\\int_0^1 2x \\, dx$.", isCorrect: false, submittedAt: date, guided: true }] },
    { skillId: "function", skillName: "Hàm số và đồ thị", questionCount: 6, correctCount: 6, wrongCount: 0, observation: "Đúng 6/6 câu.", confirmedMisconceptions: [], recentAnswers: [] },
  ] };
const bookmarks = [
  { questionId: "q1", subjectId: "math", answered: true, questionText: "Tìm đạo hàm của $f(x) = x^3 - 3x + 1$.", topicName: "Đạo hàm", correctAnswer: "$f'(x) = 3x^2 - 3$", solution: "Dùng quy tắc đạo hàm của tổng và hiệu. Hãy thử kiểm tra lại từng hạng tử.", createdAt: date },
  { questionId: "q2", subjectId: "math", answered: true, questionText: "Tính $\\int_0^1 2x \\, dx$.", topicName: "Tích phân", correctAnswer: "$1$", solution: "Một nguyên hàm của 2x là x². Thay hai cận 1 và 0.", createdAt: date },
  { questionId: "q3", subjectId: "math", answered: true, questionText: "Câu đã lưu trong bài chưa kết thúc.", topicName: "Hàm số và đồ thị", correctAnswer: null, solution: null, createdAt: date },
];
httpClient.defaults.adapter = async config => {
  if (config.method !== "get") throw new Error("Fixture chỉ đọc: không gửi bài hoặc tạo phiên trên backend.");
  const url = config.url ?? "";
  let data: unknown;
  if (url === "/subjects") data = { data: [{ subjectId: "math", subjectName: "Toán học", isActive: true }], meta: {} };
  else if (url.endsWith("/academic-context")) data = { data: context };
  else if (url.endsWith("/workspace-summary")) data = { data: { assignmentCount: 3, dailyStreak: 5, studiedToday: true, localDate: date.slice(0, 10), timezone: "Asia/Ho_Chi_Minh", generatedAt: date } };
  else if (url.endsWith("/learning-agenda")) {
    if (scenario.get("state") === "error") throw new Error("Lỗi mạng mẫu");
    data = { ongoingRuns: [], missions: [], todayPlan: [], exams: [], insights: scenario.get("state") === "empty" ? { ...insights, answerCount: 0, gradedQuestionCount: 0, pendingCount: 0, summary: "", skills: [] } : insights };
  }
  else if (url.endsWith("/question-bookmarks")) data = scenario.get("state") === "empty" ? [] : bookmarks;
  else if (url === "/adaptive-practice/active-session") data = null;
  else if (url === "/adaptive-practice/history") data = [];
  else if (url === "/adaptive-practice/proposal") data = { subjectId: "math", subjectName: "Toán học", topicNodeId: config.params?.customTopicId || "derivative", topicName: config.params?.customTopicId === "integral" ? "Tích phân" : "Đạo hàm", currentMastery: 56, targetMastery: 70, selectionReason: "Chủ đề có 4 câu cần xem lại trong bài làm gần đây. Phiên ngắn giúp bạn củng cố đúng phần này.", recommendedTimeMinutes: 10, isDiagnosticRecommended: false, defaultMode: "Weakness", availableQuestionCount: 20, availableTopics: [{ topicNodeId: "derivative", topicName: "Đạo hàm", currentMastery: 56 }, { topicNodeId: "integral", topicName: "Tích phân", currentMastery: 65 }] };
  else throw new Error(`Fixture không cung cấp ${url}`);
  return { data, status: 200, statusText: "OK", headers: {}, config };
};
useAuthStore.setState({ sessionStatus: "authenticated", user: { userId: "qa-notebook", centerId: "qa", centerName: "Trung tâm dữ liệu mẫu", username: "demo", displayName: "Nguyễn Minh Anh", accountType: "Student", role: "Student", roles: [], authorizationVersion: 1, permissions: ["learning.attempts.submit", "recommendations.student.read_own", "recommendations.student.update_own"] } });
const client = new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } });
const initial = scenario.get("surface") === "practice" ? "/hoc-tap/luyen-tap?subjectId=math&classId=class-a&topicId=derivative&minutes=10" : `/hoc-tap/so-tay?subjectId=math&classId=class-a${scenario.get("history") === "true" ? "&history=true" : ""}`;
function FixtureNotice() { return <div style={{ padding: "8px 20px", fontSize: 11, background: "#243d4c", color: "#fff", display: "flex", justifyContent: "space-between", gap: 16 }}><span>KIỂM TRA GIAO DIỆN · DỮ LIỆU MẪU · KHÔNG KẾT NỐI BACKEND</span><button type="button" onClick={() => document.documentElement.classList.toggle("dark")}>Đổi sáng / tối</button></div>; }
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={client}><FixtureNotice /><MemoryRouter initialEntries={[initial]}><Routes><Route element={<StudentLayout />}><Route path="/hoc-tap/so-tay" element={<StudentNotebookPage />} /><Route path="/hoc-tap/luyen-tap" element={<AdaptivePracticeOverviewPage />} /><Route path="*" element={<div className="p-8">Liên kết mẫu đã mở đúng tuyến. Chọn Sổ tay hoặc Luyện tập để tiếp tục kiểm tra.</div>} /></Route></Routes></MemoryRouter></QueryClientProvider>);
