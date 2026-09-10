import type { AccountType, AuthUser } from "../types/auth";

type CapabilityUser = Pick<AuthUser, "accountType" | "permissions"> | null | undefined;

export interface CapabilityRequirement {
  allOf?: readonly string[];
  anyOf?: readonly string[];
  accountTypes?: readonly AccountType[];
}

export const hasPermission = (user: CapabilityUser, permissionCode: string) =>
  user?.permissions.includes(permissionCode) ?? false;

export const hasAnyPermission = (user: CapabilityUser, permissionCodes: readonly string[]) =>
  permissionCodes.some((permissionCode) => hasPermission(user, permissionCode));

export const hasAllPermissions = (user: CapabilityUser, permissionCodes: readonly string[]) =>
  permissionCodes.every((permissionCode) => hasPermission(user, permissionCode));

export const canAccess = (user: CapabilityUser, requirement: CapabilityRequirement) => {
  if (!user) return false;
  if (requirement.accountTypes && !requirement.accountTypes.includes(user.accountType)) return false;
  if (!hasAllPermissions(user, requirement.allOf ?? [])) return false;
  if ((requirement.anyOf?.length ?? 0) > 0 && !hasAnyPermission(user, requirement.anyOf!)) return false;
  return true;
};
