import React, { useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { getCurrentUser, loginAdmin } from "../auth/authApi";
import { CENTER_LOGIN_PATH } from "../auth/loginPortal";
import { useAuthStore } from "../stores/authStore";
import { mapSafeLoginError } from "../utils/problemDetails";

const DISPLAY = "[font-family:Montserrat,'Be_Vietnam_Pro',system-ui,sans-serif]";
const INPUT = "w-full bg-white border border-[#E2D8C9] rounded-xl px-3.5 py-2.5 text-sm text-[#15110E] placeholder-[#A99C8C] focus:outline-none focus:border-[#CB1C01] focus:ring-4 focus:ring-[#FDEDE7] transition-all";
const LABEL = "block text-xs font-bold uppercase tracking-wider text-[#4B4138] mb-1.5";

/** Sign-in for platform administrators only. Center accounts use /dang-nhap. */
export const AdminLoginPage = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const sessionStatus = useAuthStore((state) => state.sessionStatus);
  const clearSession = useAuthStore((state) => state.clearSession);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  if (sessionStatus === "authenticated" && !isLoading) return <Navigate to="/" replace />;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");
    const trimmedUsername = username.trim();
    if (!trimmedUsername || !password) {
      setErrorMsg("Vui lòng nhập tên đăng nhập và mật khẩu quản trị.");
      return;
    }
    setIsLoading(true);
    try {
      queryClient.clear();
      await loginAdmin({ username: trimmedUsername, password });
      await getCurrentUser();
      navigate("/", { replace: true });
    } catch (error) {
      clearSession();
      queryClient.clear();
      setErrorMsg(mapSafeLoginError(error, "admin"));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center px-4 py-10 bg-[#FEFCF9] text-[#15110E] [font-family:'Be_Vietnam_Pro',system-ui,sans-serif] bg-[radial-gradient(60%_55%_at_100%_0%,#FBDCCF,transparent_70%),radial-gradient(50%_50%_at_0%_100%,#FEF4DA,transparent_70%)]">
      <main className="w-full max-w-md">
        <div className="mb-6 flex items-center justify-center gap-2.5">
          <span className="w-10 h-10 rounded-xl bg-[linear-gradient(135deg,#CB1C01,#F2581D)] flex items-center justify-center text-white shadow-[0_10px_24px_-10px_rgba(203,28,1,0.55)]">
            <svg aria-hidden="true" className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 3 4 6v6c0 4.5 3.4 8.3 8 9 4.6-.7 8-4.5 8-9V6Z" /><path d="m9 12 2 2 4-4" /></svg>
          </span>
          <span className={`text-xl font-extrabold tracking-tight ${DISPLAY}`}>Edu<span className="text-[#CB1C01]">Twin</span></span>
        </div>
        <div className="bg-white border border-[#EEE7DC] p-8 sm:p-10 rounded-[24px] shadow-[0_30px_60px_-28px_rgba(21,17,14,0.35)] space-y-7">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[0.12em] text-[#A81700]">Cổng quản trị nền tảng</p>
            <h1 className={`mt-1.5 text-2xl font-extrabold tracking-tight ${DISPLAY}`}>Đăng nhập quản trị</h1>
            <p className="mt-2 text-sm text-[#7A6D60]">Chỉ dành cho Quản trị viên nền tảng. Quản lý trung tâm, giáo viên và học sinh đăng nhập tại trang trung tâm.</p>
          </div>

          {errorMsg && (
            <div role="alert" data-toast-mirror="off" className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm flex items-start gap-2.5">
              <span aria-hidden="true" className="font-bold">!</span>
              <span className="flex-1">{errorMsg}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4" noValidate>
            <div>
              <label htmlFor="adminUsername" className={LABEL}>Tên đăng nhập <span className="text-[#CB1C01]">*</span></label>
              <input id="adminUsername" name="username" type="text" required autoComplete="username" placeholder="Tên tài khoản quản trị"
                value={username} onChange={(e) => setUsername(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label htmlFor="adminPassword" className={LABEL}>Mật khẩu <span className="text-[#CB1C01]">*</span></label>
              <div className="relative">
                <input id="adminPassword" name="password" type={showPassword ? "text" : "password"} required autoComplete="current-password" placeholder="••••••••••••"
                  value={password} onChange={(e) => setPassword(e.target.value)} className={`${INPUT} pr-11`} />
                <button type="button" onClick={() => setShowPassword((v) => !v)} aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 p-1.5 text-[#7A6D60] hover:text-[#15110E] rounded-lg hover:bg-[#F7F2EA] transition-colors">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    {showPassword
                      ? <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 3l18 18M10.6 10.6a2 2 0 002.8 2.8M9.9 5.1A9.8 9.8 0 0112 5c5 0 9 4.5 10 7-.4 1-1.2 2.4-2.4 3.7M6.1 6.1C4 7.5 2.6 9.7 2 12c1 2.5 5 7 10 7 1.7 0 3.3-.5 4.6-1.3" />
                      : <><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M2.5 12C3.7 8.6 7.5 5 12 5s8.3 3.6 9.5 7c-1.2 3.4-5 7-9.5 7s-8.3-3.6-9.5-7z" /></>}
                  </svg>
                </button>
              </div>
            </div>
            <button type="submit" disabled={isLoading}
              className={`mt-2 w-full flex items-center justify-center gap-2 rounded-full bg-[linear-gradient(100deg,#CB1C01,#F2581D)] shadow-[0_10px_24px_-10px_rgba(203,28,1,0.55)] hover:brightness-105 hover:-translate-y-px px-4 py-3 text-sm font-bold text-white ${DISPLAY} focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#CB1C01] disabled:opacity-50 transition-all cursor-pointer`}>
              {isLoading ? "Đang xác thực..." : "Đăng nhập quản trị"}
            </button>
          </form>

          <div className="pt-4 border-t border-[#EEE7DC] text-center text-sm text-[#7A6D60]">
            Không phải quản trị viên? <Link to={CENTER_LOGIN_PATH} className="font-bold text-[#A81700] hover:underline">Đăng nhập trung tâm</Link>
          </div>
        </div>
      </main>
    </div>
  );
};

export default AdminLoginPage;
