import type { AdaptivePracticeMode } from "../types/adaptivePractice";

const modes: AdaptivePracticeMode[] = ["Weakness", "LearningPath", "CustomTopic", "SpacedReview"];
/** URL hints prefill a form; they never create a session or bypass a server check. */
export function readPracticePreferences(params: URLSearchParams) {
  const requestedMode = params.get("mode") as AdaptivePracticeMode | null;
  const mode: AdaptivePracticeMode = requestedMode && modes.includes(requestedMode) ? requestedMode : params.get("topicId") ? "CustomTopic" : "Weakness";
  const requestedMinutes = Number(params.get("minutes"));
  return { mode, topicId: mode === "CustomTopic" ? params.get("topicId") || "" : "", minutes: [10, 15, 20].includes(requestedMinutes) ? requestedMinutes : 15 };
}

export function canStartPractice(input: {
  classResolved: boolean;
  proposalReady: boolean;
  proposalUnavailable: boolean;
  activeSessionChecked: boolean;
  hasActiveSession: boolean;
}): boolean {
  return input.classResolved && input.proposalReady && !input.proposalUnavailable &&
    input.activeSessionChecked && !input.hasActiveSession;
}
