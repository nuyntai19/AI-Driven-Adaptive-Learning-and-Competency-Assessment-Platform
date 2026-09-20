import React, { useMemo } from "react";
import { useParams, useNavigate, useSearchParams, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  getTeacherStudentTwin,
  getStudentTwinHistory,
} from "../../api/digitalTwinApi";
import { organizationApi } from "../../api/organizationApi";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";

export const TeacherStudentTwinView: React.FC = () => {
  const { studentId } = useParams<{ studentId: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const selectedSubjectId = searchParams.get("subjectId") || "";

  // Query subjects list
  const { data: subjectsData } = useQuery({
    queryKey: ["teacherSubjectsList"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const subjects = subjectsData?.data ?? [];
  const activeSubjectId = selectedSubjectId || (subjects.length > 0 ? subjects[0].subjectId : "");

  // Query student details
  const { data: studentDetail } = useQuery({
    queryKey: ["studentDetail", studentId],
    queryFn: () => (studentId ? organizationApi.getStudent(studentId) : null),
    enabled: Boolean(studentId),
  });

  // Query Digital Twin data
  const {
    data: twinData,
    isLoading: isTwinLoading,
    isError: isTwinError,
    error: twinError,
    refetch: refetchTwin,
  } = useQuery({
    queryKey: ["teacherStudentTwin", studentId, activeSubjectId],
    queryFn: () =>
      studentId && activeSubjectId
        ? getTeacherStudentTwin(studentId, activeSubjectId)
        : null,
    enabled: Boolean(studentId && activeSubjectId),
  });

  // Query Digital Twin History
  const { data: twinHistory, isLoading: isHistoryLoading } = useQuery({
    queryKey: ["teacherStudentTwinHistory", activeSubjectId],
    queryFn: () =>
      activeSubjectId ? getStudentTwinHistory(activeSubjectId) : [],
    enabled: Boolean(activeSubjectId),
  });

  // Computed metrics
  const topics = twinData?.topics ?? [];
  const behavior = twinData?.behavior;

  const averageMastery = useMemo(() => {
    if (topics.length === 0) return 0;
    const total = topics.reduce((acc, curr) => acc + curr.masteryPercentage, 0);
    return Math.round((total / topics.length) * 100);
  }, [topics]);

  const weakTopics = useMemo(() => {
    return topics.filter((t) => t.masteryPercentage < 0.6);
  }, [topics]);

  const strongTopics = useMemo(() => {
    return topics.filter((t) => t.masteryPercentage >= 0.8);
  }, [topics]);

  const studentName = studentDetail?.fullName || `Học sinh #${studentId?.slice(0, 8)}`;

  return (
    <div className="th-page-container">
      <TeacherPageHeader
        title={`Bản Sao Số (Digital Twin) - ${studentName}`}
        subtitle="Mô hình hóa năng lực cá nhân, lịch sử lập luận và phát hiện lỗ hổng tri thức theo thời gian thực"
        actions={
          <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
            <select
              className="th-select"
              value={activeSubjectId}
              onChange={(e) => {
                setSearchParams({ subjectId: e.target.value });
              }}
              style={{ minWidth: "160px" }}
            >
              {subjects.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  {sub.subjectName}
                </option>
              ))}
            </select>

            <button
              className="th-primary-button"
              onClick={() => {
                const weakNodeIds = weakTopics.map((t) => t.topicNodeId).join(",");
                navigate(
                  `/giao-vien/bai-tap/tao-moi?studentIds=${studentId}&subjectId=${activeSubjectId}${
                    weakNodeIds ? `&topicNodeId=${weakTopics[0].topicNodeId}` : ""
                  }`
                );
              }}
            >
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M12 5v14M5 12h14" />
              </svg>
              Giao Bài Bổ Trợ Riêng
            </button>
          </div>
        }
      />

      {/* Top Metrics Row */}
      <div className="th-stats-grid" style={{ marginBottom: "20px" }}>
        <TeacherMetricCard
          label="Năng Lực Tổng Thể (Mastery)"
          value={`${averageMastery}%`}
          color={averageMastery >= 75 ? "emerald" : averageMastery >= 50 ? "cyan" : "amber"}
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M12 2v20M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Chủ Đề Thành Thạo (>= 80%)"
          value={strongTopics.length}
          unit={`/${topics.length} chủ đề`}
          color="emerald"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M22 11.08V12a10 10 0 11-5.93-9.14" />
              <polyline points="22 4 12 14.01 9 11.01" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Lỗ Hổng Kiến Thức (< 60%)"
          value={weakTopics.length}
          unit="cần kèm cặp"
          color={weakTopics.length > 0 ? "rose" : "emerald"}
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              <line x1="12" y1="9" x2="12" y2="13" />
              <line x1="12" y1="17" x2="12.01" y2="17" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Tổng Số Lần Luyện Tập"
          value={behavior?.attemptCount ?? 0}
          unit="lượt nộp"
          color="indigo"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
              <polyline points="14 2 14 8 20 8" />
            </svg>
          }
        />
      </div>

      {isTwinLoading && (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "20px" }}>
          <TeacherSkeleton height={350} />
          <TeacherSkeleton height={350} />
        </div>
      )}

      {isTwinError && (
        <TeacherSafeErrorPanel
          error={twinError}
          title="Không thể tải dữ liệu Digital Twin học sinh"
          onRetry={() => refetchTwin()}
        />
      )}

      {!isTwinLoading && !isTwinError && (
        <div style={{ display: "grid", gridTemplateColumns: "1.2fr 0.8fr", gap: "20px", alignItems: "start" }}>
          {/* Left Column: Topic Mastery Breakdown */}
          <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
            <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
              <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 16px 0", display: "flex", alignItems: "center", gap: "8px" }}>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <line x1="18" y1="20" x2="18" y2="10" />
                  <line x1="12" y1="20" x2="12" y2="4" />
                  <line x1="6" y1="20" x2="6" y2="14" />
                </svg>
                Phổ Năng Lực Theo Chủ Đề Tri Thức
              </h3>

              {topics.length === 0 ? (
                <div style={{ padding: "30px", textAlign: "center", color: "var(--th-text-muted)" }}>
                  Chưa có dữ liệu đánh giá nào cho môn học này
                </div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
                  {topics.map((t) => {
                    const percentage = Math.round(t.masteryPercentage * 100);
                    let color = "var(--th-primary)";
                    let statusLabel = "Thành thạo";
                    let statusType: "success" | "warning" | "danger" | "info" = "success";

                    if (percentage < 50) {
                      color = "var(--th-danger)";
                      statusLabel = "Yếu - Cần bổ trợ";
                      statusType = "danger";
                    } else if (percentage < 70) {
                      color = "var(--th-warning)";
                      statusLabel = "Trung bình";
                      statusType = "warning";
                    } else if (percentage < 85) {
                      color = "var(--th-info)";
                      statusLabel = "Khá";
                      statusType = "info";
                    }

                    return (
                      <div
                        key={t.topicNodeId}
                        style={{
                          padding: "12px 14px",
                          borderRadius: "8px",
                          border: "1px solid var(--th-border-color)",
                          backgroundColor: "var(--th-surface-ground)",
                        }}
                      >
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
                          <div>
                            <span style={{ fontWeight: 600, fontSize: "0.95rem", color: "var(--th-text-primary)" }}>
                              {t.topicName}
                            </span>
                            <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)", marginLeft: "8px" }}>
                              ({t.evidenceCount} minh chứng)
                            </span>
                          </div>
                          <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                            <TeacherStatusBadge status={statusType} label={statusLabel} />
                            <span style={{ fontWeight: 700, fontSize: "1rem", color, minWidth: "45px", textAlign: "right" }}>
                              {percentage}%
                            </span>
                          </div>
                        </div>

                        {/* Progress Bar */}
                        <div style={{ width: "100%", height: "8px", backgroundColor: "rgba(0,0,0,0.06)", borderRadius: "4px", overflow: "hidden" }}>
                          <div
                            style={{
                              width: `${percentage}%`,
                              height: "100%",
                              backgroundColor: color,
                              borderRadius: "4px",
                              transition: "width 0.5s ease",
                            }}
                          />
                        </div>

                        {t.lastReasoningQuality !== null && t.lastReasoningQuality !== undefined && (
                          <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.75rem", color: "var(--th-text-muted)", marginTop: "6px" }}>
                            <span>Chất lượng lập luận bài gần nhất:</span>
                            <span style={{ fontWeight: 600 }}>{Math.round(t.lastReasoningQuality * 100)}%</span>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Twin Evolution History Timeline */}
            <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
              <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 16px 0", display: "flex", alignItems: "center", gap: "8px" }}>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
                Nhật Ký Tiến Hóa Năng Lực (Twin Timeline)
              </h3>

              {isHistoryLoading ? (
                <TeacherSkeleton height={150} />
              ) : !twinHistory || twinHistory.length === 0 ? (
                <div style={{ padding: "20px", textAlign: "center", color: "var(--th-text-muted)", fontSize: "0.875rem" }}>
                  Chưa có sự kiện cập nhật nào được ghi nhận gần đây
                </div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
                  {twinHistory.slice(0, 10).map((h) => {
                    const isPositive = h.delta >= 0;
                    return (
                      <div
                        key={h.historyId}
                        style={{
                          display: "flex",
                          alignItems: "flex-start",
                          gap: "12px",
                          padding: "10px 12px",
                          borderRadius: "8px",
                          border: "1px solid var(--th-border-color)",
                          backgroundColor: "var(--th-surface-card)",
                        }}
                      >
                        <div
                          style={{
                            width: "28px",
                            height: "28px",
                            borderRadius: "50%",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            backgroundColor: isPositive ? "rgba(16, 185, 129, 0.15)" : "rgba(239, 68, 68, 0.15)",
                            color: isPositive ? "var(--th-primary)" : "var(--th-danger)",
                            flexShrink: 0,
                          }}
                        >
                          {isPositive ? "+" : "−"}
                        </div>
                        <div style={{ flex: 1 }}>
                          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                            <span style={{ fontWeight: 600, fontSize: "0.85rem", color: "var(--th-text-primary)" }}>
                              {h.topicName}
                            </span>
                            <span
                              style={{
                                fontWeight: 700,
                                fontSize: "0.85rem",
                                color: isPositive ? "var(--th-primary)" : "var(--th-danger)",
                              }}
                            >
                              {isPositive ? `+${Math.round(h.delta * 100)}%` : `${Math.round(h.delta * 100)}%`}
                            </span>
                          </div>
                          <p style={{ margin: "2px 0 4px 0", fontSize: "0.8rem", color: "var(--th-text-secondary)" }}>
                            {h.explanation || `Cập nhật từ ${h.eventSource}`}
                          </p>
                          <span style={{ fontSize: "0.7rem", color: "var(--th-text-muted)" }}>
                            {new Date(h.recordedAt).toLocaleString("vi-VN")}
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          {/* Right Column: Behavior Twin & Telemetry */}
          <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
            <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
              <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 16px 0", display: "flex", alignItems: "center", gap: "8px" }}>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
                </svg>
                Hành Vi Học Tập (Behavior Twin)
              </h3>

              {behavior ? (
                <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "4px" }}>
                      <span style={{ color: "var(--th-text-secondary)" }}>Thời gian trung bình / câu:</span>
                      <span style={{ fontWeight: 700, color: "var(--th-text-primary)" }}>
                        {Math.round(behavior.avgTimeSpentSeconds)} giây
                      </span>
                    </div>
                  </div>

                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "4px" }}>
                      <span style={{ color: "var(--th-text-secondary)" }}>Tỷ lệ bỏ qua câu hỏi (Skip rate):</span>
                      <span style={{ fontWeight: 700, color: behavior.skipRate > 0.3 ? "var(--th-danger)" : "var(--th-text-primary)" }}>
                        {Math.round(behavior.skipRate * 100)}%
                      </span>
                    </div>
                    <div style={{ width: "100%", height: "6px", backgroundColor: "rgba(0,0,0,0.06)", borderRadius: "3px", overflow: "hidden" }}>
                      <div style={{ width: `${Math.min(100, Math.round(behavior.skipRate * 100))}%`, height: "100%", backgroundColor: behavior.skipRate > 0.3 ? "var(--th-danger)" : "var(--th-info)", borderRadius: "3px" }} />
                    </div>
                  </div>

                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "4px" }}>
                      <span style={{ color: "var(--th-text-secondary)" }}>Tần suất đổi đáp án:</span>
                      <span style={{ fontWeight: 700, color: "var(--th-text-primary)" }}>
                        {Math.round(behavior.changeAnswerRate * 100)}%
                      </span>
                    </div>
                    <div style={{ width: "100%", height: "6px", backgroundColor: "rgba(0,0,0,0.06)", borderRadius: "3px", overflow: "hidden" }}>
                      <div style={{ width: `${Math.min(100, Math.round(behavior.changeAnswerRate * 100))}%`, height: "100%", backgroundColor: "var(--th-warning)", borderRadius: "3px" }} />
                    </div>
                  </div>

                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "4px" }}>
                      <span style={{ color: "var(--th-text-secondary)" }}>Mức độ tự tin trung bình:</span>
                      <span style={{ fontWeight: 700, color: "var(--th-text-primary)" }}>
                        {Math.round(behavior.avgConfidence * 100)}%
                      </span>
                    </div>
                  </div>

                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "4px" }}>
                      <span style={{ color: "var(--th-text-secondary)" }}>Chuẩn hóa độ tự tin (Calibration):</span>
                      <span style={{ fontWeight: 700, color: behavior.confidenceCalibration >= 0.7 ? "var(--th-primary)" : "var(--at-secondary)" }}>
                        {Math.round(behavior.confidenceCalibration * 100)}%
                      </span>
                    </div>
                    <p style={{ fontSize: "0.75rem", color: "var(--th-text-muted)", margin: "4px 0 0 0" }}>
                      {behavior.confidenceCalibration >= 0.7
                        ? "Học sinh tự nhận thức chính xác năng lực bản thân khi làm bài."
                        : "Học sinh thường tự tin quá mức hoặc lo lắng thái quá so với điểm thực tế."}
                    </p>
                  </div>
                </div>
              ) : (
                <div style={{ padding: "20px", textAlign: "center", color: "var(--th-text-muted)" }}>
                  Chưa có dữ liệu hành vi cho học sinh này
                </div>
              )}
            </div>

            {/* Quick Pedagogy Advisory Card */}
            <div
              className="th-surface"
              style={{
                borderRadius: "12px",
                padding: "20px",
                backgroundColor: "rgba(16, 185, 129, 0.05)",
                border: "1px solid rgba(16, 185, 129, 0.2)",
              }}
            >
              <h4 style={{ fontSize: "0.95rem", fontWeight: 700, color: "var(--th-primary)", margin: "0 0 8px 0" }}>
                Gợi Ý Sư Phạm Cá Nhân Hóa (AI Advisor)
              </h4>
              <p style={{ fontSize: "0.825rem", color: "var(--th-text-secondary)", lineHeight: 1.5, margin: "0 0 12px 0" }}>
                {weakTopics.length > 0
                  ? `Học sinh đang gặp khó khăn ở ${weakTopics.length} chủ đề (${weakTopics.map((w) => w.topicName).slice(0, 2).join(", ")}...). Khuyến nghị giáo viên giao bộ câu hỏi bước đệm và yêu cầu viết giải trình chi tiết từng bước.`
                  : `Học sinh đang có tốc độ học tập rất tốt trên toàn bộ chủ đề đã học. Khuyến nghị mở rộng ngân hàng bài tập nâng cao hoặc mở sớm các chủ đề ở cấp độ tiếp theo.`}
              </p>
              <Link
                to={`/giao-vien/cau-hoi?subjectId=${activeSubjectId}`}
                style={{ fontSize: "0.8rem", color: "var(--th-primary)", fontWeight: 600, textDecoration: "none" }}
              >
                Khám phá câu hỏi phù hợp trong Ngân hàng câu hỏi →
              </Link>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
