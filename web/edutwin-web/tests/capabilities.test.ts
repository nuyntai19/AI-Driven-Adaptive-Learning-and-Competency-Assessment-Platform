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

test("r08 student capabilities require exact permissions", () => {
  const student = {
    accountType: "Student" as const,
    permissions: [
      "dashboards.student.read_own",
      "twin.student.read_own",
      "learning.attempts.submit",
    ],
  };

  assert.equal(canAccess(student, { allOf: ["dashboards.student.read_own"] }), true);
  assert.equal(canAccess(student, { allOf: ["twin.student.read_own"] }), true);
  assert.equal(canAccess(student, { allOf: ["learning.attempts.submit"] }), true);
  // Student cannot access teacher review or center dashboard
  assert.equal(canAccess(student, { allOf: ["dashboards.teacher.read_scoped"] }), false);
  assert.equal(canAccess(student, { allOf: ["dashboards.center.read"] }), false);
  assert.equal(canAccess(student, { allOf: ["twin.reasoning.review"] }), false);
});

test("r08 teacher capabilities require exact permissions", () => {
  const teacher = {
    accountType: "Teacher" as const,
    permissions: [
      "dashboards.teacher.read_scoped",
      "twin.reasoning.review",
      "twin.reasoning.override",
      "twin.student.read_scoped",
    ],
  };

  assert.equal(canAccess(teacher, { allOf: ["dashboards.teacher.read_scoped"] }), true);
  assert.equal(canAccess(teacher, { anyOf: ["twin.reasoning.review", "twin.reasoning.override"] }), true);
  assert.equal(canAccess(teacher, { allOf: ["twin.student.read_scoped"] }), true);
  // Teacher cannot access center dashboard
  assert.equal(canAccess(teacher, { allOf: ["dashboards.center.read"] }), false);
});

test("r08 center manager capabilities require exact permissions", () => {
  const manager = {
    accountType: "CenterManager" as const,
    permissions: ["dashboards.center.read"],
  };

  assert.equal(canAccess(manager, { allOf: ["dashboards.center.read"] }), true);
  assert.equal(canAccess(manager, { allOf: ["dashboards.student.read_own"] }), false);
});
