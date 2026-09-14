import React, { useState, useRef } from "react";
import { Link, useLocation, useNavigate, Outlet } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { logout } from "../auth/authApi";
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
  const menuButtonRef = useRef<HTMLButtonElement>(null);

  useModalAccessibility({
    isOpen: isMobileMenuOpen,
    onClose: () => setIsMobileMenuOpen(false),
    containerRef: drawerRef,
    initialFocusRef: drawerRef,
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
  const canAccessAudit = hasPermission(permissions.platformAuditRead);
  const canManageSecurity = hasPermission(permissions.platformAccountManageOwn);

  const initials = getInitials(user?.displayName, user?.username);

  const isCentersActive = location.pathname.startsWith("/quan-tri-nen-tang/trung-tam");
  const isAuditActive = location.pathname.startsWith("/quan-tri-nen-tang/nhat-ky");

  const breadcrumbPageTitle = isCentersActive
    ? "Trung tâm đối tác"
    : isAuditActive
    ? "Nhật ký kiểm toán"
    : "Quản trị nền tảng";

  const navigationItems = (
    <nav className="space-y-1 px-3">
      {canAccessCenters && (
        <Link
          to="/quan-tri-nen-tang/trung-tam"
          onClick={() => setIsMobileMenuOpen(false)}
          className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors ${
            isCentersActive
              ? "bg-blue-600 text-white shadow-sm shadow-blue-500/20"
              : "text-slate-300 hover:text-white hover:bg-slate-800"
          }`}
        >
          <svg className="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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

      {canAccessAudit && (
        <Link
          to="/quan-tri-nen-tang/nhat-ky"
          onClick={() => setIsMobileMenuOpen(false)}
          className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-colors ${
            isAuditActive
              ? "bg-blue-600 text-white shadow-sm shadow-blue-500/20"
              : "text-slate-300 hover:text-white hover:bg-slate-800"
          }`}
        >
          <svg className="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.75}
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
          <span>Nhật ký kiểm toán</span>
        </Link>
      )}

      {canManageSecurity && (
        <button
          type="button"
          onClick={() => {
            setIsMobileMenuOpen(false);
            setIsSecurityModalOpen(true);
          }}
          className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium text-slate-300 hover:text-white hover:bg-slate-800 transition-colors text-left"
        >
          <svg className="w-5 h-5 shrink-0 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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
    <div className="p-4 border-t border-slate-800 bg-slate-900/60">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white font-bold text-sm flex items-center justify-center shrink-0 shadow-sm shadow-blue-500/20">
          {initials}
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-white truncate" title={user?.displayName}>
            {user?.displayName || "Platform Admin"}
          </p>
          <p className="text-xs text-slate-400 font-mono truncate" title={`@${user?.username}`}>
            @{user?.username || "admin"}
          </p>
          <div className="mt-1 flex items-center gap-1.5 flex-wrap">
            <span
              className="text-[11px] text-slate-400 truncate max-w-[130px]"
              title={user?.centerName}
            >
              {user?.centerName || "EduTwin"}
            </span>
            <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-blue-500/20 text-blue-300 border border-blue-500/30">
              Root tenant
            </span>
          </div>
        </div>
      </div>

      <button
        type="button"
        onClick={handleLogout}
        disabled={isLoggingOut}
        className="mt-3.5 w-full flex items-center justify-center gap-2 px-3 py-2 text-xs font-medium text-slate-400 hover:text-white bg-slate-800 hover:bg-slate-700/80 rounded-lg transition-colors disabled:opacity-50"
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
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 flex transition-colors duration-150">
      {/* Desktop Sidebar */}
      <aside className="hidden lg:flex lg:w-64 lg:flex-col lg:fixed lg:inset-y-0 bg-slate-900 border-r border-slate-800 text-slate-100 z-30">
        <div className="h-16 px-6 flex items-center gap-3 border-b border-slate-800">
          <div className="w-8 h-8 rounded-lg bg-blue-600 flex items-center justify-center text-white font-black text-base shadow-sm shadow-blue-500/30">
            E
          </div>
          <div className="flex flex-col">
            <span className="text-sm font-bold tracking-tight text-white">EduTwin Platform</span>
            <span className="text-[10px] text-blue-400 font-semibold tracking-wider uppercase">
              Control Plane
            </span>
          </div>
        </div>

        <div className="flex-1 py-6 overflow-y-auto">
          <div className="px-5 mb-3 text-[11px] font-bold uppercase tracking-wider text-slate-400">
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
          <div className="relative flex flex-col w-72 max-w-full bg-slate-900 border-r border-slate-800 text-slate-100 shadow-2xl z-10">
            <div className="h-16 px-6 flex items-center justify-between border-b border-slate-800">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-blue-600 flex items-center justify-center text-white font-black text-base">
                  E
                </div>
                <div className="flex flex-col">
                  <span id="mobile-drawer-title" className="text-sm font-bold text-white">
                    EduTwin Platform
                  </span>
                  <span className="text-[10px] text-blue-400 font-semibold uppercase">
                    Control Plane
                  </span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsMobileMenuOpen(false)}
                aria-label="Đóng menu điều hướng"
                className="p-1 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800"
              >
                ✕
              </button>
            </div>

            <div className="flex-1 py-6 overflow-y-auto">
              <div className="px-5 mb-3 text-[11px] font-bold uppercase tracking-wider text-slate-400">
                Điều hướng nền tảng
              </div>
              {navigationItems}
            </div>

            {userProfileCard}
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <div className="flex-1 lg:pl-64 flex flex-col min-w-0">
        {/* Top Header */}
        <header className="sticky top-0 z-20 h-16 bg-white/80 dark:bg-slate-900/80 backdrop-blur-md border-b border-slate-200 dark:border-slate-800 px-4 sm:px-6 lg:px-8 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button
              ref={menuButtonRef}
              type="button"
              onClick={() => setIsMobileMenuOpen(true)}
              aria-label="Mở menu điều hướng"
              className="lg:hidden p-2 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
            >
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>

            {/* Breadcrumb Navigation */}
            <nav className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400" aria-label="Breadcrumb">
              <Link
                to="/"
                className="hover:text-blue-600 dark:hover:text-blue-400 transition-colors font-medium"
              >
                EduTwin
              </Link>
              <span>/</span>
              <span className="font-semibold text-slate-900 dark:text-slate-100">
                {breadcrumbPageTitle}
              </span>
            </nav>
          </div>

          <div className="flex items-center gap-3">
            <ThemeToggle />
          </div>
        </header>

        {/* Page Content Rendered via Outlet */}
        <main className="flex-1 p-4 sm:p-6 lg:p-8">
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
