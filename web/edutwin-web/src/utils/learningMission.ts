import { isAxiosError } from "axios";
import type { AgendaItemDto } from "../types/learningPath";

export interface BlockingLearningMission { missionId: string; runId: number; title: string; status: string; canResume: boolean }

export function pathChangeBlockersFromError(error: unknown): BlockingLearningMission[] {
  if (!isAxiosError(error) || error.response?.status !== 409 || error.response.data?.errorCode !== "LEARNING_PATH_CHANGE_BLOCKED") return [];
  const missions: unknown = error.response.data.blockingMissions;
  if (!Array.isArray(missions)) return [];
  return missions.filter((m): m is BlockingLearningMission => !!m && typeof m.missionId === "string" && !!m.missionId &&
    Number.isSafeInteger(m.runId) && m.runId > 0 && typeof m.title === "string" && typeof m.canResume === "boolean" &&
    ["InProgress", "Grading", "NeedsReview"].includes(m.status));
}

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
