import { httpClient } from "./httpClient";
import type {
  PlatformCentersListData,
  PlatformCenterListItem,
  CreatePlatformCenterRequest,
  UpdatePlatformCenterStatusRequest,
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
};
