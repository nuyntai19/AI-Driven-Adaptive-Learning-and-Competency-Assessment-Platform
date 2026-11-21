import { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import { TeacherListPage } from "../../src/pages/TeacherListPage";
import { ClassListPage } from "../../src/pages/ClassListPage";
import { organizationApi } from "../../src/api/organizationApi";
import { useAuthStore } from "../../src/stores/authStore";
import { permissions } from "../../src/auth/permissions";
import type { CreateTeacherRequest, CreateClassRequest, TeacherListParams } from "../../src/types/organization";
import "../../src/index.css";

const state = { capturedTeachers: [] as CreateTeacherRequest[], capturedClasses: [] as CreateClassRequest[], teacherQueries: [] as TeacherListParams[] };
Object.assign(window, { smokeState: state });
const subjects = [
  { subjectId: "math", subjectName: "Toán", subjectCode: "MATH", isActive: true, description: null, rowVersion: "1" },
  { subjectId: "eng", subjectName: "Tiếng Anh", subjectCode: "ENG", isActive: true, description: null, rowVersion: "1" },
  { subjectId: "chem", subjectName: "Hóa học", subjectCode: "CHEM", isActive: true, description: null, rowVersion: "1" },
  { subjectId: "inactive", subjectName: "Môn ngừng hoạt động", subjectCode: "OLD", isActive: false, description: null, rowVersion: "1" },
];
const teacher = (id: string, subjectId: string, name: string) => ({ teacherId: id, subjectId, displayName: name, username: id,
  department: subjects.find(s => s.subjectId === subjectId)!.subjectName, status: "Active" as const, classCount: 0, rowVersion: "1" });
const math = teacher("teacher.math", "math", "Giáo viên Toán");
const math2 = teacher("teacher.math2", "math", "Giáo viên Toán 2");
const eng = teacher("teacher.english", "eng", "Giáo viên Tiếng Anh");
eng.classCount = 3;
const meta = { traceId: "smoke", timestamp: new Date().toISOString() };
const classDto = { classId: "math-class", className: "Lớp Toán", academicYear: "2026-2027", gradeLevel: 10,
  subject: { subjectId: "math", subjectName: "Toán" }, teacher: { teacherId: math.teacherId, displayName: math.displayName }, studentCount: 0, status: "Active" as const, rowVersion: "1" };
organizationApi.listSubjects = async (active) => ({ data: subjects.filter(s => active === undefined || s.isActive === active), meta });
organizationApi.listTeachers = async (params) => {
  state.teacherQueries.push({ ...params });
  const data = params.subjectId === "math" ? params.page === 1 ? [math] : [math2] : params.subjectId === "eng" ? [eng] : params.subjectId ? [] : [math, eng];
  return { data, meta: { ...meta, page: params.page, pageSize: params.pageSize, totalItems: params.subjectId === "math" ? 2 : data.length, totalPages: params.subjectId === "math" ? 2 : data.length ? 1 : 0 } };
};
organizationApi.createTeacher = async (request) => { state.capturedTeachers.push({ ...request }); return teacher("new.teacher", request.subjectId, request.displayName); };
organizationApi.updateTeacher = async () => { throw { response: { status: 422, data: {
  errorCode: "TEACHER_SUBJECT_CHANGE_BLOCKED", traceId: "subject-change-support",
  detail: "Không thể đổi bộ môn vì giáo viên còn phụ trách lớp đang hoạt động thuộc môn khác.",
} } }; };
organizationApi.listClasses = async () => ({ data: [classDto], meta: { ...meta, page: 1, pageSize: 10, totalItems: 1, totalPages: 1 } });
organizationApi.createClass = async (request) => { state.capturedClasses.push({ ...request }); return classDto; };
useAuthStore.setState({ accessToken: "smoke", sessionStatus: "authenticated", user: { userId: "manager", centerId: "center", centerName: "Center", username: "manager", displayName: "Manager", accountType: "CenterManager", role: "CenterManager", roles: [], authorizationVersion: 1, permissions: Object.values(permissions) } });
export function Fixture() {
  const [view, setView] = useState("teachers");
  return <><button id="switch-classes" onClick={() => setView("classes")}>Lớp học</button>{view === "teachers" ? <TeacherListPage /> : <ClassListPage />}</>;
}
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><BrowserRouter><Fixture /></BrowserRouter></QueryClientProvider>);
