import React, { useState, useEffect, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listTeacherReviewQueue } from "../api/teacherReviewsApi";
import { organizationApi } from "../api/organizationApi";
import type { ClassDto } from "../types/organization";
import { TeacherOverrideModal } from "../components/TeacherOverrideModal";
import { ScratchpadAttachmentDrawer } from "../components/ScratchpadAttachmentDrawer";
import { RichMathText } from "../components/math/RichMathText";
import type { TeacherReviewQueueItemDto } from "../types/reviews";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { CenterManagerThemeScope } from "../components/centerManager/CenterManagerThemeScope";
import {
  resolveReviewQueueViewMode,
  isValidOccVersion,
  formatOccVersionLabel,
  executeOccRefetchWrapper,
  reconcileAttemptOccVersion,
} from "../utils/reviewQueueHelpers";

// ============================================================================
// 1. CENTER MANAGER MODERN VIEW (MASTER/DETAIL + 3-SOURCE RECONCILIATION)
// ============================================================================
const CenterManagerReviewQueueView: React.FC = () => {
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

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canOverride = hasPermission(permissions.teacherReviewsOverride);
  const canReadScratchpad = hasPermission(permissions.learningAttemptsReadScoped);

  const [notification, setNotification] = useState<string | null>(null);

  // Debounce class search input
  useEffect(() => {
    const handler = setTimeout(() => {
      setClassSearchTerm(classSearchInput);
      setClassPage(1);
    }, 300);
    return () => clearTimeout(handler);
  }, [classSearchInput]);

  // Query classes with true server-side pagination (pageSize: 20) and search
  const { data: classesData, isLoading: isLoadingClasses } = useQuery({
    queryKey: ["reviewQueueClasses", classSearchTerm, classPage],
    queryFn: () =>
      organizationApi.listClasses({
        page: classPage,
        pageSize: 20,
        search: classSearchTerm.trim() || undefined,
      }),
  });

  // Accumulate loaded classes into the cache to guarantee selected class never disappears across pages or queries
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

  // Query review queue items for the center
  const {
    data: reviewData,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ["centerManagerReviewQueue", selectedClassId, page],
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

  const handleReviewRefetch = async (): Promise<number> => {
    const res = await executeOccRefetchWrapper(() => refetch({ throwOnError: true }));
    try {
      const { updatedItem, freshVersion } = reconcileAttemptOccVersion(
        selectedReview?.attemptId,
        res?.data
      );
      setSelectedReview(updatedItem);
      return freshVersion;
    } catch (err) {
      setSelectedReview(null);
      setIsOverrideModalOpen(false);
      const msg = err instanceof Error ? err.message : "Lượt làm này không còn trong hàng đợi.";
      setNotification(msg);
      throw err;
    }
  };

  // Combine current query results with selected cached class if outside current page
  const classOptions = useMemo(() => {
    const list = classesData?.data ? [...classesData.data] : [];
    if (selectedClassId && !list.some((c) => c.classId === selectedClassId)) {
      const cached = classCache.get(selectedClassId);
      if (cached) {
        list.unshift(cached);
      }
    }
    return list;
  }, [classesData, selectedClassId, classCache]);

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        <div className="mx-auto max-w-[96rem] space-y-6">
          {/* Header Breadcrumb & Actions */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-[var(--cm-border-subtle)] pb-4">
            <div>
              <div className="flex items-center gap-2 text-xs font-semibold text-[var(--cm-text-secondary)] mb-1">
                <Link to="/quan-ly/tong-quan-trung-tam" className="hover:text-cyan-400">Trang chủ</Link>
                <span>/</span>
                <span className="text-[var(--cm-text)]">Hàng đợi duyệt bài</span>
                <span className="ml-2 inline-flex items-center rounded-full bg-indigo-500/10 border border-indigo-500/20 px-2.5 py-0.5 text-xs font-medium text-indigo-400">
                  Phạm vi toàn Trung tâm
                </span>
              </div>
              <h1 className="text-2xl font-bold text-[var(--cm-text)]">
                Hàng Đợi Duyệt Đánh Giá Suy Luận (Review Queue)
              </h1>
              <p className="mt-1 text-sm text-[var(--cm-text-secondary)]">
                Đối chiếu 3 nguồn: Bài làm gốc học sinh, AI Observation, và Can thiệp chuyên môn (Deterministic Fallback).
              </p>
            </div>

            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={() => refetch()}
                className="cm-secondary-button"
              >
                Làm mới danh sách
              </button>
            </div>
          </div>

          {/* Notification Banner */}
          {notification && (
            <div
              role="alert"
              className="cm-surface flex items-center justify-between rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm font-medium text-amber-300 shadow-sm"
            >
              <div className="flex items-center gap-2">
                <span className="text-base">⚠️</span>
                <span>{notification}</span>
              </div>
              <button
                type="button"
                onClick={() => setNotification(null)}
                className="rounded p-1 text-amber-300 hover:bg-amber-500/20"
                aria-label="Đóng thông báo"
              >
                ✕
              </button>
            </div>
          )}

          {/* Server-side Paginated & Searchable Class Selector */}
          <div className="cm-surface p-4">
            <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
              <div className="flex-1 w-full md:w-auto">
                <label htmlFor="review-class-select" className="block text-xs font-bold uppercase tracking-wider text-[var(--cm-text-secondary)] mb-1">
                  Lọc theo lớp học (Toàn bộ lớp trung tâm)
                </label>
                <div className="flex flex-col sm:flex-row gap-2">
                  <input
                    type="text"
                    placeholder="Tìm kiếm tên lớp (máy chủ)..."
                    value={classSearchInput}
                    onChange={(e) => setClassSearchInput(e.target.value)}
                    className="cm-input min-w-[200px]"
                  />
                  <select
                    id="review-class-select"
                    value={selectedClassId}
                    onChange={(e) => handleClassChange(e.target.value)}
                    className="cm-select flex-1"
                  >
                    <option value="">Tất cả các lớp trong trung tâm</option>
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
                <div className="flex items-center gap-2 self-end md:self-center text-xs text-[var(--cm-text-muted)]">
                  <span>Lớp trang {classesData.meta.page}/{classesData.meta.totalPages}</span>
                  <button
                    type="button"
                    onClick={() => setClassPage((p) => Math.max(1, p - 1))}
                    disabled={classPage <= 1 || isLoadingClasses}
                    className="cm-secondary-button text-xs py-1 px-2.5 min-h-0"
                  >
                    Trước
                  </button>
                  <button
                    type="button"
                    onClick={() => setClassPage((p) => Math.min(classesData.meta.totalPages, p + 1))}
                    disabled={classPage >= classesData.meta.totalPages || isLoadingClasses}
                    className="cm-secondary-button text-xs py-1 px-2.5 min-h-0"
                  >
                    Sau
                  </button>
                </div>
              )}
            </div>
          </div>

          {/* Loading / Error States */}
          {isLoading && (
            <div className="cm-surface flex items-center justify-center p-12">
              <span className="font-medium text-cyan-400 animate-pulse">
                Đang tải danh sách bài chờ duyệt...
              </span>
            </div>
          )}

          {isError && (
            <div className="cm-surface p-8 text-center border-rose-500/30">
              <h2 className="text-lg font-bold text-rose-400">Không thể tải danh sách duyệt bài</h2>
              <p className="mt-2 text-sm text-[var(--cm-text-secondary)]">
                {(error as Error)?.message || "Vui lòng kiểm tra lại quyền hạn hoặc kết nối mạng."}
              </p>
              <button
                type="button"
                onClick={() => refetch()}
                className="cm-primary-button mt-4"
              >
                Thử lại
              </button>
            </div>
          )}

          {/* Empty State */}
          {!isLoading && !isError && reviewData && reviewData.data.length === 0 && (
            <div className="cm-surface p-12 text-center">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-500/10 border border-emerald-500/20 text-2xl text-emerald-400">
                ✓
              </div>
              <h3 className="mt-4 text-lg font-bold text-[var(--cm-text)]">
                Hàng đợi duyệt bài hiện đang trống!
              </h3>
              <p className="mt-1 text-sm text-[var(--cm-text-muted)] max-w-md mx-auto">
                Không có bài nộp nào yêu cầu can thiệp hoặc có mức độ không chắc chắn cao trong phạm vi đã chọn.
              </p>
            </div>
          )}

          {/* Master / Detail Grid Layout (Ảnh 13 Inspiration) */}
          {!isLoading && !isError && reviewData && reviewData.data.length > 0 && (
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
              {/* MASTER PANEL (Left Column - List of Items) */}
              <div className="lg:col-span-5 cm-surface overflow-hidden flex flex-col">
                <div className="p-4 border-b border-[var(--cm-border-subtle)] flex items-center justify-between bg-[var(--cm-surface-muted)]">
                  <h2 className="text-sm font-bold uppercase tracking-wider text-[var(--cm-text)]">
                    Danh sách bài chờ duyệt ({reviewData.meta.totalItems})
                  </h2>
                  <span className="text-xs text-[var(--cm-text-muted)]">
                    Trang {reviewData.meta.page}/{reviewData.meta.totalPages}
                  </span>
                </div>

                <div className="divide-y divide-[var(--cm-border-subtle)] max-h-[680px] overflow-y-auto">
                  {reviewData.data.map((item) => {
                    const isSelected = selectedReview?.attemptId === item.attemptId;
                    return (
                      <div
                        key={item.attemptId}
                        onClick={() => setSelectedReview(item)}
                        className={`p-4 cursor-pointer transition-all ${
                          isSelected
                            ? "bg-indigo-950/40 border-l-4 border-cyan-400"
                            : "hover:bg-[var(--cm-surface-muted)]/60 border-l-4 border-transparent"
                        }`}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <span className="text-sm font-bold text-[var(--cm-text)]">{item.studentName}</span>
                            <span className="ml-2 text-xs text-[var(--cm-text-muted)]">#{item.attemptId.slice(0, 8)}</span>
                          </div>
                          <span className="text-[11px] text-[var(--cm-text-muted)] whitespace-nowrap">
                            {new Date(item.submittedAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}
                          </span>
                        </div>

                        <p className="mt-1 text-xs text-[var(--cm-text-secondary)] line-clamp-1">
                          <RichMathText text={item.questionText} />
                        </p>

                        <div className="mt-2.5 flex items-center gap-2 flex-wrap">
                          <span className={`inline-flex items-center rounded px-2 py-0.5 text-[11px] font-bold ${
                            (item.reasoningQuality ?? 0) >= 80
                              ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                              : (item.reasoningQuality ?? 0) >= 50
                              ? "bg-amber-500/10 text-amber-300 border border-amber-500/20"
                              : "bg-rose-500/10 text-rose-300 border border-rose-500/20"
                          }`}>
                            Chất lượng: {item.reasoningQuality ?? "N/A"}%
                          </span>

                          {item.analysisConfidence !== null && item.analysisConfidence !== undefined && (
                            <span className="text-[11px] text-[var(--cm-text-muted)]">
                              Tin cậy: {item.analysisConfidence.toFixed(0)}%
                            </span>
                          )}

                          {item.isFallback && (
                            <span className="inline-flex items-center rounded bg-amber-500/10 border border-amber-500/20 px-1.5 py-0.5 text-[10px] font-bold text-amber-300">
                              Fallback
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>

                {/* Left Panel Pagination Footer */}
                {reviewData.meta.totalPages > 1 && (
                  <div className="p-3 border-t border-[var(--cm-border-subtle)] bg-[var(--cm-surface-muted)] flex items-center justify-between text-xs text-[var(--cm-text-secondary)]">
                    <span>Trang {reviewData.meta.page}/{reviewData.meta.totalPages}</span>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => setPage((p) => Math.max(1, p - 1))}
                        disabled={page <= 1}
                        className="cm-secondary-button text-xs py-1 px-2.5 min-h-0"
                      >
                        Trước
                      </button>
                      <button
                        type="button"
                        onClick={() => setPage((p) => Math.min(reviewData.meta.totalPages, p + 1))}
                        disabled={page >= reviewData.meta.totalPages}
                        className="cm-secondary-button text-xs py-1 px-2.5 min-h-0"
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* DETAIL PANEL (Right Column - 3-Source Reconciliation) */}
              <div className="lg:col-span-7 space-y-6">
                {selectedReview ? (
                  <>
                    {/* Header of Detail Card */}
                    <div className="cm-surface p-5">
                      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-[var(--cm-border-subtle)] pb-3">
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="text-lg font-bold text-[var(--cm-text)]">{selectedReview.studentName}</span>
                            <Link
                              to={`/quan-ly/hoc-sinh/${selectedReview.studentId}/nang-luc?subjectId=${selectedReview.subjectId}`}
                              className="text-xs font-semibold text-cyan-400 hover:underline"
                            >
                              Hồ sơ năng lực (Twin)
                            </Link>
                          </div>
                          <span className="text-xs text-[var(--cm-text-muted)]">
                            Lượt làm #{selectedReview.attemptId} · Nộp lúc: {new Date(selectedReview.submittedAt).toLocaleString("vi-VN")}
                          </span>
                        </div>

                        <div className="flex items-center gap-2">
                          {canOverride && (
                            <button
                              type="button"
                              onClick={() => handleOpenOverride(selectedReview)}
                              className="cm-primary-button text-xs py-2 px-4"
                            >
                              Điều chỉnh điểm số (Override)
                            </button>
                          )}
                        </div>
                      </div>

                      {/* Question Content */}
                      <div className="mt-4">
                        <span className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)] block mb-1">
                          Nội dung câu hỏi
                        </span>
                        <div className="rounded-lg bg-[var(--cm-surface-raised)] p-3.5 text-sm text-[var(--cm-text)] border border-[var(--cm-border-subtle)] leading-relaxed font-medium">
                          <RichMathText text={selectedReview.questionText} />
                        </div>
                      </div>
                    </div>

                    {/* 3-SOURCE RECONCILIATION CARDS */}
                    <div className="space-y-4">
                      {/* SOURCE 1: Original Student Work & Reasoning */}
                      <div className="cm-surface p-5">
                        <div className="flex items-center justify-between mb-3 border-b border-[var(--cm-border-subtle)] pb-2">
                          <div className="flex items-center gap-2">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-cyan-500/20 border border-cyan-500/30 text-xs font-bold text-cyan-300">1</span>
                            <h3 className="text-sm font-bold text-[var(--cm-text)]">Bài làm gốc của học sinh (Student Submission)</h3>
                          </div>

                          {canReadScratchpad && (
                            <button
                              type="button"
                              onClick={() => setIsScratchpadOpen(true)}
                              className="cm-secondary-button text-xs py-1 px-3 min-h-0"
                            >
                              <span>✏️</span> Xem tệp nháp vẽ (Scratchpad)
                            </button>
                          )}
                        </div>

                        <div className="space-y-3 text-sm">
                          <div>
                            <span className="text-xs font-bold text-[var(--cm-text-muted)]">Đáp án nộp cuối cùng:</span>
                            <p className="mt-0.5 font-mono text-sm font-bold text-[var(--cm-text)] bg-[var(--cm-surface-raised)] p-2.5 rounded border border-[var(--cm-border-subtle)]">
                              {selectedReview.finalAnswer || "(Học sinh không nhập đáp án chữ)"}
                            </p>
                          </div>

                          <div>
                            <span className="text-xs font-bold text-[var(--cm-text-muted)]">Các bước suy luận (Reasoning Steps):</span>
                            {selectedReview.reasoningText ? (
                              <pre className="mt-1 whitespace-pre-wrap font-mono text-xs text-[var(--cm-text)] bg-[var(--cm-surface-raised)] p-3 rounded border border-[var(--cm-border-subtle)] max-h-48 overflow-y-auto">
                                {selectedReview.reasoningText}
                              </pre>
                            ) : (
                              <p className="mt-1 text-xs italic text-[var(--cm-text-muted)] bg-[var(--cm-surface-raised)] p-3 rounded border border-[var(--cm-border-subtle)]">
                                Không có bước giải thích suy luận chi tiết đi kèm.
                              </p>
                            )}
                          </div>
                        </div>
                      </div>

                      {/* SOURCE 2: AI Observation & Evaluation */}
                      <div className="cm-surface p-5">
                        <div className="flex items-center gap-2 mb-3 border-b border-[var(--cm-border-subtle)] pb-2">
                          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-purple-500/20 border border-purple-500/30 text-xs font-bold text-purple-300">2</span>
                          <h3 className="text-sm font-bold text-[var(--cm-text)]">
                            AI Observation & Phân tích suy luận
                          </h3>
                        </div>

                        <div className="space-y-3 text-sm">
                          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
                            <div className="rounded-lg bg-[var(--cm-surface-raised)] p-2.5 border border-[var(--cm-border-subtle)]">
                              <span className="text-[10px] uppercase font-bold text-[var(--cm-text-muted)] block">Chất lượng</span>
                              <span className="text-lg font-black text-cyan-400">
                                {selectedReview.reasoningQuality ?? "N/A"}%
                              </span>
                            </div>

                            <div className="rounded-lg bg-[var(--cm-surface-raised)] p-2.5 border border-[var(--cm-border-subtle)]">
                              <span className="text-[10px] uppercase font-bold text-[var(--cm-text-muted)] block">Độ tin cậy</span>
                              <span className="text-lg font-black text-[var(--cm-text)]">
                                {selectedReview.analysisConfidence !== null && selectedReview.analysisConfidence !== undefined
                                  ? `${selectedReview.analysisConfidence.toFixed(0)}%`
                                  : "N/A"}
                              </span>
                            </div>

                            <div className="rounded-lg bg-[var(--cm-surface-raised)] p-2.5 border border-[var(--cm-border-subtle)]">
                              <span className="text-[10px] uppercase font-bold text-[var(--cm-text-muted)] block">Phương thức</span>
                              <span className="text-xs font-bold text-[var(--cm-text)] block truncate mt-1">
                                {selectedReview.evidence?.decisionMode || "Chưa xác định"}
                              </span>
                            </div>

                            <div className="rounded-lg bg-[var(--cm-surface-raised)] p-2.5 border border-[var(--cm-border-subtle)]">
                              <span className="text-[10px] uppercase font-bold text-[var(--cm-text-muted)] block">Mức tin cậy</span>
                              <span className="text-xs font-bold text-[var(--cm-text)] block truncate mt-1">
                                {selectedReview.evidence?.trustLevel || "Chưa xác định"}
                              </span>
                            </div>
                          </div>

                          {selectedReview.isFallback && (
                            <div className="rounded-lg bg-amber-500/10 p-3 text-xs text-amber-300 border border-amber-500/20 flex items-start gap-2">
                              <span className="text-base leading-none">⚠️</span>
                              <div>
                                <span className="font-bold">Kích hoạt thuật toán dự phòng (Rule Fallback):</span> AI có độ tự tin thấp hoặc bước giải có dấu hiệu bất thường, hệ thống đã chuyển sang đối chiếu quy tắc xác định.
                              </div>
                            </div>
                          )}

                          {selectedReview.analysisFeedback && (
                            <div>
                              <span className="text-xs font-bold text-[var(--cm-text-muted)]">Phản hồi từ AI Engine:</span>
                              <p className="mt-1 rounded bg-[var(--cm-surface-raised)] p-3 text-xs text-[var(--cm-text)] border border-[var(--cm-border-subtle)]">
                                {selectedReview.analysisFeedback}
                              </p>
                            </div>
                          )}

                          {selectedReview.evidence?.reasonCodes && selectedReview.evidence.reasonCodes.length > 0 && (
                            <div>
                              <span className="text-xs font-bold text-[var(--cm-text-muted)]">Mã lý do đánh giá (Reason Codes):</span>
                              <div className="mt-1 flex flex-wrap gap-1.5">
                                {selectedReview.evidence.reasonCodes.map((rc, idx) => (
                                  <span key={idx} className="rounded bg-[var(--cm-surface-muted)] px-2 py-0.5 font-mono text-[11px] text-[var(--cm-text)] border border-[var(--cm-border-subtle)]">
                                    {rc}
                                  </span>
                                ))}
                              </div>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* SOURCE 3: Deterministic Fallback & Intervention Action */}
                      <div className="cm-surface p-5">
                        <div className="flex items-center justify-between mb-3 border-b border-[var(--cm-border-subtle)] pb-2">
                          <div className="flex items-center gap-2">
                            <span className="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-500/20 border border-emerald-500/30 text-xs font-bold text-emerald-300">3</span>
                            <h3 className="text-sm font-bold text-[var(--cm-text)]">Can thiệp chuyên môn & Quyết định cuối</h3>
                          </div>
                          <span className="text-xs font-semibold text-[var(--cm-text-muted)]">
                            Phiên bản OCC: {formatOccVersionLabel(selectedReview.evidence?.analysisOverrideVersion)}
                          </span>
                        </div>

                        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-sm">
                          <p className="text-xs text-[var(--cm-text-secondary)]">
                            Quản lý trung tâm có thể đánh giá lại chất lượng tư duy, sửa đổi kết quả và cập nhật Digital Twin của học sinh có lưu vết kiểm toán.
                          </p>

                          {canOverride && (
                            <button
                              type="button"
                              disabled={!isValidOccVersion(selectedReview.evidence?.analysisOverrideVersion)}
                              onClick={() => handleOpenOverride(selectedReview)}
                              title={!isValidOccVersion(selectedReview.evidence?.analysisOverrideVersion) ? "Phiên bản OCC không khả dụng, không thể can thiệp" : undefined}
                              className={`cm-primary-button text-xs py-2 px-4 whitespace-nowrap ${
                                !isValidOccVersion(selectedReview.evidence?.analysisOverrideVersion)
                                  ? "opacity-50 cursor-not-allowed"
                                  : ""
                              }`}
                            >
                              Ghi đè điểm số ngay
                            </button>
                          )}
                        </div>
                      </div>
                    </div>
                  </>
                ) : (
                  <div className="cm-surface p-12 text-center">
                    <p className="text-sm font-medium text-[var(--cm-text-muted)]">Chọn một bài làm từ cột bên trái để xem đối chiếu chi tiết 3 nguồn.</p>
                  </div>
                )}
              </div>
            </div>
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
            onClose={() => setIsOverrideModalOpen(false)}
            onSuccess={handleOverrideSuccess}
            onRefetch={handleReviewRefetch}
          />
        )}
      </div>
    </CenterManagerThemeScope>
  );
};

// ============================================================================
// 2. TEACHER LEGACY VIEW (100% PRESERVED LEGACY LAYOUT BEFORE GATE 7)
// ============================================================================
const TeacherReviewQueueLegacyView: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedClassId = searchParams.get("classId") || "";
  const [page, setPage] = useState<number>(1);
  const [selectedReview, setSelectedReview] = useState<TeacherReviewQueueItemDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);
  const [notification, setNotification] = useState<string | null>(null);
  const canOverride = useAuthStore((state) => state.hasPermission)(permissions.teacherReviewsOverride);

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

  const handleLegacyRefetch = async (): Promise<number> => {
    const res = await executeOccRefetchWrapper(() => refetch({ throwOnError: true }));
    try {
      const { updatedItem, freshVersion } = reconcileAttemptOccVersion(
        selectedReview?.attemptId,
        res?.data
      );
      setSelectedReview(updatedItem);
      return freshVersion;
    } catch (err) {
      setSelectedReview(null);
      setIsModalOpen(false);
      const msg = err instanceof Error ? err.message : "Lượt làm này không còn trong hàng đợi.";
      setNotification(msg);
      throw err;
    }
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

        {/* Notification Banner */}
        {notification && (
          <div
            role="alert"
            className="flex items-center justify-between rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm font-medium text-amber-900 shadow-sm"
          >
            <div className="flex items-center gap-2">
              <span className="text-base">⚠️</span>
              <span>{notification}</span>
            </div>
            <button
              type="button"
              onClick={() => setNotification(null)}
              className="rounded p-1 text-amber-700 hover:bg-amber-100"
              aria-label="Đóng thông báo"
            >
              ✕
            </button>
          </div>
        )}

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
                              to={`/quan-ly/hoc-sinh/${item.studentId}/nang-luc?subjectId=${item.subjectId}`}
                              className="text-indigo-600 hover:underline"
                            >
                              {item.studentName}
                            </Link>
                          </td>
                          <td className="px-6 py-4 max-w-md">
                            <p className="text-slate-800 line-clamp-1 font-medium">
                              <RichMathText text={item.questionText} />
                            </p>
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
                                    (Tin cậy: {item.analysisConfidence.toFixed(0)}%)
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
                            {canOverride ? (
                              <button
                                onClick={() => handleOpenOverride(item)}
                                className="rounded-lg bg-indigo-600 px-3.5 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-indigo-500"
                              >
                                Đánh giá & Điều chỉnh
                              </button>
                            ) : (
                              <span className="text-xs text-slate-400">Chỉ xem</span>
                            )}
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
      {canOverride && (
        <TeacherOverrideModal
          review={selectedReview}
          isOpen={isModalOpen}
          onClose={() => {
            setIsModalOpen(false);
            setSelectedReview(null);
          }}
          onSuccess={handleOverrideSuccess}
          onRefetch={handleLegacyRefetch}
        />
      )}
    </div>
  );
};

// ============================================================================
// 3. CANONICAL EXPORT WITH STRICT ACTOR ISOLATION
// ============================================================================
export const ReviewQueuePage: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  const mode = resolveReviewQueueViewMode(user?.accountType);
  return mode === "CenterManager" ? (
    <CenterManagerReviewQueueView />
  ) : (
    <TeacherReviewQueueLegacyView />
  );
};
