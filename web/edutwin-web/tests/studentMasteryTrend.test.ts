import assert from "node:assert/strict";
import test from "node:test";
import { buildMasteryTrend } from "../src/utils/studentMasteryTrend.ts";
import type { TwinUpdateHistoryItemDto } from "../src/types/digitalTwin.ts";

function event(id: string, topic: string, before: number, after: number, minute: number): TwinUpdateHistoryItemDto {
  return { historyId: id, topicNodeId: topic, topicName: topic, previousMastery: before, newMastery: after,
    delta: after - before, recordedAt: `2026-11-13T10:${String(minute).padStart(2, "0")}:00Z`,
    eventSource: "Practice", learningContext: "AdaptivePractice", contextLabel: "Luyện tập", explanation: "" };
}

test("average uses a fixed topic set and carries unchanged mastery across learning events", () => {
  const trend = buildMasteryTrend([event("3", "b", 20, 10, 3), event("1", "a", 0, 20, 1), event("2", "a", 20, 40, 2)]);
  assert.deepEqual(trend.rows.map(row => row.value), [10, 20, 30, 25]);
  assert.equal(trend.delta, 15);
  assert.deepEqual(trend.rows.map(row => row.index), [0, 1, 2, 3]);
  assert.deepEqual(trend.rows.map(row => row.values), [
    { a: 0, b: 20 }, { a: 20, b: 20 }, { a: 40, b: 20 }, { a: 40, b: 10 },
  ]);
});

test("topic focus preserves real zeroes, flat results, increases and decreases", () => {
  const trend = buildMasteryTrend([event("1", "a", 0, 0, 1), event("2", "b", 0, 90, 2), event("3", "a", 0, 20, 3), event("4", "a", 20, 5, 4)], "a");
  assert.deepEqual(trend.rows.map(row => row.value), [0, 0, 20, 5]);
  assert.equal(trend.current, 5);
  assert.equal(trend.focused?.id, "a");
});

test("a single observation has its recorded prior baseline; empty or invalid history is safe", () => {
  assert.deepEqual(buildMasteryTrend([event("1", "a", 10, 15, 1)]).rows.map(row => row.value), [10, 15]);
  assert.equal(buildMasteryTrend([]).current, 0);
  assert.equal(buildMasteryTrend([{ ...event("1", "a", 0, 10, 1), recordedAt: "invalid" }]).topics.length, 0);
  assert.equal(buildMasteryTrend([event("1", "a", 0, 10, 1)], "missing").current, 10);
});
