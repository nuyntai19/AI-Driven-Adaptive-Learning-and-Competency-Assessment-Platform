import type { StudentAssignmentDetailDto } from "../types/assignments";
import { isQuestionSubmissionLocked } from "./questionReview.ts";

export function isAssignmentWorkSubmitted(assignment: StudentAssignmentDetailDto | null | undefined,
  locallySubmitted = false): boolean {
  return locallySubmitted || assignment?.isSubmitted === true || assignment?.progress.status === "Completed" ||
    Boolean(assignment?.questions.length && assignment.questions.every(isQuestionSubmissionLocked));
}

export function shouldStartAssignment(assignment: StudentAssignmentDetailDto | null | undefined,
  locallySubmitted = false): boolean {
  return Boolean(assignment && !isAssignmentWorkSubmitted(assignment, locallySubmitted) && !assignment.startedAt);
}

export function getAssignmentReviewTiming(assignment: StudentAssignmentDetailDto | null | undefined) {
  if (!assignment || !isAssignmentWorkSubmitted(assignment)) return { submittedAt: null, elapsedSeconds: null };
  const valid = (value: string | null | undefined) => value && Number.isFinite(Date.parse(value)) ? value : null;
  const attempts = assignment.questions.map(q => valid(q.latestAttempt?.submittedAt)).filter((v): v is string => Boolean(v));
  const submittedAt = valid(assignment.submittedAt) ?? attempts.sort((a, b) => Date.parse(b) - Date.parse(a))[0] ?? null;
  const startedAt = valid(assignment.startedAt);
  const recorded = assignment.elapsedSeconds;
  const elapsedSeconds = recorded != null && Number.isFinite(recorded) && recorded >= 0
    ? Math.floor(recorded)
    : submittedAt && startedAt ? Math.max(0, Math.floor((Date.parse(submittedAt) - Date.parse(startedAt)) / 1000)) : null;
  return { submittedAt, elapsedSeconds };
}

export function isActiveAssignmentExpired(submitted: boolean, expiresAt: number | null, now = Date.now()) {
  return !submitted && expiresAt !== null && Number.isFinite(expiresAt) && now >= expiresAt;
}

export function formatReviewDuration(seconds: number) {
  const total = Math.max(0, Math.floor(seconds));
  return [Math.floor(total / 3600), Math.floor(total % 3600 / 60), total % 60]
    .map(value => String(value).padStart(2, "0")).join(":");
}
