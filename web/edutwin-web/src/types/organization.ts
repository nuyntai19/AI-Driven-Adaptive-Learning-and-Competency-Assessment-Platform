import type { UserStatus } from "./auth";

export interface TeacherDto {
  teacherId: string;
  username: string;
  displayName: string;
  department: string | null;
  status: UserStatus;
  classCount: number;
  rowVersion: string;
}

export interface PagedMeta {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  traceId: string;
  timestamp: string;
}

export interface TeacherListResponse {
  data: TeacherDto[];
  meta: PagedMeta;
}

export interface TeacherListParams {
  page: number;
  pageSize: number;
  search?: string;
  status?: UserStatus;
}

export type ClassStatus = "Active" | "Archived";

export interface ClassSubjectDto {
  subjectId: string;
  subjectName: string;
}

export interface ClassTeacherDto {
  teacherId: string;
  displayName: string;
}

export interface ClassDto {
  classId: string;
  className: string;
  academicYear: string;
  subject: ClassSubjectDto;
  teacher: ClassTeacherDto;
  studentCount: number;
  status: ClassStatus;
  rowVersion: string;
}

export interface ClassListResponse {
  data: ClassDto[];
  meta: PagedMeta;
}

export interface ClassListParams {
  page: number;
  pageSize: number;
  teacherId?: string;
  subjectId?: string;
  status?: ClassStatus;
}
