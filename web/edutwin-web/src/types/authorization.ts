import type { AccountType, Meta } from "./auth";

export type AuthorizationRoleStatus = "Active" | "Archived";
export type PermissionStatus = "Active" | "Deprecated";

export interface PagedMeta extends Meta {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface PermissionDto {
  permissionCode: string;
  module: string;
  resource: string;
  action: string;
  description: string;
  allowedAccountTypes: AccountType[];
  isSensitive: boolean;
  isDelegable: boolean;
  status: PermissionStatus;
}

export interface AuthorizationRoleDto {
  roleId: string;
  roleCode: string;
  roleName: string;
  accountType: AccountType;
  description: string | null;
  isSystemRole: boolean;
  status: AuthorizationRoleStatus;
  permissionCodes: string[];
  activeUserCount: number;
  rowVersion: string;
}

export interface AssignedAuthorizationRoleDto {
  roleId: string;
  roleCode: string;
  roleName: string;
  accountType: AccountType;
  assignmentStatus: "Active" | "Revoked";
  assignedAt: string;
  revokedAt: string | null;
}

export interface UserAuthorizationDto {
  userId: string;
  accountType: AccountType;
  roles: AssignedAuthorizationRoleDto[];
  permissions: string[];
  rowVersion: string;
  authorizationVersion: number;
}

export interface AuthorizationAuditDto {
  authorizationAuditId: number;
  actorUserId: string | null;
  actionType: string;
  targetType: string;
  targetId: string;
  targetUserId: string | null;
  permissionCode: string | null;
  before: unknown;
  after: unknown;
  reason: string;
  traceId: string;
  createdAt: string;
}

export interface PermissionListResponse {
  data: PermissionDto[];
  meta: PagedMeta;
}

export interface AuthorizationRoleListResponse {
  data: AuthorizationRoleDto[];
  meta: PagedMeta;
}

export interface AuthorizationRoleResponse {
  data: AuthorizationRoleDto;
  meta: Meta;
}

export interface UserAuthorizationResponse {
  data: UserAuthorizationDto;
  meta: Meta;
}

export interface AuthorizationAuditResponse {
  data: AuthorizationAuditDto[];
  meta: PagedMeta;
}

export interface CreateAuthorizationRoleRequest {
  roleCode: string;
  roleName: string;
  accountType: AccountType;
  description?: string;
}

export interface UpdateAuthorizationRoleRequest {
  roleName: string;
  description?: string;
  status: AuthorizationRoleStatus;
  rowVersion: string;
  reason: string;
}

export interface ReplaceRolePermissionsRequest {
  permissionCodes: string[];
  rowVersion: string;
  reason: string;
}

export interface ReplaceUserRolesRequest {
  roleIds: string[];
  rowVersion: string;
  reason: string;
}

export interface AuthorizationUserOption {
  userId: string;
  displayName: string;
  username: string;
  accountType: AccountType;
  status?: string;
}
