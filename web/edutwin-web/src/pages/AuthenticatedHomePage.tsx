import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { logout } from "../auth/authApi";
import { useAuthStore } from "../stores/authStore";
import type { UserRole, UserStatus } from "../types/auth";

const roleLabels: Record<UserRole, string> = {
  Student: "Học sinh",
  Teacher: "Giáo viên",
  CenterManager: "Quản lý trung tâm",
};

const statusLabels: Record<UserStatus, string> = {
  Active: "Hoạt động",
  Locked: "Đã khóa",
  Disabled: "Vô hiệu hóa",
};

export const AuthenticatedHomePage = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);

  const [isLoggingOut, setIsLoggingOut] = useState(false);

  if (!user) return null;

  const handleLogout = async () => {
    setIsLoggingOut(true);
    try {
      await logout();
    } catch {
      // Bỏ qua lỗi mạng khi logout để vẫn xóa local session
    } finally {
      queryClient.clear();
      navigate("/dang-nhap", { replace: true });
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-4xl rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b border-slate-100 pb-6 gap-4 sm:gap-0">
          <div>
            <h1 className="text-3xl font-bold text-slate-900 break-words">
              Xin chào, {user.displayName}
            </h1>
            <p className="mt-2 text-slate-500">Trung tâm: {user.centerName}</p>
          </div>
          <button
            onClick={handleLogout}
            disabled={isLoggingOut}
            className="w-full sm:w-auto rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50 disabled:opacity-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
          >
            {isLoggingOut ? "Đang xử lý..." : "Đăng xuất"}
          </button>
        </div>

        <div className="mt-6">
          <div className="mb-6 flex gap-4 flex-wrap">
            <span className="inline-flex items-center rounded-md bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
              Vai trò: {roleLabels[user.role]}
            </span>
            {user.status && (
              <span className="inline-flex items-center rounded-md bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
                Trạng thái: {statusLabels[user.status]}
              </span>
            )}
          </div>

          <div className="rounded-lg bg-amber-50 p-4 ring-1 ring-amber-200">
            <p className="text-amber-800">
              Dashboard nghiệp vụ sẽ được triển khai ở phase tiếp theo.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
