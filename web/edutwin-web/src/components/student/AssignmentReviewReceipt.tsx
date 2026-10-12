import { formatReviewDuration } from "../../utils/assignmentReviewTiming";

export function AssignmentReviewReceipt({ submittedAt, elapsedSeconds }: {
  submittedAt: string | null; elapsedSeconds: number | null;
}) {
  return (
    <div className="rounded-lg border border-emerald-300 dark:border-emerald-700 bg-emerald-50 dark:bg-emerald-950/40 px-3 py-1.5 text-xs text-emerald-800 dark:text-emerald-200">
      <span className="font-bold">✓ Đã nộp bài</span>
      {submittedAt && <span className="ml-2">Lúc {new Date(submittedAt).toLocaleString("vi-VN")}</span>}
      {elapsedSeconds !== null && <span className="ml-2 font-mono">Thời gian làm bài: {formatReviewDuration(elapsedSeconds)}</span>}
    </div>
  );
}
