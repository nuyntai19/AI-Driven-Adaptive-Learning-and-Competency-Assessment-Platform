export interface PlatformCenterListItem {
  centerId: string;
  centerCode: string;
  centerName: string;
  status: "Active" | "Suspended" | string;
  timezone: string;
  createdAt: string;
  rowVersion: string;
  initialManagerUserId?: string | null;
  initialManagerUsername?: string | null;
  initialManagerDisplayName?: string | null;
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
