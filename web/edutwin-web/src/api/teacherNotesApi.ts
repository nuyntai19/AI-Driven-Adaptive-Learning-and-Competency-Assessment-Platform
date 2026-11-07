import { httpClient } from "./httpClient";
import type {
  TeacherStudentNoteDto,
  TeacherStudentNoteResponse,
  TeacherStudentNoteListResponse,
  TeacherStudentNoteListParams,
  CreateTeacherStudentNoteRequest,
  UpdateTeacherStudentNoteRequest,
  UpsertTeacherStudentNoteRequest,
} from "../types/organization";

export const teacherNotesApi = {
  listNotes: async (params?: TeacherStudentNoteListParams): Promise<TeacherStudentNoteListResponse> => {
    const queryParams: Record<string, string | number> = {};
    if (params?.studentId?.trim()) queryParams.studentId = params.studentId.trim();
    if (params?.teacherId?.trim()) queryParams.teacherId = params.teacherId.trim();
    if (params?.page) queryParams.page = params.page;
    if (params?.pageSize) queryParams.pageSize = params.pageSize;

    const response = await httpClient.get<TeacherStudentNoteListResponse>("/teacher-student-notes", {
      params: queryParams,
    });
    return response.data;
  },

  getMyNotes: async (): Promise<TeacherStudentNoteDto[]> => {
    const response = await httpClient.get<TeacherStudentNoteListResponse>("/teacher-student-notes/my-notes");
    return response.data.data;
  },

  getNote: async (noteId: string): Promise<TeacherStudentNoteDto> => {
    const response = await httpClient.get<TeacherStudentNoteResponse>(`/teacher-student-notes/${noteId}`);
    return response.data.data;
  },

  getNoteForStudent: async (studentId: string): Promise<TeacherStudentNoteDto | null> => {
    try {
      const response = await httpClient.get<TeacherStudentNoteResponse>(
        `/teacher-student-notes/student/${encodeURIComponent(studentId)}`
      );
      return response.data.data;
    } catch (err: unknown) {
      // 404 means no note yet for this student
      if (err && typeof err === "object" && "response" in err && (err as { response?: { status?: number } }).response?.status === 404) {
        return null;
      }
      throw err;
    }
  },

  createNote: async (request: CreateTeacherStudentNoteRequest): Promise<TeacherStudentNoteDto> => {
    const response = await httpClient.post<TeacherStudentNoteResponse>("/teacher-student-notes", request);
    return response.data.data;
  },

  updateNote: async (
    noteId: string,
    request: UpdateTeacherStudentNoteRequest
  ): Promise<TeacherStudentNoteDto> => {
    const response = await httpClient.put<TeacherStudentNoteResponse>(
      `/teacher-student-notes/${noteId}`,
      request
    );
    return response.data.data;
  },

  upsertNoteForStudent: async (
    studentId: string,
    request: UpsertTeacherStudentNoteRequest
  ): Promise<TeacherStudentNoteDto> => {
    const response = await httpClient.put<TeacherStudentNoteResponse>(
      `/teacher-student-notes/student/${encodeURIComponent(studentId)}`,
      request
    );
    return response.data.data;
  },

  deleteNote: async (noteId: string, rowVersion?: string): Promise<void> => {
    await httpClient.delete(`/teacher-student-notes/${noteId}`, {
      params: rowVersion ? { rowVersion } : undefined,
    });
  },
};
