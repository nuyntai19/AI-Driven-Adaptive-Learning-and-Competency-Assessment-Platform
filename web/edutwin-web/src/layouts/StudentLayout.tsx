import React, { useState, useRef } from "react";
import { Link, NavLink, useLocation, useNavigate, useSearchParams, Outlet } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../stores/authStore";
import { logout } from "../auth/authApi";
import { organizationApi } from "../api/organizationApi";
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
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const activeSubjectId = searchParams.get("subjectId") || "";
  const isLearningPathPage = location.pathname === "/hoc-tap/lo-trinh-hoc-tap";
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

  // Fetch active subjects for the subject switcher dropdown
  const subjectsQuery = useQuery({
    queryKey: ["subjects", "student-workspace", "active"],
    queryFn: () => organizationApi.listSubjects(true),
    staleTime: 5 * 60 * 1000,
  });

  const subjects = subjectsQuery.data?.data || [];

  const handleSubjectChange = (subjectId: string) => {
    if (subjectId === activeSubjectId) return;
    const newParams = new URLSearchParams(searchParams);
    newParams.delete("classId");
    if (!subjectId) {
      newParams.delete("subjectId");
    } else {
      newParams.set("subjectId", subjectId);
    }
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

  return (
    <div className="student-shell min-h-screen bg-[var(--student-canvas)] text-[var(--student-ink)] flex flex-col antialiased transition-colors duration-200">
      {/* Top Global Navigation Bar - Responsive 2-Tier Layout */}
      <header className="sticky top-0 z-40 bg-white/95 dark:bg-[#172033]/95 backdrop-blur-md border-b border-[var(--student-border,#E8E6DF)] shadow-2xs">
        <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8">
          {/* Row 1: Brand Logo, Main Nav Tabs (>= 1440px), Core Utilities */}
          <div className="flex items-center justify-between h-16 sm:h-18 lg:h-20 gap-3 sm:gap-4 min-w-0">
            {/* Left: Logo and Desktop Nav */}
            <div className="flex items-center gap-3 xl:gap-6 min-w-0">
              <Link
                to={getTabUrl("/hoc-tap/tong-quan")}
                className="flex items-center gap-2.5 sm:gap-3 group focus-visible:outline-indigo-600 rounded-xl shrink-0"
              >
                <div className="w-8 h-8 sm:w-9 sm:h-9 rounded-xl bg-[var(--student-brand,#6546D7)] flex items-center justify-center text-white font-bold text-xs sm:text-sm shadow-xs group-hover:scale-105 transition-transform shrink-0">
                  ET
                </div>
                <div className="flex flex-col">
                  <div className="flex items-center gap-1.5 sm:gap-2">
                    <span className="text-base sm:text-lg font-bold tracking-tight text-slate-900 dark:text-white">
                      EduTwin
                    </span>
                    <span className="hidden sm:inline-flex rounded-md bg-slate-100 dark:bg-slate-800/90 px-1.5 py-0.5 text-[10px] sm:text-xs font-semibold text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-700">
                      Học thích ứng
                    </span>
                  </div>
                  <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400 hidden sm:inline">
                    Không gian học tập cá nhân
                  </span>
                </div>
              </Link>

              {/* Larger labels use a matching desktop/menu breakpoint. */}
              <nav className="hidden min-[1440px]:flex items-center gap-1 xl:gap-2 min-w-0">
                <NavLink
                  to={getTabUrl("/hoc-tap/tong-quan")}
                  className={({ isActive }) =>
                    `relative flex items-center gap-1.5 xl:gap-2 px-3 xl:px-3.5 py-2 rounded-lg text-xs xl:text-sm font-semibold whitespace-nowrap transition-all ${
                      isActive
                        ? "text-[var(--student-brand,#6546D7)] dark:text-[#856BEE] font-bold bg-[var(--student-surface-subtle,#F4F3EE)] dark:bg-[var(--student-surface-subtle,#1C2738)] after:absolute after:bottom-0 after:left-3 after:right-3 after:h-0.5 after:bg-[var(--student-brand,#6546D7)] dark:after:bg-[#856BEE] after:rounded-full"
                        : "text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-[var(--student-surface-subtle,#F4F3EE)] dark:hover:bg-[var(--student-surface-subtle,#1C2738)]"
                    }`
                  }
                >
                  <span>Tổng quan</span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/lo-trinh-hoc-tap")}
                  className={({ isActive }) =>
                    `relative flex items-center gap-1.5 xl:gap-2 px-3 xl:px-3.5 py-2 rounded-lg text-xs xl:text-sm font-semibold whitespace-nowrap transition-all ${
                      isActive || location.pathname.startsWith("/hoc-tap/lo-trinh-hoc-tap")
                        ? "text-[var(--student-brand,#6546D7)] dark:text-[#856BEE] font-bold bg-[var(--student-surface-subtle,#F4F3EE)] dark:bg-[var(--student-surface-subtle,#1C2738)] after:absolute after:bottom-0 after:left-3 after:right-3 after:h-0.5 after:bg-[var(--student-brand,#6546D7)] dark:after:bg-[#856BEE] after:rounded-full"
                        : "text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-[var(--student-surface-subtle,#F4F3EE)] dark:hover:bg-[var(--student-surface-subtle,#1C2738)]"
                    }`
                  }
                >
                  <span className="hidden xl:inline">Lộ trình học tập</span>
                  <span className="inline xl:hidden">Lộ trình</span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/bai-tap")}
                  className={({ isActive }) =>
                    `relative flex items-center gap-1.5 xl:gap-2 px-3 xl:px-3.5 py-2 rounded-lg text-xs xl:text-sm font-semibold whitespace-nowrap transition-all ${
                      isActive || location.pathname.startsWith("/hoc-tap/bai-tap")
                        ? "text-[var(--student-brand,#6546D7)] dark:text-[#856BEE] font-bold bg-[var(--student-surface-subtle,#F4F3EE)] dark:bg-[var(--student-surface-subtle,#1C2738)] after:absolute after:bottom-0 after:left-3 after:right-3 after:h-0.5 after:bg-[var(--student-brand,#6546D7)] dark:after:bg-[#856BEE] after:rounded-full"
                        : "text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-[var(--student-surface-subtle,#F4F3EE)] dark:hover:bg-[var(--student-surface-subtle,#1C2738)]"
                    }`
                  }
                >
                  <span className="hidden xl:inline">Bài tập của tôi</span>
                  <span className="inline xl:hidden">Bài tập</span>
                  <span className="ml-0.5 rounded-full bg-[var(--student-brand,#6546D7)] dark:bg-[#856BEE] px-1.5 py-0.2 text-[10px] xl:text-xs font-bold text-white leading-tight shrink-0">
                    {workspaceLabels.assignmentCount}
                  </span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                  className={({ isActive }) =>
                    `relative flex items-center gap-1.5 xl:gap-2 px-3 xl:px-3.5 py-2 rounded-lg text-xs xl:text-sm font-semibold whitespace-nowrap transition-all ${
                      isActive
                        ? "text-[var(--student-brand,#6546D7)] dark:text-[#856BEE] font-bold bg-[var(--student-surface-subtle,#F4F3EE)] dark:bg-[var(--student-surface-subtle,#1C2738)] after:absolute after:bottom-0 after:left-3 after:right-3 after:h-0.5 after:bg-[var(--student-brand,#6546D7)] dark:after:bg-[#856BEE] after:rounded-full"
                        : "text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-[var(--student-surface-subtle,#F4F3EE)] dark:hover:bg-[var(--student-surface-subtle,#1C2738)]"
                    }`
                  }
                >
                  <span className="hidden xl:inline">Hồ sơ năng lực (Twin)</span>
                  <span className="inline xl:hidden">Hồ sơ năng lực</span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/luyen-tap")}
                  className={({ isActive }) =>
                    `relative flex items-center gap-1.5 xl:gap-2 px-3 xl:px-3.5 py-2 rounded-lg text-xs xl:text-sm font-semibold whitespace-nowrap transition-all ${
                      isActive
                        ? "text-[var(--student-brand,#6546D7)] dark:text-[#856BEE] font-bold bg-[var(--student-surface-subtle,#F4F3EE)] dark:bg-[var(--student-surface-subtle,#1C2738)] after:absolute after:bottom-0 after:left-3 after:right-3 after:h-0.5 after:bg-[var(--student-brand,#6546D7)] dark:after:bg-[#856BEE] after:rounded-full"
                        : "text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-[var(--student-surface-subtle,#F4F3EE)] dark:hover:bg-[var(--student-surface-subtle,#1C2738)]"
                    }`
                  }
                >
                  <span className="hidden min-[1760px]:inline">Luyện tập thích ứng</span>
                  <span className="inline min-[1760px]:hidden">Luyện tập</span>
                </NavLink>
              </nav>
            </div>

            {/* Context controls use a separate row at every width to preserve larger readable navigation. */}
            <div className="flex items-center gap-2 sm:gap-3 shrink-0">
              {/* Dark / Light Theme Toggle - Sleek Compact Icon Button */}
              <ThemeToggle />

              <NotificationBell />

              {/* Student User Profile Dropdown */}
              <div className="relative shrink-0">
                <button
                  type="button"
                  onClick={() => setIsProfileDropdownOpen((prev) => !prev)}
                  className="flex items-center gap-2 p-1 pl-1.5 rounded-xl hover:bg-[var(--student-surface-subtle,#F4F3EE)] dark:hover:bg-[var(--student-surface-subtle,#1C2738)] transition-colors focus-visible:outline-indigo-600 border border-[var(--student-border,#E8E6DF)] cursor-pointer shadow-2xs"
                >
                  <div className="w-7 h-7 sm:w-8 sm:h-8 rounded-lg bg-[var(--student-brand,#6546D7)] text-white font-bold text-xs flex items-center justify-center shrink-0">
                    {getInitials(user?.displayName, user?.username)}
                  </div>
                  <div className="hidden min-[1760px]:flex flex-col text-left pr-2 max-w-[190px]">
                    <span className="text-xs font-bold text-slate-900 dark:text-white leading-tight truncate">
                      {user?.displayName || "Học sinh"}
                    </span>
                    <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400 leading-tight truncate" title={profileSubtitle}>
                      {profileSubtitle}
                    </span>
                  </div>
                </button>

                {/* Dropdown Menu */}
                {isProfileDropdownOpen && (
                  <div
                    className="absolute right-0 mt-2 w-56 rounded-xl bg-white dark:bg-[#172033] p-2 shadow-lg border border-[var(--student-border,#E8E6DF)] z-50 animate-in fade-in slide-in-from-top-2 duration-150"
                    onBlur={() => setTimeout(() => setIsProfileDropdownOpen(false), 200)}
                  >
                    <div className="px-3 py-2 border-b border-slate-100 dark:border-slate-800 mb-1">
                      <p className="text-xs font-bold text-slate-900 dark:text-white truncate">{user?.displayName || "Học sinh"}</p>
                      <p className="text-[11px] text-slate-400 truncate">{user?.username || ""}</p>
                      {classBadge ? (
                        <p className="mt-1 text-[11px] font-semibold text-[var(--student-brand,#6546D7)] dark:text-[#856BEE] truncate" title={profileSubtitle}>
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
                      className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors text-left cursor-pointer"
                    >
                      <svg className="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                      </svg>
                      <span>Bảo mật & Đổi mật khẩu</span>
                    </button>

                    <Link
                      to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                      onClick={() => setIsProfileDropdownOpen(false)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors text-left"
                    >
                      <svg className="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      <span>Hồ sơ năng lực của tôi</span>
                    </Link>

                    <div className="border-t border-slate-100 dark:border-slate-800 mt-1 pt-1">
                      {user?.permissions.includes("activity.history.read_own") && <Link to="/hoc-tap/nhat-ky-hoat-dong" onClick={() => setIsProfileDropdownOpen(false)} className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800">
                        <svg className="w-4 h-4 shrink-0 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4l3 2m6-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                        <span>Nhật ký hoạt động</span>
                      </Link>}
                      <button
                        type="button"
                        onClick={handleLogout}
                        disabled={isLoggingOut}
                        className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-semibold text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors text-left cursor-pointer disabled:opacity-50"
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

              {/* Mobile Menu Hamburger Button */}
              <button
                type="button"
                onClick={() => setIsMobileMenuOpen(true)}
                className="min-[1440px]:hidden p-2 rounded-xl text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 focus-visible:outline-indigo-600"
                aria-label="Mở menu điều hướng"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 6h16M4 12h16M4 18h16" />
                </svg>
              </button>
            </div>
          </div>
        </div>

        {/* Row 2 Context Sub-bar keeps subject and streak readable without squeezing the navigation. */}
        {!isAccountPage && <div className="border-t border-slate-200/80 dark:border-slate-800/80 bg-slate-50/90 dark:bg-[#0b1329]/90 backdrop-blur-xs py-2">
          <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 flex items-center justify-between gap-3 min-w-0">
            {/* Left: Subject Selector with Icon and Label */}
            <div className="flex items-center gap-2 min-w-0">
              <span className="text-xs font-semibold text-slate-600 dark:text-slate-400 flex items-center gap-1.5 shrink-0">
                <span className="hidden sm:inline">Môn học:</span>
                <span className="sm:hidden">Môn:</span>
              </span>
              <div className="relative min-w-0">
                <select
                  aria-label="Môn học đang xem"
                  value={activeSubjectId}
                  onChange={(e) => handleSubjectChange(e.target.value)}
                  className="appearance-none rounded-lg border border-[var(--student-border,#E8E6DF)] bg-white dark:bg-[#172033] hover:bg-slate-50 dark:hover:bg-slate-800 pl-3 pr-8 py-1.5 text-xs font-bold text-slate-900 dark:text-slate-100 transition-colors focus:border-[var(--student-brand,#6546D7)] focus:outline-none cursor-pointer shadow-2xs max-w-[160px] sm:max-w-[200px] truncate"
                  title="Chuyển đổi môn học đang theo dõi"
                  data-testid="student-subject-selector-sub"
                >
                  {isLearningPathPage ? <option value="" disabled>Chọn môn học</option> : <option value="">Toàn bộ</option>}
                  {subjects.map((sub) => (
                    <option key={sub.subjectId} value={sub.subjectId}>
                      {sub.subjectName}
                    </option>
                  ))}
                </select>
                <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-2 text-slate-400">
                  <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M19 9l-7 7-7-7" />
                  </svg>
                </div>
              </div>
            </div>

            {/* Right: Streak status badge on sub-bar */}
            <div
              className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-[var(--student-border,#E8E6DF)] text-slate-700 dark:text-slate-300 text-xs font-semibold shrink-0"
              title={workspaceLabels.streakTitle}
            >
              <span className="w-1.5 h-1.5 rounded-full bg-[#16835B] dark:bg-[#29AC7B]" />
              <span className="hidden sm:inline">{workspaceLabels.streak}</span>
              <span className="sm:hidden">{workspaceLabels.shortStreak}</span>
            </div>
          </div>
        </div>}
        {!isAccountPage && <StudentAcademicContext />}
      </header>

      {/* Mobile Drawer Navigation */}
      {isMobileMenuOpen && (
        <div className="fixed inset-0 z-50 min-[1440px]:hidden flex">
          <div
            className="fixed inset-0 bg-slate-900/40 backdrop-blur-xs transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
          />
          <div
            ref={drawerRef}
            className="relative ml-auto w-full max-w-xs bg-[var(--student-surface)] h-full shadow-xl p-6 flex flex-col justify-between text-slate-900 dark:text-white border-l border-[var(--student-border)]"
          >
            <div>
              <div className="flex items-center justify-between pb-4 border-b border-[var(--student-border)]">
                <div className="flex items-center gap-2">
                  <div className="w-7 h-7 rounded-lg bg-[var(--student-brand)] flex items-center justify-center text-white font-bold text-xs">
                    ET
                  </div>
                  <span className="font-bold text-slate-900 dark:text-white">EduTwin</span>
                </div>
                <button
                  ref={closeButtonRef}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="p-1 rounded-lg text-slate-400 hover:text-slate-700 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800"
                >
                  <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>

              {/* Subject selector in mobile drawer */}
              {!isAccountPage && subjects.length > 0 && (
                <div className="mt-4">
                  <label className="text-xs font-semibold text-slate-500 dark:text-slate-400 mb-1.5 block">Môn học đang chọn:</label>
                  <select
                    aria-label="Môn học trong menu"
                    value={activeSubjectId}
                    onChange={(e) => {
                      handleSubjectChange(e.target.value);
                      setIsMobileMenuOpen(false);
                    }}
                    className="w-full rounded-lg border border-[var(--student-border)] p-2 text-xs font-bold text-slate-800 dark:text-white bg-white dark:bg-[#172033]"
                  >
                    {isLearningPathPage ? <option value="" disabled>Chọn môn học</option> : <option value="">Toàn bộ</option>}
                    {subjects.map((sub) => (
                      <option key={sub.subjectId} value={sub.subjectId}>
                        {sub.subjectName}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {/* Mobile Nav Links */}
              <nav className="mt-6 flex flex-col gap-1.5">
                {user?.permissions.includes("activity.history.read_own") && <Link to="/hoc-tap/nhat-ky-hoat-dong" onClick={() => setIsMobileMenuOpen(false)} className="rounded-xl px-3 py-3 text-sm font-semibold text-slate-600 dark:text-slate-300">◷ Nhật ký hoạt động</Link>}
                <Link
                  to={getTabUrl("/hoc-tap/tong-quan")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-[var(--student-surface-subtle)] hover:text-slate-900 dark:hover:text-white"
                >
                  <span>Tổng quan</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/lo-trinh-hoc-tap")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-[var(--student-surface-subtle)] hover:text-slate-900 dark:hover:text-white"
                >
                  <span>Lộ trình học tập</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/bai-tap")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center justify-between px-3 py-2 rounded-lg text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-[var(--student-surface-subtle)] hover:text-slate-900 dark:hover:text-white"
                >
                  <span>Bài tập của tôi</span>
                  <span className="rounded-full bg-[var(--student-brand)] px-2 py-0.5 text-xs text-white">{workspaceLabels.assignmentCount}</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-[var(--student-surface-subtle)] hover:text-slate-900 dark:hover:text-white"
                >
                  <span>Hồ sơ năng lực (Twin)</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/luyen-tap")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-semibold text-[var(--student-brand)] dark:text-[#856BEE] bg-[var(--student-surface-subtle)]"
                >
                  <span>Luyện tập thích ứng</span>
                </Link>
              </nav>
            </div>

            <div className="pt-4 border-t border-[var(--student-border)] flex items-center justify-between">
              <span className="text-xs text-slate-500 dark:text-slate-400">Chế độ giao diện:</span>
              <ThemeToggle showText={true} />
            </div>
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <main className="flex-1 w-full min-w-0 pb-12">
        <Outlet />
      </main>

      {/* Security Modal */}
      <PlatformSecurityModal
        isOpen={isSecurityModalOpen}
        onClose={() => setIsSecurityModalOpen(false)}
      />
    </div>
  );
};
