import { useState } from "react";
import { useNavigate, Navigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { login, getCurrentUser } from "../auth/authApi";
import { useAuthStore } from "../stores/authStore";
import type { ProblemDetails } from "../types/auth";

export const LoginPage = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const sessionStatus = useAuthStore((state) => state.sessionStatus);
  const clearSession = useAuthStore((state) => state.clearSession);

  const [centerCode, setCenterCode] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [errorMsg, setErrorMsg] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  if (sessionStatus === "authenticated" && !isLoading) {
    return <Navigate to="/" replace />;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    const trimmedCenterCode = centerCode.trim();
    const trimmedUsername = username.trim();

    if (!trimmedCenterCode || !trimmedUsername || !password) {
      setErrorMsg("Vui lòng điền đầy đủ các thông tin bắt buộc.");
      return;
    }

    setIsLoading(true);
    try {
      queryClient.clear();
      await login({
        centerCode: trimmedCenterCode,
        username: trimmedUsername,
        password,
      });
      await getCurrentUser();
      navigate("/", { replace: true });
    } catch (error) {
      clearSession();
      queryClient.clear();
      if (isAxiosError<ProblemDetails>(error) && error.response?.data) {
        const problem = error.response.data;
        if (problem.errorCode === "AUTH_INVALID_CREDENTIALS") {
          setErrorMsg("Mã trung tâm, tên đăng nhập hoặc mật khẩu không hợp lệ.");
        } else if (problem.errorCode === "AUTH_USER_DISABLED") {
          setErrorMsg("Tài khoản hoặc trung tâm hiện không khả dụng.");
        } else {
          setErrorMsg("Không thể kết nối hệ thống. Vui lòng thử lại.");
        }
      } else {
        setErrorMsg("Không thể kết nối hệ thống. Vui lòng thử lại.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-4">
      <div className="w-full max-w-md rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
        <h1 className="mb-6 text-2xl font-bold text-center text-slate-900">
          Đăng nhập EduTwin
        </h1>

        {errorMsg && (
          <div role="alert" className="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-600 ring-1 ring-red-200">
            {errorMsg}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="centerCode" className="block text-sm font-medium text-slate-700">
              Mã trung tâm <span className="text-red-500">*</span>
            </label>
            <input
              id="centerCode"
              name="centerCode"
              type="text"
              required
              value={centerCode}
              onChange={(e) => setCenterCode(e.target.value)}
              className="mt-1 block w-full rounded-md border-slate-300 px-3 py-2 ring-1 ring-slate-200 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          <div>
            <label htmlFor="username" className="block text-sm font-medium text-slate-700">
              Tên đăng nhập <span className="text-red-500">*</span>
            </label>
            <input
              id="username"
              name="username"
              type="text"
              required
              autoComplete="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="mt-1 block w-full rounded-md border-slate-300 px-3 py-2 ring-1 ring-slate-200 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          <div>
            <label htmlFor="password" className="block text-sm font-medium text-slate-700">
              Mật khẩu <span className="text-red-500">*</span>
            </label>
            <input
              id="password"
              name="password"
              type="password"
              required
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="mt-1 block w-full rounded-md border-slate-300 px-3 py-2 ring-1 ring-slate-200 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="mt-6 flex w-full justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:opacity-50"
          >
            {isLoading ? "Đang xử lý..." : "Đăng nhập"}
          </button>
        </form>
      </div>
    </div>
  );
};
