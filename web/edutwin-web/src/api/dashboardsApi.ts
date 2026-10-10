import { httpClient } from "./httpClient";
import { isAxiosError } from "axios";
import type {
  StudentDashboardDataDto,
  StudentWorkspaceSummaryDto,
  StudentAcademicContextDto,
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

export const getStudentWorkspaceSummary = async (subjectId?: string, classId?: string, history?: boolean): Promise<StudentWorkspaceSummaryDto> => {
  const response = await httpClient.get<ApiResponse<StudentWorkspaceSummaryDto>>("/students/me/workspace-summary", {
    params: { subjectId: subjectId || undefined, classId: classId || undefined, history },
  });
  return response.data.data;
};

export const getStudentDashboard = async (subjectId?: string, classId?: string, history = false): Promise<StudentDashboardDataDto> => {
  const params: Record<string, string> = {};
  if (subjectId) {
    params.subjectId = subjectId;
  }
  if (classId) params.classId = classId;
  if (history) params.history = "true";
  const response = await httpClient.get<ApiResponse<StudentDashboardDataDto>>("/students/me/dashboard", {
    params,
  });
  return response.data.data;
};

export const getStudentAcademicContext = async (subjectId?: string, classId?: string, history = false): Promise<StudentAcademicContextDto> => {
  const response = await httpClient.get<ApiResponse<StudentAcademicContextDto>>("/students/me/academic-context", {
    params: { subjectId: subjectId || undefined, classId: classId || undefined, history },
  });
  return response.data.data;
};

// All observers of the academic-context cache use the same bookmark recovery.
// Never retry permission/network errors or silently permit the rejected class.
export const getReconciledStudentAcademicContext = async (subjectId?: string, classId?: string, history = false): Promise<StudentAcademicContextDto> => {
  try { return await getStudentAcademicContext(subjectId, classId, history); }
  catch (error) {
    if (classId && isAxiosError(error) && error.response?.status === 404)
      return getStudentAcademicContext(subjectId, "", history);
    throw error;
  }
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
