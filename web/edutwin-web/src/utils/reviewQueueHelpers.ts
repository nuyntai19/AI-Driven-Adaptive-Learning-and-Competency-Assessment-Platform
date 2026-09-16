/**
 * Canonical helper utilities for Review Queue, Digital Twin & OCC versioning.
 * Shared between production components (ReviewQueuePage, TeacherStudentTwinPage, TeacherOverrideModal)
 * and unit/integration tests to guarantee zero test drift.
 */

/**
 * Canonical OCC token validator according to backend uint contract.
 * Validates that the version is a safe integer in the range [0, 4294967295] (32-bit unsigned integer).
 */
export const isValidOccVersion = (version: unknown): version is number => {
  return (
    typeof version === "number" &&
    Number.isSafeInteger(version) &&
    version >= 0 &&
    version <= 4294967295
  );
};

/**
 * Formats OCC version label for UI display.
 * Returns "#<version>" if valid, or "Không khả dụng" if invalid/absent.
 */
export const formatOccVersionLabel = (version: unknown): string => {
  return isValidOccVersion(version) ? `#${version}` : "Không khả dụng";
};

/**
 * Resolves the view mode for ReviewQueuePage based on actor accountType.
 * CenterManager gets modern Master/Detail view; all other actors get legacy view.
 */
export const resolveReviewQueueViewMode = (accountType?: string | null): "CenterManager" | "Teacher" => {
  return accountType === "CenterManager" ? "CenterManager" : "Teacher";
};

/**
 * Resolves the view mode for TeacherStudentTwinPage based on actor accountType.
 * CenterManager gets modern bounded single-subject view; all other actors get legacy view.
 */
export const resolveStudentTwinViewMode = (accountType?: string | null): "CenterManager" | "Teacher" => {
  return accountType === "CenterManager" ? "CenterManager" : "Teacher";
};

/**
 * Executes an OCC refetch operation, ensuring fail-closed semantics:
 * - Throws if refetch fails (network error, server error, or isError = true)
 * - Throws if result data is empty
 * - Returns the fresh data on success
 */
export const executeOccRefetchWrapper = async <T>(
  refetchFn: () => Promise<{ isError?: boolean; data?: T; error?: unknown }>
): Promise<T> => {
  const result = await refetchFn();
  if (result.isError || !result.data) {
    const errorMsg =
      result.error instanceof Error
        ? result.error.message
        : typeof result.error === "string"
        ? result.error
        : "Không thể làm mới dữ liệu từ máy chủ.";
    throw new Error(errorMsg);
  }
  return result.data;
};
