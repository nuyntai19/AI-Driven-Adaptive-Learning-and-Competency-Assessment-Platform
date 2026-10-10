import { extractProblemDetails, mapSafeOperationalError } from "./problemDetails.ts";

/** Only controlled business failures may expose their explanatory detail. */
export function academicLifecycleError(error: unknown, fallback: string): string {
  const details = extractProblemDetails(error);
  if ((details.status === 400 || details.status === 409) &&
      (details.errorCode === "INVALID_STATE_TRANSITION" || details.errorCode === "VALIDATION_FAILED") && details.detail) {
    return details.detail;
  }
  return mapSafeOperationalError(error, fallback);
}
