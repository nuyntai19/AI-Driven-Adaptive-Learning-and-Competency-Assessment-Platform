import { create } from "zustand";
import type { AuthUser } from "../types/auth";

interface AuthState {
  accessToken: string | null;
  user: AuthUser | null;
  sessionStatus: "unknown" | "authenticated" | "anonymous";
}

interface AuthActions {
  setSession: (accessToken: string, user: AuthUser) => void;
  updateCurrentUser: (user: AuthUser) => void;
  clearSession: () => void;
  setSessionStatus: (status: "unknown" | "authenticated" | "anonymous") => void;
}

export const useAuthStore = create<AuthState & AuthActions>((set) => ({
  accessToken: null,
  user: null,
  sessionStatus: "unknown",
  setSession: (accessToken, user) => set({ accessToken, user, sessionStatus: "authenticated" }),
  updateCurrentUser: (user) => set({ user }),
  clearSession: () => set({ accessToken: null, user: null, sessionStatus: "anonymous" }),
  setSessionStatus: (status) => set({ sessionStatus: status }),
}));
