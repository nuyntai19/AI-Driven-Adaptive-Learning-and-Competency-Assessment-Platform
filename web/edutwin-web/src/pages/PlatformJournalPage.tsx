import { Link, Outlet, useLocation } from "react-router-dom";
import { useAuthStore } from "../stores/authStore";
import { platformJournalTabs } from "../utils/platformJournalNavigation";

export function PlatformJournalPage() {
  const user = useAuthStore(state => state.user);
  const { pathname } = useLocation();
  const tabs = platformJournalTabs(user);
  return <div className="w-full min-w-0 space-y-6">
    <header>
      <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl dark:text-white">Nhật ký</h1>
      <p className="mt-2 text-sm leading-7 text-slate-500 dark:text-slate-400">Xem hoạt động dễ hiểu hoặc truy vết kiểm toán chi tiết. Mỗi phần chỉ hiển thị dữ liệu trong phạm vi bạn được cấp quyền.</p>
    </header>
    <nav aria-label="Các phần nhật ký" className="flex flex-wrap gap-2 border-b border-slate-200 pb-2 dark:border-slate-800">
      {tabs.map(tab => <Link key={tab.path} to={tab.path} aria-current={pathname === tab.path ? "page" : undefined}
        className={`rounded-xl px-4 py-3 text-sm font-semibold transition-colors ${pathname === tab.path ? "bg-blue-600 text-white shadow-sm" : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"}`}>{tab.label}</Link>)}
    </nav>
    <Outlet />
  </div>;
}
