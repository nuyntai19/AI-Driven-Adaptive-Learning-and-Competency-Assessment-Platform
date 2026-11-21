import assert from "node:assert/strict";
import test from "node:test";
import { mapSafeOperationalError, isConcurrencyConflict } from "../src/utils/problemDetails.ts";

test("subject change blocked explains active classes and next action instead of invalid fields or stale data", () => {
  const error = { response: { status: 422, data: { errorCode: "TEACHER_SUBJECT_CHANGE_BLOCKED", detail: "SQL private provider details", traceId: "support-123" } } };
  const message = mapSafeOperationalError(error);
  assert.match(message, /giáo viên còn phụ trách lớp đang hoạt động thuộc môn khác/);
  assert.match(message, /chuyển giao các lớp này cho giáo viên phù hợp/);
  assert.doesNotMatch(message, /SQL|trường bắt buộc/);
  assert.equal(isConcurrencyConflict(error), false);
});
