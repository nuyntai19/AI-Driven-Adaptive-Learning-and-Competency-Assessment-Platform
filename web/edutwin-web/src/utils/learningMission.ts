import { isAxiosError } from "axios";
import type { AgendaItemDto } from "../types/learningPath";

export function ongoingMissionFromError(error: unknown, selected: AgendaItemDto): AgendaItemDto | null {
  if (!isAxiosError(error) || error.response?.status !== 409) return null;
  const problem = error.response.data;
  const ongoing = problem?.ongoingMission;
  if (problem?.errorCode !== "LEARNING_MISSION_IN_PROGRESS" || !ongoing ||
      typeof ongoing.missionId !== "string" || !ongoing.missionId ||
      typeof ongoing.title !== "string" || !Number.isSafeInteger(ongoing.runId) || ongoing.runId <= 0) return null;
  return { ...selected, missionId: ongoing.missionId, title: ongoing.title, skillName: "Buổi học đang dở",
    activeRunId: ongoing.runId, latestRunId: ongoing.runId, status: "InProgress", reviewOnly: false };
}

export function missionAction(mission: AgendaItemDto) {
  if (mission.status === "Completed") return { label: "Xem lại", reviewOnly: true };
  if (mission.activeRunId) return { label: "Tiếp tục", reviewOnly: false };
  if (mission.latestRunId) return { label: "Xem lại", reviewOnly: true };
  return { label: mission.canStart ? "Bắt đầu" : "Chưa đủ điều kiện", reviewOnly: false };
}
