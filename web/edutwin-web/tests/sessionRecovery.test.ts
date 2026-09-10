import assert from "node:assert/strict";
import test from "node:test";
import { shouldAttemptSessionRefresh } from "../src/auth/sessionRecovery.ts";

test("stale authorization version refreshes exactly on the first attempt", () => {
  assert.equal(shouldAttemptSessionRefresh({ status: 401, errorCode: "AUTHORIZATION_VERSION_STALE", alreadyRetried: false, isAuthEndpoint: false }), true);
  assert.equal(shouldAttemptSessionRefresh({ status: 401, errorCode: "AUTHORIZATION_VERSION_STALE", alreadyRetried: true, isAuthEndpoint: false }), false);
});

test("an expired bearer response without a problem code can refresh once", () => {
  assert.equal(shouldAttemptSessionRefresh({ status: 401, alreadyRetried: false, isAuthEndpoint: false }), true);
});

test("auth endpoints and non-401 responses never enter the refresh loop", () => {
  assert.equal(shouldAttemptSessionRefresh({ status: 401, alreadyRetried: false, isAuthEndpoint: true }), false);
  assert.equal(shouldAttemptSessionRefresh({ status: 403, errorCode: "AUTH_PERMISSION_REQUIRED", alreadyRetried: false, isAuthEndpoint: false }), false);
});

test("unrelated explicit 401 problem codes fail closed without refresh", () => {
  assert.equal(shouldAttemptSessionRefresh({ status: 401, errorCode: "ACCOUNT_DISABLED", alreadyRetried: false, isAuthEndpoint: false }), false);
});
