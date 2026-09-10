import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { logout } from "../auth/authApi";
import { useAuthStore } from "../stores/authStore";
import type { AccountType, UserStatus } from "../types/auth";
import { authorizationUiPermissions, permissions } from "../auth/permissions";

const roleLabels: Record<AccountType, string> = {
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
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const hasAnyPermission = useAuthStore((state) => state.hasAnyPermission);

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
              Loại tài khoản: {roleLabels[user.accountType]}
            </span>
            <span className="inline-flex items-center rounded-md bg-slate-50 px-2 py-1 text-xs font-medium text-slate-700 ring-1 ring-inset ring-slate-300">
              {user.roles.length} role · {user.permissions.length} permission
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

          <div className="mt-6 flex flex-wrap gap-4">
            {hasPermission(permissions.subjectsRead) && hasPermission(permissions.nodesRead) && hasPermission(permissions.edgesRead) && (
              <Link to="/kien-thuc/do-thi" className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500">Đồ thị kiến thức</Link>
            )}
          </div>

          {hasPermission(permissions.teachersRead) && (
            <div className="mt-4 flex flex-wrap gap-4">
              <Link
                to="/quan-ly/giao-vien"
                className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
              >
                Quản lý giáo viên
              </Link>
            </div>
          )}

          <div className="mt-4 flex flex-wrap gap-4">
            {hasPermission(permissions.classesRead) && <Link to="/quan-ly/lop-hoc" className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500">Danh sách lớp học</Link>}
            {hasPermission(permissions.studentsRead) && <Link to="/quan-ly/hoc-sinh" className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500">Danh sách học sinh</Link>}
            {hasPermission(permissions.curriculumsRead) && <Link to="/quan-ly/giao-trinh" className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500">Quản lý lộ trình</Link>}
            {hasPermission(permissions.questionsRead) && <Link to="/quan-ly/cau-hoi" className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500">Ngân hàng câu hỏi</Link>}
            {hasPermission(permissions.assignmentsRead) && user.accountType !== "Student" && <Link to="/quan-ly/bai-tap" className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500">Quản lý bài tập</Link>}
            {hasAnyPermission(authorizationUiPermissions) && user.accountType === "CenterManager" && <Link to="/quan-ly/phan-quyen" className="inline-flex items-center justify-center rounded-md bg-amber-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-amber-500">Phân quyền trung tâm</Link>}
          </div>

          {user.accountType === "Student" && hasPermission(permissions.assignmentsRead) && (
            <div className="mt-4 flex flex-wrap gap-4">
              <Link
                to="/hoc-tap/bai-tap"
                className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
              >
                Bài tập của tôi
              </Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
