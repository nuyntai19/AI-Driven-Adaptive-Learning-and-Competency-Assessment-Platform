import { Routes, Route, Navigate } from "react-router-dom";
import { AuthBootstrap } from "./auth/AuthBootstrap";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import { LoginPage } from "./pages/LoginPage";
import { AuthenticatedHomePage } from "./pages/AuthenticatedHomePage";
import { TeacherListPage } from "./pages/TeacherListPage";
import { ClassListPage } from "./pages/ClassListPage";
import { StudentListPage } from "./pages/StudentListPage";
import { KnowledgeGraphPage } from "./pages/KnowledgeGraphPage";
import { CurriculumListPage } from "./pages/CurriculumListPage";
import { QuestionBankPage } from "./pages/QuestionBankPage";
import { QuestionEditorPage } from "./pages/QuestionEditorPage";
import { CurriculumEditorPage } from "./pages/CurriculumEditorPage";
import { AssignmentListPage } from "./pages/AssignmentListPage";
import { AssignmentEditorPage } from "./pages/AssignmentEditorPage";
import { AssignmentProgressPage } from "./pages/AssignmentProgressPage";
import { StudentAssignmentsPage } from "./pages/StudentAssignmentsPage";
import { StudentAssignmentDetailPage } from "./pages/StudentAssignmentDetailPage";
import { StudentDashboardPage } from "./pages/StudentDashboardPage";
import { StudentTwinPage } from "./pages/StudentTwinPage";
import { LearningPlayerPage } from "./pages/LearningPlayerPage";
import { TeacherClassDashboardPage } from "./pages/TeacherClassDashboardPage";
import { ReviewQueuePage } from "./pages/ReviewQueuePage";
import { TeacherStudentTwinPage } from "./pages/TeacherStudentTwinPage";
import { CenterDashboardPage } from "./pages/CenterDashboardPage";
import { PermissionRoute } from "./routes/PermissionRoute";
import { permissions, authorizationUiPermissions } from "./auth/permissions";
import { AccessDeniedPage } from "./pages/AccessDeniedPage";
import { AuthorizationManagementPage } from "./pages/AuthorizationManagementPage";
import { useAuthStore } from "./stores/authStore";

const FallbackRoute = () => {
  const sessionStatus = useAuthStore((state) => state.sessionStatus);
  if (sessionStatus === "authenticated") {
    return <Navigate to="/" replace />;
  }
  return <Navigate to="/dang-nhap" replace />;
};

function App() {
  return (
    <AuthBootstrap>
      <Routes>
        <Route path="/dang-nhap" element={<LoginPage />} />

        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<AuthenticatedHomePage />} />
          <Route path="/khong-co-quyen" element={<AccessDeniedPage />} />

          {/* Student R08 Experiences */}
          <Route element={<PermissionRoute allOf={[permissions.dashboardsStudentRead]} />}>
            <Route path="/hoc-tap/tong-quan" element={<StudentDashboardPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.twinStudentReadOwn]} />}>
            <Route path="/hoc-tap/ho-so-nang-luc" element={<StudentTwinPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.learningAttemptsSubmit]} />}>
            <Route path="/hoc-tap/luyen-tap" element={<LearningPlayerPage />} />
            <Route path="/hoc-tap/luyen-tap/:questionId" element={<LearningPlayerPage />} />
          </Route>

          {/* Teacher R08 Experiences */}
          <Route element={<PermissionRoute allOf={[permissions.dashboardsTeacherRead]} />}>
            <Route path="/quan-ly/tong-quan-lop-hoc" element={<TeacherClassDashboardPage />} />
            <Route path="/quan-ly/lop-hoc/:classId/tong-quan" element={<TeacherClassDashboardPage />} />
          </Route>
          <Route element={<PermissionRoute anyOf={[permissions.teacherReviewsRead, permissions.teacherReviewsOverride]} />}>
            <Route path="/quan-ly/duyet-bai" element={<ReviewQueuePage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.twinStudentReadScoped]} />}>
            <Route path="/quan-ly/hoc-sinh/:studentId/nang-luc" element={<TeacherStudentTwinPage />} />
          </Route>

          {/* Center Manager R08 Experiences */}
          <Route element={<PermissionRoute allOf={[permissions.dashboardsCenterRead]} />}>
            <Route path="/quan-ly/tong-quan-trung-tam" element={<CenterDashboardPage />} />
          </Route>

          {/* Knowledge Graph */}
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
          <Route element={<PermissionRoute allOf={[permissions.curriculumsUpdate]} />}>
            <Route path="/quan-ly/giao-trinh/:id" element={<CurriculumEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.questionsRead]} />}>
            <Route path="/quan-ly/cau-hoi" element={<QuestionBankPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.questionsCreate]} />}>
            <Route path="/quan-ly/cau-hoi/tao-moi" element={<QuestionEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.questionsUpdate]} />}>
            <Route path="/quan-ly/cau-hoi/:id" element={<QuestionEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap" element={<AssignmentListPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsCreate]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap/tao-moi" element={<AssignmentEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsUpdate]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap/:id" element={<AssignmentEditorPage />} />
          </Route>
          <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/bai-tap/:id/tien-do" element={<AssignmentProgressPage />} />
          </Route>

          <Route element={<PermissionRoute allOf={[permissions.assignmentsRead]} accountTypes={["Student"]} />}>
            <Route path="/hoc-tap/bai-tap" element={<StudentAssignmentsPage />} />
            <Route path="/hoc-tap/bai-tap/:id" element={<StudentAssignmentDetailPage />} />
          </Route>

          <Route element={<PermissionRoute anyOf={authorizationUiPermissions} accountTypes={["CenterManager"]} />}>
            <Route path="/quan-ly/phan-quyen" element={<AuthorizationManagementPage />} />
          </Route>
        </Route>

        <Route path="*" element={<FallbackRoute />} />
      </Routes>
    </AuthBootstrap>
  );
}

export default App;
