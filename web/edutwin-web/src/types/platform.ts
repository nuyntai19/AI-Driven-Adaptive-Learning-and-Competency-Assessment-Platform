export interface PlatformCenterListItem {
  centerId: string;
  centerCode: string;
  centerName: string;
  status: "Active" | "Suspended" | string;
  timezone: string;
  createdAt: string;
  rowVersion: string;
  primaryManagerUserId?: string | null;
  primaryManagerUsername?: string | null;
  primaryManagerDisplayName?: string | null;
  primaryManagerUserRowVersion?: string | null;
  initialManagerUserId?: string | null;
  initialManagerUsername?: string | null;
  initialManagerDisplayName?: string | null;
  initialManagerUserRowVersion?: string | null;
  activeStudentCount?: number;
  activeTeacherCount?: number;
  classCount?: number;
  activeManagerCount?: number;
  hasActivePrimaryManager?: boolean;
}

export interface UpdateCenterMetadataRequest {
  centerName?: string;
  timezone?: string;
  expectedRowVersion?: string;
  reason: string;
}

export interface CreatePlatformCenterRequest {
  centerCode: string;
  centerName: string;
  timezone: string;
  initialManagerUsername: string;
  initialManagerDisplayName: string;
  initialManagerPassword: string;
}

export interface UpdatePlatformCenterStatusRequest {
  status: string;
  rowVersion: string;
  reason?: string;
}

export interface ResetCenterManagerPasswordRequest {
  newPassword: string;
  expectedUserRowVersion: string;
  reason?: string;
}

export interface ResetCenterManagerPasswordResponseData {
  centerId: string;
  managerUserId: string;
  newUserRowVersion: string;
  resetAtUtc: string;
  success: boolean;
}

export interface PlatformCentersListData {
  items: PlatformCenterListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PlatformCenterManagerListItem {
  userId: string;
  username: string;
  displayName: string;
  status: "Active" | "Locked" | "Disabled" | string;
  isPrimary: boolean;
  createdAt: string;
  rowVersion: string;
  authVersion: number;
}

export interface PlatformCenterManagersListData {
  items: PlatformCenterManagerListItem[];
  totalCount: number;
  primaryManagerUserId?: string | null;
  page: number;
  pageSize: number;
}

export interface CreateCenterManagerRequest {
  username: string;
  displayName: string;
  password: string;
  expectedCenterRowVersion: string;
  reason: string;
}

export interface CreateCenterManagerResponseData {
  userId: string;
  centerId: string;
  username: string;
  displayName: string;
  status: string;
  isPrimary: boolean;
  createdAt: string;
  rowVersion: string;
  centerRowVersion: string;
}

export interface UpdateCenterManagerStatusRequest {
  status: string;
  expectedUserRowVersion: string;
  reason: string;
}

export interface UpdateCenterManagerStatusData {
  userId: string;
  centerId: string;
  status: string;
  isPrimary: boolean;
  rowVersion: string;
  updatedAtUtc: string;
}

export interface MakePrimaryCenterManagerRequest {
  expectedCenterRowVersion: string;
  expectedManagerUserRowVersion: string;
  disablePreviousPrimary: boolean;
  expectedPreviousPrimaryUserRowVersion?: string;
  reason: string;
}

export interface MakePrimaryCenterManagerData {
  centerId: string;
  primaryManagerUserId: string;
  newCenterRowVersion: string;
  previousPrimaryDisabled: boolean;
  updatedAtUtc: string;
}

export interface PlatformAuditItem {
  auditId: string;
  centerId: string;
  targetCenterId?: string | null;
  targetCenterCode?: string | null;
  actorUserId?: string | null;
  actorUsername?: string | null;
  actionType: string;
  targetType: string;
  targetId: string;
  beforeData?: Record<string, unknown> | null;
  afterData?: Record<string, unknown> | null;
  reason: string;
  traceId: string;
  createdAt: string;
}

export interface PlatformAuditListData {
  items: PlatformAuditItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PlatformAuditQuery {
  page?: number;
  pageSize?: number;
  actionType?: string;
  targetType?: string;
  targetId?: string;
  targetCenterId?: string;
  actorUserId?: string;
  traceId?: string;
  fromUtc?: string;
  toUtc?: string;
  search?: string;
}

export interface PlatformSecurityProfile {
  userId: string;
  username: string;
  displayName: string;
  roleName: string;
  lastLoginAt?: string | null;
  authVersion: number;
  rowVersion: string;
  activeSessionCount: number;
}

export interface PlatformChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface PlatformRevokeSessionsRequest {
  reason?: string;
}
