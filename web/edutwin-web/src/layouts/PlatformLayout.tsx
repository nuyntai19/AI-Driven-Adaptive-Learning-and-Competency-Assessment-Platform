import React, { useState, useRef } from "react";
import { Link, useLocation, useNavigate, Outlet } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { logout } from "../auth/authApi";
import { ThemeToggle } from "../components/ThemeToggle";
import { NotificationBell } from "../components/notifications/NotificationBell";
import { NotificationIcon } from "../components/notifications/NotificationIcon";
import { PlatformSecurityModal } from "../components/PlatformSecurityModal";
import { useModalAccessibility } from "../utils/useModalAccessibility";
import { platformJournalTabs } from "../utils/platformJournalNavigation";
import "./platformDesignSystem.css";

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
  return "PA";
}

export const PlatformLayout: React.FC = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const hasAnyPermission = useAuthStore((state) => state.hasAnyPermission);

  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isSecurityModalOpen, setIsSecurityModalOpen] = useState(false);
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  // Accessibility refs for mobile drawer
  const drawerRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  useModalAccessibility({
    isOpen: isMobileMenuOpen,
    onClose: () => setIsMobileMenuOpen(false),
    containerRef: drawerRef,
    initialFocusRef: closeButtonRef,
  });

  const handleLogout = async () => {
    setIsLoggingOut(true);
    try {
      await logout();
    } catch {
      // Ignore network errors on logout to allow local session clearance
    } finally {
      queryClient.clear();
      navigate("/dang-nhap", { replace: true });
    }
  };

  // Capability-first menu guards
  const canAccessCenters = hasAnyPermission([
    permissions.platformCentersRead,
    permissions.platformCentersManage,
  ]);
  const journalTabs = platformJournalTabs(user);
  const canManageSecurity = hasPermission(permissions.platformAccountManageOwn);

  const initials = getInitials(user?.displayName, user?.username);

  const isCentersActive = location.pathname.startsWith("/quan-tri-nen-tang/trung-tam");
  const isJournalActive = location.pathname.startsWith("/quan-tri-nen-tang/nhat-ky") || location.pathname.startsWith("/quan-tri-nen-tang/hoat-dong");
  const isNotificationsActive = location.pathname.startsWith("/quan-tri-nen-tang/thong-bao");
  const navClass = (active: boolean) => `flex min-h-12 items-center gap-3 rounded-xl px-4 py-3 text-sm font-medium transition-colors ${active
    ? "bg-blue-600 text-white shadow-sm shadow-blue-500/20"
    : "text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white"}`;

  const breadcrumbPageTitle = location.pathname.startsWith("/quan-tri-nen-tang/thong-bao")
    ? "Thông báo"
    : isJournalActive
    ? "Nhật ký"
    : isCentersActive
    ? "Trung tâm đối tác"
    : location.pathname.startsWith("/quan-tri-nen-tang/van-hanh") ? "Vận hành hệ thống"
    : location.pathname.startsWith("/quan-tri-nen-tang/su-dung-ai") ? "Sử dụng AI"
    : location.pathname.startsWith("/quan-tri-nen-tang/luu-tru") ? "Lưu trữ & sao lưu"
    : "Quản trị nền tảng";

  const navigationItems = (
    <nav className="space-y-2 px-4" aria-label="Điều hướng nền tảng">
      {[
        {permission: permissions.platformOperationsRead, path: "/quan-tri-nen-tang/van-hanh", label: "Vận hành hệ thống", icon: "M3 12h4l3-8 4 16 3-8h4"},
        {permission: permissions.platformAiUsageRead, path: "/quan-tri-nen-tang/su-dung-ai", label: "Sử dụng AI", icon: "M4 20h16M7 16V9m5 7V4m5 12v-5"},
        {permission: permissions.platformStorageRead, path: "/quan-tri-nen-tang/luu-tru", label: "Lưu trữ & sao lưu", icon: "M4 6h16v12H4zM8 10h8M8 14h3"},
      ].filter(item => hasPermission(item.permission)).map(item => <Link key={item.path} to={item.path} onClick={() => setIsMobileMenuOpen(false)} aria-current={location.pathname === item.path ? "page" : undefined} className={navClass(location.pathname === item.path)}>
        <svg className="h-6 w-6 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" d={item.icon} /></svg><span>{item.label}</span>
      </Link>)}
      {journalTabs.length > 0 && <Link to={journalTabs[0].path} onClick={() => setIsMobileMenuOpen(false)} aria-current={isJournalActive ? "page" : undefined} className={navClass(isJournalActive)}>
        <svg className="h-6 w-6 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.75" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 2m6-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        <span>Nhật ký</span>
      </Link>}
      {hasPermission(permissions.platformNotificationsReadOwn) && <Link to="/quan-tri-nen-tang/thong-bao" onClick={() => setIsMobileMenuOpen(false)} aria-current={isNotificationsActive ? "page" : undefined} className={navClass(isNotificationsActive)}>
        <NotificationIcon className="h-6 w-6 shrink-0" /><span>Thông báo</span>
      </Link>}
      {canAccessCenters && (
        <Link
          to="/quan-tri-nen-tang/trung-tam"
          onClick={() => setIsMobileMenuOpen(false)}
          aria-current={isCentersActive ? "page" : undefined}
          className={navClass(isCentersActive)}
        >
          <svg className="w-6 h-6 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.75}
              d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
            />
          </svg>
          <span>Trung tâm đối tác</span>
        </Link>
      )}

      {canManageSecurity && (
        <button
          type="button"
          onClick={() => {
            setIsMobileMenuOpen(false);
            setIsSecurityModalOpen(true);
          }}
          className={`${navClass(false)} w-full text-left`}
        >
          <svg className="w-6 h-6 shrink-0 text-emerald-600 dark:text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.75}
              d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
            />
          </svg>
          <span>Bảo mật tài khoản</span>
        </button>
      )}
    </nav>
  );

  const userProfileCard = (
    <div className="p-4 border-t border-slate-200 bg-slate-50 dark:border-slate-800 dark:bg-slate-900/60">
      <div className="flex items-center gap-3">
        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white font-bold text-sm flex items-center justify-center shrink-0 shadow-sm shadow-blue-500/20">
          {initials}
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-slate-900 dark:text-white truncate" title={user?.displayName}>
            {user?.displayName || "Platform Admin"}
          </p>
          <p className="text-xs text-slate-500 dark:text-slate-400 font-mono truncate" title={`@${user?.username}`}>
            @{user?.username || "admin"}
          </p>
          <div className="mt-1 flex items-center gap-1.5 flex-wrap">
            <span
              className="text-xs text-slate-500 dark:text-slate-400 truncate max-w-[185px]"
              title={user?.centerName}
            >
              {user?.centerName || "EduTwin"}
            </span>
            <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-blue-50 text-blue-700 border border-blue-200 dark:bg-blue-500/20 dark:text-blue-300 dark:border-blue-500/30">
              Root tenant
            </span>
          </div>
        </div>
      </div>

      <button
        type="button"
        onClick={handleLogout}
        disabled={isLoggingOut}
        className="mt-3.5 min-h-11 w-full flex items-center justify-center gap-2 px-3 py-2 text-xs font-medium text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 dark:text-slate-400 dark:hover:text-white dark:bg-slate-800 dark:hover:bg-slate-700/80 rounded-lg transition-colors disabled:opacity-50"
      >
        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"
          />
        </svg>
        <span>{isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}</span>
      </button>
    </div>
  );

  return (
    <div className="platform-typography min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 flex transition-colors duration-150">
      {/* Desktop Sidebar */}
      <aside className="hidden lg:flex lg:w-80 lg:flex-col lg:fixed lg:inset-y-0 bg-white border-r border-slate-200 text-slate-900 dark:bg-slate-900 dark:border-slate-800 dark:text-slate-100 z-30">
        <div className="h-20 shrink-0 px-6 flex items-center gap-3 border-b border-slate-200 dark:border-slate-800">
          <div className="w-10 h-10 shrink-0 rounded-lg bg-blue-600 flex items-center justify-center text-white font-black text-base shadow-sm shadow-blue-500/30">
            E
          </div>
          <div className="flex flex-col">
            <span className="text-sm font-bold tracking-tight text-slate-900 dark:text-white">EduTwin Platform</span>
            <span className="text-[10px] text-blue-600 dark:text-blue-400 font-semibold tracking-wider uppercase">
              Control Plane
            </span>
          </div>
        </div>

        <div className="flex-1 py-6 overflow-y-auto">
          <div className="px-5 mb-3 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
            Điều hướng nền tảng
          </div>
          {navigationItems}
        </div>

        {userProfileCard}
      </aside>

      {/* Mobile Drawer (Dialog) */}
      {isMobileMenuOpen && (
        <div
          ref={drawerRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby="mobile-drawer-title"
          className="fixed inset-0 z-50 lg:hidden flex"
        >
          {/* Backdrop */}
          <div
            className="fixed inset-0 bg-black/60 backdrop-blur-xs transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
            aria-hidden="true"
          />

          {/* Drawer panel */}
          <div className="relative flex flex-col w-80 max-w-full bg-white border-r border-slate-200 text-slate-900 dark:bg-slate-900 dark:border-slate-800 dark:text-slate-100 shadow-2xl z-10">
            <div className="h-20 px-5 flex shrink-0 items-center justify-between gap-2 border-b border-slate-200 dark:border-slate-800">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 shrink-0 rounded-lg bg-blue-600 flex items-center justify-center text-white font-black text-base">
                  E
                </div>
                <div className="flex flex-col">
                  <span id="mobile-drawer-title" className="text-sm font-bold text-slate-900 dark:text-white">
                    EduTwin Platform
                  </span>
                  <span className="text-[10px] text-blue-600 dark:text-blue-400 font-semibold uppercase">
                    Control Plane
                  </span>
                </div>
              </div>
              <button
                ref={closeButtonRef}
                type="button"
                onClick={() => setIsMobileMenuOpen(false)}
                aria-label="Đóng menu điều hướng"
                className="grid h-11 w-11 shrink-0 place-items-center text-slate-600 hover:text-slate-900 rounded-lg hover:bg-slate-100 dark:text-slate-400 dark:hover:text-white dark:hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                ✕
              </button>
            </div>

            <div className="flex-1 py-6 overflow-y-auto">
              <div className="px-5 mb-3 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                Điều hướng nền tảng
              </div>
              {navigationItems}
            </div>

            {userProfileCard}
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <div className="flex-1 lg:pl-80 flex flex-col min-w-0">
        {/* Top Header */}
        <header className="platform-topbar sticky top-0 z-20 h-20 shrink-0 bg-white/80 dark:bg-slate-900/80 backdrop-blur-md border-b border-slate-200 dark:border-slate-800 px-4 sm:px-6 flex items-center justify-between gap-2">
          <div className="flex min-w-0 items-center gap-2 sm:gap-3">
            <button
              type="button"
              onClick={() => setIsMobileMenuOpen(true)}
              aria-label="Mở menu điều hướng"
              className="lg:hidden shrink-0 p-2 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
            >
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>

            {/* Breadcrumb Navigation */}
            <nav className="flex min-w-0 items-center gap-2 text-sm text-slate-500 dark:text-slate-400" aria-label="Breadcrumb">
              <Link
                to="/"
                className="hidden sm:inline-flex hover:text-blue-600 dark:hover:text-blue-400 transition-colors font-medium"
              >
                EduTwin
              </Link>
              <span className="hidden sm:inline">/</span>
              <span className="truncate font-semibold text-slate-900 dark:text-slate-100" title={breadcrumbPageTitle}>
                {breadcrumbPageTitle}
              </span>
            </nav>
          </div>

          <div className="flex shrink-0 items-center gap-2 sm:gap-3">
            <ThemeToggle />
            <NotificationBell />
          </div>
        </header>

        {/* Page Content Rendered via Outlet */}
        <main className="min-w-0 flex-1 p-4 sm:p-6">
          <Outlet />
        </main>
      </div>

      {/* Canonical Platform Security Modal */}
      {canManageSecurity && (
        <PlatformSecurityModal
          isOpen={isSecurityModalOpen}
          onClose={() => setIsSecurityModalOpen(false)}
        />
      )}
    </div>
  );
};
