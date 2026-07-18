import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "../stores/authStore";
import type { UserRole } from "../types/auth";

interface ProtectedRouteProps {
  allowedRoles?: UserRole[];
  children?: React.ReactNode;
}

export const ProtectedRoute = ({ allowedRoles, children }: ProtectedRouteProps) => {
  const { sessionStatus, user } = useAuthStore();

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
    return <Navigate to="/dang-nhap" replace />;
  }

  if (allowedRoles && allowedRoles.length > 0) {
    if (!allowedRoles.includes(user.role)) {
      return <Navigate to="/" replace />;
    }
  }

  return children ? <>{children}</> : <Outlet />;
};
