/** Platform administrators sign in at /admin; every center user (manager, teacher, student) at /dang-nhap. */
export const ADMIN_LOGIN_PATH = "/admin";
export const CENTER_LOGIN_PATH = "/dang-nhap";

export const isPlatformCenterCode = (centerCode: string): boolean => centerCode.trim().toUpperCase() === "PLATFORM";

export const loginPathFor = (accountType?: string | null): string =>
  accountType === "PlatformAdmin" ? ADMIN_LOGIN_PATH : CENTER_LOGIN_PATH;

/** Where an anonymous visitor of `pathname` should sign in. */
export const loginPathForRoute = (pathname: string): string =>
  pathname.startsWith("/quan-tri-nen-tang") ? ADMIN_LOGIN_PATH : CENTER_LOGIN_PATH;
