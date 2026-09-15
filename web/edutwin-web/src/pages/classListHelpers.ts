import type { AuthUser } from "../types/auth.ts";
import { permissions } from "../auth/permissions.ts";
import { hasPermission } from "../auth/capabilities.ts";

export interface ClassCapabilities {
  canCreateClass: boolean;
  canUpdateClass: boolean;
  canAddMembers: boolean;
  canRemoveMembers: boolean;
  canViewDashboard: boolean;
}

export function evaluateClassCapabilities(
  user: Pick<AuthUser, "accountType" | "permissions"> | null | undefined
): ClassCapabilities {
  return {
    canCreateClass:
      hasPermission(user, permissions.classesCreate) &&
      hasPermission(user, permissions.subjectsRead) &&
      hasPermission(user, permissions.teachersRead),
    canUpdateClass:
      hasPermission(user, permissions.classesUpdate) &&
      hasPermission(user, permissions.teachersRead),
    canAddMembers:
      hasPermission(user, permissions.classesManageMembers) &&
      hasPermission(user, permissions.studentsRead),
    canRemoveMembers: hasPermission(user, permissions.classesManageMembers),
    canViewDashboard:
      hasPermission(user, permissions.dashboardsCenterRead) ||
      hasPermission(user, permissions.dashboardsTeacherRead),
  };
}

export function toggleStudentSelection(currentSelection: string[], studentId: string): string[] {
  return currentSelection.includes(studentId)
    ? currentSelection.filter((id) => id !== studentId)
    : [...currentSelection, studentId];
}

export function mergePageSelection(currentSelection: string[], pageCandidateIds: string[]): string[] {
  return Array.from(new Set([...currentSelection, ...pageCandidateIds]));
}

export function unmergePageSelection(currentSelection: string[], pageCandidateIds: string[]): string[] {
  return currentSelection.filter((id) => !pageCandidateIds.includes(id));
}

export function getCandidateListState(options: {
  isError: boolean;
  isLoading: boolean;
  isFetching: boolean;
  candidateCount: number;
}): "error" | "loading" | "empty" | "ready" {
  if (options.isError) return "error";
  if (options.isLoading || options.isFetching) return "loading";
  if (options.candidateCount === 0) return "empty";
  return "ready";
}
