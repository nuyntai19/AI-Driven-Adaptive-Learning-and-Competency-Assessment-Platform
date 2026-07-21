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
