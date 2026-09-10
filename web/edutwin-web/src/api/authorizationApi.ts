import { httpClient } from "./httpClient";
import type {
  AuthorizationAuditResponse,
  AuthorizationRoleListResponse,
  AuthorizationRoleResponse,
  CreateAuthorizationRoleRequest,
  PermissionListResponse,
  ReplaceRolePermissionsRequest,
  ReplaceUserRolesRequest,
  UpdateAuthorizationRoleRequest,
  UserAuthorizationResponse,
} from "../types/authorization";

export const authorizationApi = {
  listPermissions: async (): Promise<PermissionListResponse> => {
    const response = await httpClient.get<PermissionListResponse>(
      "/authorization/permissions",
      { params: { page: 1, pageSize: 100, status: "Active" } },
    );
    return response.data;
  },

  listRoles: async (): Promise<AuthorizationRoleListResponse> => {
    const response = await httpClient.get<AuthorizationRoleListResponse>(
      "/authorization/roles",
      { params: { page: 1, pageSize: 100 } },
    );
    return response.data;
  },

  createRole: async (
    request: CreateAuthorizationRoleRequest,
  ): Promise<AuthorizationRoleResponse> => {
    const response = await httpClient.post<AuthorizationRoleResponse>(
      "/authorization/roles",
      request,
    );
    return response.data;
  },

  updateRole: async (
    roleId: string,
    request: UpdateAuthorizationRoleRequest,
  ): Promise<AuthorizationRoleResponse> => {
    const response = await httpClient.patch<AuthorizationRoleResponse>(
      `/authorization/roles/${roleId}`,
      request,
    );
    return response.data;
  },

  replaceRolePermissions: async (
    roleId: string,
    request: ReplaceRolePermissionsRequest,
  ): Promise<AuthorizationRoleResponse> => {
    const response = await httpClient.put<AuthorizationRoleResponse>(
      `/authorization/roles/${roleId}/permissions`,
      request,
    );
    return response.data;
  },

  getUserAuthorization: async (
    userId: string,
  ): Promise<UserAuthorizationResponse> => {
    const response = await httpClient.get<UserAuthorizationResponse>(
      `/authorization/users/${userId}/roles`,
    );
    return response.data;
  },

  replaceUserRoles: async (
    userId: string,
    request: ReplaceUserRolesRequest,
  ): Promise<UserAuthorizationResponse> => {
    const response = await httpClient.put<UserAuthorizationResponse>(
      `/authorization/users/${userId}/roles`,
      request,
    );
    return response.data;
  },

  listAudit: async (): Promise<AuthorizationAuditResponse> => {
    const response = await httpClient.get<AuthorizationAuditResponse>(
      "/authorization/audit",
      { params: { page: 1, pageSize: 50 } },
    );
    return response.data;
  },
};
