import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { Link, Navigate, Outlet, useLocation, useNavigate } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import { logout } from "../auth/authApi";
import { permissions } from "../auth/permissions";
import { TeacherThemeScope } from "../components/teacher";
import { ThemeToggle } from "../components/ThemeToggle";
import { useAuthStore } from "../stores/authStore";
import { useModalAccessibility } from "../utils/useModalAccessibility";

interface TeacherNavigationItem {
  label: string;
  to: string;
  permissions: readonly string[];
  permissionMode?: "all" | "any";
  match: (pathname: string) => boolean;
  icon?: string;
}

interface TeacherNavigationGroup {
  label: string;
  items: TeacherNavigationItem[];
}

const startsWith = (prefix: string) => (pathname: string) => pathname.startsWith(prefix);

const teacherNavigationGroups: TeacherNavigationGroup[] = [
  {
    label: "Giảng dạy & Lớp học",
    items: [
      {
        label: "Lớp học phụ trách",
        to: "/giao-vien/lop-hoc",
        permissions: [permissions.dashboardsTeacherRead, permissions.classesRead],
        permissionMode: "any",
        match: (pathname) => pathname === "/giao-vien/lop-hoc" || /^\/giao-vien\/lop-hoc\/[^/]+/.test(pathname),
      },
      {
        label: "Quản lý học sinh",
        to: "/giao-vien/hoc-sinh",
        permissions: [permissions.studentsRead, permissions.dashboardsTeacherRead, permissions.classesRead],
        permissionMode: "any",
        match: (pathname) => pathname === "/giao-vien/hoc-sinh" || /^\/giao-vien\/hoc-sinh\/[^/]+/.test(pathname),
      },
    ],
  },
  {
    label: "Học thuật & Đề thi",
    items: [
      {
        label: "Ngân hàng câu hỏi",
        to: "/giao-vien/cau-hoi",
        permissions: [permissions.questionsRead],
        match: startsWith("/giao-vien/cau-hoi"),
      },
      {
        label: "Giáo trình môn học",
        to: "/giao-vien/giao-trinh",
        permissions: [permissions.curriculumsRead],
        match: startsWith("/giao-vien/giao-trinh"),
      },
      {
        label: "Đồ thị tri thức",
        to: "/giao-vien/do-thi-tri-thuc",
        permissions: [permissions.subjectsRead],
        match: startsWith("/giao-vien/do-thi-tri-thuc"),
      },
    ],
  },
  {
    label: "Đánh giá & Chấm bài",
    items: [
      {
        label: "Danh sách bài tập",
        to: "/giao-vien/bai-tap",
        permissions: [permissions.assignmentsRead],
        match: startsWith("/giao-vien/bai-tap"),
      },
      {
        label: "Hàng đợi chấm bài",
        to: "/giao-vien/cham-bai",
        permissions: [permissions.teacherReviewsRead, permissions.twinReasoningReview],
        permissionMode: "any",
        match: startsWith("/giao-vien/cham-bai"),
      },
    ],
  },
];

function initials(displayName?: string, username?: string) {
  const parts = displayName?.trim().split(/\s+/).filter(Boolean) ?? [];
  if (parts.length > 1) return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  return (parts[0] ?? username ?? "GV").slice(0, 2).toUpperCase();
}

function TeacherNavigation({
  groups,
  pathname,
  onNavigate,
}: {
  groups: TeacherNavigationGroup[];
  pathname: string;
  onNavigate: () => void;
}) {
  return (
    <nav aria-label="Điều hướng không gian giáo viên" className="space-y-6 px-3 py-5">
      {groups.map((group) => (
        <section key={group.label} aria-labelledby={`th-nav-${group.label.replace(/\s+/g, "-").toLowerCase()}`}>
          <h2
            id={`th-nav-${group.label.replace(/\s+/g, "-").toLowerCase()}`}
            className="px-3 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--th-text-muted)]"
          >
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
                    className={`th-focus-ring flex min-h-10 items-center gap-3 rounded-xl px-3 text-sm font-medium transition-colors ${
                      active
                        ? "bg-emerald-500/15 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300 ring-1 ring-inset ring-emerald-400/30 font-semibold"
                        : "text-[var(--th-text-secondary)] hover:bg-[var(--th-surface-subtle)] hover:text-[var(--th-text)]"
                    }`}
                  >
                    <span
                      aria-hidden="true"
                      className={`h-2 w-2 rounded-full ${
                        active ? "bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.6)]" : "bg-[var(--th-text-muted)]"
                      }`}
                    />
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

function ShellSidebar({
  children,
  centerContext,
  footer,
}: {
  children: ReactNode;
  centerContext: ReactNode;
  footer?: ReactNode;
}) {
  return (
    <div className="flex h-full flex-col bg-[var(--th-surface)] border-r border-[var(--th-border-subtle)]">
      <div className="flex h-[4.5rem] items-center gap-3 border-b border-[var(--th-border-subtle)] px-5">
        <span
          aria-hidden="true"
          className="grid h-10 w-10 place-items-center rounded-xl bg-gradient-to-br from-cyan-400 to-emerald-500 text-lg font-black text-white shadow-md shadow-emerald-500/15"
        >
          E
        </span>
        <div>
          <p className="font-semibold text-[var(--th-text)]">EduTwin</p>
          <p className="text-xs uppercase tracking-[0.15em] text-emerald-700 dark:text-emerald-300 font-semibold">
            Teacher workspace
          </p>
        </div>
      </div>
      {centerContext}
      <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
      {footer}
    </div>
  );
}

export function TeacherLayout() {
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
    queryKey: ["center-profile-for-teacher"],
    queryFn: organizationApi.getCurrentCenter,
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

  const visibleGroups = useMemo(
    () =>
      teacherNavigationGroups
        .map((group) => ({
          ...group,
          items: group.items.filter((item) =>
            item.permissionMode === "any"
              ? hasAnyPermission(item.permissions)
              : hasAllPermissions(item.permissions)
          ),
        }))
        .filter((group) => group.items.length > 0),
    [hasAllPermissions, hasAnyPermission]
  );

  const activeItem = visibleGroups.flatMap((group) => group.items).find((item) => item.match(location.pathname));
  const centerName = centerQuery.data?.centerName ?? user?.centerName ?? "Trung tâm giáo dục";
  const centerMeta = centerQuery.data
    ? `${centerQuery.data.centerCode} · ${centerQuery.data.status === "Active" ? "Đang hoạt động" : centerQuery.data.status}`
    : "Không gian Sư phạm";

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
    <div className="mx-3 mt-4 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] p-3">
      <p className="text-xs font-semibold uppercase tracking-[0.14em] text-emerald-700 dark:text-emerald-300">
        Trung tâm đào tạo
      </p>
      <p className="truncate text-sm font-semibold text-[var(--th-text)]" title={centerName}>
        {centerName}
      </p>
      <p className="mt-1 truncate text-xs text-[var(--th-text-muted)]" title={centerMeta}>
        {centerMeta}
      </p>
    </div>
  );

  const sidebarFooter = (
    <div className="border-t border-[var(--th-border-subtle)] p-3 bg-[var(--th-surface)]">
      <button
        type="button"
        onClick={handleLogout}
        disabled={loggingOut}
        title="Đăng xuất khỏi hệ thống"
        className="th-focus-ring group flex w-full items-center justify-center gap-2 rounded-xl border border-rose-500/20 bg-rose-500/5 px-4 py-2.5 text-sm font-semibold text-rose-600 transition-all hover:bg-rose-500/15 hover:border-rose-500/40 hover:text-rose-700 dark:text-rose-400 dark:hover:text-rose-300"
      >
        <svg className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
        </svg>
        <span>{loggingOut ? "Đang đăng xuất…" : "Đăng xuất"}</span>
      </button>
    </div>
  );

  return (
    <TeacherThemeScope data-actor="teacher">
      <div className="min-h-screen lg:grid lg:grid-cols-[17rem_minmax(0,1fr)] bg-[var(--th-bg)] text-[var(--th-text)]">
        {/* Desktop Sidebar */}
        <aside className="fixed inset-y-0 left-0 z-30 hidden w-[17rem] border-r border-[var(--th-border-subtle)] lg:block">
          <ShellSidebar centerContext={centerContext} footer={sidebarFooter}>
            <TeacherNavigation groups={visibleGroups} pathname={location.pathname} onNavigate={() => undefined} />
          </ShellSidebar>
        </aside>

        {/* Mobile Navigation Drawer */}
        {mobileOpen && (
          <div className="fixed inset-0 z-50 flex lg:hidden" role="presentation">
            <button
              type="button"
              aria-label="Đóng menu điều hướng"
              className="absolute inset-0 bg-slate-950/75 backdrop-blur-sm"
              onClick={() => setMobileOpen(false)}
            />
            <aside
              ref={drawerRef}
              role="dialog"
              aria-modal="true"
              aria-label="Menu điều hướng giáo viên"
              tabIndex={-1}
              className="relative h-full w-[min(19rem,88vw)] border-r border-[var(--th-border)] shadow-2xl bg-[var(--th-surface)]"
            >
              <button
                ref={closeButtonRef}
                type="button"
                aria-label="Đóng menu"
                className="th-icon-button absolute right-3 top-3 z-10 h-10 w-10 border border-[var(--th-border)] bg-[var(--th-surface)]"
                onClick={() => setMobileOpen(false)}
              >
                ×
              </button>
              <ShellSidebar centerContext={centerContext} footer={sidebarFooter}>
                <TeacherNavigation groups={visibleGroups} pathname={location.pathname} onNavigate={() => setMobileOpen(false)} />
              </ShellSidebar>
            </aside>
          </div>
        )}

        {/* Main Content Area */}
        <div className="min-w-0 lg:col-start-2">
          {/* Top Navbar */}
          <header className="sticky top-0 z-20 flex h-[4.5rem] items-center justify-between gap-4 border-b border-[var(--th-border-subtle)] bg-[var(--th-surface)]/95 px-4 backdrop-blur sm:px-6 lg:px-8">
            <div className="flex min-w-0 items-center gap-3">
              <button
                type="button"
                className="th-icon-button h-10 w-10 border border-[var(--th-border)] lg:hidden"
                aria-label="Mở menu điều hướng"
                onClick={() => setMobileOpen(true)}
              >
                ☰
              </button>
              <div className="min-w-0">
                <p className="text-xs text-[var(--th-text-muted)]">Giáo viên /</p>
                <p className="truncate text-sm font-semibold text-[var(--th-text)]">
                  {activeItem?.label ?? "Không gian Sư phạm"}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2 sm:gap-3">
              <ThemeToggle />
              {/* Profile Dropdown */}
              <div className="relative">
                <button
                  ref={profileButtonRef}
                  type="button"
                  aria-expanded={profileOpen}
                  aria-haspopup="menu"
                  className="th-focus-ring flex items-center gap-3 rounded-xl p-1.5 text-left hover:bg-[var(--th-surface-muted)] transition-colors"
                  onClick={() => setProfileOpen((value) => !value)}
                >
                  <span className="grid h-9 w-9 place-items-center rounded-full bg-gradient-to-br from-cyan-400 to-emerald-500 text-xs font-bold text-slate-950 shadow-sm">
                    {initials(user?.displayName, user?.username)}
                  </span>
                  <span className="hidden sm:block">
                    <span className="block max-w-44 truncate text-sm font-semibold text-[var(--th-text)]">
                      {user?.displayName ?? user?.username}
                    </span>
                    <span className="block text-xs text-emerald-600 dark:text-emerald-400 font-medium">Giáo viên</span>
                  </span>
                </button>

                {profileOpen && (
                  <div
                    ref={profileMenuRef}
                    role="menu"
                    className="th-surface absolute right-0 mt-2 w-64 p-2 shadow-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] rounded-xl"
                  >
                    <div className="border-b border-[var(--th-border-subtle)] px-3 py-2">
                      <p className="truncate text-sm font-semibold text-[var(--th-text)]">{user?.displayName}</p>
                      <p className="truncate text-xs text-[var(--th-text-muted)]">@{user?.username}</p>
                      <p className="mt-1 truncate text-xs text-emerald-500 dark:text-emerald-300 font-medium">
                        {user?.roles[0]?.roleName ?? "Giáo viên giảng dạy"}
                      </p>
                    </div>
                    <button
                      role="menuitem"
                      type="button"
                      className="th-focus-ring mt-1 w-full rounded-lg px-3 py-2 text-left text-sm text-[var(--th-text-secondary)] hover:bg-[var(--th-surface-muted)] hover:text-[var(--th-text)]"
                      onClick={handleLogout}
                      disabled={loggingOut}
                    >
                      {loggingOut ? "Đang đăng xuất…" : "Đăng xuất"}
                    </button>
                  </div>
                )}
              </div>
            </div>
          </header>

          <main className="min-h-[calc(100vh-4.5rem)] bg-[var(--th-bg)]">
            <Outlet />
          </main>
        </div>
      </div>
    </TeacherThemeScope>
  );
}

export function TeacherLayoutBoundary() {
  const user = useAuthStore((state) => state.user);
  const location = useLocation();

  if (!user) {
    return <Navigate to="/dang-nhap" replace state={{ attemptedPath: location.pathname }} />;
  }

  if (user.accountType !== "Teacher") {
    return <Navigate to="/khong-co-quyen" replace state={{ attemptedPath: location.pathname }} />;
  }

  return <TeacherLayout />;
}
