import { lazy, Suspense, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import { AuthBootstrap } from "./auth/AuthBootstrap";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import { LoginPage } from "./pages/LoginPage";
import { PermissionRoute } from "./routes/PermissionRoute";
import { permissions, authorizationUiPermissions } from "./auth/permissions";
import { AccessDeniedPage } from "./pages/AccessDeniedPage";
import { PlatformLayout } from "./layouts/PlatformLayout";
import { RouteChunkBoundary, RouteLoadingFallback } from "./routes/RouteChunkBoundary";
import { useAuthStore } from "./stores/authStore";
import { cleanupExpiredScratchpadDrafts } from "./utils/scratchpadStorage";

const AuthenticatedHomePage = lazy(() => import("./pages/AuthenticatedHomePage").then((module) => ({ default: module.AuthenticatedHomePage })));
const TeacherListPage = lazy(() => import("./pages/TeacherListPage").then((module) => ({ default: module.TeacherListPage })));
const ClassListPage = lazy(() => import("./pages/ClassListPage").then((module) => ({ default: module.ClassListPage })));
const StudentListPage = lazy(() => import("./pages/StudentListPage").then((module) => ({ default: module.StudentListPage })));
const SubjectListPage = lazy(() => import("./pages/SubjectListPage").then((module) => ({ default: module.SubjectListPage })));
const KnowledgeGraphPage = lazy(() => import("./pages/KnowledgeGraphPage").then((module) => ({ default: module.KnowledgeGraphPage })));
const CurriculumListPage = lazy(() => import("./pages/CurriculumListPage").then((module) => ({ default: module.CurriculumListPage })));
const CurriculumEditorPage = lazy(() => import("./pages/CurriculumEditorPage").then((module) => ({ default: module.CurriculumEditorPage })));
const QuestionBankPage = lazy(() => import("./pages/QuestionBankPage").then((module) => ({ default: module.QuestionBankPage })));
const QuestionEditorPage = lazy(() => import("./pages/QuestionEditorPage").then((module) => ({ default: module.QuestionEditorPage })));
const AssignmentListPage = lazy(() => import("./pages/AssignmentListPage").then((module) => ({ default: module.AssignmentListPage })));
const AssignmentEditorPage = lazy(() => import("./pages/AssignmentEditorPage").then((module) => ({ default: module.AssignmentEditorPage })));
const AssignmentProgressPage = lazy(() => import("./pages/AssignmentProgressPage").then((module) => ({ default: module.AssignmentProgressPage })));
const StudentAssignmentsPage = lazy(() => import("./pages/StudentAssignmentsPage").then((module) => ({ default: module.StudentAssignmentsPage })));
const StudentAssignmentDetailPage = lazy(() => import("./pages/StudentAssignmentDetailPage").then((module) => ({ default: module.StudentAssignmentDetailPage })));
const StudentDashboardPage = lazy(() => import("./pages/StudentDashboardPage").then((module) => ({ default: module.StudentDashboardPage })));
const StudentTwinPage = lazy(() => import("./pages/StudentTwinPage").then((module) => ({ default: module.StudentTwinPage })));
const LearningPlayerPage = lazy(() => import("./pages/LearningPlayerPage").then((module) => ({ default: module.LearningPlayerPage })));
const TeacherClassDashboardPage = lazy(() => import("./pages/TeacherClassDashboardPage").then((module) => ({ default: module.TeacherClassDashboardPage })));
const ReviewQueuePage = lazy(() => import("./pages/ReviewQueuePage").then((module) => ({ default: module.ReviewQueuePage })));
const TeacherStudentTwinPage = lazy(() => import("./pages/TeacherStudentTwinPage").then((module) => ({ default: module.TeacherStudentTwinPage })));
import { StudentLayout } from "./layouts/StudentLayout";

const CenterDashboardPage = lazy(() => import("./pages/CenterDashboardPage").then((module) => ({ default: module.CenterDashboardPage })));
const CenterProfilePage = lazy(() => import("./pages/CenterProfilePage").then((module) => ({ default: module.CenterProfilePage })));
const AuthorizationManagementPage = lazy(() => import("./pages/AuthorizationManagementPage").then((module) => ({ default: module.AuthorizationManagementPage })));
const PlatformCentersPage = lazy(() => import("./pages/PlatformCentersPage").then((module) => ({ default: module.PlatformCentersPage })));
const PlatformAuditLogsPage = lazy(() => import("./pages/PlatformAuditLogsPage").then((module) => ({ default: module.PlatformAuditLogsPage })));
const CenterManagerLayoutBoundary = lazy(() => import("./layouts/CenterManagerLayout").then((module) => ({ default: module.CenterManagerLayoutBoundary })));

const FallbackRoute = () => {
  const sessionStatus = useAuthStore((state) => state.sessionStatus);
  if (sessionStatus === "authenticated") {
    return <Navigate to="/" replace />;
  }
  return <Navigate to="/dang-nhap" replace />;
};

function App() {
  useEffect(() => {
    // Expired drafts contain only local vector data, so TTL cleanup is safe before auth bootstraps.
    void cleanupExpiredScratchpadDrafts();
  }, []);

  return (
    <AuthBootstrap>
      <RouteChunkBoundary>
        <Suspense fallback={<RouteLoadingFallback />}>
          <Routes>
        <Route path="/dang-nhap" element={<LoginPage />} />

        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<AuthenticatedHomePage />} />
          <Route path="/khong-co-quyen" element={<AccessDeniedPage />} />

          {/* Student Portal Experiences */}
          <Route element={<StudentLayout />}>
            <Route element={<PermissionRoute allOf={[permissions.dashboardsStudentRead]} />}>
              <Route path="/hoc-tap/tong-quan" element={<StudentDashboardPage />} />
            </Route>
            <Route element={<PermissionRoute allOf={[permissions.twinStudentReadOwn]} />}>
              <Route path="/hoc-tap/ho-so-nang-luc" element={<StudentTwinPage />} />
            </Route>
            <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["Student"]} />}>
              <Route path="/hoc-tap/bai-tap" element={<StudentAssignmentsPage />} />
              <Route path="/hoc-tap/bai-tap/:id" element={<StudentAssignmentDetailPage />} />
            </Route>
          </Route>

          {/* Focused Full-Screen Learning Player */}
          <Route element={<PermissionRoute allOf={[permissions.learningAttemptsSubmit]} />}>
            <Route path="/hoc-tap/luyen-tap" element={<LearningPlayerPage />} />
            <Route path="/hoc-tap/luyen-tap/:questionId" element={<LearningPlayerPage />} />
          </Route>

          <Route element={<CenterManagerLayoutBoundary />}>
          {/* Teacher & Center Manager Class Dashboard (Composite Policy) */}
          <Route element={<PermissionRoute anyOf={[permissions.dashboardsTeacherRead, permissions.dashboardsCenterRead]} />}>
            <Route path="/quan-ly/tong-quan-lop-hoc" element={<TeacherClassDashboardPage />} />
            <Route path="/quan-ly/lop-hoc/:classId/tong-quan" element={<TeacherClassDashboardPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.teacherReviewsRead]} />}>
            <Route path="/quan-ly/duyet-bai" element={<ReviewQueuePage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.twinStudentReadScoped]} />}>
            <Route path="/quan-ly/hoc-sinh/:studentId/nang-luc" element={<TeacherStudentTwinPage />} />
          </Route>

          {/* Center Manager Profile & Dashboards */}
          <Route element={<PermissionRoute anyOf={[permissions.centerRead, permissions.centerManage]} accountTypes={["CenterManager"]} />}>
            <Route path="/quan-ly/trung-tam" element={<CenterProfilePage />} />
          </Route>

          <Route element={<PermissionRoute allOf={[permissions.dashboardsCenterRead]} />}>
            <Route path="/quan-ly/tong-quan-trung-tam" element={<CenterDashboardPage />} />
          </Route>

          {/* Knowledge Graph */}
          <Route element={<PermissionRoute allOf={[permissions.subjectsRead]} />}>
            <Route path="/quan-ly/mon-hoc" element={<SubjectListPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.subjectsRead, permissions.nodesRead, permissions.edgesRead]} />}>
            <Route path="/kien-thuc/do-thi" element={<KnowledgeGraphPage />} />
          </Route>

          {/* Organization & Academic Management */}
          <Route element={<PermissionRoute allOf={[permissions.teachersRead]} />}>
            <Route path="/quan-ly/giao-vien" element={<TeacherListPage />} />
          </Route>

          <Route element={<PermissionRoute allOf={[permissions.classesRead]} />}>
            <Route path="/quan-ly/lop-hoc" element={<ClassListPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.studentsRead]} />}>
            <Route path="/quan-ly/hoc-sinh" element={<StudentListPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.curriculumsRead]} />}>
            <Route path="/quan-ly/giao-trinh" element={<CurriculumListPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.curriculumsCreate]} />}>
            <Route path="/quan-ly/giao-trinh/tao-moi" element={<CurriculumEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.curriculumsRead]} />}>
            <Route path="/quan-ly/giao-trinh/:id" element={<CurriculumEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.questionsRead]} />}>
            <Route path="/quan-ly/cau-hoi" element={<QuestionBankPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.questionsCreate]} />}>
            <Route path="/quan-ly/cau-hoi/tao-moi" element={<QuestionEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.questionsRead]} />}>
            <Route path="/quan-ly/cau-hoi/:id" element={<QuestionEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap" element={<AssignmentListPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsCreate]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap/tao-moi" element={<AssignmentEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap/:id" element={<AssignmentEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap/:id/tien-do" element={<AssignmentProgressPage />} />
          </Route>

          <Route element={<PermissionRoute anyOf={authorizationUiPermissions} accountTypes={["CenterManager"]} />}>
            <Route path="/quan-ly/phan-quyen" element={<AuthorizationManagementPage />} />
          </Route>
          </Route>

          {/* Platform Administration */}
          <Route path="/quan-tri-nen-tang" element={<Navigate to="/quan-tri-nen-tang/trung-tam" replace />} />
          <Route element={<PermissionRoute anyOf={[permissions.platformCentersRead, permissions.platformCentersManage, permissions.platformAuditRead]} accountTypes={["PlatformAdmin"]} />}>
            <Route element={<PlatformLayout />}>
              <Route element={<PermissionRoute anyOf={[permissions.platformCentersRead, permissions.platformCentersManage]} accountTypes={["PlatformAdmin"]} />}>
                <Route path="/quan-tri-nen-tang/trung-tam" element={<PlatformCentersPage />} />
              </Route>
              <Route element={<PermissionRoute allOf={[permissions.platformAuditRead]} accountTypes={["PlatformAdmin"]} />}>
                <Route path="/quan-tri-nen-tang/nhat-ky" element={<PlatformAuditLogsPage />} />
              </Route>
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<FallbackRoute />} />
          </Routes>
        </Suspense>
      </RouteChunkBoundary>
    </AuthBootstrap>
  );
}

export default App;
