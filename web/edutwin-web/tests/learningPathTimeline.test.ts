import test from "node:test";
import assert from "node:assert/strict";
import { learningPathTimeline } from "../src/utils/learningPathTimeline.ts";
import type { AgendaItemDto, ExamMilestone } from "../src/types/learningPath.ts";

test("class exams join the route by local date while stage and prerequisite order is preserved", () => {
  const missions = [
    { milestoneOrder: 1, skillId: "base", plannedDate: "2026-10-26" },
    { milestoneOrder: 2, skillId: "target", plannedDate: "2026-10-29" },
    { milestoneOrder: 3, skillId: "base", plannedDate: "2026-11-01", isMockExam: true },
  ] as AgendaItemDto[];
  const exams = [{ assignmentId: "midterm", examAt: "2026-10-27T18:00:00Z" }] as ExamMilestone[];
  const entries = learningPathTimeline(missions, exams, "Asia/Ho_Chi_Minh");
  assert.deepEqual(entries.map(e => e.key), ["stage-1", "exam-midterm", "stage-2", "stage-3"]);
  assert.equal(entries.length, 4); // The mock sharing the first skill still has its own stage.
});

test("an exam remains visible without an accepted personal route", () => {
  const exam = { assignmentId: "final", examAt: "2026-10-28T08:00:00Z", status: "Cancelled" } as ExamMilestone;
  assert.deepEqual(learningPathTimeline([], [exam], "UTC").map(e => e.key), ["exam-final"]);
});
