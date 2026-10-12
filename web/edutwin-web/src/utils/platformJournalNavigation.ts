import type { AuthUser } from "../types/auth";
import { permissions } from "../auth/permissions.ts";

export const platformJournalRoutes = {
  overview: "/quan-tri-nen-tang/nhat-ky/tong-quan",
  audit: "/quan-tri-nen-tang/nhat-ky/kiem-toan",
} as const;

export function platformJournalTabs(user: Pick<AuthUser, "accountType" | "permissions"> | null) {
  if (user?.accountType !== "PlatformAdmin") return [];
  return [
    { path: platformJournalRoutes.overview, label: "Tổng quan", permission: permissions.platformActivityRead },
    { path: platformJournalRoutes.audit, label: "Kiểm toán chi tiết", permission: permissions.platformAuditRead },
  ].filter(tab => user.permissions.includes(tab.permission));
}
