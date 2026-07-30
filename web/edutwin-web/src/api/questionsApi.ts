import { httpClient } from "./httpClient";
import type {
  Question,
  CreateQuestionRequest,
  UpdateQuestionRequest,
  ActivateQuestionRequest,
  ArchiveQuestionRequest,
  QuestionFilter,
} from "../types/questions";
import type { ApiResponse, ApiCollectionResponse } from "../types/api";

const BASE_URL = "/questions";

export const questionsApi = {
  create: async (data: CreateQuestionRequest) => {
    const response = await httpClient.post<ApiResponse<Question>>(BASE_URL, data);
    return response.data;
  },

  getAll: async (params?: QuestionFilter) => {
    const response = await httpClient.get<ApiCollectionResponse<Question>>(BASE_URL, { params });
    return response.data;
  },

  getById: async (id: string) => {
    const response = await httpClient.get<ApiResponse<Question>>(`${BASE_URL}/${id}`);
    return response.data;
  },

  update: async (id: string, data: UpdateQuestionRequest) => {
    const response = await httpClient.patch<ApiResponse<Question>>(`${BASE_URL}/${id}`, data);
    return response.data;
  },

  activate: async (id: string, data: ActivateQuestionRequest) => {
    const response = await httpClient.post<ApiResponse<Question>>(`${BASE_URL}/${id}/activate`, data);
    return response.data;
  },

  archive: async (id: string, data: ArchiveQuestionRequest) => {
    const response = await httpClient.post<ApiResponse<Question>>(`${BASE_URL}/${id}/archive`, data);
    return response.data;
  },

  delete: async (id: string) => {
    const response = await httpClient.delete(`${BASE_URL}/${id}`);
    return response.data;
  },
};
