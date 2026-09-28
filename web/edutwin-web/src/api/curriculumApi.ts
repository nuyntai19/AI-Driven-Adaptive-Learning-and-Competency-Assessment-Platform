import { httpClient } from "./httpClient";
import type {
  Curriculum,
  CreateCurriculumRequest,
  UpdateCurriculumRequest,
  PublishCurriculumRequest,
  ArchiveCurriculumRequest,
  CloneCurriculumRequest,
  UpdateCurriculumClassesRequest,
  UpdateCurriculumNodesRequest,
  ReviewStatus,
} from "../types/curriculum";
import type { ApiResponse, ApiCollectionResponse } from "../types/api";

const BASE_URL = "/curriculums";

export const curriculumApi = {
  create: async (data: CreateCurriculumRequest) => {
    const response = await httpClient.post<ApiResponse<Curriculum>>(BASE_URL, data);
    return response.data;
  },

  getAll: async (params?: { subjectId?: string; status?: ReviewStatus }) => {
    const response = await httpClient.get<ApiCollectionResponse<Curriculum>>(BASE_URL, { params });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await httpClient.get<ApiResponse<Curriculum>>(`${BASE_URL}/${id}`);
    return response.data;
  },

  update: async (id: string, data: UpdateCurriculumRequest) => {
    const response = await httpClient.patch<ApiResponse<Curriculum>>(`${BASE_URL}/${id}`, data);
    return response.data;
  },

  publish: async (id: string, data: PublishCurriculumRequest) => {
    const response = await httpClient.post<ApiResponse<Curriculum>>(`${BASE_URL}/${id}/publish`, data);
    return response.data;
  },

  archive: async (id: string, data: ArchiveCurriculumRequest) => {
    const response = await httpClient.post<ApiResponse<Curriculum>>(`${BASE_URL}/${id}/archive`, data);
    return response.data;
  },

  clone: async (id: string, data: CloneCurriculumRequest) => {
    const response = await httpClient.post<ApiResponse<Curriculum>>(`${BASE_URL}/${id}/clone`, data);
    return response.data;
  },

  updateClasses: async (id: string, data: UpdateCurriculumClassesRequest) => {
    const response = await httpClient.put<ApiResponse<Curriculum>>(`${BASE_URL}/${id}/classes`, data);
    return response.data;
  },

  updateNodes: async (id: string, data: UpdateCurriculumNodesRequest) => {
    const response = await httpClient.put<ApiResponse<Curriculum>>(`${BASE_URL}/${id}/nodes`, data);
    return response.data;
  },
};
