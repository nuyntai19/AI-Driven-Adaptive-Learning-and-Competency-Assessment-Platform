import React from "react";
import type { EvaluateSessionDto } from "../../types/learningPath";

interface SessionEvaluationOutcomeModalProps {
  isOpen: boolean;
  outcomeData: EvaluateSessionDto | null;
  subjectId: string;
  isPolling?: boolean;
  onRetrySession: () => void;
  onNextSession: () => void;
  onClose: () => void;
}

export const SessionEvaluationOutcomeModal: React.FC<SessionEvaluationOutcomeModalProps> = ({
  isOpen,
  outcomeData,
  isPolling = false,
  onRetrySession,
  onNextSession,
  onClose,
}) => {
  if (!isOpen || !outcomeData) return null;

  const outcome = outcomeData.outcome || (outcomeData.hasPendingAIGrading ? "Grading" : outcomeData.passed ? "Passed" : "NotPassed");
  const isGrading = outcome === "Grading" || outcomeData.hasPendingAIGrading || isPolling;
  const isNeedsReview = outcome === "NeedsReview" || outcomeData.requiresTeacherReview;
  const isPassed = !isGrading && !isNeedsReview && outcome === "Passed";
  const isInsufficient = outcome === "InsufficientEvidence" || outcome === "InsufficientQuestionPool";
  const isNotPassed = !isGrading && !isNeedsReview && !isPassed && !isInsufficient;

  const checkpointRate = Math.round((outcomeData.checkpointScoreRate || 0) * 100);
  const overallRate = Math.round((outcomeData.overallScoreRate || 0) * 100);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="outcome-modal-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/70 backdrop-blur-xs overflow-y-auto animate-in fade-in duration-200"
    >
      <div className="relative w-full max-w-xl bg-white dark:bg-[#0f172a] rounded-3xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden text-slate-900 dark:text-slate-100">
        {/* Banner based on outcome */}
        {isGrading && (
          <div className="bg-gradient-to-r from-sky-600 to-indigo-600 p-6 text-white text-center">
            <div className="w-14 h-14 mx-auto rounded-2xl bg-white/20 backdrop-blur-xs flex items-center justify-center text-3xl mb-3 animate-pulse">
              🤖
            </div>
            <h2 id="outcome-modal-title" className="text-xl sm:text-2xl font-black">
              Hệ thống đang chấm bài...
            </h2>
            <p className="mt-1 text-xs sm:text-sm text-sky-100 font-medium">
              AI Reasoning Engine đang đối soát lập luận và phân tích phương pháp giải
            </p>
          </div>
        )}

        {isNeedsReview && (
          <div className="bg-gradient-to-r from-amber-600 to-orange-600 p-6 text-white text-center">
            <div className="w-14 h-14 mx-auto rounded-2xl bg-white/20 backdrop-blur-xs flex items-center justify-center text-3xl mb-3">
              ⏳
            </div>
            <h2 id="outcome-modal-title" className="text-xl sm:text-2xl font-black">
              Đang chờ giáo viên xác nhận
            </h2>
            <p className="mt-1 text-xs sm:text-sm text-amber-100 font-medium">
              Bài làm có bước lập luận đặc thù cần giáo viên bộ môn xem xét trước khi hoàn tất
            </p>
          </div>
        )}

        {isPassed && (
          <div className="bg-gradient-to-r from-emerald-600 to-teal-600 p-6 text-white text-center">
            <div className="w-14 h-14 mx-auto rounded-2xl bg-white/20 backdrop-blur-xs flex items-center justify-center text-3xl mb-3">
              🎉
            </div>
            <h2 id="outcome-modal-title" className="text-xl sm:text-2xl font-black">
              Xuất sắc! Đạt chuẩn phiên học
            </h2>
            <p className="mt-1 text-xs sm:text-sm text-emerald-100 font-medium">
              Bạn đã vượt qua Checkpoint độc lập và đủ điều kiện thăng hạng năng lực
            </p>
          </div>
        )}

        {isNotPassed && (
          <div className="bg-gradient-to-r from-rose-600 to-pink-600 p-6 text-white text-center">
            <div className="w-14 h-14 mx-auto rounded-2xl bg-white/20 backdrop-blur-xs flex items-center justify-center text-3xl mb-3">
              🎯
            </div>
            <h2 id="outcome-modal-title" className="text-xl sm:text-2xl font-black">
              Chưa đạt tiêu chí buổi học
            </h2>
            <p className="mt-1 text-xs sm:text-sm text-rose-100 font-medium">
              Cần luyện tập thêm để củng cố các lỗ hổng kiến thức trước khi đi tiếp
            </p>
          </div>
        )}

        {isInsufficient && (
          <div className="bg-gradient-to-r from-slate-600 to-slate-700 p-6 text-white text-center">
            <div className="w-14 h-14 mx-auto rounded-2xl bg-white/20 backdrop-blur-xs flex items-center justify-center text-3xl mb-3">
              ℹ️
            </div>
            <h2 id="outcome-modal-title" className="text-xl sm:text-2xl font-black">
              Chưa đủ bằng chứng đánh giá
            </h2>
            <p className="mt-1 text-xs sm:text-sm text-slate-200 font-medium">
              Ngân hàng câu hỏi hiện chưa đủ câu checkpoint phân loại cho nội dung này
            </p>
          </div>
        )}

        {/* Modal Body */}
        <div className="p-6 sm:p-8 space-y-6">
          {/* Real Metrics Grid */}
          <div className="grid grid-cols-3 gap-3 text-center">
            <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider block">
                Checkpoint
              </span>
              <span className={`text-xl sm:text-2xl font-black ${checkpointRate >= 80 ? "text-emerald-600 dark:text-emerald-400" : "text-rose-600 dark:text-rose-400"}`}>
                {checkpointRate}%
              </span>
              <span className="text-[10px] text-slate-500 block mt-0.5">
                {outcomeData.checkpointsCorrect} / {outcomeData.checkpointsAttempted} câu
              </span>
            </div>

            <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider block">
                Tổng thể
              </span>
              <span className="text-xl sm:text-2xl font-black text-slate-900 dark:text-white">
                {overallRate}%
              </span>
              <span className="text-[10px] text-slate-500 block mt-0.5">
                Toàn bộ phiên
              </span>
            </div>

            <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider block">
                Đã hoàn thành
              </span>
              <span className="text-xl sm:text-2xl font-black text-indigo-600 dark:text-indigo-400">
                {outcomeData.questionsAttempted} / {outcomeData.totalQuestions}
              </span>
              <span className="text-[10px] text-slate-500 block mt-0.5">
                Lượt làm: #{outcomeData.runNumber}
              </span>
            </div>
          </div>

          {/* Details / Explanation Box */}
          {isGrading && (
            <div className="p-4 rounded-2xl bg-sky-50 dark:bg-sky-950/40 border border-sky-200 dark:border-sky-900 text-xs sm:text-sm text-sky-900 dark:text-sky-200 space-y-2">
              <div className="flex items-center gap-2 font-bold">
                <svg className="animate-spin h-4 w-4 text-sky-600" viewBox="0 0 24 24" fill="none">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
                <span>Hệ thống đang tự động kiểm tra...</span>
              </div>
              <p className="text-xs text-sky-700 dark:text-sky-300">
                Quá trình chấm bài thông minh đang diễn ra. Khi AI phân tích xong, kết quả sẽ tự động hiển thị mà không cần tải lại trang.
              </p>
            </div>
          )}

          {isNeedsReview && (
            <div className="p-4 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900 text-xs sm:text-sm text-amber-900 dark:text-amber-200 space-y-1.5">
              <div className="font-bold flex items-center gap-1.5">
                <span>🛡</span>
                <span>Evidence Gate giữ lại để giáo viên xem xét</span>
              </div>
              <p className="text-xs text-amber-800 dark:text-amber-300 leading-relaxed">
                Để đảm bảo độ tin cậy của Hồ sơ Năng lực số, bài làm này đã được chuyển vào hàng đợi duyệt của giáo viên. Điểm số sẽ không được ghi nhận trước khi giáo viên xác nhận.
              </p>
            </div>
          )}

          {isPassed && (
            <div className="p-4 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs sm:text-sm text-emerald-900 dark:text-emerald-200 space-y-1.5">
              <div className="font-bold flex items-center gap-1.5">
                <span>✓</span>
                <span>Hồ sơ năng lực (Digital Twin) đã được cập nhật</span>
              </div>
              <p className="text-xs text-emerald-800 dark:text-emerald-300 leading-relaxed">
                Bằng chứng học tập hợp lệ đã được Evidence Gate ghi nhận. Bạn đã mở khóa phiên học tiếp theo trong lộ trình.
              </p>
            </div>
          )}

          {isNotPassed && (
            <div className="p-4 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900 text-xs sm:text-sm text-rose-900 dark:text-rose-200 space-y-2">
              <div className="font-bold flex items-center gap-1.5">
                <span>⚠️</span>
                <span>Tiêu chí chưa đạt: Checkpoint độc lập đạt {checkpointRate}% (yêu cầu ≥ 80%)</span>
              </div>
              <p className="text-xs text-rose-800 dark:text-rose-300 leading-relaxed">
                {outcomeData.evaluationDetail || "Bạn chưa trả lời chính xác câu hỏi Checkpoint kiểm chứng năng lực. Hãy chọn 'Luyện tập lại' để hệ thống phát bộ câu hỏi mới rèn luyện lại chuyên đề này."}
              </p>
            </div>
          )}

          {isInsufficient && (
            <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 text-xs sm:text-sm text-slate-700 dark:text-slate-300 space-y-1">
              <div className="font-bold">Lưu ý về dữ liệu câu hỏi</div>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Lỗi này do ngân hàng dữ liệu chưa đủ câu hỏi phân loại, không phải do bạn làm sai. Bạn có thể tiếp tục các hoạt động khác.
              </p>
            </div>
          )}
        </div>

        {/* Modal Actions */}
        <div className="p-6 bg-slate-50 dark:bg-slate-900/60 border-t border-slate-200 dark:border-slate-800 flex flex-wrap items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="px-5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 text-xs sm:text-sm font-bold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
          >
            Quay lại lộ trình
          </button>

          {isPassed && (
            <button
              type="button"
              onClick={onNextSession}
              className="px-6 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs sm:text-sm font-bold shadow-md shadow-emerald-500/20 transition-all cursor-pointer flex items-center gap-2"
            >
              <span>Tiếp tục phiên tiếp theo</span>
              <span>→</span>
            </button>
          )}

          {isNotPassed && (
            <button
              type="button"
              onClick={onRetrySession}
              className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs sm:text-sm font-bold shadow-md shadow-indigo-500/20 transition-all cursor-pointer flex items-center gap-2"
            >
              <span>↺</span>
              <span>Luyện tập lại (Đề mới)</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
