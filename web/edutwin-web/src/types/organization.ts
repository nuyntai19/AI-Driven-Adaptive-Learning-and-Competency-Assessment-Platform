import type { Meta, UserStatus } from "./auth";

export interface TeacherDto {
  teacherId: string;
  username: string;
  displayName: string;
  department: string | null;
  status: UserStatus;
  classCount: number;
  rowVersion: string;
}

export interface CreateTeacherRequest {
  username: string;
  temporaryPassword: string;
  displayName: string;
  department?: string;
}

export interface UpdateTeacherRequest {
  displayName?: string;
  department?: string | null;
  status?: UserStatus;
  rowVersion: string;
}

export interface UpdateStudentRequest {
  fullName?: string;
  gradeLevel?: number;
  status?: UserStatus;
  rowVersion: string;
}

export interface StudentSubjectGoalDto {
  goalId: string;
  studentId: string;
  subjectId: string;
  targetScore: number;
  remainingDays: number;
  currentPredictedScore: number;
  riskScore: number;
  rowVersion: string;
}

export interface UpsertStudentSubjectGoalRequest {
  targetScore: number;
  remainingDays: number;
  rowVersion?: string;
}

export interface StudentSubjectGoalResponse {
  data: StudentSubjectGoalDto;
  meta: Meta;
}


export interface StudentDetailDto {
  studentId: string;
  username: string;
  fullName: string;
  gradeLevel: number;
  status: UserStatus;
  activeClassCount: number;
  rowVersion: string;
  classes: ClassDto[];
  subjectGoals: StudentSubjectGoalDto[];
}

export interface StudentDetailResponse {
  data: StudentDetailDto;
  meta: Meta;
}

export interface ResetAccountPasswordRequest {
  newPassword: string;
  expectedUserRowVersion: string;
  reason: string;
}

export interface ResetAccountPasswordData {
  targetUserId: string;
  newRowVersion: string;
}

export interface ResetAccountPasswordResponse {
  data: ResetAccountPasswordData;
  meta: Meta;
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

export interface SubjectDto {
  subjectId: string;
  subjectCode: string;
  subjectName: string;
  description: string | null;
  isActive: boolean;
  rowVersion: string;
}

export interface SubjectListResponse {
  data: SubjectDto[];
  meta: Meta;
}

export interface SubjectResponse {
  data: SubjectDto;
  meta: Meta;
}

export interface CreateSubjectRequest {
  subjectCode: string;
  subjectName: string;
  description?: string | null;
}

export interface UpdateSubjectRequest {
  subjectCode: string;
  subjectName: string;
  description?: string | null;
  isActive: boolean;
  rowVersion: string;
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

export interface CreateClassRequest {
  className: string;
  academicYear: string;
  subjectId: string;
  teacherId: string;
}

export interface UpdateClassRequest {
  className: string;
  teacherId: string;
  status: ClassStatus;
  rowVersion: string;
}

export interface AddStudentsToClassRequest {
  studentIds: string[];
}

export interface AddStudentsToClassData {
  classId: string;
  addedCount: number;
  alreadyMemberCount: number;
}

export interface AddStudentsToClassResponse {
  data: AddStudentsToClassData;
  meta: Meta;
}

export interface ClassStudentListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
}

export interface CandidateStudentListParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

export interface ClassResponse {
  data: ClassDto;
  meta: Meta;
}

export interface StudentDto {
  studentId: string;
  username: string;
  fullName: string;
  gradeLevel: number;
  status: UserStatus;
  activeClassCount: number;
  rowVersion: string;
}

export interface StudentListParams {
  page: number;
  pageSize: number;
  search?: string;
  status?: UserStatus;
  gradeLevel?: number;
  classId?: string;
}

export interface StudentListResponse {
  data: StudentDto[];
  meta: PagedMeta;
}

export interface StudentResponse {
  data: StudentDto;
  meta: Meta;
}

export interface CreateStudentRequest {
  username: string;
  temporaryPassword: string;
  fullName: string;
  gradeLevel: number;
  classIds: string[];
}

export interface CenterProfileDto {
  centerId: string;
  centerCode: string;
  centerName: string;
  status: string;
  timezone: string;
  rowVersion: string;
}

export interface CenterProfileResponse {
  data: CenterProfileDto;
  meta: Meta;
}

export interface UpdateCenterProfileRequest {
  centerName: string;
  timezone: string;
  rowVersion: string;
}
