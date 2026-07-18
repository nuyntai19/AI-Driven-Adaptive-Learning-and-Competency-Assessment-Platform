import { Routes, Route, Navigate } from "react-router-dom";
import { AuthBootstrap } from "./auth/AuthBootstrap";
import { ProtectedRoute } from "./routes/ProtectedRoute";
import { LoginPage } from "./pages/LoginPage";
import { AuthenticatedHomePage } from "./pages/AuthenticatedHomePage";
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
        </Route>

        <Route path="*" element={<FallbackRoute />} />
      </Routes>
    </AuthBootstrap>
  );
}

export default App;
