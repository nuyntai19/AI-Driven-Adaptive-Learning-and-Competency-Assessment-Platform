import assert from "node:assert/strict";
import test from "node:test";
import { formatFocusTime, initialFocusTimer, nextFocusAlarmDeadline, remainingFocusSeconds, restoreFocusTimer } from "../src/utils/focusTimer.ts";

test("focus time follows elapsed wall time after tab suspension", () => {
  const deadline = 1_000_000 + 25 * 60 * 1000;
  assert.equal(remainingFocusSeconds(deadline, 1_000_000 + 9 * 60 * 1000), 16 * 60);
  assert.equal(remainingFocusSeconds(deadline, deadline + 120_000), 0);
});

test("clock alarm uses today's future time and tomorrow for a passed time", () => {
  const now = new Date(2026, 10, 21, 20, 15, 30).getTime();
  assert.equal(nextFocusAlarmDeadline("20:30", now), new Date(2026, 10, 21, 20, 30).getTime());
  assert.equal(nextFocusAlarmDeadline("20:15", now), new Date(2026, 10, 22, 20, 15).getTime());
  assert.equal(nextFocusAlarmDeadline("00:05", new Date(2026, 11, 31, 23, 59).getTime()), new Date(2027, 0, 1, 0, 5).getTime());
  for (const time of ["", "24:00", "20:60", "9:30", "bad"]) assert.equal(nextFocusAlarmDeadline(time, now), null);
});

test("restore supports existing countdowns and retains clock alarm settings", () => {
  const now = new Date(2026, 10, 21, 20, 15).getTime();
  const deadline = nextFocusAlarmDeadline("20:30", now)!;
  const running = { ...initialFocusTimer, mode: "alarm", alarmTime: "20:30", totalSeconds: 900, remaining: 900, deadline, soundEnabled: false };
  assert.deepEqual(restoreFocusTimer(running, now + 60_000), { ...running, remaining: 840 });
  const expired = restoreFocusTimer(running, deadline + 60_000);
  assert.equal(expired.finished, true);
  assert.equal(expired.deadline, null);
  assert.equal(expired.remaining, 0);
  assert.equal(expired.soundEnabled, false);
  assert.equal(expired.acknowledged, false);
  const old = { minutes: 10, remaining: 600, deadline: now + 600_000, finished: false };
  assert.equal(restoreFocusTimer(old, now).mode, "duration");
  assert.equal(restoreFocusTimer(old, now).totalSeconds, 600);
  assert.deepEqual(restoreFocusTimer({ ...running, remaining: -1 }, now), initialFocusTimer);
  assert.deepEqual(restoreFocusTimer({ ...running, alarmTime: "bad" }, now), initialFocusTimer);
});

test("partial seconds round up and formatting preserves minutes", () => {
  assert.equal(remainingFocusSeconds(10_001, 10_000), 1);
  assert.equal(formatFocusTime(25 * 60), "25:00");
  assert.equal(formatFocusTime(59), "00:59");
  assert.equal(formatFocusTime(0), "00:00");
});
