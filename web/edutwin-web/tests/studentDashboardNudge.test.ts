import assert from "node:assert/strict";
import test from "node:test";
import { dashboardGreeting, dashboardNudge } from "../src/utils/studentDashboardNudge.ts";

test("greeting follows the time of day", () => {
  assert.equal(dashboardGreeting(8), "Chào buổi sáng");
  assert.equal(dashboardGreeting(12), "Chào buổi trưa");
  assert.equal(dashboardGreeting(15), "Chào buổi chiều");
  assert.equal(dashboardGreeting(21), "Chào buổi tối");
});

test("a streak at risk comes before everything else", () => {
  assert.match(dashboardNudge({ streak: 4, studiedToday: false, gainToday: { topicName: "Hàm số", delta: 2 } }), /giữ chuỗi 4 ngày/);
});

test("a real gain from today is named", () => {
  assert.match(dashboardNudge({ streak: 3, studiedToday: true, gainToday: { topicName: "Hàm số", delta: 2.5 } }), /Hàm số vừa tăng 2\.5%/);
});

test("no activity never claims progress", () => {
  const text = dashboardNudge({});
  assert.doesNotMatch(text, /chuỗi|tăng/i);
});
