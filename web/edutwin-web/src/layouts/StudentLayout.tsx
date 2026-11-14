import React, { useEffect, useState, useRef } from "react";
import { Link, NavLink, useLocation, useNavigate, useSearchParams, Outlet } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../stores/authStore";
import { logout } from "../auth/authApi";
import { getStudentWorkspaceSummary, getReconciledStudentAcademicContext } from "../api/dashboardsApi";
import { studentWorkspaceLabels } from "../utils/studentWorkspaceSummary";
import { ThemeToggle } from "../components/ThemeToggle";
import { NotificationBell } from "../components/notifications/NotificationBell";
import { PlatformSecurityModal } from "../components/PlatformSecurityModal";
import { useModalAccessibility } from "../utils/useModalAccessibility";
import { StudentAcademicContext } from "../components/student/StudentAcademicContext";
import { studentScopeChangeUrl } from "../utils/studentAcademicNavigation";
import {
  formatStudentClassBadge,
  formatStudentProfileSubtitle,
  resolveStudentActiveDisplayClass,
} from "../utils/studentAcademicScope";
import { readLastStudentSubject, rememberStudentSubject, resolveStudentSubjectId } from "../utils/studentSubjectScope";
import { useStudentSubjects } from "../utils/useStudentSubjects";
import { useStudentTheme } from "../hooks/useStudentTheme";

function getInitials(displayName?: string, username?: string): string {
  if (displayName) {
    const parts = displayName.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    if (parts.length === 1 && parts[0].length > 0) {
      return parts[0].slice(0, 2).toUpperCase();
    }
  }
  if (username && username.length > 0) {
    return username.slice(0, 2).toUpperCase();
  }
  return "HS";
}

export const StudentLayout: React.FC = () => {
  useStudentTheme();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const activeSubjectId = searchParams.get("subjectId") || "";
  const activeClassId = searchParams.get("classId") || "";
  const isHistory = searchParams.get("history") === "true";
  const isAccountPage = ["/hoc-tap/thong-bao", "/hoc-tap/nhat-ky-hoat-dong"].includes(location.pathname);

  const user = useAuthStore((state) => state.user);
  const workspaceQuery = useQuery({
    queryKey: ["student-workspace-summary", user?.centerId, user?.userId, activeSubjectId || "all", searchParams.get("classId"), searchParams.get("history") === "true"],
    queryFn: () => getStudentWorkspaceSummary(activeSubjectId || undefined, searchParams.get("classId") || undefined, searchParams.get("history") === "true"),
    enabled: !!user,
    staleTime: 30_000,
    refetchInterval: 60_000,
    refetchOnWindowFocus: "always",
  });
  const workspaceLabels = studentWorkspaceLabels(workspaceQuery.data, workspaceQuery.isError);

  const academicContextQuery = useQuery({
    queryKey: ["student-academic-context", user?.centerId, user?.userId, activeSubjectId, activeClassId, isHistory],
    queryFn: () => getReconciledStudentAcademicContext(activeSubjectId, activeClassId, isHistory),
    enabled: !!user,
    staleTime: 30_000,
  });

  const displayClass = resolveStudentActiveDisplayClass(
    academicContextQuery.data?.classes ?? [],
    academicContextQuery.data?.selectedClassId,
    activeClassId
  );
  const classBadge = formatStudentClassBadge(displayClass);
  const profileSubtitle = formatStudentProfileSubtitle(displayClass);

  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isProfileDropdownOpen, setIsProfileDropdownOpen] = useState(false);
  const [isSecurityModalOpen, setIsSecurityModalOpen] = useState(false);
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  // The header is the only subject picker; every student page reads its choice from the URL.
  const { subjects, isLoading: isSubjectsLoading } = useStudentSubjects(isHistory);

  // A detail page belongs to its own record, so opening one never changes the subject.
  const isDetailPage = /^\/hoc-tap\/bai-tap\/[^/]+/.test(location.pathname) || searchParams.has("pathId");
  const followsSubject = !isAccountPage && !isDetailPage;
  const targetSubjectId = followsSubject && !isSubjectsLoading
    ? resolveStudentSubjectId(subjects, activeSubjectId, readLastStudentSubject(user?.userId))
    : null;
  const isSubjectResolving = followsSubject &&
    (isSubjectsLoading || (targetSubjectId !== null && targetSubjectId !== activeSubjectId));

  useEffect(() => {
    if (!targetSubjectId) return;
    rememberStudentSubject(user?.userId, targetSubjectId);
    if (targetSubjectId === activeSubjectId) return;
    const next = new URLSearchParams(searchParams);
    next.delete("classId");
    next.set("subjectId", targetSubjectId);
    navigate(`${location.pathname}?${next}`, { replace: true });
  }, [targetSubjectId, activeSubjectId, searchParams, location.pathname, navigate, user?.userId]);

  const handleSubjectChange = (subjectId: string) => {
    if (subjectId === activeSubjectId) return;
    const newParams = new URLSearchParams(searchParams);
    newParams.delete("classId");
    newParams.set("subjectId", subjectId);
    navigate(studentScopeChangeUrl(location.pathname, newParams));
  };

  const handleLogout = async () => {
    setIsLoggingOut(true);
    try {
      await logout();
    } catch {
      // Ignore network errors on logout
    } finally {
      queryClient.clear();
      navigate("/dang-nhap", { replace: true });
    }
  };

  // Accessibility refs for mobile menu
  const drawerRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  useModalAccessibility({
    isOpen: isMobileMenuOpen,
    onClose: () => setIsMobileMenuOpen(false),
    containerRef: drawerRef,
    initialFocusRef: closeButtonRef,
  });

  // Helper to preserve subjectId when navigating tabs
  const getTabUrl = (path: string) => {
    const p = new URLSearchParams();
    for (const key of ["subjectId", "classId", "history"]) { const value = searchParams.get(key); if (value) p.set(key, value); }
    return p.size ? `${path}?${p}` : path;
  };

  const navItems: { to: string; label: string; short: string; icon: React.ReactNode; active: boolean; badge?: React.ReactNode }[] = [
    { to: "/hoc-tap/tong-quan", label: "Tổng quan", short: "Tổng quan", icon: <NavIcon d="M3 10.5 12 3l9 7.5M5 9.5V21h14V9.5M10 21v-6h4v6" />, active: location.pathname === "/hoc-tap/tong-quan" },
    { to: "/hoc-tap/lo-trinh-hoc-tap", label: "Lộ trình học tập", short: "Lộ trình", icon: <NavIcon d="M8.5 19H17a3.5 3.5 0 0 0 0-7H7a3.5 3.5 0 0 1 0-7h8.5M6 21.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5ZM18 7.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z" />, active: location.pathname.startsWith("/hoc-tap/lo-trinh-hoc-tap") },
    { to: "/hoc-tap/bai-tap", label: "Bài tập của tôi", short: "Bài tập", icon: <NavIcon d="M7 4h10a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2ZM9 4V3h6v1M9 11h6M9 15h4" />, active: location.pathname.startsWith("/hoc-tap/bai-tap"), badge: workspaceLabels.assignmentCount },
    { to: "/hoc-tap/ho-so-nang-luc", label: "Hồ sơ năng lực (Twin)", short: "Hồ sơ năng lực", icon: <NavIcon d="M12 3 20.5 9.2 17.3 19.2H6.7L3.5 9.2ZM12 8l3.5 2.6-1.3 4.2H9.8l-1.3-4.2Z" />, active: location.pathname.startsWith("/hoc-tap/ho-so-nang-luc") },
    { to: "/hoc-tap/luyen-tap", label: "Luyện tập thích ứng", short: "Luyện tập", icon: <NavIcon d="M6 7v10M18 7v10M3 10v4M21 10v4M6 12h12" />, active: location.pathname.startsWith("/hoc-tap/luyen-tap") },
  ];

  return (
    <div className="student-shell min-h-screen bg-[var(--student-canvas)] text-[var(--student-ink)] flex flex-col antialiased transition-colors duration-200">
      {/* Top navigation: brand, pill tabs (desktop) and account controls; phones use the bottom tab bar. */}
      <header className="sticky top-0 z-40 bg-[color-mix(in_srgb,var(--student-canvas)_86%,transparent)] backdrop-blur-xl backdrop-saturate-150 border-b border-[var(--student-border)]">
        <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16 gap-3 sm:gap-4 min-w-0">
            <div className="flex items-center gap-3 xl:gap-5 min-w-0">
              <Link
                to={getTabUrl("/hoc-tap/tong-quan")}
                className="flex items-center gap-2.5 group rounded-xl shrink-0"
                aria-label="EduTwin, về trang Tổng quan"
              >
                <span className="w-9 h-9 rounded-[11px] flex items-center justify-center text-white shadow-[var(--student-glow)] bg-[linear-gradient(135deg,var(--student-brand),var(--student-brand-2))] transition-transform duration-300 group-hover:-rotate-[10deg] shrink-0">
                  <NavIcon d="M8.5 19H17a3.5 3.5 0 0 0 0-7H7a3.5 3.5 0 0 1 0-7h8.5M6 21.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5ZM18 7.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z" />
                </span>
                <span className="text-[19px] font-extrabold tracking-tight text-[var(--student-ink)] [font-family:var(--student-font-display)]">
                  Edu<span className="text-[var(--student-brand)]">Twin</span>
                </span>
              </Link>

              <nav className="hidden lg:flex items-center gap-1 min-w-0" aria-label="Điều hướng học tập">
                {navItems.map((item) => (
                  <NavLink
                    key={item.to}
                    to={getTabUrl(item.to)}
                    title={item.label}
                    className={`flex items-center gap-2 rounded-full px-3.5 py-2 text-[15px] font-semibold whitespace-nowrap transition-colors ${
                      item.active
                        ? "st-nav-active"
                        : "text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)] hover:text-[var(--student-ink)]"
                    }`}
                  >
                    {item.icon}
                    <span className={item.active ? "" : "hidden xl:inline"}>{item.short}</span>
                    {item.badge !== undefined && (
                      <span className={`min-w-5 h-5 px-1.5 rounded-full text-[11px] font-bold leading-5 text-center ${item.active ? "bg-white text-[var(--student-brand)]" : "bg-[var(--student-brand)] text-white"}`}>
                        {item.badge}
                      </span>
                    )}
                  </NavLink>
                ))}
              </nav>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              {!isAccountPage && (
                <div
                  className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-[var(--atlas-sun-soft)] text-[var(--student-ink)] text-[13px] font-bold whitespace-nowrap"
                  title={workspaceLabels.streakTitle}
                >
                  <svg className="w-4 h-4 text-[var(--student-brand-2)]" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 22c4 0 7-2.8 7-6.8 0-4.2-3.2-6.7-4.6-10.7-.3-.8-1.3-.9-1.7-.2-.9 1.6-1 3.4-2.4 4.6C9.6 7.3 9 6 8.6 5.2c-.3-.6-1.2-.6-1.5.1C5.9 7.9 5 10.4 5 15.2 5 19.2 8 22 12 22Z" /></svg>
                  <span className="hidden xl:inline">{workspaceLabels.streak}</span>
                  <span className="xl:hidden">{workspaceLabels.shortStreak}</span>
                </div>
              )}

              <ThemeToggle className="!rounded-full !border-[var(--student-border)] !bg-[var(--student-surface)]" />

              <NotificationBell />

              {/* Student User Profile Dropdown */}
              <div className="relative shrink-0">
                <button
                  type="button"
                  onClick={() => setIsProfileDropdownOpen((prev) => !prev)}
                  aria-expanded={isProfileDropdownOpen}
                  aria-label="Mở menu tài khoản"
                  className="flex items-center gap-2 p-1 rounded-full hover:bg-[var(--student-surface-subtle)] transition-colors border border-[var(--student-border)] bg-[var(--student-surface)] cursor-pointer"
                >
                  <div className="w-8 h-8 rounded-full bg-[var(--student-ink)] text-[var(--student-canvas)] font-bold text-xs flex items-center justify-center shrink-0 [font-family:var(--student-font-display)]">
                    {getInitials(user?.displayName, user?.username)}
                  </div>
                  <div className="hidden 2xl:flex flex-col text-left pr-3 max-w-[190px]">
                    <span className="text-[13px] font-bold text-[var(--student-ink)] leading-tight truncate">
                      {user?.displayName || "Học sinh"}
                    </span>
                    <span className="text-[11px] font-medium text-[var(--student-muted)] leading-tight truncate" title={profileSubtitle}>
                      {profileSubtitle}
                    </span>
                  </div>
                </button>

                {isProfileDropdownOpen && (
                  <div
                    className="absolute right-0 mt-2 w-64 rounded-2xl bg-[var(--student-surface)] p-2 shadow-[var(--student-shadow-lg)] border border-[var(--student-border)] z-50 st-rise"
                    onBlur={() => setTimeout(() => setIsProfileDropdownOpen(false), 200)}
                  >
                    <div className="px-3 py-2 border-b border-[var(--student-border)] mb-1">
                      <p className="text-sm font-bold text-[var(--student-ink)] truncate">{user?.displayName || "Học sinh"}</p>
                      <p className="text-xs text-[var(--student-muted)] truncate">{user?.username || ""}</p>
                      {classBadge ? (
                        <p className="mt-1 text-xs font-semibold text-[var(--student-brand)] truncate" title={profileSubtitle}>
                          {profileSubtitle}
                        </p>
                      ) : null}
                    </div>

                    <button
                      type="button"
                      onClick={() => {
                        setIsProfileDropdownOpen(false);
                        setIsSecurityModalOpen(true);
                      }}
                      className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-sm font-semibold text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)] transition-colors text-left cursor-pointer"
                    >
                      <svg className="w-4 h-4 text-[var(--student-muted)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                      </svg>
                      <span>Bảo mật & Đổi mật khẩu</span>
                    </button>

                    <Link
                      to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                      onClick={() => setIsProfileDropdownOpen(false)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-sm font-semibold text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)] transition-colors text-left"
                    >
                      <svg className="w-4 h-4 text-[var(--student-muted)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      <span>Hồ sơ năng lực của tôi</span>
                    </Link>

                    <div className="border-t border-[var(--student-border)] mt-1 pt-1">
                      {user?.permissions.includes("activity.history.read_own") && <Link to="/hoc-tap/nhat-ky-hoat-dong" onClick={() => setIsProfileDropdownOpen(false)} className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-semibold text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)]">
                        <svg className="w-4 h-4 shrink-0 text-[var(--student-muted)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4l3 2m6-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                        <span>Nhật ký hoạt động</span>
                      </Link>}
                      <button
                        type="button"
                        onClick={handleLogout}
                        disabled={isLoggingOut}
                        className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-sm font-semibold text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors text-left cursor-pointer disabled:opacity-50"
                      >
                        <svg className="w-4 h-4 text-rose-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                        </svg>
                        <span>{isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}</span>
                      </button>
                    </div>
                  </div>
                )}
              </div>

              <button
                type="button"
                onClick={() => setIsMobileMenuOpen(true)}
                className="lg:hidden p-2 rounded-full text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)]"
                aria-label="Mở menu điều hướng"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 6h16M4 12h16M4 18h16" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Context row: the one subject picker for every student page, plus class scope. Scrolls away so the sticky bar stays slim. */}
        {!isAccountPage && <div className="pt-2">
          <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 flex flex-wrap items-center gap-x-4 gap-y-2 min-w-0 py-1">
            <div className="flex items-center gap-2 min-w-0">
              <span className="text-[13px] font-semibold text-[var(--student-muted)] shrink-0">
                <span className="hidden sm:inline">Môn học</span>
                <span className="sm:hidden">Môn</span>
              </span>
              <div className="relative min-w-0">
                <select
                  aria-label="Môn học đang xem"
                  value={activeSubjectId}
                  onChange={(e) => handleSubjectChange(e.target.value)}
                  className="appearance-none rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] hover:border-[var(--student-brand)] pl-3.5 pr-8 py-1.5 text-[13px] font-bold text-[var(--student-ink)] transition-colors focus:border-[var(--student-brand)] focus:outline-none cursor-pointer max-w-[180px] sm:max-w-[240px] truncate"
                  title="Chuyển đổi môn học đang theo dõi"
                  data-testid="student-subject-selector-sub"
                >
                  {!activeSubjectId && <option value="" disabled hidden>{subjects.length > 0 ? "Chọn môn học" : "Chưa có môn học"}</option>}
                  {subjects.map((sub) => (
                    <option key={sub.subjectId} value={sub.subjectId}>
                      {sub.subjectName}
                    </option>
                  ))}
                </select>
                <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-2.5 text-[var(--student-muted)]">
                  <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M19 9l-7 7-7-7" />
                  </svg>
                </div>
              </div>
            </div>
            {!isAccountPage && <StudentAcademicContext inline />}
          </div>
        </div>}

      {/* Mobile Drawer Navigation */}
      {isMobileMenuOpen && (
        <div className="fixed inset-0 z-50 lg:hidden flex">
          <div
            className="fixed inset-0 bg-black/40 backdrop-blur-xs transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
          />
          <div
            ref={drawerRef}
            className="relative ml-auto w-full max-w-xs bg-[var(--student-surface)] h-full shadow-xl p-6 flex flex-col justify-between text-[var(--student-ink)] border-l border-[var(--student-border)]"
          >
            <div>
              <div className="flex items-center justify-between pb-4 border-b border-[var(--student-border)]">
                <span className="font-extrabold text-lg [font-family:var(--student-font-display)]">Edu<span className="text-[var(--student-brand)]">Twin</span></span>
                <button
                  ref={closeButtonRef}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="p-1 rounded-lg text-[var(--student-muted)] hover:bg-[var(--student-surface-subtle)]"
                  aria-label="Đóng menu"
                >
                  <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>

              {!isAccountPage && subjects.length > 0 && (
                <div className="mt-4">
                  <label className="text-xs font-semibold text-[var(--student-muted)] mb-1.5 block">Môn học đang chọn:</label>
                  <select
                    aria-label="Môn học trong menu"
                    value={activeSubjectId}
                    onChange={(e) => {
                      handleSubjectChange(e.target.value);
                      setIsMobileMenuOpen(false);
                    }}
                    className="w-full rounded-xl border border-[var(--student-border)] p-2 text-sm font-bold text-[var(--student-ink)] bg-[var(--student-surface)]"
                  >
                    {!activeSubjectId && <option value="" disabled hidden>Chọn môn học</option>}
                    {subjects.map((sub) => (
                      <option key={sub.subjectId} value={sub.subjectId}>
                        {sub.subjectName}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <nav className="mt-6 flex flex-col gap-1.5" aria-label="Điều hướng trong menu">
                {navItems.map((item) => (
                  <Link
                    key={item.to}
                    to={getTabUrl(item.to)}
                    onClick={() => setIsMobileMenuOpen(false)}
                    className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-semibold ${item.active ? "st-nav-active" : "text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)]"}`}
                  >
                    {item.icon}
                    <span className="flex-1">{item.label}</span>
                    {item.badge !== undefined && <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${item.active ? "bg-white text-[var(--student-brand)]" : "bg-[var(--student-brand)] text-white"}`}>{item.badge}</span>}
                  </Link>
                ))}
                {user?.permissions.includes("activity.history.read_own") && <Link to="/hoc-tap/nhat-ky-hoat-dong" onClick={() => setIsMobileMenuOpen(false)} className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)]">
                  <NavIcon d="M12 8v4l3 2m6-2a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                  <span className="flex-1">Nhật ký hoạt động</span>
                </Link>}
              </nav>
            </div>

            <div className="pt-4 border-t border-[var(--student-border)] flex items-center justify-between">
              <span className="text-xs text-[var(--student-muted)]">Chế độ giao diện:</span>
              <ThemeToggle showText={true} />
            </div>
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <main className="flex-1 w-full min-w-0 pb-12">
        {isSubjectResolving ? (
          <div role="status" className="p-8 text-center text-sm text-[var(--student-muted)]">Đang tải môn học...</div>
        ) : (
          <Outlet />
        )}
      </main>
      {/* Keeps the last content clear of the phone tab bar. */}
      <div className="h-20 lg:hidden" aria-hidden="true" />

      {/* Phone tab bar */}
      <nav
        className="lg:hidden fixed inset-x-0 bottom-0 z-40 grid grid-cols-5 border-t border-[var(--student-border)] bg-[color-mix(in_srgb,var(--student-surface)_92%,transparent)] backdrop-blur-xl px-2 pt-1.5 pb-[calc(6px+env(safe-area-inset-bottom,0px))]"
        aria-label="Điều hướng nhanh"
      >
        {navItems.map((item) => (
          <Link
            key={item.to}
            to={getTabUrl(item.to)}
            aria-current={item.active ? "page" : undefined}
            className={`relative flex flex-col items-center gap-0.5 py-1.5 text-[11px] font-semibold ${item.active ? "text-[var(--student-brand)]" : "text-[var(--student-muted)]"}`}
          >
            {item.active && <span className="absolute -top-1.5 h-[3px] w-7 rounded-b bg-[var(--student-brand)]" aria-hidden="true" />}
            <span className="relative">
              {item.icon}
              {item.badge !== undefined && Number(item.badge) > 0 && <span className="absolute -top-1 -right-1.5 h-2 w-2 rounded-full bg-[var(--student-brand)]" aria-hidden="true" />}
            </span>
            <span className="truncate max-w-full">{item.short === "Hồ sơ năng lực" ? "Năng lực" : item.short}</span>
          </Link>
        ))}
      </nav>

      {/* Security Modal */}
      <PlatformSecurityModal
        isOpen={isSecurityModalOpen}
        onClose={() => setIsSecurityModalOpen(false)}
      />
    </div>
  );
};

function NavIcon({ d }: { d: string }) {
  return (
    <svg className="w-[18px] h-[18px] shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={d} />
    </svg>
  );
}
