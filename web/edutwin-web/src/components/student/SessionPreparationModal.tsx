import React from "react";
import type { LearningPathSessionDto } from "../../types/learningPath";

interface SessionPreparationModalProps {
  isOpen: boolean;
  onClose: () => void;
  session: LearningPathSessionDto | null;
  subjectName: string;
  onStart: (sessionId: string) => Promise<void>;
  isStarting: boolean;
  error?: string | null;
}

export const SessionPreparationModal: React.FC<SessionPreparationModalProps> = ({
  isOpen,
  onClose,
  session,
  subjectName,
  onStart,
  isStarting,
  error,
}) => {
  if (!isOpen || !session) return null;

  const requiredRate = Math.round((session.requiredCorrectRate || 0.8) * 100);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="prep-modal-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs overflow-y-auto animate-in fade-in duration-200"
    >
      <div className="relative w-full max-w-2xl bg-white dark:bg-[#0f172a] rounded-3xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden text-slate-900 dark:text-slate-100">
        {/* Header gradient banner */}
        <div className="bg-gradient-to-r from-indigo-600 via-indigo-700 to-purple-700 p-6 text-white">
          <div className="flex items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-2">
                <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wider bg-white/20 text-white backdrop-blur-xs">
                  {subjectName} · Buổi học thích ứng
                </span>
                <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-amber-400 text-slate-950">
                  ⏱ {session.estimatedMinutes} phút
                </span>
              </div>
              <h2 id="prep-modal-title" className="mt-2 text-xl sm:text-2xl font-black text-white">
                {session.title}
              </h2>
            </div>
            <button
              type="button"
              onClick={onClose}
              disabled={isStarting}
              className="p-1.5 rounded-xl bg-white/10 hover:bg-white/20 text-white transition-colors cursor-pointer"
              aria-label="Đóng"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>

        {/* Content Body */}
        <div className="p-6 sm:p-8 space-y-6 max-h-[75vh] overflow-y-auto">
          {error && (
            <div className="p-4 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2.5">
              <span>⚠️</span>
              <span>{error}</span>
            </div>
          )}

          {/* 1. Vì sao nên học phiên này */}
          <div className="space-y-1.5">
            <h3 className="text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400 flex items-center gap-1.5">
              <span>💡</span> Vì sao nên học buổi này?
            </h3>
            <div className="p-4 rounded-2xl bg-indigo-50/70 dark:bg-indigo-950/30 border border-indigo-100 dark:border-indigo-900/50 text-xs sm:text-sm text-slate-700 dark:text-slate-300 leading-relaxed font-medium">
              Chủ đề <strong>{session.topicName}</strong> là mấu chốt được EduTwin đề xuất nhằm củng cố nền tảng kiến thức, giải quyết các lỗ hổng phát hiện từ đồ thị tri thức và nâng cao độ thành thạo mục tiêu ({session.targetMastery}%).
            </div>
          </div>

          {/* 2. Mục tiêu cần đạt */}
          <div className="space-y-1.5">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
              <span>🎯</span> Mục tiêu cần đạt
            </h3>
            <p className="text-sm font-semibold text-slate-800 dark:text-slate-200 pl-1">
              {session.objective}
            </p>
          </div>

          {/* 3. Cấu trúc hoạt động & Checkpoint */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/80 dark:border-slate-800 space-y-2">
              <span className="text-xs font-bold text-slate-900 dark:text-white flex items-center gap-1.5">
                <span>📝</span> Hoạt động trên EduTwin
              </span>
              <ul className="text-xs text-slate-600 dark:text-slate-400 space-y-1 list-disc pl-4">
                {session.onlineActivities.length > 0 ? (
                  session.onlineActivities.map((act) => <li key={act}>{act}</li>)
                ) : (
                  <li>Luyện tập câu hỏi thích ứng theo tiến độ cá nhân</li>
                )}
              </ul>
            </div>

            <div className="p-4 rounded-2xl bg-amber-50/60 dark:bg-amber-950/30 border border-amber-200/70 dark:border-amber-900/40 space-y-2">
              <span className="text-xs font-bold text-amber-900 dark:text-amber-300 flex items-center gap-1.5">
                <span>🏁</span> Checkpoint kiểm chứng năng lực
              </span>
              <p className="text-xs text-amber-800 dark:text-amber-300 leading-relaxed">
                Cuối buổi sẽ có câu hỏi <strong>Checkpoint độc lập</strong>. Tại câu hỏi này, bạn phải tự giải độc lập — <strong>không có gợi ý hoặc giải pháp AI</strong> để chứng minh thực lực.
              </p>
            </div>
          </div>

          {/* 4. Điều kiện hoàn thành */}
          <div className="p-4 rounded-2xl bg-emerald-50/60 dark:bg-emerald-950/30 border border-emerald-200/70 dark:border-emerald-900/40 space-y-2">
            <span className="text-xs font-bold text-emerald-900 dark:text-emerald-300 flex items-center gap-1.5">
              <span>⚖️</span> Tiêu chí đánh giá & Cập nhật Digital Twin
            </span>
            <div className="text-xs text-emerald-800 dark:text-emerald-300 space-y-1">
              <div>
                • Tỷ lệ đạt câu hỏi Checkpoint: <strong>≥ {requiredRate}%</strong>.
              </div>
              {session.completionCriteria.map((c) => (
                <div key={c}>• {c}</div>
              ))}
              <div className="text-[11px] text-emerald-700 dark:text-emerald-400 italic pt-1">
                Bằng chứng chỉ được Evidence Gate ghi nhận và nâng điểm Hồ sơ Năng lực (Mastery) khi vượt qua Checkpoint độc lập một cách đáng tin cậy.
              </div>
            </div>
          </div>
        </div>

        {/* Footer actions */}
        <div className="p-6 bg-slate-50 dark:bg-slate-900/60 border-t border-slate-200 dark:border-slate-800 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            disabled={isStarting}
            className="px-5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 text-xs sm:text-sm font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
          >
            Để sau
          </button>
          <button
            type="button"
            onClick={() => onStart(session.sessionId)}
            disabled={isStarting}
            className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 active:scale-95 text-white text-xs sm:text-sm font-bold shadow-md shadow-indigo-500/20 transition-all flex items-center gap-2 cursor-pointer disabled:opacity-50"
          >
            {isStarting ? (
              <>
                <svg className="animate-spin h-4 w-4 text-white" viewBox="0 0 24 24" fill="none">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
                <span>Đang khởi tạo phiên...</span>
              </>
            ) : (
              <>
                <span>🚀</span>
                <span>Bắt đầu học ngay</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
