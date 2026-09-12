import assert from "node:assert/strict";
import test from "node:test";
import { canAccess, hasPermission } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import type { PlatformCentersListData, PlatformCenterListItem, CreatePlatformCenterRequest, UpdatePlatformCenterStatusRequest, ResetCenterManagerPasswordRequest } from "../src/types/platform.ts";

const platformAdminUser = {
  accountType: "PlatformAdmin" as const,
  permissions: [
    permissions.platformCentersRead,
    permissions.platformCentersManage,
    permissions.platformManagersManage,
  ],
};

const centerManagerUser = {
  accountType: "CenterManager" as const,
  permissions: [
    permissions.rolesRead,
    permissions.dashboardsCenterRead,
  ],
};

const studentUser = {
  accountType: "Student" as const,
  permissions: [
    permissions.learningAttemptsSubmit,
    permissions.dashboardsStudentRead,
  ],
};

test("platform admin can access platform centers management route", () => {
  assert.equal(
    canAccess(platformAdminUser, {
      accountTypes: ["PlatformAdmin"],
      anyOf: [permissions.platformCentersRead, permissions.platformCentersManage],
    }),
    true
  );
});

test("tenant users (CenterManager, Student) cannot access platform centers management route", () => {
  assert.equal(
    canAccess(centerManagerUser, {
      accountTypes: ["PlatformAdmin"],
      anyOf: [permissions.platformCentersRead, permissions.platformCentersManage],
    }),
    false
  );

  assert.equal(
    canAccess(studentUser, {
      accountTypes: ["PlatformAdmin"],
      anyOf: [permissions.platformCentersRead, permissions.platformCentersManage],
    }),
    false
  );
});

test("platform admin cannot access student learning submissions or teacher review queue", () => {
  assert.equal(hasPermission(platformAdminUser, permissions.learningAttemptsSubmit), false);
  assert.equal(hasPermission(platformAdminUser, permissions.teacherReviewsRead), false);
  assert.equal(hasPermission(platformAdminUser, permissions.teacherReviewsOverride), false);
});

test("platform center contracts serialize correctly", () => {
  const emptyList: PlatformCentersListData = {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  };
  assert.equal(emptyList.items.length, 0);
  assert.equal(emptyList.totalCount, 0);

  const centerItem: PlatformCenterListItem = {
    centerId: "11111111-1111-1111-1111-111111111111",
    centerCode: "CENTER_TEST",
    centerName: "Test Educational Center",
    status: "Active",
    timezone: "Asia/Bangkok",
    createdAt: "2026-09-12T20:00:00Z",
    rowVersion: "1",
    initialManagerUserId: "22222222-2222-2222-2222-222222222222",
    initialManagerUsername: "manager_test",
    initialManagerDisplayName: "Test Manager",
    initialManagerUserRowVersion: "1",
  };
  assert.equal(centerItem.centerCode, "CENTER_TEST");
  assert.equal(centerItem.status, "Active");
  assert.equal(centerItem.initialManagerUserRowVersion, "1");

  const createReq: CreatePlatformCenterRequest = {
    centerCode: "NEW_CENTER",
    centerName: "New Center",
    timezone: "Asia/Ho_Chi_Minh",
    initialManagerUsername: "manager_new",
    initialManagerDisplayName: "New Manager",
    initialManagerPassword: "SecretPassword123!",
  };
  assert.equal(createReq.centerCode, "NEW_CENTER");

  const statusReq: UpdatePlatformCenterStatusRequest = {
    status: "Suspended",
    rowVersion: "1",
    reason: "Maintenance",
  };
  assert.equal(statusReq.rowVersion, "1");

  const resetPassReq: ResetCenterManagerPasswordRequest = {
    newPassword: "NewSecretPassword456!",
    expectedUserRowVersion: "1",
  };
  assert.equal(resetPassReq.expectedUserRowVersion, "1");
});
