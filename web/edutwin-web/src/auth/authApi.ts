import { httpClient, publicTransport } from "../api/httpClient";
import { useAuthStore } from "../stores/authStore";
import type { LoginRequest, LoginResponse, CurrentUserResponse } from "../types/auth";
import { clearUserScratchpadDrafts } from "../utils/scratchpadStorage";

export const login = async (request: LoginRequest): Promise<void> => {
  const response = await publicTransport.post<LoginResponse>("/auth/login", request);
  const { accessToken, user } = response.data.data;
  useAuthStore.getState().setSession(accessToken, user);
};

export const refreshSession = async (): Promise<void> => {
  const response = await publicTransport.post<LoginResponse>("/auth/refresh");
  const { accessToken, user } = response.data.data;
  useAuthStore.getState().setSession(accessToken, user);
};

export const getCurrentUser = async (): Promise<void> => {
  const response = await httpClient.get<CurrentUserResponse>("/auth/me");
  useAuthStore.getState().updateCurrentUser(response.data.data);
};

export const logout = async (): Promise<void> => {
  const user = useAuthStore.getState().user;
  try {
    await publicTransport.post("/auth/logout");
  } finally {
    if (user) {
      try {
        await clearUserScratchpadDrafts(user.centerId, user.userId);
      } catch {
        // Session invalidation must still succeed even if browser storage is unavailable.
      }
    }
    useAuthStore.getState().clearSession();
  }
};

export const bootstrapSession = async (): Promise<void> => {
  try {
    await refreshSession();
    await getCurrentUser();
  } catch {
    useAuthStore.getState().clearSession();
  }
};
