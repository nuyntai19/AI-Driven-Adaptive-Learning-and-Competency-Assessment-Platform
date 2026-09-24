import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import { logout } from "../auth/authApi";
import { permissions } from "../auth/permissions";
import { TeacherThemeScope } from "../components/teacher";
import { ThemeToggle } from "../components/ThemeToggle";
import { useAuthStore } from "../stores/authStore";
import { useModalAccessibility } from "../utils/useModalAccessibility";

interface TeacherNavigationItem {
  number: string;
  label: string;
  subLabel: string;
  meta: string;
  badge: string;
  to: string;
  permissions: readonly string[];
  permissionMode?: "all" | "any";
  match: (pathname: string) => boolean;
  icon: string;
}

const startsWith = (prefix: string) => (pathname: string) => pathname.startsWith(prefix);

const allTeacherNavItems: TeacherNavigationItem[] = [
  {
    number: "01",
    label: "Lớp học phụ trách",
    subLabel: "Quản lý & Theo dõi lớp",
    meta: "Niên khóa 2025 - 2026",
    badge: "Lớp học",
    to: "/giao-vien/lop-hoc",
    permissions: [permissions.dashboardsTeacherRead, permissions.classesRead],
    permissionMode: "any",
    match: (pathname) => pathname === "/giao-vien/lop-hoc" || /^\/giao-vien\/lop-hoc\/[^/]+/.test(pathname),
    icon: "👥",
  },
  {
    number: "02",
    label: "Quản lý học sinh",
    subLabel: "Hồ sơ, Bảng điểm & Báo cáo",
    meta: "Học sinh & Báo cáo",
    badge: "Học sinh",
    to: "/giao-vien/hoc-sinh",
    permissions: [permissions.studentsRead, permissions.dashboardsTeacherRead, permissions.classesRead],
    permissionMode: "any",
    match: (pathname) => pathname === "/giao-vien/hoc-sinh" || /^\/giao-vien\/hoc-sinh\/[^/]+/.test(pathname),
    icon: "👨‍🎓",
  },
  {
    number: "03",
    label: "Hàng đợi chấm bài",
    subLabel: "Chấm điểm & Phản hồi bài tập",
    meta: "Hàng đợi chấm thi",
    badge: "Chấm bài",
    to: "/giao-vien/cham-bai",
    permissions: [permissions.teacherReviewsRead],
    match: startsWith("/giao-vien/cham-bai"),
    icon: "✍️",
  },
  {
    number: "04",
    label: "Ngân hàng câu hỏi",
    subLabel: "Kho đề & Quản lý câu hỏi",
    meta: "Trắc nghiệm & Tự luận",
    badge: "Ngân hàng",
    to: "/giao-vien/cau-hoi",
    permissions: [permissions.questionsRead],
    match: startsWith("/giao-vien/cau-hoi"),
    icon: "📚",
  },
  {
    number: "05",
    label: "Giáo trình môn học",
    subLabel: "Cấu trúc & Kế hoạch đào tạo",
    meta: "Khung chương trình",
    badge: "Giáo trình",
    to: "/giao-vien/giao-trinh",
    permissions: [permissions.curriculumsRead],
    match: startsWith("/giao-vien/giao-trinh"),
    icon: "📖",
  },
  {
    number: "06",
    label: "Đồ thị tri thức",
    subLabel: "Sơ đồ & Mạng lưới năng lực",
    meta: "Cây tri thức chuẩn",
    badge: "Đồ thị",
    to: "/giao-vien/do-thi-tri-thuc",
    permissions: [permissions.subjectsRead, permissions.nodesRead, permissions.edgesRead],
    match: startsWith("/giao-vien/do-thi-tri-thuc"),
    icon: "🕸️",
  },
  {
    number: "07",
    label: "Danh sách bài tập",
    subLabel: "Giao bài & Theo dõi tiến độ",
    meta: "Bài tập & Đề kiểm tra",
    badge: "Bài tập",
    to: "/giao-vien/bai-tap",
    permissions: [permissions.assignmentsRead],
    match: (pathname) => pathname === "/giao-vien/bai-tap" || /^\/giao-vien\/bai-tap\/[^/]+(?!\/tao-moi)/.test(pathname),
    icon: "📋",
  },
  {
    number: "08",
    label: "Tạo bài tập mới",
    subLabel: "Thiết lập & Phát hành đề",
    meta: "Soạn thảo nhanh",
    badge: "Tạo mới",
    to: "/giao-vien/bai-tap/tao-moi",
    permissions: [permissions.assignmentsCreate],
    match: (pathname) => pathname === "/giao-vien/bai-tap/tao-moi",
    icon: "➕",
  },
];

function initials(displayName?: string, username?: string) {
  const parts = displayName?.trim().split(/\s+/).filter(Boolean) ?? [];
  if (parts.length > 1) return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  return (parts[0] ?? username ?? "GV").slice(0, 2).toUpperCase();
}

function RetroHeaderTokens() {
  return (
    <div className="flex items-center gap-2.5">
      {/* Cyan/Teal 4-petal flower icon */}
      <svg className="h-8 w-8 drop-shadow-sm" viewBox="0 0 36 36" fill="none" aria-hidden="true">
        <circle cx="13" cy="13" r="6" fill="#06b6d4" />
        <circle cx="23" cy="13" r="6" fill="#06b6d4" />
        <circle cx="13" cy="23" r="6" fill="#06b6d4" />
        <circle cx="23" cy="23" r="6" fill="#06b6d4" />
        <circle cx="18" cy="18" r="4.5" fill="#cffafe" />
      </svg>

      {/* Ice Cyan donut icon */}
      <svg className="h-8 w-8 drop-shadow-sm" viewBox="0 0 36 36" fill="none" aria-hidden="true">
        <circle cx="18" cy="18" r="11" fill="#e0f2fe" />
        <circle cx="18" cy="18" r="4.5" fill="#0284c7" />
      </svg>

      {/* Ocean Blue daisy icon */}
      <svg className="h-8 w-8 drop-shadow-sm" viewBox="0 0 36 36" fill="none" aria-hidden="true">
        <circle cx="18" cy="10" r="5" fill="#38bdf8" />
        <circle cx="25.5" cy="15.5" r="5" fill="#38bdf8" />
        <circle cx="22.5" cy="24.5" r="5" fill="#38bdf8" />
        <circle cx="13.5" cy="24.5" r="5" fill="#38bdf8" />
        <circle cx="10.5" cy="15.5" r="5" fill="#38bdf8" />
        <circle cx="18" cy="18" r="4.5" fill="#ffffff" />
        <circle cx="18" cy="18" r="2.5" fill="#0284c7" />
      </svg>
    </div>
  );
}

function TeacherNavCards({
  items,
  pathname,
  onNavigate,
}: {
  items: TeacherNavigationItem[];
  pathname: string;
  onNavigate: () => void;
}) {
  return (
    <nav aria-label="Điều hướng không gian giáo viên" className="space-y-2.5">
      {items.map((item, index) => {
        const active = item.match(pathname);
        const itemNumber = String(index + 1).padStart(2, "0");

        if (active) {
          return (
            <Link
              key={item.to}
              to={item.to}
              aria-current="page"
              onClick={onNavigate}
              className="group block rounded-2xl bg-white p-4 text-slate-900 shadow-md transition-all duration-200 hover:shadow-lg"
            >
              <div className="flex items-start justify-between gap-2">
                <span className="text-base font-extrabold tracking-tight text-slate-900 group-hover:text-sky-700 transition-colors">
                  {item.label}
                </span>
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[var(--th-nav-card-inactive)] text-[11px] font-bold text-sky-950 shadow-inner">
                  {itemNumber}
                </span>
              </div>
              <p className="mt-1 text-xs font-medium text-slate-600 line-clamp-1">{item.subLabel}</p>
              <div className="mt-3 flex items-center justify-between border-t border-slate-100 pt-2 text-[10px] text-slate-400">
                <span>{item.meta}</span>
                <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 font-semibold text-slate-700">
                  <span>{item.icon}</span>
                  <span>{item.badge}</span>
                </span>
              </div>
            </Link>
          );
        }

        return (
          <Link
            key={item.to}
            to={item.to}
            onClick={onNavigate}
            className="group flex items-center justify-between rounded-2xl bg-[var(--th-nav-card-inactive)] px-4 py-3 text-[var(--th-nav-text-dark)] shadow-sm transition-all duration-150 hover:bg-[var(--th-nav-card-inactive-hover)] hover:shadow hover:-translate-y-0.5 active:translate-y-0"
          >
            <div className="flex items-center gap-2 min-w-0 pr-2">
              <span className="text-sm font-extrabold tracking-tight truncate text-[var(--th-nav-text-dark)]">
                {item.label}
              </span>
            </div>
            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full border border-black/15 bg-black/5 text-[11px] font-bold text-[var(--th-nav-text-dark)]">
              {itemNumber}
            </span>
          </Link>
        );
      })}
    </nav>
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
  const [centerModalOpen, setCenterModalOpen] = useState(false);
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

  const visibleItems = useMemo(
    () =>
      allTeacherNavItems.filter((item) =>
        item.permissionMode === "any"
          ? hasAnyPermission(item.permissions)
          : hasAllPermissions(item.permissions)
      ),
    [hasAllPermissions, hasAnyPermission]
  );

  const activeItem = visibleItems.find((item) => item.match(location.pathname));
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

  const sidebarContent = (
    <div className="flex h-full flex-col justify-between p-4 sm:p-5 text-white">
      {/* Top Header & Navigation Cards */}
      <div className="space-y-5">
        {/* Retro Header Icons & Title */}
        <div className="pt-1">
          <RetroHeaderTokens />
          <div className="mt-3">
            <h2 className="text-xl font-black tracking-tight text-white drop-shadow-sm">EduTwin</h2>
            <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-sky-100/90">
              Teacher Workspace
            </p>
          </div>
        </div>

        {/* Navigation Cards */}
        <div className="max-h-[calc(100vh-22rem)] overflow-y-auto pr-0.5 space-y-2.5 custom-scrollbar">
          <TeacherNavCards
            items={visibleItems}
            pathname={location.pathname}
            onNavigate={() => setMobileOpen(false)}
          />
        </div>
      </div>

      {/* Bottom CTA Callout & Footer */}
      <div className="mt-6 space-y-4 pt-4 border-t border-white/20">
        {/* Callout text */}
        <div>
          <p className="text-base font-bold leading-snug text-white drop-shadow-sm">
            Khu vực Giảng dạy & Đánh giá năng lực
          </p>
          <button
            type="button"
            onClick={() => setCenterModalOpen(true)}
            className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl bg-[#0f172a] px-4 py-2.5 text-xs font-bold text-white shadow-lg transition-transform duration-150 hover:bg-slate-900 hover:scale-[1.02] active:scale-[0.98]"
          >
            <span>🏫</span>
            <span className="truncate">{centerName}</span>
          </button>
        </div>

        {/* Bottom Card Footer */}
        <div className="flex items-center justify-between rounded-2xl bg-white px-3.5 py-2.5 text-slate-900 shadow-md">
          <span className="text-xs font-black tracking-tight text-slate-900">
            ©2026 EduTwin.
          </span>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <button
              type="button"
              onClick={handleLogout}
              disabled={loggingOut}
              title="Đăng xuất"
              className="grid h-7 w-7 place-items-center rounded-lg bg-rose-50 text-xs font-bold text-rose-600 transition-colors hover:bg-rose-100"
              aria-label="Đăng xuất"
            >
              🚪
            </button>
          </div>
        </div>
      </div>
    </div>
  );

  return (
    <TeacherThemeScope data-actor="teacher">
      <div className="flex min-h-screen bg-[var(--th-bg)] text-[var(--th-text)]">
        {/* Desktop Sidebar with Cool Tone Theme */}
        <aside
          aria-label="Thanh điều hướng Giáo viên"
          className="hidden lg:fixed lg:inset-y-0 lg:left-0 lg:z-30 lg:block lg:w-80 bg-[var(--th-sidebar-bg)] shadow-2xl overflow-y-auto"
        >
          {sidebarContent}
        </aside>

        {/* Mobile Navigation Drawer */}
        {mobileOpen && (
          <div
            className="fixed inset-0 z-50 flex lg:hidden bg-slate-950/70 backdrop-blur-sm"
            onMouseDown={(event) => event.target === event.currentTarget && setMobileOpen(false)}
          >
            <aside
              ref={drawerRef}
              role="dialog"
              aria-modal="true"
              aria-label="Menu điều hướng giáo viên"
              className="w-80 max-w-[85vw] bg-[var(--th-sidebar-bg)] shadow-2xl overflow-y-auto"
            >
              <div className="p-3 flex justify-end">
                <button
                  ref={closeButtonRef}
                  type="button"
                  onClick={() => setMobileOpen(false)}
                  className="rounded-xl bg-black/20 px-3 py-1.5 text-xs font-bold text-white hover:bg-black/40"
                >
                  ✕ Đóng
                </button>
              </div>
              {sidebarContent}
            </aside>
          </div>
        )}

        {/* Main Content Area */}
        <div className="flex flex-1 flex-col lg:pl-80">
          {/* Top Navbar */}
          <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-[var(--th-border-subtle)] bg-[var(--th-surface)]/90 px-4 sm:px-6 lg:px-8 backdrop-blur-md">
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={() => setMobileOpen(true)}
                className="th-icon-button h-10 w-10 border border-[var(--th-border)] lg:hidden"
                aria-label="Mở menu điều hướng"
              >
                ☰
              </button>
              <div className="flex items-center gap-2.5">
                <span className="hidden sm:inline-flex items-center rounded-lg bg-sky-500/10 px-2 py-1 text-xs font-bold text-sky-700 dark:text-sky-300">
                  {activeItem?.icon ?? "🎓"} Không gian Sư phạm
                </span>
                <span className="hidden sm:inline text-xs text-[var(--th-text-muted)]">/</span>
                <h1 className="text-sm sm:text-base font-bold text-[var(--th-text)]">
                  {activeItem?.label ?? "Giáo viên"}
                </h1>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <div className="hidden sm:block">
                <ThemeToggle />
              </div>

              {/* Profile Dropdown */}
              <div className="relative">
                <button
                  ref={profileButtonRef}
                  type="button"
                  onClick={() => setProfileOpen((prev) => !prev)}
                  className="th-focus-ring flex items-center gap-2.5 rounded-xl border border-[var(--th-border)] bg-[var(--th-surface-raised)] p-1.5 pr-3 text-left transition-colors hover:border-[var(--th-text-muted)]"
                  aria-expanded={profileOpen}
                  aria-haspopup="menu"
                >
                  <span className="grid h-8 w-8 place-items-center rounded-lg bg-[var(--at-accent)] text-xs font-bold text-white shadow-sm">
                    {initials(user?.displayName, user?.username)}
                  </span>
                  <div className="hidden text-xs md:block">
                    <p className="font-bold text-[var(--th-text)] truncate max-w-[140px]">
                      {user?.displayName ?? user?.username}
                    </p>
                    <p className="text-[10px] text-[var(--th-text-muted)] font-medium">Giáo viên phụ trách</p>
                  </div>
                </button>

                {profileOpen && (
                  <div
                    ref={profileMenuRef}
                    role="menu"
                    className="absolute right-0 mt-2 w-64 rounded-2xl border border-[var(--th-border)] bg-[var(--th-surface)] p-2 shadow-2xl z-50"
                  >
                    <div className="p-3 border-b border-[var(--th-border-subtle)] text-xs">
                      <p className="font-bold text-[var(--th-text)]">{user?.displayName}</p>
                      <p className="text-[11px] text-[var(--th-text-muted)] mt-0.5">@{user?.username}</p>
                      <p className="text-[10px] text-sky-600 dark:text-sky-400 font-semibold mt-1">
                        {centerName}
                      </p>
                    </div>
                    <div className="p-1 space-y-1">
                      <button
                        type="button"
                        onClick={() => {
                          setProfileOpen(false);
                          setCenterModalOpen(true);
                        }}
                        className="w-full text-left rounded-xl p-2 text-xs font-medium text-[var(--th-text)] hover:bg-[var(--th-surface-muted)] transition-colors"
                      >
                        🏫 Thông tin trung tâm
                      </button>
                      <button
                        type="button"
                        onClick={handleLogout}
                        disabled={loggingOut}
                        className="w-full text-left rounded-xl p-2 text-xs font-bold text-rose-500 hover:bg-rose-500/10 transition-colors"
                      >
                        {loggingOut ? "Đang đăng xuất..." : "🚪 Đăng xuất"}
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </header>

          {/* Main Page Workspace Outlet */}
          <main className="flex-1 p-4 sm:p-6 lg:p-8">
            <Outlet />
          </main>
        </div>
      </div>

      {/* Center Details Modal */}
      {centerModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          onClick={() => setCenterModalOpen(false)}
        >
          <div
            className="w-full max-w-md rounded-2xl border border-[var(--th-border)] bg-[var(--th-surface)] p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center gap-3">
              <span className="grid h-10 w-10 place-items-center rounded-xl bg-[var(--at-accent)] text-xl font-bold text-white shadow-md">
                🏫
              </span>
              <div>
                <h3 className="text-base font-bold text-[var(--th-text)]">{centerName}</h3>
                <p className="text-xs text-[var(--th-text-muted)]">{centerMeta}</p>
              </div>
            </div>

            <div className="mt-5 space-y-3 rounded-xl bg-[var(--th-surface-muted)] p-4 text-xs text-[var(--th-text-secondary)]">
              <div className="flex justify-between">
                <span className="text-[var(--th-text-muted)]">Mã trung tâm:</span>
                <span className="font-semibold text-[var(--th-text)]">{centerQuery.data?.centerCode ?? "N/A"}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--th-text-muted)]">Trạng thái:</span>
                <span className="font-semibold text-emerald-500">
                  {centerQuery.data?.status === "Active" ? "Đang hoạt động" : centerQuery.data?.status ?? "Hoạt động"}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--th-text-muted)]">Vai trò của bạn:</span>
                <span className="font-semibold text-sky-600 dark:text-sky-400">Giáo viên (Teacher)</span>
              </div>
            </div>

            <div className="mt-6 flex justify-end">
              <button
                type="button"
                onClick={() => setCenterModalOpen(false)}
                className="th-primary-button text-xs py-2 px-4 rounded-xl"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </TeacherThemeScope>
  );
}

export function TeacherLayoutBoundary() {
  return <TeacherLayout />;
}
