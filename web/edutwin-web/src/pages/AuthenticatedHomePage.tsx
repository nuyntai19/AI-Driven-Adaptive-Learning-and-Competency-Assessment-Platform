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
  PlatformAdmin: "Quản trị viên nền tảng",
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
      <div className="mx-auto max-w-5xl rounded-2xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b border-slate-100 pb-6 gap-4 sm:gap-0">
          <div>
            <h1 className="text-3xl font-bold text-slate-900 break-words">
              Xin chào, {user.displayName}
            </h1>
            <p className="mt-1 text-slate-500">Trung tâm: {user.centerName}</p>
          </div>
          <button
            onClick={handleLogout}
            disabled={isLoggingOut}
            className="w-full sm:w-auto rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50 disabled:opacity-50"
          >
            {isLoggingOut ? "Đang xử lý..." : "Đăng xuất"}
          </button>
        </div>

        <div className="mt-6">
          <div className="mb-6 flex gap-3 flex-wrap">
            <span className="inline-flex items-center rounded-md bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
              Loại tài khoản: {roleLabels[user.accountType]}
            </span>
            <span className="inline-flex items-center rounded-md bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-700 ring-1 ring-inset ring-slate-300">
              {user.roles.length} vai trò · {user.permissions.length} quyền hạn
            </span>
            {user.status && (
              <span className="inline-flex items-center rounded-md bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
                Trạng thái: {statusLabels[user.status]}
              </span>
            )}
          </div>

          {/* Capability-First Dashboards Section */}
          <div className="space-y-4">
            <h2 className="text-sm font-bold uppercase tracking-wider text-slate-400">
              Không gian làm việc & Dashboard Năng lực
            </h2>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {/* Student Experience */}
              {hasPermission(permissions.dashboardsStudentRead) && (
                <div className="rounded-xl border border-indigo-100 bg-gradient-to-br from-indigo-50/60 to-purple-50/60 p-5 flex flex-col justify-between">
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-indigo-600">
                      Học tập cá nhân hóa
                    </span>
                    <h3 className="mt-1 text-lg font-bold text-slate-900">
                      Dashboard Học Sinh
                    </h3>
                    <p className="mt-1 text-xs text-slate-600">
                      Theo dõi độ thuần thục chuyên đề, điểm số dự báo và lộ trình ôn luyện thích ứng.
                    </p>
                  </div>
                  <div className="mt-4 flex flex-col gap-2">
                    <Link
                      to="/hoc-tap/tong-quan"
                      className="inline-flex items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-500"
                    >
                      Xem Dashboard →
                    </Link>
                    {hasPermission(permissions.twinStudentReadOwn) && (
                      <Link
                        to="/hoc-tap/ho-so-nang-luc"
                        className="inline-flex items-center justify-center rounded-lg bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-200 hover:bg-slate-50"
                      >
                        Hồ sơ Năng lực (Twin)
                      </Link>
                    )}
                    {hasPermission(permissions.learningAttemptsSubmit) && (
                      <Link
                        to="/hoc-tap/luyen-tap"
                        className="inline-flex items-center justify-center rounded-lg bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-200 hover:bg-slate-50"
                      >
                        Luyện tập thích ứng
                      </Link>
                    )}
                  </div>
                </div>
              )}

              {/* Teacher Experience */}
              {(hasPermission(permissions.dashboardsTeacherRead) || hasPermission(permissions.teacherReviewsRead)) && (
                <div className="rounded-xl border border-teal-100 bg-gradient-to-br from-teal-50/60 to-emerald-50/60 p-5 flex flex-col justify-between">
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-teal-700">
                      Giảng dạy & Giám sát
                    </span>
                    <h3 className="mt-1 text-lg font-bold text-slate-900">
                      Dashboard Lớp Học
                    </h3>
                    <p className="mt-1 text-xs text-slate-600">
                      Giám sát học sinh nguy cơ cao, nhóm hổng kiến thức và can thiệp kịp thời.
                    </p>
                  </div>
                  <div className="mt-4 flex flex-col gap-2">
                    {hasPermission(permissions.dashboardsTeacherRead) && (
                      <Link
                        to="/quan-ly/tong-quan-lop-hoc"
                        className="inline-flex items-center justify-center rounded-lg bg-teal-700 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-teal-600"
                      >
                        Dashboard Lớp học →
                      </Link>
                    )}
                    {hasPermission(permissions.teacherReviewsRead) && (
                      <Link
                        to="/quan-ly/duyet-bai"
                        className="inline-flex items-center justify-center rounded-lg bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-200 hover:bg-slate-50"
                      >
                        Hàng đợi duyệt bài
                      </Link>
                    )}
                  </div>
                </div>
              )}

              {/* Center Manager Experience */}
              {hasPermission(permissions.dashboardsCenterRead) && (
                <div className="rounded-xl border border-purple-100 bg-gradient-to-br from-purple-50/60 to-pink-50/60 p-5 flex flex-col justify-between">
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-purple-700">
                      Quản trị trung tâm
                    </span>
                    <h3 className="mt-1 text-lg font-bold text-slate-900">
                      Dashboard Trung Tâm
                    </h3>
                    <p className="mt-1 text-xs text-slate-600">
                      Chỉ số vận hành toàn diện, bảng xếp hạng các lớp và sức khỏe hệ thống Digital Twin.
                    </p>
                  </div>
                  <div className="mt-4 flex flex-col gap-2">
                    <Link
                      to="/quan-ly/tong-quan-trung-tam"
                      className="inline-flex items-center justify-center rounded-lg bg-purple-700 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-purple-600"
                    >
                      Xem Dashboard Trung tâm →
                    </Link>
                  </div>
                </div>
              )}

              {/* Platform Administration Experience */}
              {(hasPermission(permissions.platformCentersRead) || hasPermission(permissions.platformCentersManage)) && (
                <div className="rounded-xl border border-blue-100 bg-gradient-to-br from-blue-50/60 to-cyan-50/60 p-5 flex flex-col justify-between">
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-blue-700">
                      Nền tảng hệ thống
                    </span>
                    <h3 className="mt-1 text-lg font-bold text-slate-900">
                      Quản Trị Trung Tâm Đối Tác
                    </h3>
                    <p className="mt-1 text-xs text-slate-600">
                      Khởi tạo trung tâm mới, quản lý vòng đời kích hoạt/tạm ngưng và hỗ trợ quản trị viên.
                    </p>
                  </div>
                  <div className="mt-4 flex flex-col gap-2">
                    <Link
                      to="/quan-tri-nen-tang/trung-tam"
                      className="inline-flex items-center justify-center rounded-lg bg-blue-700 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-blue-600"
                    >
                      Vào Quản Trị Trung Tâm →
                    </Link>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Academic & Operations Management Links */}
          <div className="mt-8 border-t border-slate-100 pt-6">
            <h2 className="text-sm font-bold uppercase tracking-wider text-slate-400 mb-3">
              Quản lý học thuật & Nghiệp vụ
            </h2>

            <div className="flex flex-wrap gap-3">
              {hasPermission(permissions.subjectsRead) && hasPermission(permissions.nodesRead) && hasPermission(permissions.edgesRead) && (
                <Link
                  to="/kien-thuc/do-thi"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Đồ thị kiến thức
                </Link>
              )}

              {hasPermission(permissions.teachersRead) && (
                <Link
                  to="/quan-ly/giao-vien"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Quản lý giáo viên
                </Link>
              )}

              {hasPermission(permissions.classesRead) && (
                <Link
                  to="/quan-ly/lop-hoc"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Danh sách lớp học
                </Link>
              )}

              {hasPermission(permissions.studentsRead) && (
                <Link
                  to="/quan-ly/hoc-sinh"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Danh sách học sinh
                </Link>
              )}

              {hasPermission(permissions.curriculumsRead) && (
                <Link
                  to="/quan-ly/giao-trinh"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Quản lý lộ trình
                </Link>
              )}

              {hasPermission(permissions.questionsRead) && (
                <Link
                  to="/quan-ly/cau-hoi"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Ngân hàng câu hỏi
                </Link>
              )}

              {(hasPermission(permissions.assignmentsCreate) || (hasPermission(permissions.assignmentsRead) && user.accountType !== "Student")) && (
                <Link
                  to="/quan-ly/bai-tap"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Quản lý bài tập
                </Link>
              )}

              {user.accountType === "Student" && hasPermission(permissions.assignmentsRead) && (
                <Link
                  to="/hoc-tap/bai-tap"
                  className="rounded-lg bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Bài tập của tôi
                </Link>
              )}

              {hasAnyPermission(authorizationUiPermissions) && (
                <Link
                  to="/quan-ly/phan-quyen"
                  className="rounded-lg bg-amber-50 px-3.5 py-2 text-xs font-semibold text-amber-800 ring-1 ring-inset ring-amber-300 hover:bg-amber-100"
                >
                  Phân quyền trung tâm
                </Link>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
