import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getCenterDashboard } from "../api/dashboardsApi";
import type { CenterDashboardDataDto } from "../types/dashboards";

export const CenterDashboardPage = () => {
  const {
    data: dashboard,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery<CenterDashboardDataDto>({
    queryKey: ["centerDashboard"],
    queryFn: () => getCenterDashboard(),
  });

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        {/* Header Breadcrumbs */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 mb-1">
              <Link to="/" className="hover:text-indigo-600">Trang chủ</Link>
              <span>/</span>
              <span className="text-slate-900">Dashboard Quản lý Trung tâm</span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900">
              Tổng Quan Năng Lực Toàn Trung Tâm
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Giám sát tình trạng học thuật, xếp hạng các lớp học và thống kê học sinh nguy cơ cao theo lớp.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => refetch()}
              className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Làm mới
            </button>
          </div>
        </div>

        {/* Loading / Error states */}
        {isLoading && (
          <div className="flex items-center justify-center rounded-2xl bg-white p-12 shadow-sm ring-1 ring-slate-200">
            <span className="font-medium text-indigo-600 animate-pulse">
              Đang tải dữ liệu Trung tâm...
            </span>
          </div>
        )}

        {isError && (
          <div className="rounded-2xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
            <h2 className="text-lg font-bold text-red-600">Không thể tải dữ liệu Trung tâm</h2>
            <p className="mt-2 text-sm text-slate-500">
              {(error as Error)?.message || "Vui lòng kiểm tra lại kết nối hoặc quyền hạn quản lý."}
            </p>
            <button
              onClick={() => refetch()}
              className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
          </div>
        )}

        {!isLoading && !isError && dashboard && (
          <>
            {/* KPI Cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Tổng số lớp học
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-slate-900">
                    {dashboard.summary.classCount}
                  </span>
                  <span className="text-xs text-slate-500">lớp</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Đang hoạt động trong trung tâm</p>
              </div>

              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Tổng số học sinh
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-slate-900">
                    {dashboard.summary.studentCount}
                  </span>
                  <span className="text-xs text-slate-500">em</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Đã kích hoạt hồ sơ năng lực</p>
              </div>

              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Đội ngũ giáo viên
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-slate-900">
                    {dashboard.summary.teacherCount}
                  </span>
                  <span className="text-xs text-slate-500">thầy cô</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Phụ trách giảng dạy</p>
              </div>

              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Số môn học đang đào tạo
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-indigo-600">
                    {dashboard.masteryBySubject.length}
                  </span>
                  <span className="text-xs text-slate-500">môn</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Tích hợp mô hình Digital Twin</p>
              </div>
            </div>

            {/* Subject Mastery and Class Rankings Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Subject Mastery Breakdown (1 col) */}
              <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
                <h2 className="text-base font-bold text-slate-900 mb-1">
                  Năng Lực Theo Môn Học
                </h2>
                <p className="text-xs text-slate-500 mb-4 border-b border-slate-100 pb-3">
                  Độ thuần thục trung bình của học sinh ở từng môn.
                </p>

                {dashboard.masteryBySubject.length === 0 ? (
                  <p className="text-xs text-slate-500 py-4 text-center">Chưa có dữ liệu môn học.</p>
                ) : (
                  <div className="space-y-4">
                    {dashboard.masteryBySubject.map((sm) => (
                      <div key={sm.subjectId} className="space-y-1.5">
                        <div className="flex items-center justify-between text-xs">
                          <span className="font-semibold text-slate-800">{sm.subjectName}</span>
                          <span className="font-bold text-indigo-600">
                            {sm.averageMastery.toFixed(1)}%
                          </span>
                        </div>
                        <div className="h-2.5 rounded-full bg-slate-100 overflow-hidden">
                          <div
                            className={`h-full rounded-full ${
                              sm.averageMastery >= 75
                                ? "bg-emerald-500"
                                : sm.averageMastery >= 50
                                ? "bg-indigo-500"
                                : "bg-amber-500"
                            }`}
                            style={{ width: `${Math.min(100, Math.max(0, sm.averageMastery))}%` }}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Class Rankings (2 cols) */}
              <div className="lg:col-span-2 rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
                <div className="flex items-center justify-between mb-4 border-b border-slate-100 pb-3">
                  <div>
                    <h2 className="text-base font-bold text-slate-900">
                      Bảng Xếp Hạng & Tình Hình Các Lớp
                    </h2>
                    <p className="text-xs text-slate-500">
                      Sắp xếp theo thứ hạng năng lực và tỷ lệ hoàn thành bài tập.
                    </p>
                  </div>
                  <span className="text-xs font-semibold text-slate-500">
                    {dashboard.classRanking.length} lớp
                  </span>
                </div>

                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-slate-200 text-left text-xs">
                    <thead className="text-slate-400 font-bold uppercase">
                      <tr>
                        <th className="py-2.5">Hạng</th>
                        <th className="py-2.5">Lớp học</th>
                        <th className="py-2.5">Môn</th>
                        <th className="py-2.5">Năng lực TB</th>
                        <th className="py-2.5">Tỷ lệ hoàn thành</th>
                        <th className="py-2.5">Nguy cơ cao</th>
                        <th className="py-2.5 text-right">Hành động</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {dashboard.classRanking.map((c) => (
                        <tr key={c.classId} className="hover:bg-slate-50">
                          <td className="py-3 font-black text-slate-900 whitespace-nowrap">
                            #{c.rank}
                          </td>
                          <td className="py-3 font-semibold text-slate-900 whitespace-nowrap">
                            {c.className}
                          </td>
                          <td className="py-3 text-slate-600 whitespace-nowrap">
                            {c.subjectName}
                          </td>
                          <td className="py-3 whitespace-nowrap">
                            <span className="font-bold text-indigo-600">
                              {c.averageMastery.toFixed(1)}%
                            </span>
                          </td>
                          <td className="py-3 text-slate-700 whitespace-nowrap">
                            {c.assignmentCompletionRate.toFixed(0)}%
                          </td>
                          <td className="py-3 whitespace-nowrap">
                            <span
                              className={`rounded px-2 py-0.5 font-bold ${
                                c.highRiskStudentCount > 0
                                  ? "bg-red-100 text-red-800"
                                  : "bg-emerald-100 text-emerald-800"
                              }`}
                            >
                              {c.highRiskStudentCount} em
                            </span>
                          </td>
                          <td className="py-3 text-right whitespace-nowrap">
                            <Link
                              to={`/quan-ly/lop-hoc/${c.classId}/tong-quan`}
                              className="rounded bg-indigo-50 px-2.5 py-1 font-bold text-indigo-700 hover:bg-indigo-100"
                            >
                              Chi tiết lớp →
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
