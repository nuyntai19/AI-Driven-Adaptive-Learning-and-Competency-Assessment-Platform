import React, { useState, useRef } from "react";
import { Link, NavLink, useLocation, useNavigate, useSearchParams, Outlet } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../stores/authStore";
import { logout } from "../auth/authApi";
import { organizationApi } from "../api/organizationApi";
import { ThemeToggle } from "../components/ThemeToggle";
import { PlatformSecurityModal } from "../components/PlatformSecurityModal";
import { useModalAccessibility } from "../utils/useModalAccessibility";

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
  const [searchParams, setSearchParams] = useSearchParams();
  const activeSubjectId = searchParams.get("subjectId") || "";

  const user = useAuthStore((state) => state.user);
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
  const currentSubject = subjects.find((s) => s.subjectId === activeSubjectId) || subjects[0];

  // Auto-set subjectId in query string if missing and subjects exist
  React.useEffect(() => {
    if (!activeSubjectId && subjects.length > 0) {
      const newParams = new URLSearchParams(searchParams);
      newParams.set("subjectId", subjects[0].subjectId);
      setSearchParams(newParams, { replace: true });
    }
  }, [activeSubjectId, subjects, searchParams, setSearchParams]);

  const handleSubjectChange = (subjectId: string) => {
    const newParams = new URLSearchParams(searchParams);
    newParams.set("subjectId", subjectId);
    setSearchParams(newParams);
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
    const targetSubjectId = activeSubjectId || (currentSubject ? currentSubject.subjectId : "");
    return targetSubjectId ? `${path}?subjectId=${targetSubjectId}` : path;
  };

  return (
    <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] text-slate-900 dark:text-slate-100 flex flex-col antialiased transition-colors duration-200">
      {/* Top Global Navigation Bar - Sleek Single-Line Layout */}
      <header className="sticky top-0 z-40 bg-white/95 dark:bg-[#0f172a]/95 backdrop-blur-md border-b border-slate-200/90 dark:border-slate-800 shadow-xs">
        <div className="w-full max-w-[1700px] mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-20 gap-4">
            {/* Left: Logo and Brand */}
            <div className="flex items-center gap-6 shrink-0">
              <Link
                to={getTabUrl("/hoc-tap/tong-quan")}
                className="flex items-center gap-3 group focus-visible:outline-indigo-600 rounded-xl shrink-0"
              >
                <div className="w-10 h-10 rounded-2xl bg-gradient-to-tr from-indigo-600 via-indigo-700 to-purple-600 flex items-center justify-center text-white font-black text-xl shadow-md shadow-indigo-500/25 group-hover:scale-105 transition-transform shrink-0">
                  ✦
                </div>
                <div className="flex flex-col">
                  <div className="flex items-center gap-2">
                    <span className="text-lg font-black tracking-tight text-slate-900 dark:text-white">
                      EduTwin
                    </span>
                    <span className="rounded-lg bg-indigo-50 dark:bg-indigo-950/70 px-2 py-0.5 text-xs font-extrabold text-indigo-700 dark:text-indigo-300 uppercase tracking-wider ring-1 ring-inset ring-indigo-600/30">
                      AI Adaptive
                    </span>
                  </div>
                  <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 hidden sm:inline">
                    Cổng học tập học sinh
                  </span>
                </div>
              </Link>

              {/* Desktop Nav Tabs - Non-wrapping Horizontal Menu */}
              <nav className="hidden lg:flex items-center gap-1.5 overflow-x-auto whitespace-nowrap shrink-0">
                <NavLink
                  to={getTabUrl("/hoc-tap/tong-quan")}
                  className={({ isActive }) =>
                    `flex items-center gap-2 px-4 py-2.5 rounded-2xl text-sm font-extrabold whitespace-nowrap shrink-0 transition-all ${
                      isActive
                        ? "bg-indigo-50 dark:bg-indigo-950/70 text-indigo-700 dark:text-indigo-300 shadow-xs ring-1 ring-indigo-300/80 dark:ring-indigo-700"
                        : "text-slate-700 dark:text-slate-200 hover:text-indigo-600 dark:hover:text-indigo-400 hover:bg-slate-100/90 dark:hover:bg-slate-800/90"
                    }`
                  }
                >
                  <svg className="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
                  </svg>
                  <span>Tổng quan</span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/bai-tap")}
                  className={({ isActive }) =>
                    `flex items-center gap-2 px-4 py-2.5 rounded-2xl text-sm font-extrabold whitespace-nowrap shrink-0 transition-all ${
                      isActive || location.pathname.startsWith("/hoc-tap/bai-tap")
                        ? "bg-indigo-50 dark:bg-indigo-950/70 text-indigo-700 dark:text-indigo-300 shadow-xs ring-1 ring-indigo-300/80 dark:ring-indigo-700"
                        : "text-slate-700 dark:text-slate-200 hover:text-indigo-600 dark:hover:text-indigo-400 hover:bg-slate-100/90 dark:hover:bg-slate-800/90"
                    }`
                  }
                >
                  <svg className="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  <span>Bài tập của tôi</span>
                  <span className="ml-0.5 rounded-full bg-indigo-600 px-2 py-0.5 text-xs font-black text-white leading-tight shrink-0">
                    3
                  </span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                  className={({ isActive }) =>
                    `flex items-center gap-2 px-4 py-2.5 rounded-2xl text-sm font-extrabold whitespace-nowrap shrink-0 transition-all ${
                      isActive
                        ? "bg-indigo-50 dark:bg-indigo-950/70 text-indigo-700 dark:text-indigo-300 shadow-xs ring-1 ring-indigo-300/80 dark:ring-indigo-700"
                        : "text-slate-700 dark:text-slate-200 hover:text-indigo-600 dark:hover:text-indigo-400 hover:bg-slate-100/90 dark:hover:bg-slate-800/90"
                    }`
                  }
                >
                  <svg className="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                  </svg>
                  <span>Hồ sơ năng lực (Twin)</span>
                </NavLink>

                <NavLink
                  to={getTabUrl("/hoc-tap/luyen-tap")}
                  className={({ isActive }) =>
                    `flex items-center gap-2 px-4 py-2.5 rounded-2xl text-sm font-extrabold whitespace-nowrap shrink-0 transition-all ${
                      isActive
                        ? "bg-indigo-50 dark:bg-indigo-950/70 text-indigo-700 dark:text-indigo-300 shadow-xs ring-1 ring-indigo-300/80 dark:ring-indigo-700"
                        : "text-slate-700 dark:text-slate-200 hover:text-indigo-600 dark:hover:text-indigo-400 hover:bg-slate-100/90 dark:hover:bg-slate-800/90"
                    }`
                  }
                >
                  <svg className="w-5 h-5 text-purple-600 dark:text-purple-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                  <span>Luyện tập thích ứng</span>
                </NavLink>
              </nav>
            </div>

            {/* Right: Controls, Subject selector, Streak, Theme, Profile */}
            <div className="flex items-center gap-3 shrink-0">
              {/* Subject Selector Dropdown */}
              {subjects.length > 0 && (
                <div className="relative hidden sm:block">
                  <select
                    value={activeSubjectId || (currentSubject ? currentSubject.subjectId : "")}
                    onChange={(e) => handleSubjectChange(e.target.value)}
                    className="appearance-none rounded-2xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 pl-4 pr-9 py-2 text-sm font-extrabold text-slate-900 dark:text-slate-100 transition-colors focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 cursor-pointer shadow-xs"
                    title="Chuyển đổi môn học đang theo dõi"
                  >
                    {subjects.map((sub) => (
                      <option key={sub.subjectId} value={sub.subjectId}>
                        {sub.subjectName}
                      </option>
                    ))}
                  </select>
                  <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-2.5 text-slate-500 dark:text-slate-400">
                    <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M19 9l-7 7-7-7" />
                    </svg>
                  </div>
                </div>
              )}

              {/* Study Streak Badge */}
              <div
                className="hidden md:flex items-center gap-2 px-3.5 py-2 rounded-2xl bg-amber-50 dark:bg-amber-950/50 border border-amber-300 dark:border-amber-700/80 text-amber-900 dark:text-amber-200 text-sm font-extrabold shadow-xs whitespace-nowrap shrink-0"
                title="Chuỗi học tập liên tiếp của bạn"
              >
                <span className="text-base leading-none">🔥</span>
                <span>7 ngày liên tiếp</span>
              </div>

              {/* Dark / Light Theme Toggle - Sleek Compact Icon Button */}
              <ThemeToggle />

              {/* Notifications Bell */}
              <button
                type="button"
                className="relative p-2.5 rounded-2xl text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors focus-visible:outline-indigo-600 cursor-pointer shrink-0 border border-slate-200 dark:border-slate-800"
                title="Thông báo học tập"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                </svg>
                <span className="absolute top-2 right-2 w-2.5 h-2.5 rounded-full bg-rose-500 ring-2 ring-white dark:ring-slate-900" />
              </button>

              {/* Student User Profile Dropdown */}
              <div className="relative shrink-0">
                <button
                  type="button"
                  onClick={() => setIsProfileDropdownOpen((prev) => !prev)}
                  className="flex items-center gap-2.5 p-1.5 pl-2 rounded-2xl hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors focus-visible:outline-indigo-600 border border-slate-300/80 dark:border-slate-700 cursor-pointer shadow-2xs"
                >
                  <div className="w-9 h-9 rounded-xl bg-gradient-to-tr from-indigo-500 to-purple-600 text-white font-extrabold text-xs flex items-center justify-center shadow-xs shrink-0">
                    {getInitials(user?.displayName, user?.username)}
                  </div>
                  <div className="hidden xl:flex flex-col text-left pr-2">
                    <span className="text-sm font-extrabold text-slate-900 dark:text-white leading-tight">
                      {user?.displayName || "Nguyễn Văn An"}
                    </span>
                    <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 leading-tight">
                      Lớp 12A1 · Học sinh
                    </span>
                  </div>
                </button>

                {/* Dropdown Menu */}
                {isProfileDropdownOpen && (
                  <div
                    className="absolute right-0 mt-2 w-56 rounded-2xl bg-white dark:bg-slate-900 p-2 shadow-xl ring-1 ring-slate-200 dark:ring-slate-800 z-50 animate-in fade-in slide-in-from-top-2 duration-150"
                    onBlur={() => setTimeout(() => setIsProfileDropdownOpen(false), 200)}
                  >
                    <div className="px-3 py-2 border-b border-slate-100 dark:border-slate-800 mb-1">
                      <p className="text-xs font-bold text-slate-900 dark:text-white">{user?.displayName || "Nguyễn Văn An"}</p>
                      <p className="text-[11px] text-slate-400 truncate">{user?.username || "an.nguyen@edutwin.edu.vn"}</p>
                    </div>

                    <button
                      type="button"
                      onClick={() => {
                        setIsProfileDropdownOpen(false);
                        setIsSecurityModalOpen(true);
                      }}
                      className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors text-left cursor-pointer"
                    >
                      <svg className="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                      </svg>
                      <span>Bảo mật & Đổi mật khẩu</span>
                    </button>

                    <Link
                      to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                      onClick={() => setIsProfileDropdownOpen(false)}
                      className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors text-left"
                    >
                      <svg className="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      <span>Hồ sơ năng lực của tôi</span>
                    </Link>

                    <div className="border-t border-slate-100 dark:border-slate-800 mt-1 pt-1">
                      <button
                        type="button"
                        onClick={handleLogout}
                        disabled={isLoggingOut}
                        className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-semibold text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors text-left cursor-pointer disabled:opacity-50"
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
                className="lg:hidden p-2 rounded-xl text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 focus-visible:outline-indigo-600"
                aria-label="Mở menu điều hướng"
              >
                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 6h16M4 12h16M4 18h16" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Mobile Drawer Navigation */}
      {isMobileMenuOpen && (
        <div className="fixed inset-0 z-50 lg:hidden flex">
          <div
            className="fixed inset-0 bg-slate-900/40 backdrop-blur-xs transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
          />
          <div
            ref={drawerRef}
            className="relative ml-auto w-full max-w-xs bg-white dark:bg-slate-900 h-full shadow-2xl p-6 flex flex-col justify-between text-slate-900 dark:text-white"
          >
            <div>
              <div className="flex items-center justify-between pb-4 border-b border-slate-100 dark:border-slate-800">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-lg bg-indigo-600 flex items-center justify-center text-white font-bold text-sm">
                    ✦
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
              {subjects.length > 0 && (
                <div className="mt-4">
                  <label className="text-xs font-semibold text-slate-500 dark:text-slate-400 mb-1.5 block">Môn học đang chọn:</label>
                  <select
                    value={activeSubjectId}
                    onChange={(e) => {
                      handleSubjectChange(e.target.value);
                      setIsMobileMenuOpen(false);
                    }}
                    className="w-full rounded-xl border border-slate-300 dark:border-slate-700 p-2 text-xs font-bold text-slate-800 dark:text-white bg-white dark:bg-slate-800"
                  >
                    {subjects.map((sub) => (
                      <option key={sub.subjectId} value={sub.subjectId}>
                        {sub.subjectName}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {/* Mobile Nav Links */}
              <nav className="mt-6 flex flex-col gap-2">
                <Link
                  to={getTabUrl("/hoc-tap/tong-quan")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3.5 py-2.5 rounded-xl text-sm font-semibold text-slate-800 dark:text-slate-200 hover:bg-indigo-50 dark:hover:bg-indigo-950 hover:text-indigo-700 dark:hover:text-indigo-300"
                >
                  <span>Tổng quan</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/bai-tap")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center justify-between px-3.5 py-2.5 rounded-xl text-sm font-semibold text-slate-800 dark:text-slate-200 hover:bg-indigo-50 dark:hover:bg-indigo-950 hover:text-indigo-700 dark:hover:text-indigo-300"
                >
                  <span>Bài tập của tôi</span>
                  <span className="rounded-full bg-indigo-600 px-2 py-0.5 text-xs text-white">3</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/ho-so-nang-luc")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3.5 py-2.5 rounded-xl text-sm font-semibold text-slate-800 dark:text-slate-200 hover:bg-indigo-50 dark:hover:bg-indigo-950 hover:text-indigo-700 dark:hover:text-indigo-300"
                >
                  <span>Hồ sơ năng lực (Twin)</span>
                </Link>
                <Link
                  to={getTabUrl("/hoc-tap/luyen-tap")}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex items-center gap-3 px-3.5 py-2.5 rounded-xl text-sm font-semibold text-purple-700 dark:text-purple-300 bg-purple-50 dark:bg-purple-950/50"
                >
                  <span>Luyện tập thích ứng</span>
                </Link>
              </nav>
            </div>

            <div className="pt-4 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
              <span className="text-xs text-slate-500 dark:text-slate-400">Chế độ giao diện:</span>
              <ThemeToggle showText={true} />
            </div>
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <main className="flex-1 w-full pb-12">
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
