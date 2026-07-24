import { Routes, Route, Navigate } from "react-router-dom";
import { AuthBootstrap } from "./auth/AuthBootstrap";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import { LoginPage } from "./pages/LoginPage";
import { AuthenticatedHomePage } from "./pages/AuthenticatedHomePage";
import { TeacherListPage } from "./pages/TeacherListPage";
import { ClassListPage } from "./pages/ClassListPage";
import { StudentListPage } from "./pages/StudentListPage";
import { KnowledgeGraphPage } from "./pages/KnowledgeGraphPage";
import { RoleRoute } from "./routes/RoleRoute";
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
          <Route path="/kien-thuc/do-thi" element={<KnowledgeGraphPage />} />

          <Route element={<RoleRoute allowedRoles={["CenterManager"]} />}>
            <Route path="/quan-ly/giao-vien" element={<TeacherListPage />} />
          </Route>

          <Route element={<RoleRoute allowedRoles={["CenterManager", "Teacher"]} />}>
            <Route path="/quan-ly/lop-hoc" element={<ClassListPage />} />
            <Route path="/quan-ly/hoc-sinh" element={<StudentListPage />} />
          </Route>
        </Route>

        <Route path="*" element={<FallbackRoute />} />
      </Routes>
    </AuthBootstrap>
  );
}

export default App;
