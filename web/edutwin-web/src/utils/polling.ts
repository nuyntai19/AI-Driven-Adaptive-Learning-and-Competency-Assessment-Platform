export const TERMINAL_JOB_STATUSES = new Set([
  "completed",
  "fallbackcompleted",
  "failedterminal",
]);

export const SUCCESSFUL_TERMINAL_JOB_STATUSES = new Set([
  "completed",
  "fallbackcompleted",
]);

// This only limits the blocking UI, never the server job or saved submission.
export const ANALYSIS_FOREGROUND_WAIT_MS = 45_000;

export function shouldShowAnalysisWaitingScreen(
  jobId: string | null,
  backgroundJobId: string | null,
  networkPaused: boolean
): boolean {
  return Boolean(jobId && jobId !== backgroundJobId && !networkPaused);
}

export function isTerminalStatus(status: string | null | undefined): boolean {
  if (!status) return false;
  return TERMINAL_JOB_STATUSES.has(status.toLowerCase().trim());
}

export function isSuccessfulTerminalStatus(
  status: string | null | undefined
): boolean {
  if (!status) return false;
  return SUCCESSFUL_TERMINAL_JOB_STATUSES.has(status.toLowerCase().trim());
}

export function shouldContinuePolling(
  status: string | null | undefined,
  currentAttempt: number,
  maxAttempts: number = 60
): boolean {
  if (currentAttempt >= maxAttempts) return false;
  return !isTerminalStatus(status);
}
