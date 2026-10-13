import type { AuthUser } from "../types/auth";

export interface RouteAccessResolution {
  allowed: boolean;
  redirect?: string;
}

/**
 * Resolves access to Teacher Layout Boundary (/giao-vien/*).
 * Fail-closed policy:
 * - Unauthenticated -> /dang-nhap
 * - Non-Teacher (CenterManager, Student, PlatformAdmin, etc.) -> /khong-co-quyen
 * - Teacher -> allowed
 */
export function resolveTeacherLayoutAccess(
  user: Pick<AuthUser, "accountType"> | null | undefined
): RouteAccessResolution {
  if (!user) {
    return { allowed: false, redirect: "/dang-nhap" };
  }
  if (user.accountType !== "Teacher") {
    return { allowed: false, redirect: "/khong-co-quyen" };
  }
  return { allowed: true };
}

/**
 * Resolves redirect target for legacy/locked academic endpoints (/quan-ly/giao-trinh, /quan-ly/cau-hoi, /quan-ly/duyet-bai, etc.).
 * - Teacher -> redirects to modern dedicated teacher route (e.g. /giao-vien/giao-trinh, /giao-vien/cham-bai)
 * - Non-Teacher (CenterManager, Student, etc.) -> fail-closed redirect to /khong-co-quyen
 */
export function resolveAcademicRedirect(
  user: Pick<AuthUser, "accountType"> | null | undefined,
  teacherTarget: string
): string {
  if (user?.accountType === "Teacher") {
    return teacherTarget;
  }
  return "/khong-co-quyen";
}

/**
 * Resolves access to CenterManager Layout Boundary (/quan-ly/* administrative governance).
 * - Non-CenterManager (Teacher, Student, etc.) -> fail-closed redirect to /khong-co-quyen
 * - CenterManager -> allowed
 */
export function resolveCenterManagerLayoutAccess(
  user: Pick<AuthUser, "accountType"> | null | undefined
): RouteAccessResolution {
  if (!user) {
    return { allowed: false, redirect: "/dang-nhap" };
  }
  if (user.accountType !== "CenterManager") {
    return { allowed: false, redirect: "/khong-co-quyen" };
  }
  return { allowed: true };
}
