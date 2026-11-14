import { Navigate, Outlet, useLocation } from "react-router-dom";
import { loginPathForRoute } from "../auth/loginPortal";
import { useAuthStore } from "../stores/authStore";

interface ProtectedRouteProps {
  children?: React.ReactNode;
}

export const ProtectedRoute = ({ children }: ProtectedRouteProps) => {
  const { sessionStatus, user } = useAuthStore();
  const location = useLocation();

  if (sessionStatus === "unknown") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <div role="status" className="text-indigo-600 font-medium">
          Đang tải...
        </div>
      </div>
    );
  }

  if (sessionStatus === "anonymous" || !user) {
    return <Navigate to={loginPathForRoute(location.pathname)} replace />;
  }

  return children ? <>{children}</> : <Outlet />;
};
