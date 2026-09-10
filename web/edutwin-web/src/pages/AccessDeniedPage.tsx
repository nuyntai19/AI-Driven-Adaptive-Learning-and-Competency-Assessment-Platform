import { Link, useLocation } from "react-router-dom";

export const AccessDeniedPage = () => {
  const location = useLocation();
  const attemptedPath = (location.state as { attemptedPath?: string } | null)
    ?.attemptedPath;

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 p-6">
      <section className="w-full max-w-lg rounded-xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
        <p className="text-sm font-semibold uppercase tracking-wide text-amber-700">
          Không có quyền truy cập
        </p>
        <h1 className="mt-2 text-2xl font-bold text-slate-900">
          Tài khoản chưa được cấp quyền cho chức năng này
        </h1>
        <p className="mt-3 text-sm text-slate-600">
          Liên hệ quản lý trung tâm nếu bạn cần sử dụng chức năng này. Việc ẩn nút
          trên giao diện không thay thế kiểm tra quyền tại API.
        </p>
        {attemptedPath && (
          <p className="mt-3 break-all rounded-md bg-slate-100 px-3 py-2 text-xs text-slate-500">
            Đường dẫn: {attemptedPath}
          </p>
        )}
        <Link
          to="/"
          className="mt-6 inline-flex rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
        >
          Về trang chính
        </Link>
      </section>
    </main>
  );
};
