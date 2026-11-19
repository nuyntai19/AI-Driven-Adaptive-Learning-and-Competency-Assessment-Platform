import { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import { SubjectListPage } from "../../src/pages/SubjectListPage";
import { organizationApi } from "../../src/api/organizationApi";
import { useAuthStore } from "../../src/stores/authStore";
import { permissions } from "../../src/auth/permissions";
import { useStudentSubjects } from "../../src/utils/useStudentSubjects";
import type { SubjectDto, UpdateSubjectRequest } from "../../src/types/organization";
import "../../src/index.css";

const subjects: SubjectDto[] = [
  { subjectId: "math", subjectCode: "MATH", subjectName: "Toán", isActive: true, activeClassCount: 4, description: null, rowVersion: "1" },
  { subjectId: "eng", subjectCode: "ENG", subjectName: "Tiếng Anh", isActive: true, activeClassCount: 0, description: null, rowVersion: "1" },
  { subjectId: "physics", subjectCode: "PHY", subjectName: "Vật lý", isActive: false, activeClassCount: 0, description: null, rowVersion: "1" },
  { subjectId: "race", subjectCode: "CHEM", subjectName: "Hóa học", isActive: true, activeClassCount: 0, description: null, rowVersion: "1" },
  { subjectId: "unrelated", subjectCode: "OTHER", subjectName: "Môn của học sinh khác", isActive: false, activeClassCount: 0, description: null, rowVersion: "1" },
];
const state = { updates: [] as Array<{ id: string; request: UpdateSubjectRequest }>, subjectQueries: [] as Array<boolean | undefined> };
Object.assign(window, { lifecycleState: state });
const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
client.setQueryData(["student-academic-context", "center", "student", "", "", false], {
  classes: [{ subjectId: "math", isHistorical: false }, { subjectId: "eng", isHistorical: true }],
});
organizationApi.listSubjects = async active => {
  state.subjectQueries.push(active);
  return { data: subjects.filter(s => active === undefined || s.isActive === active).map(s => ({ ...s })), meta: { traceId: "smoke", timestamp: new Date().toISOString() } };
};
organizationApi.getSubject = async id => ({ ...subjects.find(s => s.subjectId === id)! });
organizationApi.updateSubject = async (id, request) => {
  state.updates.push({ id, request: { ...request } });
  if (id === "race" && request.isActive === false) {
    throw Object.assign(new Error("Active class conflict"), { isAxiosError: true, response: { status: 409, data: {
      errorCode: "INVALID_STATE_TRANSITION", activeClassCount: 1,
      detail: "Không thể ngừng giảng dạy môn Hóa học vì còn 1 lớp đang hoạt động. Vui lòng kết thúc hoặc lưu trữ các lớp này trước.",
    } } });
  }
  const subject = subjects.find(s => s.subjectId === id)!;
  Object.assign(subject, request);
  return { ...subject };
};
const manager = { userId: "manager", centerId: "center", centerName: "Center", username: "manager", displayName: "Manager", accountType: "CenterManager" as const,
  role: "CenterManager" as const, roles: [], authorizationVersion: 1, permissions: Object.values(permissions) };
useAuthStore.setState({ user: manager, sessionStatus: "authenticated", accessToken: "fixture" });
function StudentProbe() {
  const [history, setHistory] = useState(false);
  const query = useStudentSubjects(history);
  return <div><button id="view-history" onClick={() => setHistory(true)}>Lớp đã kết thúc</button><button id="view-current" onClick={() => setHistory(false)}>Lớp đang học</button>
    <p id="student-subjects">{query.isLoading ? "loading" : query.subjects.map(s => s.subjectName).join(",")}</p></div>;
}
export function Fixture() {
  const [student, setStudent] = useState(false);
  return <><button id="student-probe" onClick={() => {
    useAuthStore.setState({ user: { ...manager, userId: "student", accountType: "Student", role: "Student" } });
    setStudent(true);
  }}>Học sinh</button>{student ? <StudentProbe /> : <SubjectListPage />}</>;
}
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={client}><BrowserRouter><Fixture /></BrowserRouter></QueryClientProvider>);
