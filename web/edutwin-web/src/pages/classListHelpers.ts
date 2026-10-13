import type { AuthUser } from "../types/auth.ts";
import { permissions } from "../auth/permissions.ts";
import { hasPermission } from "../auth/capabilities.ts";

export interface ClassCapabilities {
  canCreateClass: boolean;
  canUpdateClass: boolean;
  canDeleteClass: boolean;
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
    canDeleteClass: hasPermission(user, permissions.classesDelete),
    canAddMembers:
      hasPermission(user, permissions.classesManageMembers) &&
      hasPermission(user, permissions.studentsRead),
    canRemoveMembers: hasPermission(user, permissions.classesManageMembers),
    canViewDashboard:
      user?.accountType === "CenterManager" && hasPermission(user, permissions.classesRead) &&
      hasPermission(user, permissions.studentsRead),
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

export type CandidateGradeCache = Record<string, number | null | undefined>;

export function updateCandidateGradeCache(
  cache: CandidateGradeCache,
  candidates: Array<{ studentId: string; gradeLevel?: number | null }>
): CandidateGradeCache {
  let changed = false;
  const next = { ...cache };
  for (const c of candidates) {
    if (next[c.studentId] !== c.gradeLevel) {
      next[c.studentId] = c.gradeLevel;
      changed = true;
    }
  }
  return changed ? next : cache;
}

export function hasGradeMismatch(
  selectedStudentIds: string[],
  gradeCache: CandidateGradeCache,
  classGradeLevel: number | null | undefined,
  currentCandidates?: Array<{ studentId: string; gradeLevel?: number | null }>
): boolean {
  if (!classGradeLevel || selectedStudentIds.length === 0) return false;
  return selectedStudentIds.some((id) => {
    let studentGrade = gradeCache[id];
    if (studentGrade === undefined && currentCandidates) {
      const c = currentCandidates.find((cand) => cand.studentId === id);
      if (c) {
        studentGrade = c.gradeLevel;
      }
    }
    return studentGrade !== undefined && studentGrade !== null && studentGrade !== classGradeLevel;
  });
}
