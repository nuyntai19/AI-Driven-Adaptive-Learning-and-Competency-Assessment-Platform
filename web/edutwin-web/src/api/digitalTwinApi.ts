import { httpClient } from "./httpClient";
import type {
  StudentTwinDataDto,
  TwinUpdateHistoryItemDto,
  PagedList,
} from "../types/digitalTwin";

interface ApiResponse<T> {
  data: T;
  meta: {
    traceId?: string;
    timestamp: string;
  };
}

export const getStudentTwin = async (subjectId?: string): Promise<StudentTwinDataDto> => {
  const params: Record<string, string> = {};
  if (subjectId) {
    params.subjectId = subjectId;
  }
  const response = await httpClient.get<ApiResponse<StudentTwinDataDto>>("/students/me/twin", {
    params,
  });
  return response.data.data;
};

export const getStudentTwinHistory = async (
  subjectId?: string,
  topicNodeId?: string,
  page: number = 1,
  pageSize: number = 20
): Promise<PagedList<TwinUpdateHistoryItemDto>> => {
  const params: Record<string, string | number> = { page, pageSize };
  if (subjectId) {
    params.subjectId = subjectId;
  }
  if (topicNodeId) {
    params.topicNodeId = topicNodeId;
  }
  const response = await httpClient.get<ApiResponse<PagedList<TwinUpdateHistoryItemDto>>>(
    "/students/me/twin/history",
    { params }
  );
  return response.data.data;
};

export const getTeacherStudentTwin = async (
  studentId: string,
  subjectId?: string
): Promise<StudentTwinDataDto> => {
  const params: Record<string, string> = {};
  if (subjectId) {
    params.subjectId = subjectId;
  }
  const response = await httpClient.get<ApiResponse<StudentTwinDataDto>>(
    `/teachers/me/students/${encodeURIComponent(studentId)}/twin`,
    { params }
  );
  return response.data.data;
};
