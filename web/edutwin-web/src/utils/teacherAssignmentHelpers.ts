/** The assignment API stores one-based orderIndex values. Do not increment them again. */
export function getAssignmentQuestionNumber(orderIndex: number | undefined, fallbackIndex: number): number {
  return typeof orderIndex === "number" && Number.isInteger(orderIndex) && orderIndex > 0
    ? orderIndex
    : fallbackIndex + 1;
}
