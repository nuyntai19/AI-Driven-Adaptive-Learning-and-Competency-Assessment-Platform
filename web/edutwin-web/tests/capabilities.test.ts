import assert from "node:assert/strict";
import test from "node:test";
import { canAccess, hasAllPermissions, hasAnyPermission, hasPermission } from "../src/auth/capabilities.ts";

const user = {
  accountType: "Teacher" as const,
  permissions: ["organization.classes.read", "organization.students.read"],
};

test("permission helpers fail closed without a user", () => {
  assert.equal(hasPermission(null, "organization.classes.read"), false);
  assert.equal(canAccess(null, { allOf: [] }), false);
});

test("permission helpers evaluate exact codes", () => {
  assert.equal(hasPermission(user, "organization.classes.read"), true);
  assert.equal(hasPermission(user, "organization.classes.create"), false);
  assert.equal(hasAnyPermission(user, ["missing", "organization.students.read"]), true);
  assert.equal(hasAllPermissions(user, ["organization.classes.read", "organization.students.read"]), true);
  assert.equal(hasAllPermissions(user, ["organization.classes.read", "missing"]), false);
});

test("capability access combines account type, allOf and anyOf", () => {
  assert.equal(canAccess(user, {
    accountTypes: ["Teacher"],
    allOf: ["organization.classes.read"],
    anyOf: ["missing", "organization.students.read"],
  }), true);
  assert.equal(canAccess(user, { accountTypes: ["CenterManager"] }), false);
  assert.equal(canAccess(user, { allOf: ["organization.classes.create"] }), false);
  assert.equal(canAccess(user, { anyOf: ["missing"] }), false);
});

test("legacy role fields cannot grant a capability", () => {
  const compatibilityRoleOnly = { accountType: "CenterManager" as const, permissions: [] };
  assert.equal(canAccess(compatibilityRoleOnly, { allOf: ["authorization.roles.read"] }), false);
});
