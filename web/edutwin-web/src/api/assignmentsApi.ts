import { httpClient } from './httpClient';
import type {
  AssignmentDto,
  CreateAssignmentRequest,
  UpdateAssignmentRequest,
  PublishAssignmentRequest,
  CloseAssignmentRequest,
  StudentAssignmentDetailDto,
  StudentAssignmentListItemDto,
  AssignmentProgressItemDto,
  AssignmentStatus,
  ProgressStatus,
} from '../types/assignments';
import type { ApiCollectionResponse, ApiResponse } from '../types/api';
import type { ClassDto, StudentDto } from '../types/organization';
import type { Question } from '../types/questions';

// --- TEACHER / CENTER MANAGER ENDPOINTS ---

export interface GetAssignmentsParams {
  classId?: string;
  status?: AssignmentStatus;
  page?: number;
  pageSize?: number;
}

export const getAssignments = async (params: GetAssignmentsParams = {}) => {
  const { data } = await httpClient.get<ApiCollectionResponse<AssignmentDto>>('/assignments', {
    params,
  });
  return data;
};

export const getAssignmentById = async (id: string) => {
  const { data } = await httpClient.get<ApiResponse<AssignmentDto>>(`/assignments/${id}`);
  return data;
};

export const createAssignment = async (request: CreateAssignmentRequest) => {
  const { data } = await httpClient.post<ApiResponse<AssignmentDto>>('/assignments', request);
  return data;
};

export const updateAssignment = async (id: string, request: UpdateAssignmentRequest) => {
  const { data } = await httpClient.patch<ApiResponse<AssignmentDto>>(`/assignments/${id}`, request);
  return data;
};

export const publishAssignment = async (id: string, request: PublishAssignmentRequest) => {
  const { data } = await httpClient.post<ApiResponse<AssignmentDto>>(`/assignments/${id}/publish`, request);
  return data;
};

export const closeAssignment = async (id: string, request: CloseAssignmentRequest) => {
  const { data } = await httpClient.post<ApiResponse<AssignmentDto>>(`/assignments/${id}/close`, request);
  return data;
};

export const getAssignmentProgress = async (id: string) => {
  const { data } = await httpClient.get<ApiCollectionResponse<AssignmentProgressItemDto>>(`/assignments/${id}/progress`);
  return data;
};

export interface GetAssignmentClassesParams {
  status?: string;
  subjectId?: string;
  teacherId?: string;
  page?: number;
  pageSize?: number;
}

export const getAssignmentClasses = async (params: GetAssignmentClassesParams = {}) => {
  const { data } = await httpClient.get<ApiCollectionResponse<ClassDto>>('/classes', {
    params: { status: 'Active', page: 1, pageSize: 20, ...params },
  });
  return data;
};

export interface GetAssignableQuestionsParams {
  subjectId: string;
  topicId?: string;
  difficulty?: number;
  type?: Question['questionType'];
  page?: number;
  pageSize?: number;
}

export const getAssignableQuestions = async (
  subjectIdOrParams: string | GetAssignableQuestionsParams,
  optionalParams?: Omit<GetAssignableQuestionsParams, 'subjectId'>
) => {
  const params: GetAssignableQuestionsParams =
    typeof subjectIdOrParams === 'string'
      ? { subjectId: subjectIdOrParams, ...optionalParams }
      : subjectIdOrParams;

  const { data } = await httpClient.get<ApiCollectionResponse<Question>>('/questions', {
    params: { status: 'Active', page: 1, pageSize: 20, ...params },
  });
  return data;
};

export interface GetAssignmentClassStudentsParams {
  classId: string;
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export const getAssignmentClassStudents = async (
  classIdOrParams: string | GetAssignmentClassStudentsParams,
  optionalParams?: Omit<GetAssignmentClassStudentsParams, 'classId'>
) => {
  const params: GetAssignmentClassStudentsParams =
    typeof classIdOrParams === 'string'
      ? { classId: classIdOrParams, ...optionalParams }
      : classIdOrParams;

  const { classId, ...queryParams } = params;
  const { data } = await httpClient.get<ApiCollectionResponse<StudentDto>>(`/classes/${classId}/students`, {
    params: { status: 'Active', page: 1, pageSize: 20, ...queryParams },
  });
  return data;
};

// --- STUDENT ENDPOINTS ---

export interface GetStudentAssignmentsParams {
  status?: ProgressStatus;
  subjectId?: string;
  page?: number;
  pageSize?: number;
}

export const getStudentAssignments = async (params: GetStudentAssignmentsParams = {}) => {
  const { data } = await httpClient.get<ApiCollectionResponse<StudentAssignmentListItemDto>>('/students/me/assignments', {
    params,
  });
  return data;
};

export const getStudentAssignmentById = async (id: string) => {
  const { data } = await httpClient.get<ApiResponse<StudentAssignmentDetailDto>>(`/students/me/assignments/${id}`);
  return data;
};

export const startStudentAssignment = async (id: string) => {
  const { data } = await httpClient.post<ApiResponse<StudentAssignmentDetailDto>>(`/students/me/assignments/${id}/start`);
  return data;
};
