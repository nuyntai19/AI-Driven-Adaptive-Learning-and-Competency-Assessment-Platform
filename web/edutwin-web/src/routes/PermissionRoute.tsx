import type { ReactNode } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuthStore } from "../stores/authStore";
import type { AccountType } from "../types/auth";
import { canAccess } from "../auth/capabilities";

interface PermissionRouteProps {
  allOf?: readonly string[];
  anyOf?: readonly string[];
  accountTypes?: readonly AccountType[];
  children?: ReactNode;
}

export const PermissionRoute = ({
  allOf = [],
  anyOf = [],
  accountTypes,
  children,
}: PermissionRouteProps) => {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);

  if (!canAccess(user, { allOf, anyOf, accountTypes })) {
    return (
      <Navigate
        to="/khong-co-quyen"
        replace
        state={{ attemptedPath: location.pathname }}
      />
    );
  }

  return children ? <>{children}</> : <Outlet />;
};
