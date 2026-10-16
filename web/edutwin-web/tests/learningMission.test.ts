import test from "node:test";
import assert from "node:assert/strict";
import { missionAction, ongoingMissionFromError } from "../src/utils/learningMission.ts";
import type { AgendaItemDto } from "../src/types/learningPath.ts";

const mission = { missionId: "selected", status: "NotStarted", canStart: true } as AgendaItemDto;
test("one action opens completed work for review and resumes unfinished work", () => {
  assert.deepEqual(missionAction({ ...mission, status: "Completed", latestRunId: 8 }), { label: "Xem lại", reviewOnly: true });
  assert.deepEqual(missionAction({ ...mission, activeRunId: 8, latestRunId: 8 }), { label: "Tiếp tục", reviewOnly: false });
  assert.deepEqual(missionAction({ ...mission, canStart: false, latestRunId: 8 }), { label: "Xem lại", reviewOnly: true });
  assert.equal(missionAction(mission).label, "Bắt đầu");
});
test("structured conflict opens the named unfinished mission instead of readonly empty review", () => {
  const error = { isAxiosError: true, response: { status: 409, data: { errorCode: "LEARNING_MISSION_IN_PROGRESS", ongoingMission: { missionId: "ongoing", runId: 42, title: "Rèn từ vựng" } } } };
  const resumed = ongoingMissionFromError(error, mission);
  assert.equal(resumed?.missionId, "ongoing"); assert.equal(resumed?.activeRunId, 42);
  assert.equal(resumed?.title, "Rèn từ vựng"); assert.equal(resumed?.reviewOnly, false);
  assert.equal(ongoingMissionFromError(new Error("unrelated"), mission), null);
  error.response.data.ongoingMission.runId = -1;
  assert.equal(ongoingMissionFromError(error, mission), null);
});
