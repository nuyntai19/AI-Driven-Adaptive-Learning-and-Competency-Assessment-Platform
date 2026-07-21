import { httpClient } from "./httpClient";
import type { TeacherListParams, TeacherListResponse } from "../types/organization";

export const organizationApi = {
  listTeachers: async (params: TeacherListParams): Promise<TeacherListResponse> => {
    // Only include search and status if they have actual values
    const queryParams: Record<string, string | number> = {
      page: params.page,
      pageSize: params.pageSize,
    };

    const search = params.search?.trim();
    if (search) {
      queryParams.search = search;
    }

    if (params.status) {
      queryParams.status = params.status;
    }

    const response = await httpClient.get<TeacherListResponse>("/teachers", {
      params: queryParams,
    });
    return response.data;
  },
};
