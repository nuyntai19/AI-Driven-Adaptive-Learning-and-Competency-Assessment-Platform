export type UserRole = "Student" | "Teacher" | "CenterManager";
export type UserStatus = "Active" | "Locked" | "Disabled";

export interface AuthUser {
  userId: string;
  centerId: string;
  centerName: string;
  username: string;
  displayName: string;
  role: UserRole;
  status?: UserStatus;
}

export interface LoginRequest {
  centerCode: string;
  username: string;
  password: string;
}

export interface Meta {
  traceId: string;
  timestamp: string;
}

export interface LoginData {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  user: AuthUser;
}

export interface LoginResponse {
  data: LoginData;
  meta: Meta;
}

export interface CurrentUserResponse {
  data: AuthUser;
  meta: Meta;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}
