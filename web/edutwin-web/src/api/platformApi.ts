import { httpClient } from "./httpClient";
import type {
  PlatformCentersListData,
  PlatformCenterListItem,
  CreatePlatformCenterRequest,
  UpdatePlatformCenterStatusRequest,
  UpdateCenterMetadataRequest,
  ResetCenterManagerPasswordRequest,
  ResetCenterManagerPasswordResponseData,
} from "../types/platform";

export interface ListCentersParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
}

export const platformApi = {
  listCenters: async (params: ListCentersParams = {}): Promise<PlatformCentersListData> => {
    const queryParams: Record<string, string | number> = {
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    };

    if (params.search?.trim()) {
      queryParams.search = params.search.trim();
    }

    if (params.status?.trim()) {
      queryParams.status = params.status.trim();
    }

    const response = await httpClient.get<{ data: PlatformCentersListData }>("/platform/centers", {
      params: queryParams,
    });
    return response.data.data;
  },

  createCenter: async (request: CreatePlatformCenterRequest): Promise<PlatformCenterListItem> => {
    const response = await httpClient.post<{ data: PlatformCenterListItem }>("/platform/centers", request);
    return response.data.data;
  },

  updateCenterMetadata: async (
    centerId: string,
    request: UpdateCenterMetadataRequest
  ): Promise<PlatformCenterListItem> => {
    const response = await httpClient.patch<{ data: PlatformCenterListItem }>(
      `/platform/centers/${centerId}`,
      request
    );
    return response.data.data;
  },

  updateCenterStatus: async (
    centerId: string,
    request: UpdatePlatformCenterStatusRequest
  ): Promise<PlatformCenterListItem> => {
    const response = await httpClient.patch<{ data: PlatformCenterListItem }>(
      `/platform/centers/${centerId}/status`,
      request
    );
    return response.data.data;
  },

  resetCenterManagerPassword: async (
    centerId: string,
    managerUserId: string,
    request: ResetCenterManagerPasswordRequest
  ): Promise<ResetCenterManagerPasswordResponseData> => {
    const response = await httpClient.post<{ data: ResetCenterManagerPasswordResponseData }>(
      `/platform/centers/${centerId}/managers/${managerUserId}/reset-password`,
      request
    );
    return response.data.data;
  },

  listCenterManagers: async (
    centerId: string,
    params: { page?: number; pageSize?: number; search?: string; status?: string } = {}
  ): Promise<import("../types/platform").PlatformCenterManagersListData> => {
    const queryParams: Record<string, string | number> = {
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    };
    if (params.search?.trim()) queryParams.search = params.search.trim();
    if (params.status?.trim()) queryParams.status = params.status.trim();

    const response = await httpClient.get<{ data: import("../types/platform").PlatformCenterManagersListData }>(
      `/platform/centers/${centerId}/managers`,
      { params: queryParams }
    );
    return response.data.data;
  },

  createCenterManager: async (
    centerId: string,
    request: import("../types/platform").CreateCenterManagerRequest
  ): Promise<import("../types/platform").CreateCenterManagerResponseData> => {
    const response = await httpClient.post<{ data: import("../types/platform").CreateCenterManagerResponseData }>(
      `/platform/centers/${centerId}/managers`,
      request
    );
    return response.data.data;
  },

  updateCenterManagerStatus: async (
    centerId: string,
    userId: string,
    request: import("../types/platform").UpdateCenterManagerStatusRequest
  ): Promise<import("../types/platform").UpdateCenterManagerStatusData> => {
    const response = await httpClient.patch<{ data: import("../types/platform").UpdateCenterManagerStatusData }>(
      `/platform/centers/${centerId}/managers/${userId}/status`,
      request
    );
    return response.data.data;
  },

  makePrimaryCenterManager: async (
    centerId: string,
    userId: string,
    request: import("../types/platform").MakePrimaryCenterManagerRequest
  ): Promise<import("../types/platform").MakePrimaryCenterManagerData> => {
    const response = await httpClient.post<{ data: import("../types/platform").MakePrimaryCenterManagerData }>(
      `/platform/centers/${centerId}/managers/${userId}/make-primary`,
      request
    );
    return response.data.data;
  },

  listAuditLogs: async (
    query: import("../types/platform").PlatformAuditQuery = {}
  ): Promise<import("../types/platform").PlatformAuditListData> => {
    const queryParams: Record<string, string | number> = {};
    if (query.page) queryParams.page = query.page;
    if (query.pageSize) queryParams.pageSize = query.pageSize;
    if (query.actionType?.trim()) queryParams.actionType = query.actionType.trim();
    if (query.targetType?.trim()) queryParams.targetType = query.targetType.trim();
    if (query.targetId?.trim()) queryParams.targetId = query.targetId.trim();
    if (query.targetCenterId?.trim()) queryParams.targetCenterId = query.targetCenterId.trim();
    if (query.actorUserId?.trim()) queryParams.actorUserId = query.actorUserId.trim();
    if (query.traceId?.trim()) queryParams.traceId = query.traceId.trim();
    if (query.fromUtc?.trim()) queryParams.fromUtc = query.fromUtc.trim();
    if (query.toUtc?.trim()) queryParams.toUtc = query.toUtc.trim();
    if (query.search?.trim()) queryParams.search = query.search.trim();

    const response = await httpClient.get<{ data: import("../types/platform").PlatformAuditListData }>(
      `/platform/audit-logs`,
      { params: queryParams }
    );
    return response.data.data;
  },

  getAuditLog: async (
    auditId: string
  ): Promise<import("../types/platform").PlatformAuditItem> => {
    const response = await httpClient.get<{ data: import("../types/platform").PlatformAuditItem }>(
      `/platform/audit-logs/${auditId}`
    );
    return response.data.data;
  },

  getSecurityProfile: async (): Promise<import("../types/platform").PlatformSecurityProfile> => {
    const response = await httpClient.get<{ data: import("../types/platform").PlatformSecurityProfile }>(
      "/platform/me/security"
    );
    return response.data.data;
  },

  changePassword: async (
    request: import("../types/platform").PlatformChangePasswordRequest
  ): Promise<{ success: boolean; message: string }> => {
    const response = await httpClient.post<{ data: { success: boolean; message: string } }>(
      "/platform/me/change-password",
      request
    );
    return response.data.data;
  },

  revokeSessions: async (
    request?: import("../types/platform").PlatformRevokeSessionsRequest
  ): Promise<{ success: boolean; message: string }> => {
    const response = await httpClient.post<{ data: { success: boolean; message: string } }>(
      "/platform/me/revoke-sessions",
      request ?? {}
    );
    return response.data.data;
  },
};
