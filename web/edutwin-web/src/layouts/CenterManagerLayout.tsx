import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import { logout } from "../auth/authApi";
import { permissions } from "../auth/permissions";
import { CenterManagerThemeScope } from "../components/centerManager";
import { ThemeToggle } from "../components/ThemeToggle";
import { useAuthStore } from "../stores/authStore";
import { useModalAccessibility } from "../utils/useModalAccessibility";

interface NavigationItem {
  label: string;
  to: string;
  permissions: readonly string[];
  permissionMode?: "all" | "any";
  match: (pathname: string) => boolean;
}

interface NavigationGroup {
  label: string;
  items: NavigationItem[];
}

const startsWith = (prefix: string) => (pathname: string) => pathname.startsWith(prefix);

const navigationGroups: NavigationGroup[] = [
  {
    label: "Tổng quan",
    items: [
      { label: "Tổng quan trung tâm", to: "/quan-ly/tong-quan-trung-tam", permissions: [permissions.dashboardsCenterRead], match: startsWith("/quan-ly/tong-quan-trung-tam") },
      { label: "Hồ sơ trung tâm", to: "/quan-ly/trung-tam", permissions: [permissions.centerRead, permissions.centerManage], permissionMode: "any", match: startsWith("/quan-ly/trung-tam") },
    ],
  },
  {
    label: "Tổ chức",
    items: [
      { label: "Giáo viên", to: "/quan-ly/giao-vien", permissions: [permissions.teachersRead], match: startsWith("/quan-ly/giao-vien") },
      { label: "Học sinh", to: "/quan-ly/hoc-sinh", permissions: [permissions.studentsRead], match: startsWith("/quan-ly/hoc-sinh") },
      { label: "Lớp học", to: "/quan-ly/lop-hoc", permissions: [permissions.classesRead], match: (pathname) => pathname === "/quan-ly/lop-hoc" },
      { label: "Môn học", to: "/quan-ly/mon-hoc", permissions: [permissions.subjectsRead], match: startsWith("/quan-ly/mon-hoc") },
    ],
  },
  {
    label: "Nội dung học thuật",
    items: [
      { label: "Đồ thị tri thức", to: "/kien-thuc/do-thi", permissions: [permissions.subjectsRead, permissions.nodesRead, permissions.edgesRead], match: startsWith("/kien-thuc/do-thi") },
      { label: "Giáo trình", to: "/quan-ly/giao-trinh", permissions: [permissions.curriculumsRead], match: startsWith("/quan-ly/giao-trinh") },
      { label: "Ngân hàng câu hỏi", to: "/quan-ly/cau-hoi", permissions: [permissions.questionsRead], match: startsWith("/quan-ly/cau-hoi") },
      { label: "Bài tập", to: "/quan-ly/bai-tap", permissions: [permissions.assignmentsRead], match: startsWith("/quan-ly/bai-tap") },
    ],
  },
  {
    label: "Giám sát học tập",
    items: [
      { label: "Tiến độ và báo cáo", to: "/quan-ly/tong-quan-lop-hoc", permissions: [permissions.dashboardsTeacherRead, permissions.dashboardsCenterRead], permissionMode: "any", match: (pathname) => pathname === "/quan-ly/tong-quan-lop-hoc" || /^\/quan-ly\/lop-hoc\/[^/]+\/tong-quan$/.test(pathname) },
      { label: "Hàng đợi duyệt bài", to: "/quan-ly/duyet-bai", permissions: [permissions.teacherReviewsRead], match: startsWith("/quan-ly/duyet-bai") },
    ],
  },
  {
    label: "Quản trị & bảo mật",
    items: [
      { label: "Vai trò & phân quyền", to: "/quan-ly/phan-quyen", permissions: [permissions.rolesRead, permissions.permissionsRead, permissions.userRolesRead, permissions.auditRead], permissionMode: "any", match: startsWith("/quan-ly/phan-quyen") },
    ],
  },
];

function initials(displayName?: string, username?: string) {
  const parts = displayName?.trim().split(/\s+/).filter(Boolean) ?? [];
  if (parts.length > 1) return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  return (parts[0] ?? username ?? "CM").slice(0, 2).toUpperCase();
}

function Navigation({ groups, pathname, onNavigate }: { groups: NavigationGroup[]; pathname: string; onNavigate: () => void }) {
  return (
    <nav aria-label="Điều hướng quản lý trung tâm" className="space-y-6 px-3 py-5">
      {groups.map((group) => (
        <section key={group.label} aria-labelledby={`cm-nav-${group.label.replace(/\s+/g, "-").toLowerCase()}`}>
          <h2 id={`cm-nav-${group.label.replace(/\s+/g, "-").toLowerCase()}`} className="px-3 text-[10px] font-semibold uppercase tracking-[0.16em] text-[var(--cm-text-muted)]">
            {group.label}
          </h2>
          <ul className="mt-2 space-y-1">
            {group.items.map((item) => {
              const active = item.match(pathname);
              return (
                <li key={item.to}>
                  <Link
                    to={item.to}
                    aria-current={active ? "page" : undefined}
                    onClick={onNavigate}
                    className={`cm-focus-ring flex min-h-10 items-center gap-3 rounded-xl px-3 text-sm font-medium transition-colors ${
                      active
                        ? "bg-indigo-500/15 text-indigo-700 dark:bg-indigo-500/20 dark:text-cyan-300 ring-1 ring-inset ring-indigo-400/30 font-semibold"
                        : "text-[var(--cm-text-secondary)] hover:bg-[var(--cm-surface-subtle)] hover:text-[var(--cm-text)]"
                    }`}
                  >
                    <span aria-hidden="true" className={`h-2 w-2 rounded-full ${active ? "bg-[var(--cm-cyan)]" : "bg-[var(--cm-text-muted)]"}`} />
                    {item.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </nav>
  );
}

function ShellSidebar({ children, centerContext }: { children: ReactNode; centerContext: ReactNode }) {
  return (
    <div className="flex h-full flex-col bg-[var(--cm-surface)] border-r border-[var(--cm-border-subtle)]">
      <div className="flex h-[4.5rem] items-center gap-3 border-b border-[var(--cm-border-subtle)] px-5">
        <span aria-hidden="true" className="grid h-10 w-10 place-items-center rounded-xl bg-gradient-to-br from-cyan-400 to-indigo-500 text-lg font-black text-white">E</span>
        <div>
          <p className="font-semibold text-[var(--cm-text)]">EduTwin</p>
          <p className="text-[10px] uppercase tracking-[0.15em] text-cyan-700 dark:text-cyan-300 font-semibold">Center workspace</p>
        </div>
      </div>
      {centerContext}
      <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
    </div>
  );
}

export function CenterManagerLayout() {
  const user = useAuthStore((state) => state.user);
  const hasAnyPermission = useAuthStore((state) => state.hasAnyPermission);
  const hasAllPermissions = useAuthStore((state) => state.hasAllPermissions);
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const drawerRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const profileButtonRef = useRef<HTMLButtonElement>(null);
  const profileMenuRef = useRef<HTMLDivElement>(null);

  useModalAccessibility({
    isOpen: mobileOpen,
    onClose: () => setMobileOpen(false),
    containerRef: drawerRef,
    initialFocusRef: closeButtonRef,
  });

  const centerQuery = useQuery({
    queryKey: ["center-profile"],
    queryFn: organizationApi.getCurrentCenter,
    enabled: Boolean(user && hasAnyPermission([permissions.centerRead, permissions.centerManage])),
    staleTime: 60_000,
  });

  useEffect(() => {
    if (!profileOpen) return;

    const closeProfileMenu = (event: MouseEvent | KeyboardEvent) => {
      if (event instanceof KeyboardEvent && event.key !== "Escape") return;
      if (event instanceof MouseEvent) {
        const target = event.target as Node;
        if (profileMenuRef.current?.contains(target) || profileButtonRef.current?.contains(target)) return;
      }
      setProfileOpen(false);
      if (event instanceof KeyboardEvent) profileButtonRef.current?.focus();
    };

    document.addEventListener("mousedown", closeProfileMenu);
    document.addEventListener("keydown", closeProfileMenu);
    return () => {
      document.removeEventListener("mousedown", closeProfileMenu);
      document.removeEventListener("keydown", closeProfileMenu);
    };
  }, [profileOpen]);

  const visibleGroups = useMemo(() => navigationGroups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => item.permissionMode === "any"
        ? hasAnyPermission(item.permissions)
        : hasAllPermissions(item.permissions)),
    }))
    .filter((group) => group.items.length > 0), [hasAllPermissions, hasAnyPermission]);

  const activeItem = visibleGroups.flatMap((group) => group.items).find((item) => item.match(location.pathname));
  const centerName = centerQuery.data?.centerName ?? user?.centerName ?? "Trung tâm của bạn";
  const centerMeta = centerQuery.data
    ? `${centerQuery.data.centerCode} · ${centerQuery.data.status === "Active" ? "Đang hoạt động" : centerQuery.data.status}`
    : "Phạm vi trung tâm hiện tại";

  const handleLogout = async () => {
    setLoggingOut(true);
    try {
      await logout();
    } catch {
      useAuthStore.getState().clearSession();
    } finally {
      queryClient.clear();
      navigate("/dang-nhap", { replace: true });
    }
  };

  const centerContext = (
    <div className="mx-3 mt-4 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] p-3">
      <p className="text-[10px] font-semibold uppercase tracking-[0.14em] text-cyan-700 dark:text-cyan-300">Phạm vi hiện tại</p>
      <p className="truncate text-sm font-semibold text-[var(--cm-text)]" title={centerName}>{centerName}</p>
      <p className="mt-1 truncate text-xs text-[var(--cm-text-muted)]" title={centerMeta}>{centerMeta}</p>
    </div>
  );

  return (
    <CenterManagerThemeScope>
      <div className="min-h-screen lg:grid lg:grid-cols-[17rem_minmax(0,1fr)]">
        <aside className="fixed inset-y-0 left-0 z-30 hidden w-[17rem] border-r border-[var(--cm-border-subtle)] lg:block">
          <ShellSidebar centerContext={centerContext}>
            <Navigation groups={visibleGroups} pathname={location.pathname} onNavigate={() => undefined} />
          </ShellSidebar>
        </aside>

        {mobileOpen && (
          <div className="fixed inset-0 z-50 flex lg:hidden" role="presentation">
            <button type="button" aria-label="Đóng menu điều hướng" className="absolute inset-0 bg-slate-950/75" onClick={() => setMobileOpen(false)} />
            <aside ref={drawerRef} role="dialog" aria-modal="true" aria-label="Menu quản lý trung tâm" tabIndex={-1} className="relative h-full w-[min(19rem,88vw)] border-r border-[var(--cm-border)] shadow-2xl">
              <button ref={closeButtonRef} type="button" aria-label="Đóng menu" className="cm-icon-button absolute right-3 top-3 z-10 h-10 w-10 border border-[var(--cm-border)] bg-[var(--cm-surface)]" onClick={() => setMobileOpen(false)}>×</button>
              <ShellSidebar centerContext={centerContext}>
                <Navigation groups={visibleGroups} pathname={location.pathname} onNavigate={() => setMobileOpen(false)} />
              </ShellSidebar>
            </aside>
          </div>
        )}

        <div className="min-w-0 lg:col-start-2">
          <header className="sticky top-0 z-20 flex h-[4.5rem] items-center justify-between gap-4 border-b border-[var(--cm-border-subtle)] bg-[var(--cm-surface)]/95 px-4 backdrop-blur sm:px-6">
            <div className="flex min-w-0 items-center gap-3">
              <button type="button" className="cm-icon-button h-10 w-10 border border-[var(--cm-border)] lg:hidden" aria-label="Mở menu điều hướng" onClick={() => setMobileOpen(true)}>☰</button>
              <div className="min-w-0">
                <p className="text-xs text-[var(--cm-text-muted)]">CenterManager /</p>
                <p className="truncate text-sm font-semibold text-[var(--cm-text)]">{activeItem?.label ?? "Không gian trung tâm"}</p>
              </div>
            </div>

            <div className="flex items-center gap-2 sm:gap-3">
              <ThemeToggle />
              <div className="relative">
                <button
                  ref={profileButtonRef}
                  type="button"
                  aria-expanded={profileOpen}
                  aria-haspopup="menu"
                  className="cm-focus-ring flex items-center gap-3 rounded-xl p-1.5 text-left hover:bg-[var(--cm-surface-muted)]"
                  onClick={() => setProfileOpen((value) => !value)}
                >
                  <span className="grid h-9 w-9 place-items-center rounded-full bg-indigo-400 text-xs font-bold text-slate-950">{initials(user?.displayName, user?.username)}</span>
                  <span className="hidden sm:block">
                    <span className="block max-w-44 truncate text-sm font-semibold text-[var(--cm-text)]">{user?.displayName ?? user?.username}</span>
                    <span className="block text-xs text-[var(--cm-text-secondary)]">Center Manager</span>
                  </span>
                </button>
                {profileOpen && (
                  <div ref={profileMenuRef} role="menu" className="cm-surface absolute right-0 mt-2 w-64 p-2 shadow-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface)]">
                    <div className="border-b border-[var(--cm-border-subtle)] px-3 py-2">
                      <p className="truncate text-sm font-semibold text-[var(--cm-text)]">{user?.displayName}</p>
                      <p className="truncate text-xs text-[var(--cm-text-muted)]">@{user?.username}</p>
                      <p className="mt-1 truncate text-xs text-cyan-500 dark:text-cyan-300">{user?.roles[0]?.roleName ?? "Quản lý trung tâm"}</p>
                    </div>
                    <button role="menuitem" type="button" className="cm-focus-ring mt-1 w-full rounded-lg px-3 py-2 text-left text-sm text-[var(--cm-text-secondary)] hover:bg-[var(--cm-surface-muted)] hover:text-[var(--cm-text)]" onClick={handleLogout} disabled={loggingOut}>
                      {loggingOut ? "Đang đăng xuất…" : "Đăng xuất"}
                    </button>
                  </div>
                )}
              </div>
            </div>
          </header>

          <main className="min-h-[calc(100vh-4.5rem)] bg-[var(--cm-bg)]">
            <Outlet />
          </main>
        </div>
      </div>
    </CenterManagerThemeScope>
  );
}

export function CenterManagerLayoutBoundary() {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? <CenterManagerLayout /> : <Outlet />;
}
