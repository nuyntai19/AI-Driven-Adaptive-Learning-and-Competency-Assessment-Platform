import { useState, useMemo } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAssignment } from "../../features/assignments/useAssignment";
import { useAssignmentProgress } from "../../features/assignments/useAssignmentProgress";
import { questionsApi } from "../../api/questionsApi";
import { organizationApi } from "../../api/organizationApi";
import type { Question } from "../../types/questions";
import type { ProgressStatus } from "../../types/assignments";
import { TeacherModal } from "./TeacherOverlays";
import {
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "./TeacherPrimitives";
import { RichMathText } from "../math/RichMathText";

interface TeacherAssignmentQuickViewModalProps {
  assignmentId: string | null;
  isOpen: boolean;
  onClose: () => void;
  initialTab?: "questions" | "students" | "details";
}

export function TeacherAssignmentQuickViewModal({
  assignmentId,
  isOpen,
  onClose,
  initialTab = "questions",
}: TeacherAssignmentQuickViewModalProps) {
  const [activeTab, setActiveTab] = useState<"questions" | "students" | "details">(initialTab);
  const [questionSearch, setQuestionSearch] = useState("");
  const [studentSearch, setStudentSearch] = useState("");

  // 1. Fetch assignment details
  const assignmentQuery = useAssignment(assignmentId || undefined);
  const assignment = assignmentQuery.data?.data;

  // 2. Fetch class information
  const classId = assignment?.classId;
  const classQuery = useQuery({
    queryKey: ["quick-view-class", classId],
    queryFn: () => (classId ? organizationApi.getClass(classId) : null),
    enabled: Boolean(classId && isOpen),
    staleTime: 60_000,
  });

  // 3. Fetch all active students in the class
  const classStudentsQuery = useQuery({
    queryKey: ["quick-view-class-students", classId],
    queryFn: () =>
      classId
        ? organizationApi.getClassStudents(classId, { page: 1, pageSize: 100, status: "Active" })
        : null,
    enabled: Boolean(classId && isOpen),
    staleTime: 60_000,
  });

  // 4. If Published or Closed, fetch progress for student status
  const isPublishedOrClosed = assignment?.status === "Published" || assignment?.status === "Closed";
  const progressQuery = useAssignmentProgress(isPublishedOrClosed && assignmentId ? assignmentId : undefined);
  const progressMap = useMemo(() => {
    const map = new Map<string, { status: ProgressStatus; completedCount: number; totalCount: number }>();
    if (progressQuery.data?.data) {
      for (const item of progressQuery.data.data) {
        map.set(item.studentId.toLowerCase(), {
          status: item.status,
          completedCount: item.completedQuestionCount,
          totalCount: item.totalQuestionCount,
        });
      }
    }
    return map;
  }, [progressQuery.data?.data]);

  // 5. Fetch full question details for all questions in this assignment
  const questionIds = useMemo(() => {
    return assignment?.questions?.map((q) => q.questionId) || [];
  }, [assignment?.questions]);

  const questionsDetailQuery = useQuery({
    queryKey: ["quick-view-questions-details", questionIds],
    queryFn: async () => {
      if (questionIds.length === 0) return new Map<string, Question>();
      const results = await Promise.allSettled(
        questionIds.map((qid) => questionsApi.getById(qid))
      );
      const map = new Map<string, Question>();
      results.forEach((res) => {
        if (res.status === "fulfilled" && res.value?.data) {
          map.set(res.value.data.questionId, res.value.data);
        }
      });
      return map;
    },
    enabled: questionIds.length > 0 && isOpen,
    staleTime: 120_000,
  });

  const questionDetailsMap = questionsDetailQuery.data || new Map<string, Question>();

  // Determine assigned students
  const classStudents = classStudentsQuery.data?.data || [];
  const assignedStudents = useMemo(() => {
    if (!assignment) return [];

    const targetIds = (assignment.targets || []).map((t) => t.studentId.toLowerCase());
    const isSelectedStudents =
      assignment.targets &&
      assignment.targets.length > 0 &&
      assignment.targets[0].targetSource === "SelectedStudents";

    if (isSelectedStudents) {
      // Return students matching targetIds
      return classStudents.filter((s) => targetIds.includes(s.studentId.toLowerCase()));
    }

    // WholeClass mode: if targets are materialized after publish, use them or use all active class students
    if (targetIds.length > 0) {
      const filtered = classStudents.filter((s) => targetIds.includes(s.studentId.toLowerCase()));
      if (filtered.length > 0) return filtered;
    }
    return classStudents;
  }, [assignment, classStudents]);

  // Filtered Questions
  const filteredQuestions = useMemo(() => {
    const list = assignment?.questions || [];
    if (!questionSearch.trim()) return list;

    const term = questionSearch.toLowerCase().trim();
    return list.filter((aq) => {
      const qObj = questionDetailsMap.get(aq.questionId);
      const textMatch = qObj?.questionText?.toLowerCase().includes(term);
      const idMatch = aq.questionId.toLowerCase().includes(term);
      return textMatch || idMatch;
    });
  }, [assignment?.questions, questionDetailsMap, questionSearch]);

  // Filtered Students
  const filteredStudents = useMemo(() => {
    if (!studentSearch.trim()) return assignedStudents;
    const term = studentSearch.toLowerCase().trim();
    return assignedStudents.filter((s) => {
      return (
        s.fullName.toLowerCase().includes(term) ||
        s.username.toLowerCase().includes(term) ||
        s.studentId.toLowerCase().includes(term)
      );
    });
  }, [assignedStudents, studentSearch]);

  const totalPoints = useMemo(() => {
    return (assignment?.questions || []).reduce((acc, q) => acc + (q.points || 10), 0);
  }, [assignment?.questions]);

  const formatProgressStatus = (status?: ProgressStatus) => {
    switch (status) {
      case "Completed":
        return <TeacherStatusBadge status="Completed" label="Đã nộp bài" tone="success" />;
      case "InProgress":
        return <TeacherStatusBadge status="InProgress" label="Đang làm bài" tone="info" />;
      case "Overdue":
        return <TeacherStatusBadge status="Overdue" label="Quá hạn nộp" tone="danger" />;
      case "NotStarted":
      default:
        return <TeacherStatusBadge status="NotStarted" label="Chưa bắt đầu" tone="neutral" />;
    }
  };

  const isWholeClassMode =
    !assignment?.targets ||
    assignment.targets.length === 0 ||
    assignment.targets[0]?.targetSource === "WholeClass";

  const isLoading = assignmentQuery.isLoading || classQuery.isLoading;

  return (
    <TeacherModal
      isOpen={isOpen}
      size="lg"
      title={assignment ? `Nội Dung Bài Tập: ${assignment.title}` : "Chi Tiết Bài Tập"}
      description={
        assignment
          ? `Lớp: ${classQuery.data?.className || assignment.classId} · Môn: ${classQuery.data?.subject?.subjectName || "Đa môn"}`
          : "Xem lại danh sách câu hỏi và danh sách học sinh được phân công."
      }
      onClose={onClose}
      footer={
        <div className="flex flex-wrap items-center justify-between gap-3 w-full">
          <div className="text-xs text-[var(--th-text-muted)]">
            Trạng thái: <strong>{assignment?.status || "..."}</strong>
            {assignment?.dueAt && (
              <span className="ml-3">
                Hạn nộp: {new Date(assignment.dueAt).toLocaleString("vi-VN")}
              </span>
            )}
          </div>
          <div className="flex gap-2">
            <button type="button" onClick={onClose} className="th-secondary-button text-xs py-1.5 px-4">
              Đóng
            </button>
            {assignment?.status === "Draft" ? (
              <Link
                to={`/giao-vien/bai-tap/${assignment.assignmentId}`}
                onClick={onClose}
                className="th-primary-button text-xs py-1.5 px-4"
              >
                Chỉnh sửa bài tập →
              </Link>
            ) : (
              <Link
                to={`/giao-vien/bai-tap/${assignment?.assignmentId}/tien-do`}
                onClick={onClose}
                className="th-primary-button text-xs py-1.5 px-4"
              >
                Xem tiến độ lớp học →
              </Link>
            )}
          </div>
        </div>
      }
    >
      {isLoading ? (
        <div className="space-y-4">
          <TeacherSkeleton className="h-10 w-full rounded-xl" />
          <TeacherSkeleton className="h-44 w-full rounded-2xl" />
          <TeacherSkeleton className="h-44 w-full rounded-2xl" />
        </div>
      ) : assignmentQuery.isError ? (
        <TeacherSafeErrorPanel
          error={assignmentQuery.error}
          fallback="Không thể tải thông tin chi tiết bài tập."
          onRetry={() => assignmentQuery.refetch()}
        />
      ) : !assignment ? (
        <div className="p-8 text-center text-xs text-[var(--th-text-muted)]">
          Không tìm thấy dữ liệu bài tập.
        </div>
      ) : (
        <div className="space-y-5">
          {/* Top Quick Overview Banner */}
          <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-muted)] p-4 flex flex-wrap items-center justify-between gap-4">
            <div className="flex flex-wrap items-center gap-2.5">
              <TeacherStatusBadge status={assignment.status} />
              <span className="text-xs font-semibold text-[var(--th-text)]">
                {classQuery.data?.className} ({classQuery.data?.academicYear})
              </span>
              <span className="text-xs text-[var(--th-text-muted)]">•</span>
              <span className="text-xs font-medium text-[var(--th-teal)]">
                {classQuery.data?.subject?.subjectName}
              </span>
            </div>

            <div className="flex flex-wrap items-center gap-3 text-xs text-[var(--th-text-secondary)]">
              <span>
                📚 <strong>{assignment.questionCount}</strong> câu hỏi ({totalPoints} điểm)
              </span>
              <span>•</span>
              <span>
                👥 <strong>{assignedStudents.length}</strong> học sinh ({isWholeClassMode ? "Cả lớp" : "Chỉ định riêng"})
              </span>
            </div>
          </div>

          {/* Navigation Tabs */}
          <div className="flex border-b border-[var(--th-border-subtle)] gap-2">
            <button
              type="button"
              onClick={() => setActiveTab("questions")}
              className={`pb-2.5 px-3 text-xs font-bold transition flex items-center gap-2 border-b-2 ${
                activeTab === "questions"
                  ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                  : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
              }`}
            >
              <span>📚 Danh sách câu hỏi</span>
              <span className="rounded-full bg-[var(--th-surface-muted)] px-2 py-0.5 text-[10px] font-bold">
                {assignment.questionCount}
              </span>
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("students")}
              className={`pb-2.5 px-3 text-xs font-bold transition flex items-center gap-2 border-b-2 ${
                activeTab === "students"
                  ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                  : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
              }`}
            >
              <span>👥 Học sinh phân công</span>
              <span className="rounded-full bg-[var(--th-surface-muted)] px-2 py-0.5 text-[10px] font-bold">
                {assignedStudents.length}
              </span>
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("details")}
              className={`pb-2.5 px-3 text-xs font-bold transition flex items-center gap-2 border-b-2 ${
                activeTab === "details"
                  ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                  : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
              }`}
            >
              <span>📝 Thông tin & Hướng dẫn</span>
            </button>
          </div>

          {/* TAB 1: DANH SÁCH CÂU HỎI */}
          {activeTab === "questions" && (
            <div className="space-y-4">
              {/* Filter / Search Bar */}
              <div className="flex items-center justify-between gap-3">
                <input
                  type="text"
                  placeholder="Tìm kiếm nội dung câu hỏi..."
                  value={questionSearch}
                  onChange={(e) => setQuestionSearch(e.target.value)}
                  className="th-input w-full max-w-sm text-xs py-1.5"
                />
                <span className="text-xs text-[var(--th-text-muted)] shrink-0">
                  Hiển thị <strong>{filteredQuestions.length}</strong> / <strong>{assignment.questionCount}</strong> câu
                </span>
              </div>

              {questionsDetailQuery.isLoading && (
                <div className="space-y-3">
                  <TeacherSkeleton className="h-28 w-full rounded-xl" />
                  <TeacherSkeleton className="h-28 w-full rounded-xl" />
                </div>
              )}

              {filteredQuestions.length === 0 ? (
                <div className="p-8 text-center text-xs text-[var(--th-text-muted)] rounded-xl border border-dashed border-[var(--th-border)]">
                  Không tìm thấy câu hỏi nào phù hợp.
                </div>
              ) : (
                <div className="space-y-3 max-h-[50vh] overflow-y-auto pr-1">
                  {filteredQuestions.map((aq, index) => {
                    const qDetail = questionDetailsMap.get(aq.questionId);
                    const orderNum = aq.orderIndex !== undefined ? aq.orderIndex + 1 : index + 1;
                    const maxScore = aq.points || qDetail?.maxScore || 10;
                    const qType = qDetail?.questionType || "MultipleChoice";

                    return (
                      <div
                        key={aq.questionId}
                        className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-card)] p-4 space-y-3 hover:border-[var(--th-teal)]/40 transition shadow-sm"
                      >
                        {/* Header of question card */}
                        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--th-border-subtle)] pb-2.5">
                          <div className="flex items-center gap-2">
                            <span className="rounded-lg bg-[var(--th-teal)]/15 px-2 py-0.5 text-xs font-black text-[var(--th-teal)]">
                              Câu {orderNum}
                            </span>
                            <span className="th-badge th-badge-info text-[10px]">
                              {qType === "MultipleChoice" ? "Trắc nghiệm" : "Tự luận"}
                            </span>
                            {qDetail?.difficulty && (
                              <span className="text-[11px] text-[var(--th-text-muted)]">
                                Độ khó: <strong>{qDetail.difficulty}/5</strong>
                              </span>
                            )}
                          </div>
                          <span className="font-mono text-xs font-bold text-[var(--th-text)]">
                            {maxScore} điểm
                          </span>
                        </div>

                        {/* Question Content */}
                        <div className="text-xs text-[var(--th-text)] leading-relaxed font-medium">
                          {qDetail?.questionText ? (
                            <RichMathText text={qDetail.questionText} />
                          ) : (
                            <span className="font-mono text-[var(--th-text-muted)]">
                              Mã câu hỏi: #{aq.questionId} (Đang tải nội dung...)
                            </span>
                          )}
                        </div>

                        {/* Options if Multiple Choice */}
                        {qDetail?.options && qDetail.options.length > 0 && (
                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-2 border-t border-[var(--th-border-subtle)]">
                            {qDetail.options.map((opt) => (
                              <div
                                key={opt.optionId}
                                className="flex items-start gap-2 p-2 rounded-lg bg-[var(--th-surface-ground)] text-xs text-[var(--th-text-secondary)]"
                              >
                                <span className="font-bold text-[var(--th-teal)] shrink-0">
                                  {opt.label}.
                                </span>
                                <div className="leading-snug">
                                  <RichMathText text={opt.text} />
                                </div>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {/* TAB 2: HỌC SINH PHÂN CÔNG */}
          {activeTab === "students" && (
            <div className="space-y-4">
              {/* Target Mode Info Banner */}
              <div
                className={`p-3 rounded-xl border text-xs flex items-center justify-between ${
                  isWholeClassMode
                    ? "border-teal-500/40 bg-teal-500/10 text-teal-300"
                    : "border-amber-500/40 bg-amber-500/10 text-amber-300"
                }`}
              >
                <div>
                  <span className="font-bold uppercase tracking-wider text-[11px] block">
                    {isWholeClassMode ? "🎯 Chế độ: Toàn bộ lớp học" : "🎯 Chế độ: Chỉ định nhóm học sinh"}
                  </span>
                  <p className="mt-0.5 text-xs text-[var(--th-text-secondary)]">
                    {isWholeClassMode
                      ? `Bài tập áp dụng cho toàn bộ học sinh lớp ${classQuery.data?.className || ""}. Học sinh mới vào lớp cũng sẽ nhận được bài.`
                      : `Bài tập được thiết lập riêng cho nhóm ${assignedStudents.length} học sinh cần bổ trợ / kèm cặp kiến thức.`}
                  </p>
                </div>
                <span className="text-xs font-black shrink-0 px-2 py-1 rounded bg-[var(--th-surface)] border border-[var(--th-border)] text-[var(--th-text)]">
                  {assignedStudents.length} học sinh
                </span>
              </div>

              {/* Search Bar */}
              <div className="flex items-center justify-between gap-3">
                <input
                  type="text"
                  placeholder="Tìm học sinh theo tên, mã hoặc username..."
                  value={studentSearch}
                  onChange={(e) => setStudentSearch(e.target.value)}
                  className="th-input w-full max-w-sm text-xs py-1.5"
                />
                <span className="text-xs text-[var(--th-text-muted)] shrink-0">
                  Hiển thị <strong>{filteredStudents.length}</strong> / <strong>{assignedStudents.length}</strong> học sinh
                </span>
              </div>

              {/* Students Table */}
              <div className="overflow-x-auto rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-card)] max-h-[50vh]">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="border-b border-[var(--th-border-subtle)] bg-[var(--th-surface-muted)] text-[var(--th-text-muted)] font-semibold uppercase tracking-wider">
                      <th className="px-4 py-3">STT</th>
                      <th className="px-4 py-3">Học sinh</th>
                      <th className="px-4 py-3">Tài khoản</th>
                      <th className="px-4 py-3">Khối</th>
                      <th className="px-4 py-3">
                        {isPublishedOrClosed ? "Tình trạng làm bài" : "Phân bổ"}
                      </th>
                      <th className="px-4 py-3 text-right">Bản sao số</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[var(--th-border-subtle)]">
                    {filteredStudents.length === 0 ? (
                      <tr>
                        <td colSpan={6} className="p-8 text-center text-xs text-[var(--th-text-muted)]">
                          Không tìm thấy học sinh nào phù hợp.
                        </td>
                      </tr>
                    ) : (
                      filteredStudents.map((student, idx) => {
                        const progress = progressMap.get(student.studentId.toLowerCase());

                        return (
                          <tr key={student.studentId} className="hover:bg-[var(--th-surface-muted)] transition-colors">
                            <td className="px-4 py-3 text-[var(--th-text-muted)]">{idx + 1}</td>
                            <td className="px-4 py-3 font-semibold text-[var(--th-text)]">
                              {student.fullName}
                            </td>
                            <td className="px-4 py-3 font-mono text-[var(--th-text-secondary)]">
                              @{student.username}
                            </td>
                            <td className="px-4 py-3 text-[var(--th-text-secondary)]">
                              Khối {student.gradeLevel}
                            </td>
                            <td className="px-4 py-3">
                              {isPublishedOrClosed ? (
                                <div className="flex items-center gap-2">
                                  {formatProgressStatus(progress?.status)}
                                  {progress && (
                                    <span className="text-[11px] text-[var(--th-text-muted)] font-mono">
                                      ({progress.completedCount}/{progress.totalCount || assignment.questionCount} câu)
                                    </span>
                                  )}
                                </div>
                              ) : (
                                <span className="th-badge th-badge-neutral text-[10px]">
                                  {isWholeClassMode ? "Toàn lớp" : "Chỉ định nhóm"}
                                </span>
                              )}
                            </td>
                            <td className="px-4 py-3 text-right">
                              <Link
                                to={`/giao-vien/hoc-sinh/${student.studentId}/twin`}
                                onClick={onClose}
                                className="th-secondary-button text-[11px] py-1 px-2.5 inline-block"
                              >
                                Xem Twin →
                              </Link>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 3: THÔNG TIN & HƯỚNG DẪN */}
          {activeTab === "details" && (
            <div className="space-y-4 text-xs">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-card)] p-5">
                <div>
                  <span className="text-[10px] uppercase font-bold text-[var(--th-text-muted)] block mb-1">
                    Tiêu đề bài tập:
                  </span>
                  <p className="text-sm font-bold text-[var(--th-text)]">{assignment.title}</p>
                </div>
                <div>
                  <span className="text-[10px] uppercase font-bold text-[var(--th-text-muted)] block mb-1">
                    Lớp & Môn học:
                  </span>
                  <p className="font-semibold text-[var(--th-text)]">
                    {classQuery.data?.className} ({classQuery.data?.academicYear}) - Môn {classQuery.data?.subject?.subjectName}
                  </p>
                </div>
                <div>
                  <span className="text-[10px] uppercase font-bold text-[var(--th-text-muted)] block mb-1">
                    Trạng thái xuất bản:
                  </span>
                  <div>
                    <TeacherStatusBadge status={assignment.status} />
                  </div>
                </div>
                <div>
                  <span className="text-[10px] uppercase font-bold text-[var(--th-text-muted)] block mb-1">
                    Hạn chót nộp bài:
                  </span>
                  <p className="font-semibold text-[var(--th-text)]">
                    {assignment.dueAt ? new Date(assignment.dueAt).toLocaleString("vi-VN") : "Không giới hạn thời gian"}
                  </p>
                </div>
              </div>

              {assignment.instructions ? (
                <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-card)] p-5 space-y-2">
                  <span className="text-[10px] uppercase font-bold text-[var(--th-text-muted)] block">
                    Lời dặn & Hướng dẫn làm bài cho học sinh:
                  </span>
                  <p className="text-xs text-[var(--th-text-secondary)] whitespace-pre-line leading-relaxed">
                    {assignment.instructions}
                  </p>
                </div>
              ) : (
                <div className="rounded-xl border border-dashed border-[var(--th-border)] p-5 text-center text-xs text-[var(--th-text-muted)]">
                  Chưa có hướng dẫn làm bài bổ sung cho bài tập này.
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </TeacherModal>
  );
}
