import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { ADMIN_LOGIN_PATH, CENTER_LOGIN_PATH, isPlatformCenterCode, loginPathFor, loginPathForRoute } from "../src/auth/loginPortal.ts";

test("platform administrators use /admin, every center account uses /dang-nhap", () => {
  assert.equal(ADMIN_LOGIN_PATH, "/admin");
  assert.equal(CENTER_LOGIN_PATH, "/dang-nhap");
  assert.equal(loginPathFor("PlatformAdmin"), "/admin");
  for (const type of ["CenterManager", "Teacher", "Student", undefined, null]) assert.equal(loginPathFor(type), "/dang-nhap");
  assert.equal(loginPathForRoute("/quan-tri-nen-tang/trung-tam"), "/admin");
  assert.equal(loginPathForRoute("/hoc-tap"), "/dang-nhap");
});

test("the center login page refuses the platform tenant in any casing", () => {
  for (const code of ["PLATFORM", "platform", " Platform "]) assert.equal(isPlatformCenterCode(code), true);
  assert.equal(isPlatformCenterCode("EDUTWIN_A"), false);
  const loginPage = readFileSync(new URL("../src/pages/LoginPage.tsx", import.meta.url), "utf8");
  assert.match(loginPage, /isPlatformCenterCode\(trimmedCenterCode\)/);
  const app = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  assert.match(app, /<Route path="\/admin" element=\{<AdminLoginPage \/>\} \/>/);
});
