/** Relative to the authenticated API client, never a public image URL. */
export function getAttemptAttachmentPath(attemptId: string | number): string {
  if (typeof attemptId === "number" && (!Number.isSafeInteger(attemptId) || attemptId <= 0)) {
    throw new Error("Mã lượt nộp không hợp lệ.");
  }
  const id = String(attemptId);
  if (!/^[1-9][0-9]*$/.test(id)) throw new Error("Mã lượt nộp không hợp lệ.");
  return `/learning/attempts/${id}/attachment`;
}
