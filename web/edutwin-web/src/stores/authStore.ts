import { create } from "zustand";
import type { AuthUser } from "../types/auth";
import { hasAllPermissions, hasAnyPermission, hasPermission } from "../auth/capabilities.ts";

interface AuthState {
  accessToken: string | null;
  user: AuthUser | null;
  sessionStatus: "unknown" | "authenticated" | "anonymous";
  hasPermission: (permissionCode: string) => boolean;
  hasAnyPermission: (permissionCodes: readonly string[]) => boolean;
  hasAllPermissions: (permissionCodes: readonly string[]) => boolean;
}

interface AuthActions {
  setSession: (accessToken: string, user: AuthUser) => void;
  updateCurrentUser: (user: AuthUser) => void;
  clearSession: () => void;
  setSessionStatus: (status: "unknown" | "authenticated" | "anonymous") => void;
}

export const useAuthStore = create<AuthState & AuthActions>((set, get) => ({
  accessToken: null,
  user: null,
  sessionStatus: "unknown",
  hasPermission: (permissionCode) =>
    hasPermission(get().user, permissionCode),
  hasAnyPermission: (permissionCodes) =>
    hasAnyPermission(get().user, permissionCodes),
  hasAllPermissions: (permissionCodes) =>
    hasAllPermissions(get().user, permissionCodes),
  setSession: (accessToken, user) => set({ accessToken, user, sessionStatus: "authenticated" }),
  updateCurrentUser: (user) => set({ user }),
  clearSession: () => set({ accessToken: null, user: null, sessionStatus: "anonymous" }),
  setSessionStatus: (status) => set({ sessionStatus: status }),
}));
