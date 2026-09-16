import React, { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../../api/organizationApi";
import type {
  StudentDetailDto,
  StudentSubjectGoalDto,
  UpsertStudentSubjectGoalRequest,
} from "../../types/organization";
import { extractProblemDetails, isConcurrencyConflict, mapSafeOperationalError } from "../../utils/problemDetails";
import { ConcurrencyBanner } from "./CenterManagerPrimitives";
import { Modal } from "./CenterManagerOverlays";

interface SubjectGoalsModalProps {
  isOpen: boolean;
  student: StudentDetailDto | null;
  onClose: () => void;
  onSuccess?: () => void;
}

export const SubjectGoalsModal: React.FC<SubjectGoalsModalProps> = ({
  isOpen,
  student,
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

  // Fetch subjects list for selector
  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "active-for-goals"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: isOpen,
  });

  const subjects = useMemo(() => subjectsData?.data ?? [], [subjectsData?.data]);

  // Find if student already has a goal for selected subject
  const currentGoal: StudentSubjectGoalDto | undefined = useMemo(() => {
    if (!student?.subjectGoals || !selectedSubjectId) return undefined;
    return student.subjectGoals.find((g) => g.subjectId === selectedSubjectId);
  }, [student?.subjectGoals, selectedSubjectId]);

  // When selectedSubjectId changes or modal opens, update form inputs
  useEffect(() => {
    if (currentGoal) {
      setTargetScore(currentGoal.targetScore.toString());
      setRemainingDays(currentGoal.remainingDays?.toString() ?? "30");
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
      studentId,
      subjectId,
      request,
    }: {
      studentId: string;
      subjectId: string;
      request: UpsertStudentSubjectGoalRequest;
    }) => {
      return await organizationApi.upsertStudentSubjectGoal(studentId, subjectId, request);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["studentDetail", student?.studentId] });
      await queryClient.invalidateQueries({ queryKey: ["students"] });
      setFeedback({
        type: "success",
        message: "Đã cập nhật mục tiêu môn học thành công. Dữ liệu Digital Twin đã được đồng bộ.",
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

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!student || !selectedSubjectId) return;

    const score = parseFloat(targetScore);
    if (Number.isNaN(score) || score < 0 || score > 10) {
      setFeedback({
        type: "error",
        message: "Điểm mục tiêu phải là số từ 0.0 đến 10.0.",
      });
      return;
    }

    const days = parseInt(remainingDays, 10);
    if (Number.isNaN(days) || days < 1 || days > 365) {
      setFeedback({
        type: "error",
        message: "Thời gian còn lại phải từ 1 đến 365 ngày.",
      });
      return;
    }

    setFeedback(null);
    upsertMutation.mutate({
      studentId: student.studentId,
      subjectId: selectedSubjectId,
      request: {
        targetScore: score,
        remainingDays: days,
        rowVersion: currentGoal?.rowVersion,
      },
    });
  };

  const handleReload = async () => {
    if (student) {
      await queryClient.invalidateQueries({ queryKey: ["studentDetail", student.studentId] });
      setFeedback(null);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      title="Mục tiêu môn học (Digital Twin)"
      description={student ? `Thiết lập mục tiêu điểm số và thời hạn cho học sinh ${student.fullName} (@${student.username})` : undefined}
      onClose={onClose}
      maxWidth="max-w-xl"
    >
      <div className="space-y-5">
        {feedback?.type === "conflict" && (
          <ConcurrencyBanner onReload={handleReload} isReloading={upsertMutation.isPending} />
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
              <p className="mt-1 font-mono text-xs opacity-75">Mã lỗi: {feedback.traceId}</p>
            )}
          </div>
        )}

        {/* Existing Goals Overview */}
        {student?.subjectGoals && student.subjectGoals.length > 0 && (
          <div>
            <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)]">
              Mục tiêu hiện có ({student.subjectGoals.length})
            </h3>
            <div className="mt-2 divide-y divide-[var(--cm-border-subtle)] rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)]">
              {student.subjectGoals.map((goal) => (
                <div
                  key={goal.subjectId}
                  className="flex items-center justify-between p-3 text-sm"
                >
                  <div>
                    <span className="font-medium text-[var(--cm-text)]">
                      {goal.subjectName || goal.subjectCode || goal.subjectId}
                    </span>
                    {goal.remainingDays !== undefined && (
                      <span className="ml-2 text-xs text-[var(--cm-text-muted)]">
                        · Còn {goal.remainingDays} ngày
                      </span>
                    )}
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
                      className="text-xs text-indigo-400 hover:text-indigo-300"
                    >
                      Chọn
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Upsert Form */}
        <form onSubmit={handleSubmit} className="space-y-4 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-4">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)]">
            {currentGoal ? "Cập nhật mục tiêu môn đã chọn" : "Thêm mục tiêu môn mới"}
          </h3>

          <div>
            <label htmlFor="goal-subject-select" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
              Môn học <span className="text-rose-400">*</span>
            </label>
            <select
              id="goal-subject-select"
              value={selectedSubjectId}
              onChange={(e) => setSelectedSubjectId(e.target.value)}
              disabled={isLoadingSubjects || upsertMutation.isPending}
              className="cm-field mt-1 w-full px-3 text-sm"
              required
            >
              {subjects.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  {sub.subjectName} ({sub.subjectCode})
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="goal-target-score" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                Điểm mục tiêu (0.0 - 10.0) <span className="text-rose-400">*</span>
              </label>
              <input
                type="number"
                id="goal-target-score"
                step="0.1"
                min="0"
                max="10"
                value={targetScore}
                onChange={(e) => setTargetScore(e.target.value)}
                disabled={upsertMutation.isPending}
                className="cm-field mt-1 w-full px-3 text-sm"
                required
              />
            </div>

            <div>
              <label htmlFor="goal-remaining-days" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                Thời hạn (ngày) <span className="text-rose-400">*</span>
              </label>
              <input
                type="number"
                id="goal-remaining-days"
                min="1"
                max="365"
                value={remainingDays}
                onChange={(e) => setRemainingDays(e.target.value)}
                disabled={upsertMutation.isPending}
                className="cm-field mt-1 w-full px-3 text-sm"
                required
              />
            </div>
          </div>

          {currentGoal?.rowVersion && (
            <p className="text-[11px] font-mono text-[var(--cm-text-muted)]">
              Phiên bản dữ liệu (RowVersion): {currentGoal.rowVersion}
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
              disabled={upsertMutation.isPending}
              className="cm-primary-button text-sm"
            >
              {upsertMutation.isPending ? "Đang lưu..." : currentGoal ? "Cập nhật mục tiêu" : "Thiết lập mục tiêu"}
            </button>
          </div>
        </form>
      </div>
    </Modal>
  );
};
