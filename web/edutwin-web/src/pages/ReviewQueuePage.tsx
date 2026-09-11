import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listTeacherReviewQueue } from "../api/teacherReviewsApi";
import { organizationApi } from "../api/organizationApi";
import { TeacherOverrideModal } from "../components/TeacherOverrideModal";
import type { TeacherReviewQueueItemDto } from "../types/reviews";

export const ReviewQueuePage = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedClassId = searchParams.get("classId") || "";
  const [page, setPage] = useState<number>(1);
  const [selectedReview, setSelectedReview] = useState<TeacherReviewQueueItemDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);

  // Load teacher classes for filtering
  const { data: classesData } = useQuery({
    queryKey: ["teacherClassesList"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 50 }),
  });

  // Load review queue items
  const {
    data: reviewData,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ["teacherReviewQueue", selectedClassId, page],
    queryFn: () =>
      listTeacherReviewQueue({
        classId: selectedClassId || undefined,
        page,
        pageSize: 15,
      }),
  });

  const handleClassChange = (newClassId: string) => {
    setPage(1);
    if (newClassId) {
      setSearchParams({ classId: newClassId });
    } else {
      setSearchParams({});
    }
  };

  const handleOpenOverride = (review: TeacherReviewQueueItemDto) => {
    setSelectedReview(review);
    setIsModalOpen(true);
  };

  const handleOverrideSuccess = () => {
    refetch();
  };

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        {/* Header Breadcrumb & Actions */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 mb-1">
              <Link to="/" className="hover:text-indigo-600">Trang chủ</Link>
              <span>/</span>
              <span className="text-slate-900">Hàng đợi duyệt bài</span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900">
              Hàng Đợi Duyệt Đánh Giá Suy Luận (Review Queue)
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Xem xét các lượt giải bài của học sinh có AI confidence thấp hoặc thuật toán kích hoạt dự phòng (Fallback).
            </p>
          </div>

          <div className="flex items-center gap-3">
            {/* Filter by class */}
            <select
              value={selectedClassId}
              onChange={(e) => handleClassChange(e.target.value)}
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm focus:border-indigo-500 focus:outline-none"
            >
              <option value="">Tất cả các lớp phụ trách</option>
              {classesData?.data.map((c) => (
                <option key={c.classId} value={c.classId}>
                  {c.className}
                </option>
              ))}
            </select>

            <button
              onClick={() => refetch()}
              className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Làm mới
            </button>
          </div>
        </div>

        {/* Content Section */}
        {isLoading && (
          <div className="flex items-center justify-center rounded-2xl bg-white p-12 shadow-sm ring-1 ring-slate-200">
            <span className="font-medium text-indigo-600 animate-pulse">Đang tải danh sách chờ duyệt...</span>
          </div>
        )}

        {isError && (
          <div className="rounded-2xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
            <h2 className="text-lg font-bold text-red-600">Không thể tải hàng đợi duyệt bài</h2>
            <p className="mt-2 text-sm text-slate-500">
              {(error as Error)?.message || "Vui lòng kiểm tra lại quyền truy cập hoặc kết nối mạng."}
            </p>
            <button
              onClick={() => refetch()}
              className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
          </div>
        )}

        {!isLoading && !isError && reviewData && (
          <>
            {reviewData.data.length === 0 ? (
              <div className="rounded-2xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-50 text-2xl text-emerald-600">
                  ✓
                </div>
                <h3 className="mt-4 text-lg font-bold text-slate-900">
                  Hàng đợi hiện đang trống!
                </h3>
                <p className="mt-1 text-sm text-slate-500">
                  Tất cả các bài giải cần can thiệp hoặc có mức độ không chắc chắn cao đã được xử lý hoàn tất.
                </p>
              </div>
            ) : (
              <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-200">
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                    <thead className="bg-slate-50 text-xs font-bold uppercase tracking-wider text-slate-500">
                      <tr>
                        <th className="px-6 py-3.5">Học sinh</th>
                        <th className="px-6 py-3.5">Câu hỏi & Đáp án</th>
                        <th className="px-6 py-3.5">AI Đánh giá</th>
                        <th className="px-6 py-3.5">Thời gian nộp</th>
                        <th className="px-6 py-3.5 text-right">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100 bg-white">
                      {reviewData.data.map((item) => (
                        <tr key={item.attemptId} className="hover:bg-slate-50/80 transition-colors">
                          <td className="px-6 py-4 font-semibold text-slate-900 whitespace-nowrap">
                            <Link
                              to={`/quan-ly/hoc-sinh/${item.studentId}/nang-luc`}
                              className="text-indigo-600 hover:underline"
                            >
                              {item.studentName}
                            </Link>
                          </td>
                          <td className="px-6 py-4 max-w-md">
                            <p className="text-slate-800 line-clamp-1 font-medium">{item.questionText}</p>
                            <p className="mt-1 text-xs text-slate-500">
                              Đáp án: <span className="font-mono font-semibold text-slate-700">{item.finalAnswer || "(Trống)"}</span>
                            </p>
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap">
                            <div className="flex flex-col gap-1">
                              <div className="flex items-center gap-2">
                                <span className="text-xs text-slate-600">
                                  Chất lượng: <strong className="text-slate-900">{item.reasoningQuality ?? "N/A"}%</strong>
                                </span>
                                {item.analysisConfidence !== null && item.analysisConfidence !== undefined && (
                                  <span className="text-xs text-slate-400">
                                    (Tin cậy: {(item.analysisConfidence * 100).toFixed(0)}%)
                                  </span>
                                )}
                              </div>
                              {item.isFallback && (
                                <span className="inline-flex w-fit items-center rounded bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800">
                                  Thuật toán dự phòng (Fallback)
                                </span>
                              )}
                            </div>
                          </td>
                          <td className="px-6 py-4 text-xs text-slate-500 whitespace-nowrap">
                            {new Date(item.submittedAt).toLocaleString("vi-VN")}
                          </td>
                          <td className="px-6 py-4 text-right whitespace-nowrap">
                            <button
                              onClick={() => handleOpenOverride(item)}
                              className="rounded-lg bg-indigo-600 px-3.5 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-indigo-500"
                            >
                              Đánh giá & Điều chỉnh
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Pagination footer */}
                {reviewData.meta.totalPages > 1 && (
                  <div className="flex items-center justify-between border-t border-slate-200 bg-white px-6 py-3">
                    <span className="text-xs text-slate-500">
                      Hiển thị trang {reviewData.meta.page} / {reviewData.meta.totalPages} (Tổng {reviewData.meta.totalItems} lượt chờ duyệt)
                    </span>
                    <div className="flex gap-2">
                      <button
                        onClick={() => setPage((p) => Math.max(1, p - 1))}
                        disabled={page <= 1}
                        className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        Trước
                      </button>
                      <button
                        onClick={() => setPage((p) => Math.min(reviewData.meta.totalPages, p + 1))}
                        disabled={page >= reviewData.meta.totalPages}
                        className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>

      {/* Teacher Override Modal */}
      <TeacherOverrideModal
        review={selectedReview}
        isOpen={isModalOpen}
        onClose={() => {
          setIsModalOpen(false);
          setSelectedReview(null);
        }}
        onSuccess={handleOverrideSuccess}
      />
    </div>
  );
};
