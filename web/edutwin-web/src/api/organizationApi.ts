import { httpClient } from "./httpClient";
import type { TeacherListParams, TeacherListResponse, ClassListParams, ClassListResponse, CreateTeacherRequest, TeacherDto, StudentListParams, StudentListResponse, CreateStudentRequest, StudentDto, StudentResponse, SubjectListResponse, CreateClassRequest, ClassDto, ClassResponse } from "../types/organization";

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

  createTeacher: async (request: CreateTeacherRequest): Promise<TeacherDto> => {
    const payload = {
      username: request.username.trim(),
      temporaryPassword: request.temporaryPassword,
      displayName: request.displayName.trim(),
      department: request.department?.trim() || undefined,
    };

    const response = await httpClient.post<TeacherDto>("/teachers", payload);
    return response.data;
  },

  listClasses: async (params: ClassListParams): Promise<ClassListResponse> => {
    const queryParams: Record<string, string | number> = {
      page: params.page,
      pageSize: params.pageSize,
    };

    const teacherId = params.teacherId?.trim();
    if (teacherId) {
      queryParams.teacherId = teacherId;
    }

    const subjectId = params.subjectId?.trim();
    if (subjectId) {
      queryParams.subjectId = subjectId;
    }

    if (params.status) {
      queryParams.status = params.status;
    }

    const response = await httpClient.get<ClassListResponse>("/classes", {
      params: queryParams,
    });
    return response.data;
  },

  createClass: async (request: CreateClassRequest): Promise<ClassDto> => {
    const payload = {
      className: request.className.trim(),
      academicYear: request.academicYear.trim(),
      subjectId: request.subjectId.trim(),
      teacherId: request.teacherId.trim(),
    };
    const response = await httpClient.post<ClassResponse>("/classes", payload);
    return response.data.data;
  },

  listSubjects: async (isActive?: boolean): Promise<SubjectListResponse> => {
    const queryParams: Record<string, boolean> = {};
    if (isActive !== undefined) {
      queryParams.isActive = isActive;
    }
    const response = await httpClient.get<SubjectListResponse>("/subjects", {
      params: queryParams,
    });
    return response.data;
  },

  listStudents: async (params: StudentListParams): Promise<StudentListResponse> => {
    const queryParams: Record<string, string | number> = {
      page: params.page,
      pageSize: params.pageSize,
    };
    if (params.search?.trim()) queryParams.search = params.search.trim();
    if (params.status) queryParams.status = params.status;
    if (params.gradeLevel) queryParams.gradeLevel = params.gradeLevel;
    if (params.classId?.trim()) queryParams.classId = params.classId.trim();

    const response = await httpClient.get<StudentListResponse>("/students", {
      params: queryParams,
    });
    return response.data;
  },

  createStudent: async (request: CreateStudentRequest): Promise<StudentDto> => {
    const payload = {
      username: request.username.trim(),
      temporaryPassword: request.temporaryPassword,
      fullName: request.fullName.trim(),
      gradeLevel: request.gradeLevel,
      classIds: Array.from(new Set(request.classIds)),
    };

    const response = await httpClient.post<StudentResponse>("/students", payload);
    return response.data.data;
  },
};
