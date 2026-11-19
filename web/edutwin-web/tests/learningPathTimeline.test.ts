import test from "node:test";
import assert from "node:assert/strict";
import { learningPathTimeline } from "../src/utils/learningPathTimeline.ts";
import type { AgendaItemDto } from "../src/types/learningPath.ts";

test("personal stages preserve prerequisite order and give self-practice mocks their own stage", () => {
  const missions = [
    { milestoneOrder: 1, skillId: "base", plannedDate: "2026-10-26" },
    { milestoneOrder: 2, skillId: "target", plannedDate: "2026-10-29" },
    { milestoneOrder: 3, skillId: "base", plannedDate: "2026-11-01", isMockExam: true },
  ] as AgendaItemDto[];
  const entries = learningPathTimeline(missions);
  assert.deepEqual(entries.map(e => e.key), ["stage-1", "stage-2", "stage-3"]);
  assert.deepEqual(entries.map(e => e.missions[0]), missions);
});

test("a student with no personal missions has an empty route", () => {
  assert.deepEqual(learningPathTimeline([]), []);
});
