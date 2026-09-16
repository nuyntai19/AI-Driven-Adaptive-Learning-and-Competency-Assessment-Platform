import React, { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../../api/organizationApi";
import type {
  StudentDetailDto,
  StudentSubjectGoalDto,
  SubjectDto,
  UpsertStudentSubjectGoalRequest,
} from "../../types/organization";
import { extractProblemDetails, isConcurrencyConflict, mapSafeOperationalError } from "../../utils/problemDetails";
import { ConcurrencyBanner, SafeErrorPanel, Skeleton } from "./CenterManagerPrimitives";
import { Modal } from "./CenterManagerOverlays";

interface SubjectGoalsModalProps {
  isOpen: boolean;
  studentId: string | null;
  studentName?: string;
  onClose: () => void;
  onSuccess?: () => void;
}

export const SubjectGoalsModal: React.FC<SubjectGoalsModalProps> = ({
  isOpen,
  studentId,
  studentName,
  onClose,
  onSuccess,
}) => {
  const queryClient = useQueryClient();

  const [selectedSubjectId, setSelectedSubjectId] = useState<string>("");
  const [targetScore, setTargetScore] = useState<string>("8.0");
  const [remainingDays, setRemainingDays] = useState<string>("30");
  const [feedback, setFeedback] = useState<{
    type: "success" | "error" | "conflict";
    message: string;
    traceId?: string;
  } | null>(null);

  // Canonical query for student details (including latest subjectGoals)
  const {
    data: studentDetail,
    isLoading: isLoadingStudent,
    isError: isStudentError,
    error: studentError,
    refetch: refetchStudent,
  } = useQuery({
    queryKey: ["studentDetail", studentId],
    queryFn: () => organizationApi.getStudent(studentId!),
    enabled: isOpen && !!studentId,
  });

  // Local goals state that is immediately updated on mutation success
  // to guarantee RowVersion is never stale across consecutive edits
  const [goals, setGoals] = useState<StudentSubjectGoalDto[]>([]);

  useEffect(() => {
    if (studentDetail?.subjectGoals) {
      setGoals(studentDetail.subjectGoals);
    }
  }, [studentDetail?.subjectGoals]);

  // Fetch active subjects list for selector and name lookup
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isError: isSubjectsError,
    error: subjectsError,
    refetch: refetchSubjects,
  } = useQuery({
    queryKey: ["subjects", "active-for-goals"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: isOpen,
  });

  const subjects = useMemo(() => subjectsData?.data ?? [], [subjectsData?.data]);

  const subjectsMap = useMemo(() => {
    const map = new Map<string, SubjectDto>();
    for (const sub of subjects) {
      map.set(sub.subjectId, sub);
    }
    return map;
  }, [subjects]);

  // Find if student already has a goal for selected subject
  const currentGoal: StudentSubjectGoalDto | undefined = useMemo(() => {
    if (!selectedSubjectId) return undefined;
    return goals.find((g) => g.subjectId === selectedSubjectId);
  }, [goals, selectedSubjectId]);

  // When selectedSubjectId changes or modal opens, update form inputs
  useEffect(() => {
    if (currentGoal) {
      setTargetScore(currentGoal.targetScore.toString());
      setRemainingDays(currentGoal.remainingDays.toString());
    } else {
      setTargetScore("8.0");
      setRemainingDays("30");
    }
  }, [currentGoal]);

  // Set default subject on load
  useEffect(() => {
    if (subjects.length > 0 && !selectedSubjectId) {
      setSelectedSubjectId(subjects[0].subjectId);
    }
  }, [subjects, selectedSubjectId]);

  // Reset feedback on modal open
  useEffect(() => {
    if (isOpen) {
      setFeedback(null);
    }
  }, [isOpen]);

  const upsertMutation = useMutation({
    mutationFn: async ({
      targetStudentId,
      subjectId,
      request,
    }: {
      targetStudentId: string;
      subjectId: string;
      request: UpsertStudentSubjectGoalRequest;
    }) => {
      return await organizationApi.upsertStudentSubjectGoal(targetStudentId, subjectId, request);
    },
    onSuccess: (updatedGoal: StudentSubjectGoalDto) => {
      // 1. Immediately update local goals in modal so subsequent edits use the new RowVersion
      setGoals((prev) => {
        const index = prev.findIndex((g) => g.subjectId === updatedGoal.subjectId);
        if (index >= 0) {
          const next = [...prev];
          next[index] = updatedGoal;
          return next;
        }
        return [...prev, updatedGoal];
      });

      // 2. Immediately update the React Query cache for ["studentDetail", studentId]
      queryClient.setQueryData<StudentDetailDto>(["studentDetail", studentId], (old) => {
        if (!old) return old;
        const existing = old.subjectGoals || [];
        const index = existing.findIndex((g) => g.subjectId === updatedGoal.subjectId);
        const nextGoals = index >= 0
          ? existing.map((g, i) => (i === index ? updatedGoal : g))
          : [...existing, updatedGoal];
        return {
          ...old,
          subjectGoals: nextGoals,
        };
      });

      // 3. Invalidate queries in background
      queryClient.invalidateQueries({ queryKey: ["studentDetail", studentId] });
      queryClient.invalidateQueries({ queryKey: ["students"] });

      setFeedback({
        type: "success",
        message: "Đã lưu mục tiêu môn học thành công. Phiên bản dữ liệu và mô hình Digital Twin đã được đồng bộ.",
      });
      onSuccess?.();
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        setFeedback({
          type: "conflict",
          message: "Dữ liệu mục tiêu đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại dữ liệu mới nhất.",
        });
      } else {
        const details = extractProblemDetails(error);
        setFeedback({
          type: "error",
          message: mapSafeOperationalError(error, "Không thể lưu mục tiêu môn học. Vui lòng kiểm tra lại giá trị nhập."),
          traceId: details.traceId ?? undefined,
        });
      }
    },
  });

  const isFormDisabled =
    upsertMutation.isPending ||
    isLoadingStudent ||
    isLoadingSubjects ||
    isStudentError ||
    isSubjectsError ||
    !studentDetail ||
    subjects.length === 0;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isFormDisabled || !studentId || !selectedSubjectId || !studentDetail) return;

    const score = parseFloat(targetScore);
    if (Number.isNaN(score) || score < 0 || score > 10) {
      setFeedback({
        type: "error",
        message: "Điểm mục tiêu phải là số từ 0.0 đến 10.0.",
      });
      return;
    }

    // Backend contract: Range 0 to 3650 days (UpsertStudentSubjectGoalRequest.cs)
    const days = parseInt(remainingDays, 10);
    if (Number.isNaN(days) || days < 0 || days > 3650) {
      setFeedback({
        type: "error",
        message: "Thời hạn phải là số nguyên từ 0 đến 3650 ngày (tối đa 10 năm).",
      });
      return;
    }

    setFeedback(null);
    upsertMutation.mutate({
      targetStudentId: studentId,
      subjectId: selectedSubjectId,
      request: {
        targetScore: score,
        remainingDays: days,
        rowVersion: currentGoal ? currentGoal.rowVersion : undefined,
      },
    });
  };

  const handleReload = async () => {
    await refetchStudent();
    setFeedback(null);
  };

  const displayName = studentName || studentDetail?.fullName || "học viên";

  return (
    <Modal
      isOpen={isOpen}
      title="Mục tiêu môn học (Digital Twin)"
      description={`Thiết lập mục tiêu điểm số và thời hạn năng lực cho ${displayName}`}
      onClose={onClose}
      maxWidth="max-w-xl"
    >
      <div className="space-y-5">
        {feedback?.type === "conflict" && (
          <ConcurrencyBanner onReload={handleReload} isReloading={isLoadingStudent || upsertMutation.isPending} />
        )}

        {/* Student detail query error */}
        {isStudentError && (
          <SafeErrorPanel
            error={studentError}
            fallback="Không thể tải thông tin chi tiết học sinh và danh sách mục tiêu."
            onRetry={() => refetchStudent()}
          />
        )}

        {/* Subjects list query error */}
        {isSubjectsError && (
          <SafeErrorPanel
            error={subjectsError}
            fallback="Không thể tải danh sách môn học hoạt động."
            onRetry={() => refetchSubjects()}
          />
        )}

        {/* Loading skeleton state */}
        {(isLoadingStudent || isLoadingSubjects) && (
          <div className="space-y-2 py-1" role="status" aria-label="Đang tải dữ liệu mục tiêu">
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-8 w-full" />
          </div>
        )}

        {/* Empty subjects warning */}
        {!isLoadingSubjects && !isSubjectsError && subjects.length === 0 && (
          <div role="status" className="rounded-xl border border-amber-400/30 bg-amber-400/10 p-3 text-xs text-amber-200">
            Không tìm thấy môn học nào đang hoạt động trong trung tâm để thiết lập mục tiêu.
          </div>
        )}

        {feedback && feedback.type !== "conflict" && (
          <div
            role="alert"
            className={`rounded-xl border p-4 text-sm ${
              feedback.type === "success"
                ? "border-emerald-400/30 bg-emerald-400/10 text-emerald-200"
                : "border-rose-400/30 bg-rose-400/10 text-rose-200"
            }`}
          >
            <p className="font-semibold">{feedback.message}</p>
            {feedback.traceId && (
              <p className="mt-1 font-mono text-xs opacity-75">Mã theo dõi: {feedback.traceId}</p>
            )}
          </div>
        )}

        {/* Existing Goals Overview (only if student detail loaded successfully) */}
        {!isStudentError && goals.length > 0 && (
          <div>
            <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)]">
              Mục tiêu hiện có ({goals.length})
            </h3>
            <div className="mt-2 divide-y divide-[var(--cm-border-subtle)] rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)]">
              {goals.map((goal) => {
                const sub = subjectsMap.get(goal.subjectId);
                const label = sub ? `${sub.subjectName} (${sub.subjectCode})` : `Môn học (#${goal.subjectId.slice(0, 8)})`;
                return (
                  <div
                    key={goal.subjectId}
                    className="flex items-center justify-between p-3 text-sm"
                  >
                    <div>
                      <span className="font-medium text-[var(--cm-text)]">
                        {label}
                      </span>
                      <span className="ml-2 text-xs text-[var(--cm-text-muted)]">
                        · Còn {goal.remainingDays} ngày
                      </span>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="font-bold text-[var(--cm-cyan)]">
                        {goal.targetScore} / 10
                      </span>
                      {goal.currentPredictedScore !== undefined && (
                        <span className="text-xs text-[var(--cm-text-secondary)]">
                          (Dự báo: {goal.currentPredictedScore})
                        </span>
                      )}
                      <button
                        type="button"
                        onClick={() => setSelectedSubjectId(goal.subjectId)}
                        disabled={isFormDisabled}
                        className="text-xs text-indigo-400 hover:text-indigo-300 underline disabled:opacity-50"
                      >
                        Chọn sửa
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Upsert Form */}
        <form onSubmit={handleSubmit} className="space-y-4 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-4">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)]">
            {currentGoal ? "Cập nhật mục tiêu môn đã chọn" : "Thiết lập mục tiêu môn mới"}
          </h3>

          <div>
            <label htmlFor="goal-subject-select" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
              Môn học <span className="text-rose-400">*</span>
            </label>
            <select
              id="goal-subject-select"
              value={selectedSubjectId}
              onChange={(e) => setSelectedSubjectId(e.target.value)}
              disabled={isFormDisabled}
              className="cm-field mt-1 w-full px-3 text-sm disabled:opacity-50"
              required
            >
              {subjects.length === 0 ? (
                <option value="">(Không có môn học khả dụng)</option>
              ) : (
                subjects.map((sub) => (
                  <option key={sub.subjectId} value={sub.subjectId}>
                    {sub.subjectName} ({sub.subjectCode})
                  </option>
                ))
              )}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="goal-target-score" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                Điểm mục tiêu (0.00 – 10.00) <span className="text-rose-400">*</span>
              </label>
              <input
                type="number"
                id="goal-target-score"
                step="0.01"
                min="0"
                max="10"
                value={targetScore}
                onChange={(e) => setTargetScore(e.target.value)}
                disabled={isFormDisabled}
                className="cm-field mt-1 w-full px-3 text-sm disabled:opacity-50"
                required
              />
              <p className="mt-1 text-[11px] text-[var(--cm-text-muted)]">Thang điểm 10, tối đa 2 chữ số thập phân.</p>
            </div>

            <div>
              <label htmlFor="goal-remaining-days" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                Thời hạn còn lại (0 – 3650 ngày) <span className="text-rose-400">*</span>
              </label>
              <input
                type="number"
                id="goal-remaining-days"
                step="1"
                min="0"
                max="3650"
                value={remainingDays}
                onChange={(e) => setRemainingDays(e.target.value)}
                disabled={isFormDisabled}
                className="cm-field mt-1 w-full px-3 text-sm disabled:opacity-50"
                required
              />
              <p className="mt-1 text-[11px] text-[var(--cm-text-muted)]">Số ngày trước kỳ đánh giá (tối đa 10 năm).</p>
            </div>
          </div>

          {currentGoal?.rowVersion && (
            <p className="text-[11px] font-mono text-[var(--cm-text-muted)]">
              Phiên bản đồng thời (RowVersion): {currentGoal.rowVersion}
            </p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              disabled={upsertMutation.isPending}
              className="cm-secondary-button text-sm"
            >
              Đóng
            </button>
            <button
              type="submit"
              id="btn-submit-subject-goal"
              disabled={isFormDisabled}
              className="cm-primary-button text-sm disabled:opacity-50"
            >
              {upsertMutation.isPending ? "Đang lưu..." : currentGoal ? "Cập nhật mục tiêu" : "Thiết lập mục tiêu"}
            </button>
          </div>
        </form>
      </div>
    </Modal>
  );
};
