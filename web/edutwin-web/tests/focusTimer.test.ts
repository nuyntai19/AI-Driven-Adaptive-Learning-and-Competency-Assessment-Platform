import assert from "node:assert/strict";
import test from "node:test";
import { formatFocusTime, remainingFocusSeconds } from "../src/utils/focusTimer.ts";

test("focus time follows elapsed wall time after tab suspension", () => {
  const deadline = 1_000_000 + 25 * 60 * 1000;
  assert.equal(remainingFocusSeconds(deadline, 1_000_000 + 9 * 60 * 1000), 16 * 60);
  assert.equal(remainingFocusSeconds(deadline, deadline + 120_000), 0);
});

test("partial seconds round up and formatting preserves minutes", () => {
  assert.equal(remainingFocusSeconds(10_001, 10_000), 1);
  assert.equal(formatFocusTime(25 * 60), "25:00");
  assert.equal(formatFocusTime(59), "00:59");
  assert.equal(formatFocusTime(0), "00:00");
});
