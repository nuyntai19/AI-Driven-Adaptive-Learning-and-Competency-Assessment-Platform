import React, { useState, useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listTeacherReviewQueue } from "../api/teacherReviewsApi";
import { organizationApi } from "../api/organizationApi";
import type { ClassDto } from "../types/organization";
import { TeacherOverrideModal } from "../components/TeacherOverrideModal";
import { ScratchpadAttachmentDrawer } from "../components/ScratchpadAttachmentDrawer";
import type { TeacherReviewQueueItemDto } from "../types/reviews";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { CenterManagerThemeScope } from "../components/centerManager/CenterManagerThemeScope";

export const ReviewQueuePage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedClassId = searchParams.get("classId") || "";
  const [page, setPage] = useState<number>(1);
  const [selectedReview, setSelectedReview] = useState<TeacherReviewQueueItemDto | null>(null);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState<boolean>(false);
  const [isScratchpadOpen, setIsScratchpadOpen] = useState<boolean>(false);

  // Class selector state with server-side search, pagination, and persistent cache
  const [classSearchInput, setClassSearchInput] = useState<string>("");
  const [classSearchTerm, setClassSearchTerm] = useState<string>("");
  const [classPage, setClassPage] = useState<number>(1);
  const [classCache, setClassCache] = useState<Map<string, ClassDto>>(new Map());

  const user = useAuthStore((state) => state.user);
  const isCenterManager = user?.accountType === "CenterManager";
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canOverride = hasPermission(permissions.teacherReviewsOverride);
  const canReadScratchpad = hasPermission(permissions.learningAttemptsReadScoped);

  // Debounce class search input
  useEffect(() => {
    const handler = setTimeout(() => {
      setClassSearchTerm(classSearchInput);
      setClassPage(1);
    }, 300);
    return () => clearTimeout(handler);
  }, [classSearchInput]);

  // Query classes with server-side pagination (pageSize: 20)
  const { data: classesData, isLoading: isLoadingClasses } = useQuery({
    queryKey: ["reviewQueueClasses", classPage],
    queryFn: () =>
      organizationApi.listClasses({
        page: classPage,
        pageSize: 20,
      }),
  });

  // Accumulate loaded classes into the cache to guarantee selected class never disappears
  useEffect(() => {
    if (classesData?.data) {
      setClassCache((prev) => {
        const next = new Map(prev);
        for (const cls of classesData.data) {
          next.set(cls.classId, cls);
        }
        return next;
      });
    }
  }, [classesData]);

  // Query review queue items
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

  // Keep selectedReview synchronized or select first on load
  useEffect(() => {
    if (reviewData?.data && reviewData.data.length > 0) {
      setSelectedReview((current) => {
        if (!current) {
          return reviewData.data[0];
        }
        const updated = reviewData.data.find((item) => item.attemptId === current.attemptId);
        return updated ?? reviewData.data[0];
      });
    } else if (reviewData?.data && reviewData.data.length === 0) {
      setSelectedReview(null);
    }
  }, [reviewData]);

  const handleClassChange = (newClassId: string) => {
    setPage(1);
    setSelectedReview(null);
    if (newClassId) {
      setSearchParams({ classId: newClassId });
    } else {
      setSearchParams({});
    }
  };

  const handleOpenOverride = (review: TeacherReviewQueueItemDto) => {
    setSelectedReview(review);
    setIsOverrideModalOpen(true);
  };

  const handleOverrideSuccess = () => {
    refetch();
  };

  // Combine cached classes for selection options, filtered by classSearchTerm if present
  const classOptions = Array.from(classCache.values()).filter((c) => {
    if (!classSearchTerm.trim()) return true;
    return c.className.toLowerCase().includes(classSearchTerm.trim().toLowerCase());
  });

  const pageContent = (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        {/* Header Breadcrumb & Actions */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 mb-1">
              <Link to="/" className="hover:text-indigo-600">Trang chủ</Link>
              <span>/</span>
              <span className="text-slate-900">Hàng đợi duyệt bài</span>
              {isCenterManager && (
                <span className="ml-2 inline-flex items-center rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-600/20">
                  Phạm vi toàn Trung tâm
                </span>
              )}
            </div>
            <h1 className="text-2xl font-bold text-slate-900">
              Hàng Đợi Duyệt Đánh Giá Suy Luận (Review Queue)
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Đối chiếu 3 nguồn: Bài làm gốc học sinh, AI Observation, và Can thiệp chuyên môn (Deterministic Fallback).
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => refetch()}
              className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50 transition-colors"
            >
              Làm mới danh sách
            </button>
          </div>
        </div>

        {/* Server-side Paginated & Searchable Class Selector */}
        <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
          <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
            <div className="flex-1 w-full md:w-auto">
              <label htmlFor="review-class-select" className="block text-xs font-bold uppercase tracking-wider text-slate-600 mb-1">
                Lọc theo lớp học ({isCenterManager ? "Toàn bộ lớp trung tâm" : "Các lớp phụ trách"})
              </label>
              <div className="flex flex-col sm:flex-row gap-2">
                <input
                  type="text"
                  placeholder="Tìm kiếm tên lớp..."
                  value={classSearchInput}
                  onChange={(e) => setClassSearchInput(e.target.value)}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 min-w-[200px]"
                />
                <select
                  id="review-class-select"
                  value={selectedClassId}
                  onChange={(e) => handleClassChange(e.target.value)}
                  className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 shadow-sm focus:border-indigo-500 focus:outline-none"
                >
                  <option value="">
                    {isCenterManager ? "Tất cả các lớp trong trung tâm" : "Tất cả các lớp phụ trách"}
                  </option>
                  {classOptions.map((c) => (
                    <option key={c.classId} value={c.classId}>
                      {c.className} {c.academicYear ? `(${c.academicYear})` : ""}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {/* Classes Pagination Controls */}
            {classesData && classesData.meta && classesData.meta.totalPages > 1 && (
              <div className="flex items-center gap-2 self-end md:self-center text-xs text-slate-500">
                <span>Lớp trang {classesData.meta.page}/{classesData.meta.totalPages}</span>
                <button
                  type="button"
                  onClick={() => setClassPage((p) => Math.max(1, p - 1))}
                  disabled={classPage <= 1 || isLoadingClasses}
                  className="rounded border border-slate-300 px-2 py-1 text-slate-600 hover:bg-slate-100 disabled:opacity-40"
                >
                  ◀
                </button>
                <button
                  type="button"
                  onClick={() => setClassPage((p) => Math.min(classesData.meta.totalPages, p + 1))}
                  disabled={classPage >= classesData.meta.totalPages || isLoadingClasses}
                  className="rounded border border-slate-300 px-2 py-1 text-slate-600 hover:bg-slate-100 disabled:opacity-40"
                >
                  ▶
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Loading / Error States */}
        {isLoading && (
          <div className="flex items-center justify-center rounded-2xl bg-white p-12 shadow-sm ring-1 ring-slate-200">
            <span className="font-medium text-indigo-600 animate-pulse">Đang tải hàng đợi duyệt bài...</span>
          </div>
        )}

        {isError && (
          <div className="rounded-2xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
            <h2 className="text-lg font-bold text-red-600">Không thể tải hàng đợi duyệt bài</h2>
            <p className="mt-2 text-sm text-slate-500">
              {(error as Error)?.message || "Vui lòng kiểm tra lại quyền truy cập hoặc kết nối mạng."}
            </p>
            <button
              type="button"
              onClick={() => refetch()}
              className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
          </div>
        )}

        {/* Master / Detail Layout */}
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
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
                {/* Left Panel: Master List (5 cols) */}
                <div className="lg:col-span-5 space-y-4">
                  <div className="rounded-xl bg-white shadow-sm ring-1 ring-slate-200 overflow-hidden">
                    <div className="border-b border-slate-200 bg-slate-50 px-4 py-3 flex items-center justify-between">
                      <span className="text-xs font-bold uppercase tracking-wider text-slate-700">
                        Danh sách chờ duyệt ({reviewData.meta.totalItems})
                      </span>
                      <span className="text-xs text-slate-500">
                        Trang {reviewData.meta.page}/{reviewData.meta.totalPages}
                      </span>
                    </div>

                    <div className="divide-y divide-slate-100 max-h-[calc(100vh-280px)] overflow-y-auto">
                      {reviewData.data.map((item) => {
                        const isSelected = selectedReview?.attemptId === item.attemptId;
                        return (
                          <div
                            key={item.attemptId}
                            onClick={() => setSelectedReview(item)}
                            className={`p-4 cursor-pointer transition-colors ${
                              isSelected
                                ? "bg-indigo-50/70 border-l-4 border-indigo-600"
                                : "hover:bg-slate-50 border-l-4 border-transparent"
                            }`}
                          >
                            <div className="flex items-start justify-between gap-2">
                              <div className="font-semibold text-slate-900 text-sm">
                                {item.studentName}
                              </div>
                              <span className="text-[11px] font-mono text-slate-400 shrink-0">
                                #{item.attemptId}
                              </span>
                            </div>

                            <p className="mt-1 text-xs text-slate-600 line-clamp-2 leading-relaxed">
                              {item.questionText}
                            </p>

                            <div className="mt-2 flex flex-wrap items-center gap-2">
                              <span className="rounded bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-700">
                                Chất lượng: <strong className="text-slate-900">{item.reasoningQuality ?? "N/A"}%</strong>
                              </span>

                              {item.isFallback && (
                                <span className="rounded bg-amber-100 px-2 py-0.5 text-[10px] font-bold text-amber-800">
                                  Dự phòng (Fallback)
                                </span>
                              )}

                              <span className="text-[10px] text-slate-400 ml-auto">
                                {new Date(item.submittedAt).toLocaleTimeString("vi-VN", {
                                  hour: "2-digit",
                                  minute: "2-digit",
                                })}
                              </span>
                            </div>
                          </div>
                        );
                      })}
                    </div>

                    {/* Pagination Footer */}
                    {reviewData.meta.totalPages > 1 && (
                      <div className="flex items-center justify-between border-t border-slate-200 bg-white px-4 py-2.5">
                        <button
                          type="button"
                          onClick={() => setPage((p) => Math.max(1, p - 1))}
                          disabled={page <= 1}
                          className="rounded-md border border-slate-300 px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-40"
                        >
                          Trước
                        </button>
                        <span className="text-xs text-slate-500">
                          {page} / {reviewData.meta.totalPages}
                        </span>
                        <button
                          type="button"
                          onClick={() => setPage((p) => Math.min(reviewData.meta.totalPages, p + 1))}
                          disabled={page >= reviewData.meta.totalPages}
                          className="rounded-md border border-slate-300 px-2.5 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-40"
                        >
                          Sau
                        </button>
                      </div>
                    )}
                  </div>
                </div>

                {/* Right Panel: 3-Source Reconciliation Detail (7 cols) */}
                <div className="lg:col-span-7 space-y-4">
                  {selectedReview ? (
                    <div className="space-y-4">
                      {/* Panel 1: Original Student Work & Scratchpad */}
                      <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                        <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-3">
                          <div className="flex items-center gap-2">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-indigo-100 text-xs font-bold text-indigo-700">
                              1
                            </span>
                            <h3 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
                              Bài làm gốc của học sinh
                            </h3>
                          </div>
                          {canReadScratchpad && (
                            <button
                              type="button"
                              onClick={() => setIsScratchpadOpen(true)}
                              className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-50 px-3 py-1.5 text-xs font-bold text-indigo-700 hover:bg-indigo-100 transition-colors"
                            >
                              <span>🎨 Mở nháp vẽ (Scratchpad)</span>
                            </button>
                          )}
                        </div>

                        <div className="space-y-3 text-sm">
                          <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                            <div>
                              Học sinh:{" "}
                              <Link
                                to={`/quan-ly/hoc-sinh/${selectedReview.studentId}/nang-luc?subjectId=${selectedReview.subjectId}`}
                                className="font-bold text-indigo-600 hover:underline"
                              >
                                {selectedReview.studentName}
                              </Link>
                            </div>
                            <span className="text-slate-400">
                              Nộp lúc: {new Date(selectedReview.submittedAt).toLocaleString("vi-VN")}
                            </span>
                          </div>

                          <div>
                            <span className="text-xs font-bold text-slate-500 block mb-1">
                              Đề bài câu hỏi:
                            </span>
                            <div className="rounded-lg bg-slate-50 p-3 text-slate-800 text-sm leading-relaxed border border-slate-200">
                              {selectedReview.questionText}
                            </div>
                          </div>

                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                            <div>
                              <span className="text-xs font-bold text-slate-500 block mb-1">
                                Đáp án học sinh chọn:
                              </span>
                              <div className="rounded-lg bg-slate-100 px-3 py-2 font-mono font-bold text-slate-900 border border-slate-200">
                                {selectedReview.finalAnswer || "(Trống)"}
                              </div>
                            </div>

                            <div>
                              <span className="text-xs font-bold text-slate-500 block mb-1">
                                Trạng thái nháp:
                              </span>
                              <div className="rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600 border border-slate-200 flex items-center justify-between">
                                <span>{selectedReview.reasoningText ? "Có ghi chú lời giải" : "Không có ghi chú"}</span>
                                {canReadScratchpad ? (
                                  <span className="text-emerald-600 font-medium">Đã kích hoạt drawer</span>
                                ) : (
                                  <span className="text-slate-400">Yêu cầu quyền nháp</span>
                                )}
                              </div>
                            </div>
                          </div>

                          {selectedReview.reasoningText && (
                            <div>
                              <span className="text-xs font-bold text-slate-500 block mb-1">
                                Ghi chú các bước giải của học sinh:
                              </span>
                              <p className="rounded-lg bg-slate-50 p-3 font-mono text-xs text-slate-700 whitespace-pre-wrap border border-slate-200">
                                {selectedReview.reasoningText}
                              </p>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Panel 2: AI Observation & Reasoning Analysis */}
                      <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                        <div className="flex items-center gap-2 border-b border-slate-100 pb-3 mb-3">
                          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-purple-100 text-xs font-bold text-purple-700">
                            2
                          </span>
                          <h3 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
                            AI Observation & Phân tích suy luận
                          </h3>
                        </div>

                        <div className="space-y-3 text-sm">
                          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
                            <div className="rounded-lg bg-slate-50 p-2.5 border border-slate-200">
                              <span className="text-[10px] uppercase font-bold text-slate-400 block">Chất lượng</span>
                              <span className="text-lg font-black text-indigo-600">
                                {selectedReview.reasoningQuality ?? "N/A"}%
                              </span>
                            </div>

                            <div className="rounded-lg bg-slate-50 p-2.5 border border-slate-200">
                              <span className="text-[10px] uppercase font-bold text-slate-400 block">Độ tin cậy</span>
                              <span className="text-lg font-black text-slate-700">
                                {selectedReview.analysisConfidence !== null && selectedReview.analysisConfidence !== undefined
                                  ? `${selectedReview.analysisConfidence.toFixed(0)}%`
                                  : "N/A"}
                              </span>
                            </div>

                            <div className="rounded-lg bg-slate-50 p-2.5 border border-slate-200">
                              <span className="text-[10px] uppercase font-bold text-slate-400 block">Phương thức</span>
                              <span className="text-xs font-bold text-slate-800 block truncate mt-1">
                                {selectedReview.evidence?.decisionMode || selectedReview.evidence?.mode || "Rule-based"}
                              </span>
                            </div>

                            <div className="rounded-lg bg-slate-50 p-2.5 border border-slate-200">
                              <span className="text-[10px] uppercase font-bold text-slate-400 block">Mức tin cậy</span>
                              <span className="text-xs font-bold text-slate-800 block truncate mt-1">
                                {selectedReview.evidence?.trustLevel || "Standard"}
                              </span>
                            </div>
                          </div>

                          {selectedReview.isFallback && (
                            <div className="rounded-lg bg-amber-50 p-3 text-xs text-amber-800 border border-amber-200 flex items-start gap-2">
                              <span className="text-base leading-none">⚠️</span>
                              <div>
                                <strong>Kích hoạt thuật toán dự phòng (Fallback): </strong>
                                Lượt làm này được đánh giá dựa trên luật xác định do AI confidence thấp hoặc không chắc chắn về các bước suy luận.
                              </div>
                            </div>
                          )}

                          {selectedReview.evidence?.reasonCodes && selectedReview.evidence.reasonCodes.length > 0 && (
                            <div>
                              <span className="text-xs font-bold text-slate-500 block mb-1">
                                Mã lý do phát hiện (Reason Codes):
                              </span>
                              <div className="flex flex-wrap gap-1.5">
                                {selectedReview.evidence.reasonCodes.map((code) => (
                                  <span
                                    key={code}
                                    className="rounded bg-slate-100 px-2 py-0.5 font-mono text-[11px] font-semibold text-slate-700 border border-slate-200"
                                  >
                                    {code}
                                  </span>
                                ))}
                              </div>
                            </div>
                          )}

                          {selectedReview.analysisFeedback && (
                            <div>
                              <span className="text-xs font-bold text-slate-500 block mb-1">
                                Nhận xét tự động của AI:
                              </span>
                              <p className="rounded-lg bg-slate-50 p-3 text-xs text-slate-700 border border-slate-200 leading-relaxed">
                                {selectedReview.analysisFeedback}
                              </p>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Panel 3: Deterministic Fallback & Human Governance Panel */}
                      <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                        <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-3">
                          <div className="flex items-center gap-2">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-100 text-xs font-bold text-emerald-700">
                              3
                            </span>
                            <h3 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
                              Can thiệp & Điều chỉnh chuyên môn
                            </h3>
                          </div>

                          <div className="text-xs font-mono font-medium text-slate-500">
                            Phiên bản OCC:{" "}
                            <span className="font-bold text-slate-800">
                              {selectedReview.evidence?.analysisOverrideVersion ?? 0}
                            </span>
                          </div>
                        </div>

                        <div className="space-y-3">
                          <p className="text-xs text-slate-600 leading-relaxed">
                            Quyền điều chỉnh chuyên môn cho phép cập nhật tính đúng/sai, thang đo suy luận và nhận xét trực tiếp vào Hồ sơ Năng lực (Digital Twin) của học sinh với đầy đủ dấu vết kiểm toán.
                          </p>

                          <div className="flex items-center justify-between pt-2">
                            <span className="text-xs text-slate-500">
                              Mã phân tích: <strong className="font-mono text-slate-700">{selectedReview.analysisId}</strong>
                            </span>

                            {canOverride ? (
                              <button
                                type="button"
                                onClick={() => handleOpenOverride(selectedReview)}
                                className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-500 transition-colors"
                              >
                                Đánh giá & Điều chỉnh (Override)
                              </button>
                            ) : (
                              <span className="rounded-lg bg-slate-100 px-3 py-1.5 text-xs text-slate-500 font-medium">
                                Chế độ chỉ xem (Thiếu quyền can thiệp)
                              </span>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="rounded-xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200 text-slate-400">
                      Chọn một bài làm từ danh sách bên trái để đối chiếu 3 nguồn dữ liệu.
                    </div>
                  )}
                </div>
              </div>
            )}
          </>
        )}
      </div>

      {/* Scratchpad Attachment Drawer */}
      <ScratchpadAttachmentDrawer
        attemptId={selectedReview?.attemptId ?? null}
        isOpen={isScratchpadOpen}
        onClose={() => setIsScratchpadOpen(false)}
        studentName={selectedReview?.studentName}
        questionText={selectedReview?.questionText}
      />

      {/* Teacher Override Modal */}
      {canOverride && (
        <TeacherOverrideModal
          review={selectedReview}
          isOpen={isOverrideModalOpen}
          onClose={() => {
            setIsOverrideModalOpen(false);
          }}
          onSuccess={handleOverrideSuccess}
        />
      )}
    </div>
  );

  if (isCenterManager) {
    return <CenterManagerThemeScope data-actor="center-manager">{pageContent}</CenterManagerThemeScope>;
  }

  return pageContent;
};
