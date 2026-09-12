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
};
