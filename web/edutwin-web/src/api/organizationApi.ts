import { httpClient } from "./httpClient";
import type {
  TeacherListParams,
  TeacherListResponse,
  ClassListParams,
  ClassListResponse,
  CreateTeacherRequest,
  UpdateTeacherRequest,
  TeacherDto,
  StudentListParams,
  StudentListResponse,
  CreateStudentRequest,
  UpdateStudentRequest,
  StudentDto,
  StudentResponse,
  StudentDetailDto,
  StudentDetailResponse,
  StudentSubjectGoalDto,
  UpsertStudentSubjectGoalRequest,
  StudentSubjectGoalResponse,
  ResetAccountPasswordRequest,
  ResetAccountPasswordData,
  ResetAccountPasswordResponse,
  SubjectDto,
  SubjectListResponse,
  SubjectResponse,
  CreateSubjectRequest,
  UpdateSubjectRequest,
  CreateClassRequest,
  UpdateClassRequest,
  AddStudentsToClassRequest,
  AddStudentsToClassData,
  AddStudentsToClassResponse,
  ClassStudentListParams,
  CandidateStudentListParams,
  ClassDto,
  ClassResponse,
  CenterProfileDto,
  CenterProfileResponse,
  UpdateCenterProfileRequest,
} from "../types/organization";

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

  getClass: async (classId: string): Promise<ClassDto> => {
    const response = await httpClient.get<ClassResponse>(`/classes/${classId}`);
    return response.data.data;
  },

  updateClass: async (classId: string, request: UpdateClassRequest): Promise<ClassDto> => {
    const payload = {
      className: request.className.trim(),
      teacherId: request.teacherId.trim(),
      status: request.status,
      rowVersion: request.rowVersion,
    };
    const response = await httpClient.patch<ClassResponse>(`/classes/${classId}`, payload);
    return response.data.data;
  },

  getClassStudents: async (
    classId: string,
    params?: ClassStudentListParams
  ): Promise<StudentListResponse> => {
    const response = await httpClient.get<StudentListResponse>(`/classes/${classId}/students`, {
      params,
    });
    return response.data;
  },

  getClassCandidateStudents: async (
    classId: string,
    params?: CandidateStudentListParams
  ): Promise<StudentListResponse> => {
    const queryParams: Record<string, string | number> = {};
    if (params?.page) queryParams.page = params.page;
    if (params?.pageSize) queryParams.pageSize = params.pageSize;
    if (params?.search?.trim()) queryParams.search = params.search.trim();

    const response = await httpClient.get<StudentListResponse>(
      `/classes/${classId}/candidate-students`,
      { params: queryParams }
    );
    return response.data;
  },

  addStudentsToClass: async (
    classId: string,
    request: AddStudentsToClassRequest
  ): Promise<AddStudentsToClassData> => {
    const response = await httpClient.post<AddStudentsToClassResponse>(
      `/classes/${classId}/students`,
      request
    );
    return response.data.data;
  },

  removeStudentFromClass: async (classId: string, studentId: string): Promise<void> => {
    await httpClient.delete(`/classes/${classId}/students/${studentId}`);
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

  getSubject: async (subjectId: string): Promise<SubjectDto> => {
    const response = await httpClient.get<SubjectResponse>(`/subjects/${subjectId}`);
    return response.data.data;
  },

  createSubject: async (request: CreateSubjectRequest): Promise<SubjectDto> => {
    const payload = {
      subjectCode: request.subjectCode.trim(),
      subjectName: request.subjectName.trim(),
      description: request.description?.trim() || null,
    };
    const response = await httpClient.post<SubjectResponse>("/subjects", payload);
    return response.data.data;
  },

  updateSubject: async (
    subjectId: string,
    request: UpdateSubjectRequest
  ): Promise<SubjectDto> => {
    const payload = {
      subjectCode: request.subjectCode.trim(),
      subjectName: request.subjectName.trim(),
      description: request.description?.trim() || null,
      isActive: request.isActive,
      rowVersion: request.rowVersion,
    };
    const response = await httpClient.patch<SubjectResponse>(
      `/subjects/${subjectId}`,
      payload
    );
    return response.data.data;
  },

  deleteSubject: async (subjectId: string): Promise<void> => {
    await httpClient.delete(`/subjects/${subjectId}`);
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

  getTeacher: async (teacherId: string): Promise<TeacherDto> => {
    const response = await httpClient.get<TeacherDto>(`/teachers/${teacherId}`);
    return response.data;
  },

  updateTeacher: async (teacherId: string, request: UpdateTeacherRequest): Promise<TeacherDto> => {
    const response = await httpClient.patch<TeacherDto>(`/teachers/${teacherId}`, request);
    return response.data;
  },

  deleteTeacher: async (teacherId: string): Promise<void> => {
    await httpClient.delete(`/teachers/${teacherId}`);
  },

  resetTeacherPassword: async (
    teacherId: string,
    request: ResetAccountPasswordRequest
  ): Promise<ResetAccountPasswordData> => {
    const response = await httpClient.post<ResetAccountPasswordResponse>(
      `/teachers/${teacherId}/reset-password`,
      request
    );
    return response.data.data;
  },

  getStudent: async (studentId: string): Promise<StudentDetailDto> => {
    const response = await httpClient.get<StudentDetailResponse>(`/students/${studentId}`);
    return response.data.data;
  },

  updateStudent: async (studentId: string, request: UpdateStudentRequest): Promise<StudentDto> => {
    const response = await httpClient.patch<StudentResponse>(`/students/${studentId}`, request);
    return response.data.data;
  },

  deleteStudent: async (studentId: string): Promise<void> => {
    await httpClient.delete(`/students/${studentId}`);
  },

  resetStudentPassword: async (
    studentId: string,
    request: ResetAccountPasswordRequest
  ): Promise<ResetAccountPasswordData> => {
    const response = await httpClient.post<ResetAccountPasswordResponse>(
      `/students/${studentId}/reset-password`,
      request
    );
    return response.data.data;
  },

  upsertStudentSubjectGoal: async (
    studentId: string,
    subjectId: string,
    request: UpsertStudentSubjectGoalRequest
  ): Promise<StudentSubjectGoalDto> => {
    const payload = {
      targetScore: request.targetScore,
      remainingDays: request.remainingDays,
      rowVersion: request.rowVersion,
    };
    const response = await httpClient.put<StudentSubjectGoalResponse>(
      `/students/${studentId}/goals/${subjectId}`,
      payload
    );
    return response.data.data;
  },

  getCurrentCenter: async (): Promise<CenterProfileDto> => {
    const response = await httpClient.get<CenterProfileResponse>("/centers/me");
    return response.data.data;
  },

  updateCurrentCenter: async (request: UpdateCenterProfileRequest): Promise<CenterProfileDto> => {
    const payload = {
      centerName: request.centerName.trim(),
      timezone: request.timezone.trim(),
      rowVersion: request.rowVersion,
    };
    const response = await httpClient.patch<CenterProfileResponse>("/centers/me", payload);
    return response.data.data;
  },
};
