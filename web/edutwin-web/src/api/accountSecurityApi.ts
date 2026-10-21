import { httpClient } from "./httpClient";
import type { PlatformSecurityProfile, PlatformChangePasswordRequest, PlatformRevokeSessionsRequest } from "../types/platform";

export const accountSecurityApi = {
  getSecurityProfile: async () => (await httpClient.get<{ data: PlatformSecurityProfile }>("/account/me/security")).data.data,
  changePassword: async (request: PlatformChangePasswordRequest) =>
    (await httpClient.post<{ data: { success: boolean; message: string } }>("/account/me/change-password", request)).data.data,
  revokeSessions: async (request?: PlatformRevokeSessionsRequest) =>
    (await httpClient.post<{ data: { success: boolean; message: string } }>("/account/me/revoke-sessions", request ?? {})).data.data,
};
