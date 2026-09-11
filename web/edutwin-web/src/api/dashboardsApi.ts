import { httpClient } from "./httpClient";
import type {
  StudentDashboardDataDto,
  ClassDashboardDataDto,
  CenterDashboardDataDto,
} from "../types/dashboards";

interface ApiResponse<T> {
  data: T;
  meta: {
    traceId?: string;
    timestamp: string;
  };
}

export const getStudentDashboard = async (subjectId?: string): Promise<StudentDashboardDataDto> => {
  const params: Record<string, string> = {};
  if (subjectId) {
    params.subjectId = subjectId;
  }
  const response = await httpClient.get<ApiResponse<StudentDashboardDataDto>>("/students/me/dashboard", {
    params,
  });
  return response.data.data;
};

export const getClassDashboard = async (
  classId: string,
  riskThreshold: number = 70
): Promise<ClassDashboardDataDto> => {
  const response = await httpClient.get<ApiResponse<ClassDashboardDataDto>>(
    `/classes/${encodeURIComponent(classId)}/dashboard`,
    {
      params: { riskThreshold },
    }
  );
  return response.data.data;
};

export const getCenterDashboard = async (
  subjectId?: string,
  riskThreshold: number = 70
): Promise<CenterDashboardDataDto> => {
  const params: Record<string, string | number> = { riskThreshold };
  if (subjectId) {
    params.subjectId = subjectId;
  }
  const response = await httpClient.get<ApiResponse<CenterDashboardDataDto>>("/centers/me/dashboard", {
    params,
  });
  return response.data.data;
};
