import { httpClient } from "./httpClient";
import type {
  Question,
  CreateQuestionRequest,
  UpdateQuestionRequest,
  ActivateQuestionRequest,
  ArchiveQuestionRequest,
  QuestionFilter,
  QuestionImportPreviewResponse,
  QuestionImportPreviewRequest,
  QuestionImportV2PreviewRequest,
  QuestionImportConfirmRequest,
  QuestionImportConfirmResponse,
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

  previewImport: async (file: File, settings: QuestionImportPreviewRequest, signal?: AbortSignal) => {
    const formData = new FormData();
    formData.append("file", file);
    for (const [key, value] of Object.entries(settings)) {
      if (value !== undefined && value !== null) formData.append(key, String(value));
    }
    const response = await httpClient.post<QuestionImportPreviewResponse>(`${BASE_URL}/import/preview`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
      signal,
    });
    return response.data;
  },

  confirmImport: async (data: QuestionImportConfirmRequest) => {
    const response = await httpClient.post<QuestionImportConfirmResponse>(`${BASE_URL}/import/confirm`, data);
    return response.data;
  },

  previewImportV2: async (file: File, settings: QuestionImportV2PreviewRequest, signal?: AbortSignal) => {
    const formData = new FormData();
    formData.append("file", file);
    for (const [key, value] of Object.entries(settings)) {
      if (value !== undefined && value !== null) formData.append(key, String(value));
    }
    const response = await httpClient.post<QuestionImportPreviewResponse>(`${BASE_URL}/import-v2/preview`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
      signal,
    });
    return response.data;
  },

  confirmImportV2: async (data: QuestionImportConfirmRequest) => {
    const response = await httpClient.post<QuestionImportConfirmResponse>(`${BASE_URL}/import-v2/confirm`, data);
    return response.data;
  },

  getImportV2Asset: async (assetId: string, previewToken: string, revision: number, signal?: AbortSignal): Promise<Blob> => {
    const response = await httpClient.get<Blob>(`${BASE_URL}/import-v2/assets/${encodeURIComponent(assetId)}`, {
      params: { revision }, headers: { "X-Import-Preview-Token": previewToken }, responseType: "blob", signal,
    });
    return response.data;
  },

  discardImportV2: async (previewToken: string, revision: number) => {
    await httpClient.delete(`${BASE_URL}/import-v2/session`, { params: { revision }, headers: { "X-Import-Preview-Token": previewToken } });
  },
};
