import React, { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getLearningAgenda } from "../../api/learningPathApi";
import type { AgendaItemDto, LearningAgendaDto } from "../../types/learningPath";

interface LearningAgendaSectionProps {
  subjectId: string;
  onStartMission: (mission: AgendaItemDto) => void;
  onResumeRun: (sessionRunId: number, mission: AgendaItemDto) => void;
}

export const LearningAgendaSection: React.FC<LearningAgendaSectionProps> = ({
  subjectId,
  onStartMission,
  onResumeRun,
}) => {
  const [selectedMinutes, setSelectedMinutes] = useState<number>(15);

  const { data: agendaData, isLoading, error } = useQuery<LearningAgendaDto>({
    queryKey: ["learning-agenda", subjectId, selectedMinutes],
    queryFn: () => getLearningAgenda(subjectId, selectedMinutes),
    enabled: !!subjectId,
    staleTime: 60 * 1000,
  });

  const agenda = agendaData;
  const primary = agenda?.primaryMission;
  const alternatives = agenda?.alternativeOptions || [];
  const ongoing = agenda?.ongoingRuns || [];
  const reviewTasks = agenda?.reviewDueTasks || [];

  return (
    <div className="space-y-6">
      {/* Time Budget Selector Bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs">
        <div>
          <h3 className="text-base sm:text-lg font-black text-slate-900 dark:text-white flex items-center gap-2">
            <span>📅</span> Kế Hoạch Hôm Nay (Adaptive Agenda)
          </h3>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5 font-medium">
            Hệ thống tự động đề xuất phiên học phù hợp nhất với quỹ thời gian thực tế của bạn hôm nay.
          </p>
        </div>

        {/* Time Budget Buttons */}
        <div className="flex items-center gap-2 bg-slate-100 dark:bg-slate-800 p-1.5 rounded-2xl shrink-0">
          {[
            { minutes: 5, label: "5 phút", desc: "Ôn nhanh" },
            { minutes: 15, label: "15 phút", desc: "Tiêu chuẩn" },
            { minutes: 30, label: "30 phút", desc: "Chuyên sâu" },
          ].map((b) => (
            <button
              key={b.minutes}
              type="button"
              onClick={() => setSelectedMinutes(b.minutes)}
              className={`px-3.5 py-1.5 rounded-xl text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedMinutes === b.minutes
                  ? "bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-400 shadow-xs font-black"
                  : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white"
              }`}
            >
              <span>{b.label}</span>
              <span className="text-[10px] opacity-70">({b.desc})</span>
            </button>
          ))}
        </div>
      </div>

      {isLoading ? (
        <div className="h-48 bg-slate-100 dark:bg-slate-800 rounded-3xl animate-pulse" />
      ) : error ? (
        <div className="p-4 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-xs text-rose-700 dark:text-rose-300">
          Không thể tải kế hoạch học tập hôm nay. Vui lòng thử lại.
        </div>
      ) : (
        <div className="space-y-6">
          {/* Ongoing sessions warning if student has an incomplete mission */}
          {ongoing.length > 0 && (
            <div className="p-4 rounded-2xl bg-indigo-50 dark:bg-indigo-950/50 border border-indigo-200 dark:border-indigo-800 flex items-center justify-between gap-4">
              <div className="flex items-center gap-3">
                <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-indigo-600 text-white text-xs font-bold shrink-0">
                  ⚡
                </span>
                <div>
                  <h4 className="text-xs font-black text-indigo-900 dark:text-indigo-200">
                    Bạn có {ongoing.length} bài học đang làm dở
                  </h4>
                  <p className="text-[11px] text-indigo-700 dark:text-indigo-300">
                    {ongoing[0].title} · {ongoing[0].skillName}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => {
                  if (ongoing[0].activeRunId) {
                    onResumeRun(ongoing[0].activeRunId, ongoing[0]);
                  } else {
                    onStartMission(ongoing[0]);
                  }
                }}
                className="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-xs transition-colors shrink-0 cursor-pointer"
              >
                Tiếp tục bài này →
              </button>
            </div>
          )}

          {/* Primary Mission Card */}
          {primary ? (
            <div className="rounded-3xl bg-gradient-to-br from-indigo-600 via-indigo-700 to-purple-800 text-white p-6 sm:p-8 shadow-xl shadow-indigo-600/10 space-y-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-black bg-white/20 backdrop-blur-xs text-white">
                  <span>⚡</span> Nhiệm vụ ưu tiên số 1 hôm nay
                </div>
                <span className="px-3 py-1 rounded-full text-xs font-black bg-white/10 text-indigo-100 border border-white/20">
                  ⏱ Ước tính: {primary.estimatedDurationMinutes} phút
                </span>
              </div>

              <div className="space-y-2">
                <h3 className="text-xl sm:text-2xl font-black text-white">
                  {primary.title}
                </h3>
                <p className="text-xs sm:text-sm text-indigo-100 font-medium leading-relaxed max-w-3xl">
                  {primary.objective}
                </p>
                <div className="pt-1 flex items-center gap-2 text-xs text-indigo-200">
                  <span className="font-bold">Kỹ năng mục tiêu:</span>
                  <span className="px-2 py-0.5 rounded-md bg-white/10 text-white font-black text-[11px]">
                    {primary.skillName} ({primary.skillId})
                  </span>
                </div>
              </div>

              {/* Activity flow sequence preview */}
              <div className="pt-2 border-t border-white/20">
                <span className="text-[11px] font-bold text-indigo-200 uppercase tracking-wider block mb-2">
                  Chuỗi hoạt động tương tác (V4 Rich Flow):
                </span>
                <div className="flex flex-wrap items-center gap-2">
                  {primary.activityTypes.map((act, idx) => (
                    <div
                      key={act}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-white/15 backdrop-blur-xs text-xs font-bold text-white border border-white/10"
                    >
                      <span className="text-[10px] opacity-80">{idx + 1}.</span>
                      <span>
                        {act === "WorkedExample"
                          ? "Ví dụ mẫu có phản chiếu"
                          : act === "FindError"
                          ? "Tìm lỗi sai trong lời giải"
                          : act === "IndependentCheck"
                          ? "Luyện tập độc lập (No hints)"
                          : act === "TransferChallenge"
                          ? "Thử thách vận dụng thực tế"
                          : act}
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="pt-2 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <p className="text-xs text-indigo-100/90 italic font-medium">
                  💡 Lý do đề xuất: {primary.recommendationReason}
                </p>

                <button
                  type="button"
                  onClick={() => onStartMission(primary)}
                  className="px-7 py-3 rounded-2xl bg-white hover:bg-slate-50 text-indigo-950 text-xs sm:text-sm font-black shadow-lg shadow-black/10 transition-all cursor-pointer flex items-center justify-center gap-2 shrink-0"
                >
                  <span>🚀</span>
                  <span>Bắt đầu nhiệm vụ ngay</span>
                </button>
              </div>
            </div>
          ) : (
            <div className="p-8 rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200 dark:border-slate-800 text-center space-y-2">
              <span className="text-3xl">🎉</span>
              <h4 className="text-base font-black text-slate-800 dark:text-slate-200">
                Bạn đã hoàn thành các nhiệm vụ cần thiết!
              </h4>
              <p className="text-xs text-slate-500">
                Hãy chuyển sang mức thời lượng 30 phút hoặc xem lại bản đồ năng lực để chọn thử thách tiếp theo.
              </p>
            </div>
          )}

          {/* Alternative options */}
          {alternatives.length > 0 && (
            <div className="space-y-3">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                Hoặc chọn nhiệm vụ thay thế:
              </h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {alternatives.map((alt) => (
                  <div
                    key={alt.missionId}
                    className="rounded-2xl p-4 border border-slate-200 dark:border-slate-800 bg-white dark:bg-[#0f172a] shadow-xs flex flex-col justify-between space-y-3"
                  >
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
                          {alt.skillName}
                        </span>
                        <span className="text-xs font-bold text-slate-500">
                          ⏱ {alt.estimatedDurationMinutes} phút
                        </span>
                      </div>
                      <h5 className="text-sm font-black text-slate-900 dark:text-white">
                        {alt.title}
                      </h5>
                      <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-2">
                        {alt.objective}
                      </p>
                    </div>

                    <div className="pt-2 flex items-center justify-between">
                      <span className="text-[10px] text-slate-400 italic">
                        {alt.recommendationReason}
                      </span>
                      <button
                        type="button"
                        onClick={() => onStartMission(alt)}
                        className="px-3.5 py-1.5 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-indigo-50 hover:text-indigo-600 text-xs font-bold text-slate-700 dark:text-slate-300 transition-colors cursor-pointer"
                      >
                        Chọn bài này →
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Review Due spaced repetition tasks */}
          {reviewTasks.length > 0 && (
            <div className="p-4 rounded-2xl bg-rose-50/70 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900 space-y-2">
              <div className="flex items-center gap-2 text-rose-900 dark:text-rose-200 font-bold text-xs">
                <span>🔄</span>
                <span>Ôn tập ngắt quãng (Spaced Repetition)</span>
              </div>
              <p className="text-xs text-rose-700 dark:text-rose-300">
                Có {reviewTasks.length} kỹ năng đã đến thời hạn cần củng cố để duy trì phong độ.
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
