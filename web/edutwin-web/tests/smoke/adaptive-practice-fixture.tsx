import { createRoot } from "react-dom/client";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AdaptivePracticeOverviewPage } from "../../src/pages/AdaptivePracticeOverviewPage";
import { useAuthStore } from "../../src/stores/authStore";
import { httpClient } from "../../src/api/httpClient";
import type { AdaptivePracticeProposal, AdaptivePracticeTopicOption } from "../../src/types/adaptivePractice";
import "../../src/index.css";

// All requests are intercepted locally. This fixture never reads or writes live data.
const state = new URLSearchParams(window.location.search).get("state");
const topics: AdaptivePracticeTopicOption[] = [
  { topicNodeId: "function", topicName: "Hàm số", currentMastery: 61, hasPrerequisites: false, canPractice: false, availableQuestionCount: 0, unavailableReason: "Câu hỏi đang dùng trong bài tập chưa đóng hoặc chưa hết hạn." },
  { topicNodeId: "quadratic", topicName: "Hàm số và phương trình bậc hai", currentMastery: 80, hasPrerequisites: false, canPractice: true, availableQuestionCount: 9 },
  { topicNodeId: "vector", topicName: "Vectơ và tọa độ trong mặt phẳng", currentMastery: 78, hasPrerequisites: false, canPractice: true, availableQuestionCount: 6 },
];
if (state === "empty") for (const topic of topics) {
  topic.canPractice = false;
  topic.availableQuestionCount = 0;
  topic.unavailableReason = "Chưa có câu hỏi luyện tập hợp lệ cho chủ đề này trong khối đang học.";
}
httpClient.defaults.adapter = async config => {
  let data: unknown;
  const url = config.url ?? "";
  if (config.method === "post" && url === "/adaptive-practice/sessions") {
    const request = JSON.parse(config.data);
    if (request.topicNodeId === "function") throw new Error("Blocked topic must never start");
    data = { sessionId: "fixture-session" };
  } else if (config.method !== "get") throw new Error("Unexpected mutation in isolated fixture");
  else if (url.endsWith("/academic-context")) data = { data: { selectedClassId: "class-a", isHistory: false,
    classes: [{ classId: "class-a", className: "Toán 10", subjectId: "math", gradeLevel: 10, isHistorical: false }],
    curriculums: [{ curriculumId: "curriculum-a", classId: "class-a", title: "Toán 10", applicationRole: "Primary" }] } };
  else if (url === "/adaptive-practice/active-session") data = null;
  else if (url === "/adaptive-practice/history" || url.endsWith("/question-bookmarks")) data = [];
  else if (url === "/adaptive-practice/proposal") {
    const topic = topics.find(t => t.topicNodeId === config.params?.customTopicId) ?? topics[2];
    data = { subjectId: "math", subjectName: "Toán", topicNodeId: topic.topicNodeId, topicName: topic.topicName,
      currentMastery: topic.currentMastery, targetMastery: 93, focusSkillDescription: "Củng cố chủ đề",
      selectionReason: config.params?.mode === "CustomTopic" ? "Học sinh chủ động chọn chủ đề." : "Hàm số 61% tạm chưa luyện được. Đề xuất Vectơ để luyện thay thế, duy trì năng lực đã vững 75%.",
      recommendedTimeMinutes: 15, isDiagnosticRecommended: false, defaultMode: config.params?.mode ?? "Weakness",
      availableQuestionCount: topic.availableQuestionCount, availableTopics: topics, unavailableReason: topic.unavailableReason,
    } satisfies AdaptivePracticeProposal;
  } else throw new Error(`Fixture does not implement ${url}`);
  return { data, status: 200, statusText: "OK", headers: {}, config };
};
useAuthStore.setState({ sessionStatus: "authenticated", user: { userId: "fixture", centerId: "fixture-center", centerName: "Dữ liệu kiểm thử", username: "fixture", displayName: "Học sinh kiểm thử", accountType: "Student", role: "Student", roles: [], authorizationVersion: 1, permissions: ["learning.attempts.submit", "recommendations.student.read_own", "recommendations.student.update_own"] } });
const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={queryClient}><MemoryRouter initialEntries={["/hoc-tap/luyen-tap?subjectId=math&classId=class-a"]}><Routes><Route path="/hoc-tap/luyen-tap" element={<AdaptivePracticeOverviewPage />} /><Route path="/hoc-tap/luyen-tap/phien/:id" element={<p>Phiên kiểm thử đã bắt đầu</p>} /></Routes></MemoryRouter></QueryClientProvider>);
