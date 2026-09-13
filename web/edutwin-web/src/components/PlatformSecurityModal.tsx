import React, { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { platformApi } from "../api/platformApi";
import type { ProblemDetails } from "../types/auth";

interface PlatformSecurityModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const PlatformSecurityModal: React.FC<PlatformSecurityModalProps> = ({
  isOpen,
  onClose,
}) => {
  const queryClient = useQueryClient();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [revokeReason, setRevokeReason] = useState("");

  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  const { data: profile, isLoading, refetch } = useQuery({
    queryKey: ["platform-security-profile"],
    queryFn: () => platformApi.getSecurityProfile(),
    enabled: isOpen,
  });

  const changePasswordMutation = useMutation({
    mutationFn: () =>
      platformApi.changePassword({
        currentPassword,
        newPassword,
        confirmPassword,
      }),
    onSuccess: (res) => {
      setMessage({ type: "success", text: res.message || "Đã đổi mật khẩu thành công." });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      queryClient.invalidateQueries({ queryKey: ["platform-security-profile"] });
      refetch();
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        setMessage({
          type: "error",
          text: error.response?.data?.detail || "Không thể đổi mật khẩu. Vui lòng kiểm tra lại.",
        });
      } else {
        setMessage({ type: "error", text: "Đã xảy ra lỗi khi đổi mật khẩu." });
      }
    },
  });

  const revokeSessionsMutation = useMutation({
    mutationFn: () =>
      platformApi.revokeSessions({
        reason: revokeReason.trim() || undefined,
      }),
    onSuccess: (res) => {
      setMessage({ type: "success", text: res.message || "Đã thu hồi tất cả phiên làm việc thành công." });
      setRevokeReason("");
      queryClient.invalidateQueries({ queryKey: ["platform-security-profile"] });
      refetch();
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        setMessage({
          type: "error",
          text: error.response?.data?.detail || "Không thể thu hồi phiên.",
        });
      } else {
        setMessage({ type: "error", text: "Đã xảy ra lỗi khi thu hồi phiên." });
      }
    },
  });

  if (!isOpen) return null;

  const handleChangePasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setMessage(null);
    if (newPassword.length < 12) {
      setMessage({ type: "error", text: "Mật khẩu mới phải có ít nhất 12 ký tự." });
      return;
    }
    if (newPassword !== confirmPassword) {
      setMessage({ type: "error", text: "Xác nhận mật khẩu mới không khớp." });
      return;
    }
    changePasswordMutation.mutate();
  };

  const handleRevokeSessionsSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setMessage(null);
    revokeSessionsMutation.mutate();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
      <div className="bg-white dark:bg-gray-800 rounded-2xl max-w-xl w-full p-6 shadow-xl border border-gray-200 dark:border-gray-700 space-y-5 max-h-[90vh] overflow-y-auto">
        <div className="flex justify-between items-center border-b border-gray-200 dark:border-gray-700 pb-3">
          <div className="flex items-center gap-2">
            <span className="text-xl">🛡️</span>
            <h3 className="text-lg font-bold text-gray-900 dark:text-white">
              Bảo Mật Tài Khoản Platform Admin
            </h3>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-lg">
            ✕
          </button>
        </div>

        {message && (
          <div
            className={`p-3 rounded-lg text-sm border flex items-center justify-between ${
              message.type === "success"
                ? "bg-green-50 text-green-800 dark:bg-green-900/30 dark:text-green-300 border-green-200 dark:border-green-800"
                : "bg-red-50 text-red-800 dark:bg-red-900/30 dark:text-red-300 border-red-200 dark:border-red-800"
            }`}
          >
            <span>{message.text}</span>
            <button onClick={() => setMessage(null)} className="text-xs hover:underline ml-2">
              Đóng
            </button>
          </div>
        )}

        {/* Security Profile Overview */}
        <div className="bg-gray-50 dark:bg-gray-900/40 p-4 rounded-xl border border-gray-200 dark:border-gray-700 space-y-2">
          <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
            Thông Tin Phiên & Định Danh
          </h4>
          {isLoading ? (
            <div className="text-xs text-gray-400 py-2">Đang tải hồ sơ...</div>
          ) : profile ? (
            <div className="grid grid-cols-2 gap-3 text-xs">
              <div>
                <span className="text-gray-500 dark:text-gray-400">Tài khoản:</span>{" "}
                <strong className="text-gray-900 dark:text-white font-mono">@{profile.username}</strong>
              </div>
              <div>
                <span className="text-gray-500 dark:text-gray-400">Tên hiển thị:</span>{" "}
                <strong className="text-gray-900 dark:text-white">{profile.displayName}</strong>
              </div>
              <div>
                <span className="text-gray-500 dark:text-gray-400">Auth Version:</span>{" "}
                <span className="inline-flex px-1.5 py-0.5 rounded bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300 font-mono">
                  v{profile.authVersion}
                </span>
              </div>
              <div>
                <span className="text-gray-500 dark:text-gray-400">Phiên đang hoạt động:</span>{" "}
                <span className="inline-flex px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300 font-semibold">
                  {profile.activeSessionCount} phiên
                </span>
              </div>
            </div>
          ) : null}
        </div>

        {/* Change Password Form */}
        <form onSubmit={handleChangePasswordSubmit} className="space-y-3 pt-2">
          <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
            Đổi Mật Khẩu Cá Nhân
          </h4>
          <div>
            <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
              Mật khẩu hiện tại *
            </label>
            <input
              type="password"
              required
              placeholder="••••••••••••"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Mật khẩu mới (≥12 ký tự) *
              </label>
              <input
                type="password"
                required
                minLength={12}
                placeholder="••••••••••••"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Xác nhận mật khẩu mới *
              </label>
              <input
                type="password"
                required
                minLength={12}
                placeholder="••••••••••••"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
              />
            </div>
          </div>
          <div className="flex justify-end pt-1">
            <button
              type="submit"
              disabled={changePasswordMutation.isPending}
              className="px-4 py-2 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg disabled:opacity-50 transition-colors"
            >
              {changePasswordMutation.isPending ? "Đang xử lý..." : "Cập nhật mật khẩu"}
            </button>
          </div>
        </form>

        {/* Revoke All Sessions Form */}
        <div className="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-3">
          <h4 className="text-xs font-bold uppercase tracking-wider text-red-600 dark:text-red-400">
            Thu Hồi Toàn Bộ Phiên Đăng Nhập
          </h4>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Thao tác này sẽ ngay lập tức vô hiệu hóa toàn bộ refresh token đang hoạt động của tài khoản này trên tất cả trình duyệt và thiết bị khác.
          </p>
          <form onSubmit={handleRevokeSessionsSubmit} className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Lý do thu hồi (tùy chọn)
              </label>
              <input
                type="text"
                placeholder="VD: Chủ động thu hồi sau khi sử dụng thiết bị công cộng"
                value={revokeReason}
                onChange={(e) => setRevokeReason(e.target.value)}
                className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
              />
            </div>
            <div className="flex justify-end">
              <button
                type="submit"
                disabled={revokeSessionsMutation.isPending}
                className="px-4 py-2 text-xs font-medium text-white bg-red-600 hover:bg-red-700 rounded-lg disabled:opacity-50 transition-colors"
              >
                {revokeSessionsMutation.isPending ? "Đang thu hồi..." : "Thu hồi tất cả phiên khác"}
              </button>
            </div>
          </form>
        </div>

        <div className="flex justify-end pt-2 border-t border-gray-200 dark:border-gray-700">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
