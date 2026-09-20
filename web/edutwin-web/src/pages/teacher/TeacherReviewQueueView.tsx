import React, { useState, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  listTeacherReviewQueue,
  overrideReasoningAnalysis,
} from "../../api/teacherReviewsApi";
import { organizationApi } from "../../api/organizationApi";
import type {
  TeacherReviewQueueItemDto,
  ErrorType,
  TeacherOverrideRequest,
  TeacherOverrideResponse,
} from "../../types/reviews";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import { RichMathText } from "../../components/math/RichMathText";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";

export const TeacherReviewQueueView: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedClassId = searchParams.get("classId") || "";
  const queryClient = useQueryClient();

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canOverride = hasPermission(permissions.twinReasoningOverride);

  const [selectedItem, setSelectedItem] = useState<TeacherReviewQueueItemDto | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [replayResult, setReplayResult] = useState<TeacherOverrideResponse["data"] | null>(null);
  const [overrideFeedbackMessage, setOverrideFeedbackMessage] = useState<string | null>(null);

  // Override Form State
  const [reasoningQuality, setReasoningQuality] = useState<number>(80);
  const [errorType, setErrorType] = useState<ErrorType>("None");
  const [feedback, setFeedback] = useState<string>("");
  const [isCorrect, setIsCorrect] = useState<boolean>(true);
  const [awardedScore, setAwardedScore] = useState<number>(10);
  const [overrideReason, setOverrideReason] = useState<string>("");

  // Classes list for filter
  const { data: classesData } = useQuery({
    queryKey: ["teacherClassesList"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 50 }),
  });

  // Review Queue Query
  const {
    data: queueData,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ["teacherReviewQueue", selectedClassId],
    queryFn: () => listTeacherReviewQueue({ classId: selectedClassId || undefined, pageSize: 50 }),
  });

  const queueItems = queueData?.data ?? [];

  // Filter items by search query
  const filteredItems = useMemo(() => {
    if (!searchQuery.trim()) return queueItems;
    const q = searchQuery.toLowerCase();
    return queueItems.filter(
      (item) =>
        item.studentName.toLowerCase().includes(q) ||
        item.questionText.toLowerCase().includes(q) ||
        item.analysisFeedback?.toLowerCase().includes(q)
    );
  }, [queueItems, searchQuery]);

  // Sync selected item when list loads or changes
  React.useEffect(() => {
    if (filteredItems.length > 0 && (!selectedItem || !filteredItems.some((i) => i.analysisId === selectedItem.analysisId))) {
      setSelectedItem(filteredItems[0]);
      resetOverrideForm(filteredItems[0]);
    } else if (filteredItems.length === 0) {
      setSelectedItem(null);
    }
  }, [filteredItems]);

  const resetOverrideForm = (item: TeacherReviewQueueItemDto) => {
    setReasoningQuality(Math.round((item.reasoningQuality ?? 0.8) * 100));
    setFeedback(item.analysisFeedback ?? "");
    setIsCorrect(!item.isFallback);
    setAwardedScore(item.isFallback ? 5 : 10);
    setErrorType(item.isFallback ? "Reasoning" : "None");
    setOverrideReason("");
    setReplayResult(null);
    setOverrideFeedbackMessage(null);
  };

  const handleSelectItem = (item: TeacherReviewQueueItemDto) => {
    setSelectedItem(item);
    resetOverrideForm(item);
  };

  // Override Mutation
  const overrideMutation = useMutation({
    mutationFn: async (payload: TeacherOverrideRequest) => {
      if (!selectedItem) throw new Error("Chưa chọn bài làm cần chấm");
      return await overrideReasoningAnalysis(selectedItem.analysisId, payload);
    },
    onSuccess: (data) => {
      setReplayResult(data.data);
      setOverrideFeedbackMessage("Đã lưu kết quả chấm đè và tính toán lại Digital Twin thành công!");
      queryClient.invalidateQueries({ queryKey: ["teacherReviewQueue"] });
    },
    onError: (err: any) => {
      setOverrideFeedbackMessage(`Lỗi khi ghi đè kết quả: ${err.message || "Thao tác thất bại"}`);
    },
  });

  const handleSubmitOverride = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    setOverrideFeedbackMessage(null);

    const scoreNum = Number(awardedScore);
    if (isNaN(scoreNum) || scoreNum < 0 || scoreNum > 10) {
      setOverrideFeedbackMessage("Điểm số công nhận không hợp lệ. Vui lòng nhập số trong khoảng từ 0.0 đến 10.0.");
      return;
    }

    if (!overrideReason.trim() || overrideReason.trim().length < 3) {
      setOverrideFeedbackMessage("Vui lòng nhập lý do điều chỉnh kết quả chấm (tối thiểu 3 ký tự để lưu vết nhật ký hệ thống).");
      return;
    }

    if (overrideReason.trim().length > 500) {
      setOverrideFeedbackMessage("Lý do điều chỉnh không được vượt quá 500 ký tự.");
      return;
    }

    if (feedback.trim().length > 1000) {
      setOverrideFeedbackMessage("Nhận xét cho học sinh không được vượt quá 1000 ký tự.");
      return;
    }

    const payload: TeacherOverrideRequest = {
      reasoningQuality: Math.max(0, Math.min(100, reasoningQuality)) / 100,
      errorType,
      feedback: feedback.trim(),
      isCorrect,
      awardedScore: scoreNum,
      reason: overrideReason.trim(),
      overrideVersion: selectedItem.evidence?.analysisOverrideVersion ?? 1,
    };

    overrideMutation.mutate(payload);
  };

  // Calculate metrics
  const totalInQueue = queueItems.length;
  const fallbackCount = queueItems.filter((i) => i.isFallback).length;
  const avgConfidence = totalInQueue > 0
    ? Math.round(
        (queueItems.reduce((acc, curr) => acc + (curr.analysisConfidence ?? 0), 0) / totalInQueue) * 100
      )
    : 0;

  return (
    <div className="th-page-container">
      <TeacherPageHeader
        title="Hàng Đợi Chấm Bài & Đánh Giá AI"
        subtitle="Rà soát các bài làm học sinh cần can thiệp sư phạm, duyệt giải trình AI và điều chỉnh điểm số Digital Twin"
        actions={
          <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
            <select
              className="th-select"
              value={selectedClassId}
              onChange={(e) => {
                const val = e.target.value;
                if (val) {
                  setSearchParams({ classId: val });
                } else {
                  setSearchParams({});
                }
              }}
              style={{ minWidth: "180px" }}
            >
              <option value="">Tất cả lớp phụ trách</option>
              {classesData?.data?.map((cls) => (
                <option key={cls.classId} value={cls.classId}>
                  {cls.className} ({cls.academicYear})
                </option>
              ))}
            </select>
            <button
              className="th-button-secondary"
              onClick={() => refetch()}
              disabled={isLoading}
            >
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
              </svg>
              Làm mới
            </button>
          </div>
        }
      />

      {/* Metrics Row */}
      <div className="th-stats-grid" style={{ marginBottom: "20px" }}>
        <TeacherMetricCard
          label="Bài cần chấm & duyệt"
          value={totalInQueue}
          unit="bài"
          color="emerald"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M9 11l3 3L22 4" />
              <path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Độ tin cậy AI trung bình"
          value={`${avgConfidence}%`}
          color="cyan"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M12 2v20M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Lập luận chưa rõ ràng (Fallback)"
          value={fallbackCount}
          unit="bài"
          color="amber"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              <line x1="12" y1="9" x2="12" y2="13" />
              <line x1="12" y1="17" x2="12.01" y2="17" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Trạng thái phân quyền"
          value={canOverride ? "Được phép chấm đè" : "Chỉ xem"}
          color={canOverride ? "emerald" : "indigo"}
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0110 0v4" />
            </svg>
          }
        />
      </div>

      {isLoading && (
        <div style={{ display: "grid", gridTemplateColumns: "380px 1fr", gap: "20px" }}>
          <TeacherSkeleton height={450} />
          <TeacherSkeleton height={450} />
        </div>
      )}

      {isError && (
        <TeacherSafeErrorPanel
          error={error}
          title="Không thể tải hàng đợi chấm bài"
          onRetry={() => refetch()}
        />
      )}

      {!isLoading && !isError && queueItems.length === 0 && (
        <div className="th-surface" style={{ padding: "48px", textAlign: "center", borderRadius: "12px" }}>
          <div style={{ width: "64px", height: "64px", borderRadius: "50%", background: "var(--th-primary-subtle)", color: "var(--th-primary)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 16px" }}>
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M22 11.08V12a10 10 0 11-5.93-9.14" />
              <polyline points="22 4 12 14.01 9 11.01" />
            </svg>
          </div>
          <h3 style={{ fontSize: "1.2rem", fontWeight: 600, color: "var(--th-text-primary)", marginBottom: "8px" }}>
            Hàng đợi trống!
          </h3>
          <p style={{ color: "var(--th-text-secondary)", maxWidth: "460px", margin: "0 auto" }}>
            Hiện không có bài làm nào của học sinh cần giáo viên can thiệp hoặc chấm lại. Bạn có thể kiểm tra danh sách bài tập hoặc sang trang bảng điều khiển lớp học.
          </p>
        </div>
      )}

      {!isLoading && !isError && queueItems.length > 0 && (
        <div style={{ display: "grid", gridTemplateColumns: "360px 1fr", gap: "20px", alignItems: "start" }}>
          {/* Left Column: List of items */}
          <div className="th-surface" style={{ borderRadius: "12px", overflow: "hidden", display: "flex", flexDirection: "column", maxHeight: "calc(100vh - 220px)" }}>
            <div style={{ padding: "14px 16px", borderBottom: "1px solid var(--th-border-color)" }}>
              <input
                type="text"
                className="th-input"
                placeholder="Tìm theo học sinh, nội dung câu hỏi..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{ width: "100%", fontSize: "0.875rem" }}
              />
            </div>

            <div style={{ overflowY: "auto", flex: 1, padding: "8px" }}>
              {filteredItems.map((item) => {
                const isSelected = selectedItem?.analysisId === item.analysisId;
                return (
                  <div
                    key={item.analysisId}
                    onClick={() => handleSelectItem(item)}
                    style={{
                      padding: "12px 14px",
                      borderRadius: "8px",
                      marginBottom: "6px",
                      cursor: "pointer",
                      border: isSelected ? "2px solid var(--th-primary)" : "1px solid var(--th-border-color)",
                      backgroundColor: isSelected ? "var(--th-primary-subtle)" : "var(--th-surface-card)",
                      transition: "all 0.15s ease",
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                      <span style={{ fontWeight: 600, fontSize: "0.95rem", color: "var(--th-text-primary)" }}>
                        {item.studentName}
                      </span>
                      <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                        {item.hasStudentReviewRequest && (
                          <span
                            style={{
                              fontSize: "0.7rem",
                              fontWeight: 700,
                              padding: "2px 8px",
                              borderRadius: "9999px",
                              backgroundColor: "rgba(168, 85, 247, 0.2)",
                              color: "#d8b4fe",
                              border: "1px solid rgba(168, 85, 247, 0.4)",
                            }}
                            title={item.studentReviewReason ? `Học sinh yêu cầu xem lại: "${item.studentReviewReason}"` : "Học sinh yêu cầu xem xét kết quả AI"}
                          >
                            🙋 Học sinh khiếu nại
                          </span>
                        )}
                        {item.isFallback ? (
                          <TeacherStatusBadge status="warning" label="AI Cần Rà Soát" />
                        ) : (
                          <TeacherStatusBadge status="info" label="Chờ Duyệt" />
                        )}
                      </div>
                    </div>

                    <p style={{
                      fontSize: "0.825rem",
                      color: "var(--th-text-secondary)",
                      margin: "0 0 6px 0",
                      display: "-webkit-box",
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: "vertical",
                      overflow: "hidden"
                    }}>
                      {item.questionText}
                    </p>

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "0.75rem", color: "var(--th-text-muted)" }}>
                      <span>Tin cậy: {item.analysisConfidence ? `${Math.round(item.analysisConfidence * 100)}%` : "N/A"}</span>
                      <span>{new Date(item.submittedAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit" })}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Right Column: Selected Item Detail & Override Form */}
          {selectedItem ? (
            <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
              {selectedItem.hasStudentReviewRequest && (
                <div
                  style={{
                    padding: "14px 18px",
                    borderRadius: "12px",
                    border: "1px solid rgba(168, 85, 247, 0.4)",
                    backgroundColor: "rgba(168, 85, 247, 0.1)",
                    display: "flex",
                    alignItems: "flex-start",
                    gap: "12px",
                  }}
                >
                  <span style={{ fontSize: "1.3rem" }}>🙋</span>
                  <div style={{ flex: 1 }}>
                    <span style={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.05em", color: "#d8b4fe", display: "block", marginBottom: "4px" }}>
                      Yêu cầu xem xét lại từ học sinh (Student Review Request)
                    </span>
                    <p style={{ margin: "0 0 6px 0", fontSize: "0.875rem", fontWeight: 500, color: "#fff", fontStyle: "italic" }}>
                      "{selectedItem.studentReviewReason || "Học sinh yêu cầu giáo viên xem xét lại kết quả chấm/phân tích AI."}"
                    </p>
                    <span style={{ fontSize: "0.75rem", color: "rgba(216, 180, 254, 0.8)" }}>
                      Giáo viên vui lòng đối chiếu lời giải của học sinh và thực hiện chấm đè (override) để cập nhật điểm và Digital Twin chính xác.
                    </span>
                  </div>
                </div>
              )}

              {/* Submission Details Card */}
              <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "16px", paddingBottom: "12px", borderBottom: "1px solid var(--th-border-color)" }}>
                  <div>
                    <h3 style={{ fontSize: "1.15rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 4px 0" }}>
                      {selectedItem.studentName}
                    </h3>
                    <div style={{ display: "flex", gap: "10px", alignItems: "center", fontSize: "0.85rem", color: "var(--th-text-secondary)" }}>
                      <span>Nộp bài: {new Date(selectedItem.submittedAt).toLocaleString("vi-VN")}</span>
                      <span>•</span>
                      <span>Mã câu hỏi: {selectedItem.questionId}</span>
                    </div>
                  </div>
                  <Link
                    to={`/giao-vien/hoc-sinh/${selectedItem.studentId}/twin`}
                    className="th-button-secondary"
                    style={{ fontSize: "0.8rem", padding: "6px 12px", textDecoration: "none" }}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" />
                      <circle cx="12" cy="7" r="4" />
                    </svg>
                    Xem Digital Twin
                  </Link>
                </div>

                {/* Question Section */}
                <div style={{ marginBottom: "16px" }}>
                  <label style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--th-text-muted)", textTransform: "uppercase", letterSpacing: "0.5px" }}>
                    Đề bài câu hỏi
                  </label>
                  <div style={{ marginTop: "6px", padding: "12px", backgroundColor: "var(--th-surface-ground)", borderRadius: "8px", border: "1px solid var(--th-border-color)", fontSize: "0.875rem", lineHeight: 1.6 }}>
                    <RichMathText text={selectedItem.questionText} />
                  </div>
                </div>

                {/* Student's Answer Section */}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px", marginBottom: "16px" }}>
                  <div>
                    <label style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--th-text-muted)", textTransform: "uppercase", letterSpacing: "0.5px" }}>
                      Câu trả lời của học sinh
                    </label>
                    <div style={{ marginTop: "6px", padding: "12px", backgroundColor: "var(--th-surface-ground)", borderRadius: "8px", border: "1px solid var(--th-border-color)", fontWeight: 600, color: "var(--th-primary)" }}>
                      {selectedItem.finalAnswer || "(Chưa có câu trả lời)"}
                    </div>
                  </div>
                  <div>
                    <label style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--th-text-muted)", textTransform: "uppercase", letterSpacing: "0.5px" }}>
                      Giải trình các bước (Reasoning)
                    </label>
                    <div style={{ marginTop: "6px", padding: "12px", backgroundColor: "var(--th-surface-ground)", borderRadius: "8px", border: "1px solid var(--th-border-color)", minHeight: "44px" }}>
                      {selectedItem.reasoningText ? (
                        <RichMathText text={selectedItem.reasoningText} />
                      ) : (
                        <span style={{ color: "var(--th-text-muted)", fontStyle: "italic" }}>Học sinh không nhập giải trình chi tiết</span>
                      )}
                    </div>
                  </div>
                </div>

                {/* AI Automated Assessment Panel */}
                <div style={{ padding: "14px", backgroundColor: "rgba(14, 165, 233, 0.08)", borderRadius: "8px", border: "1px solid rgba(14, 165, 233, 0.25)" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: "6px", fontWeight: 600, color: "var(--th-info)", fontSize: "0.9rem" }}>
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
                      </svg>
                      Đánh Giá Tự Động Từ AI Model
                    </div>
                    <span style={{ fontSize: "0.8rem", color: "var(--th-text-secondary)" }}>
                      Độ tin cậy: <strong>{selectedItem.analysisConfidence ? `${Math.round(selectedItem.analysisConfidence * 100)}%` : "N/A"}</strong>
                    </span>
                  </div>
                  <p style={{ margin: "0 0 8px 0", fontSize: "0.875rem", color: "var(--th-text-primary)" }}>
                    {selectedItem.analysisFeedback || "Không có nhận xét chi tiết từ mô hình AI."}
                  </p>
                  <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", fontSize: "0.75rem" }}>
                    <span className="th-badge th-badge-neutral">Quyết định: {selectedItem.evidence?.decisionMode || "Tự động"}</span>
                    <span className="th-badge th-badge-neutral">Mức độ tin cậy: {selectedItem.evidence?.trustLevel || "Tiêu chuẩn"}</span>
                    {selectedItem.evidence?.reasonCodes?.map((code, idx) => (
                      <span key={idx} className="th-badge th-badge-warning">{code}</span>
                    ))}
                  </div>
                </div>
              </div>

              {/* Teacher Override Form */}
              <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
                <h4 style={{ fontSize: "1rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 16px 0", display: "flex", alignItems: "center", gap: "8px" }}>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                    <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
                  </svg>
                  Ghi Đè Kết Quả & Đánh Giá Của Giáo Viên
                </h4>

                {overrideFeedbackMessage && (
                  <div
                    style={{
                      padding: "12px 14px",
                      borderRadius: "8px",
                      marginBottom: "16px",
                      backgroundColor: replayResult ? "rgba(16, 185, 129, 0.12)" : "rgba(239, 68, 68, 0.12)",
                      border: `1px solid ${replayResult ? "var(--th-primary)" : "var(--th-danger)"}`,
                      color: replayResult ? "var(--th-primary)" : "var(--th-danger)",
                      fontSize: "0.875rem",
                      fontWeight: 500,
                    }}
                  >
                    {overrideFeedbackMessage}
                  </div>
                )}

                {/* Replay Result Banner */}
                {replayResult && (
                  <div style={{ marginBottom: "16px", padding: "14px", backgroundColor: "rgba(16, 185, 129, 0.08)", borderRadius: "8px", border: "1px solid var(--th-primary)" }}>
                    <h5 style={{ margin: "0 0 8px 0", color: "var(--th-primary)", fontWeight: 700, fontSize: "0.9rem" }}>
                      Kết quả cập nhật mô hình Digital Twin:
                    </h5>
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: "10px", fontSize: "0.825rem" }}>
                      <div>
                        <span style={{ color: "var(--th-text-muted)" }}>Mastery cũ:</span>
                        <div style={{ fontWeight: 600 }}>{Math.round(replayResult.replay.previousMastery * 100)}%</div>
                      </div>
                      <div>
                        <span style={{ color: "var(--th-text-muted)" }}>Mastery mới:</span>
                        <div style={{ fontWeight: 600, color: "var(--th-primary)" }}>{Math.round(replayResult.replay.newMastery * 100)}%</div>
                      </div>
                      <div>
                        <span style={{ color: "var(--th-text-muted)" }}>Điểm rủi ro mới:</span>
                        <div style={{ fontWeight: 600 }}>{Math.round(replayResult.replay.newRiskScore * 100)}%</div>
                      </div>
                      <div>
                        <span style={{ color: "var(--th-text-muted)" }}>Lượt replay:</span>
                        <div style={{ fontWeight: 600 }}>{replayResult.replay.attemptsReplayed} lần</div>
                      </div>
                    </div>
                  </div>
                )}

                <form onSubmit={handleSubmitOverride} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "14px" }}>
                    <div>
                      <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                        Điểm số công nhận (Thang 10) <span style={{ color: "var(--th-danger)" }}>*</span>
                      </label>
                      <input
                        type="number"
                        min="0"
                        max="10"
                        step="0.5"
                        className="th-input"
                        value={awardedScore}
                        onChange={(e) => {
                          const val = e.target.value;
                          if (val === "") {
                            setAwardedScore(0);
                          } else {
                            setAwardedScore(Math.max(0, Math.min(10, Number(val))));
                          }
                        }}
                        disabled={!canOverride || overrideMutation.isPending}
                        required
                      />
                    </div>

                    <div>
                      <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                        Chất lượng lập luận ({reasoningQuality}%)
                      </label>
                      <input
                        type="range"
                        min="0"
                        max="100"
                        value={reasoningQuality}
                        onChange={(e) => setReasoningQuality(Number(e.target.value))}
                        disabled={!canOverride || overrideMutation.isPending}
                        style={{ width: "100%", accentColor: "var(--th-primary)" }}
                      />
                    </div>

                    <div>
                      <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                        Phân loại lỗi sai
                      </label>
                      <select
                        className="th-select"
                        value={errorType}
                        onChange={(e) => setErrorType(e.target.value as ErrorType)}
                        disabled={!canOverride || overrideMutation.isPending}
                      >
                        <option value="None">Không có lỗi (Chuẩn xác)</option>
                        <option value="Knowledge">Lỗ hổng kiến thức</option>
                        <option value="Skill">Kỹ năng tính toán</option>
                        <option value="Reasoning">Lỗi lập luận logic</option>
                        <option value="Behavior">Lỗi thao tác / bất cẩn</option>
                        <option value="Presentation">Trình bày chưa chuẩn</option>
                        <option value="Unknown">Chưa xác định</option>
                      </select>
                    </div>
                  </div>

                  <div style={{ display: "flex", alignItems: "center", gap: "8px", margin: "4px 0" }}>
                    <input
                      type="checkbox"
                      id="isCorrectCheckbox"
                      checked={isCorrect}
                      onChange={(e) => setIsCorrect(e.target.checked)}
                      disabled={!canOverride || overrideMutation.isPending}
                      style={{ width: "16px", height: "16px", accentColor: "var(--th-primary)" }}
                    />
                    <label htmlFor="isCorrectCheckbox" style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--th-text-primary)", cursor: "pointer" }}>
                      Đánh dấu câu trả lời đạt yêu cầu đúng
                    </label>
                  </div>

                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                      <label style={{ fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)" }}>
                        Nhận xét của giáo viên (Gửi trực tiếp đến học sinh)
                      </label>
                      <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>{feedback.length}/1000 ký tự</span>
                    </div>
                    <textarea
                      className="th-textarea"
                      rows={2}
                      maxLength={1000}
                      value={feedback}
                      onChange={(e) => setFeedback(e.target.value)}
                      placeholder="Lời khuyên, hướng dẫn sửa lỗi cho học sinh..."
                      disabled={!canOverride || overrideMutation.isPending}
                    />
                  </div>

                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                      <label style={{ fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)" }}>
                        Lý do điều chỉnh (Lưu vết nhật ký hệ thống) <span style={{ color: "var(--th-danger)" }}>*</span>
                      </label>
                      <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>{overrideReason.length}/500 ký tự</span>
                    </div>
                    <input
                      type="text"
                      maxLength={500}
                      className="th-input"
                      value={overrideReason}
                      onChange={(e) => setOverrideReason(e.target.value)}
                      placeholder="Ví dụ: AI chấm nhầm do học sinh giải theo phương pháp thứ 2..."
                      disabled={!canOverride || overrideMutation.isPending}
                      required
                    />
                  </div>

                  <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "10px" }}>
                    <button
                      type="button"
                      className="th-button-secondary"
                      onClick={() => resetOverrideForm(selectedItem)}
                      disabled={overrideMutation.isPending}
                    >
                      Khôi phục mặc định
                    </button>
                    <button
                      type="submit"
                      className="th-primary-button"
                      disabled={!canOverride || overrideMutation.isPending}
                    >
                      {overrideMutation.isPending ? "Đang lưu & Replay..." : "Xác nhận & Cập nhật Digital Twin"}
                    </button>
                  </div>
                </form>
              </div>
            </div>
          ) : (
            <div className="th-surface" style={{ padding: "40px", textAlign: "center", borderRadius: "12px", color: "var(--th-text-muted)" }}>
              Chọn một bài làm từ danh sách bên trái để xem chi tiết và chấm bài
            </div>
          )}
        </div>
      )}
    </div>
  );
};
