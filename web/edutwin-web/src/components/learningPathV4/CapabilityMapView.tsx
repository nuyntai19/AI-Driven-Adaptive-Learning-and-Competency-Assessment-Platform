import React from "react";
import type { CapabilityMapDto, SkillCapabilityStatusDto } from "../../types/learningPath";

interface CapabilityMapViewProps {
  capabilityMap: CapabilityMapDto | null;
  isLoading: boolean;
  onOpenGoalModal: () => void;
  onStartDiagnostic: (skillId: string) => void;
  onLearnSkill: (skillId: string) => void;
  subjectName?: string;
}

export const CapabilityMapView: React.FC<CapabilityMapViewProps> = ({
  capabilityMap,
  isLoading,
  onOpenGoalModal,
  onStartDiagnostic,
  onLearnSkill,
  subjectName = "Toán học",
}) => {
  if (isLoading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 bg-slate-100 dark:bg-slate-800 rounded-3xl" />
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
            <div key={i} className="h-44 bg-slate-100 dark:bg-slate-800 rounded-2xl" />
          ))}
        </div>
      </div>
    );
  }

  const skills = capabilityMap?.skills || [];
  const provenCount = capabilityMap?.provenCapabilities?.length || 0;
  const supportCount = capabilityMap?.areasNeedingSupport?.length || 0;
  const reviewCount = capabilityMap?.reviewDueSkills?.length || 0;
  const unknownCount = skills.filter((s) => s.state === "Unknown").length;

  const renderStateBadge = (state: string) => {
    switch (state) {
      case "Confirmed":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-black bg-emerald-100 text-emerald-800 dark:bg-emerald-950/70 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-700">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            Đã xác nhận vững
          </span>
        );
      case "Demonstrated":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-black bg-teal-100 text-teal-800 dark:bg-teal-950/70 dark:text-teal-300 border border-teal-300 dark:border-teal-700">
            <span className="w-1.5 h-1.5 rounded-full bg-teal-500" />
            Đã làm chủ
          </span>
        );
      case "Developing":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-black bg-amber-100 text-amber-800 dark:bg-amber-950/70 dark:text-amber-300 border border-amber-300 dark:border-amber-700 animate-pulse">
            <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
            Đang phát triển
          </span>
        );
      case "ReviewDue":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-black bg-rose-100 text-rose-800 dark:bg-rose-950/70 dark:text-rose-300 border border-rose-300 dark:border-rose-700">
            <span className="w-1.5 h-1.5 rounded-full bg-rose-500" />
            Đến hạn ôn tập
          </span>
        );
      case "Unknown":
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-black bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border border-slate-300 dark:border-slate-600">
            <span className="w-1.5 h-1.5 rounded-full bg-slate-400" />
            Chưa có bằng chứng
          </span>
        );
    }
  };

  const renderAvailabilityBadge = (avail: string) => {
    switch (avail) {
      case "Available":
        return (
          <span className="text-[10px] font-bold text-emerald-600 dark:text-emerald-400">
            ✓ Sẵn sàng học
          </span>
        );
      case "PrerequisiteBlocked":
        return (
          <span className="text-[10px] font-bold text-slate-400 dark:text-slate-500">
            🔒 Cần học kỹ năng tiên quyết
          </span>
        );
      default:
        return null;
    }
  };

  return (
    <div className="space-y-6">
      {/* Capability Overview Banner */}
      <div className="rounded-3xl bg-gradient-to-br from-indigo-900 via-slate-900 to-indigo-950 text-white p-6 sm:p-8 shadow-xl border border-indigo-800/40 relative overflow-hidden">
        <div className="absolute right-0 top-0 translate-x-8 -translate-y-8 w-64 h-64 bg-indigo-500/10 rounded-full blur-3xl pointer-events-none" />
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-6 relative z-10">
          <div className="space-y-2">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-bold bg-indigo-500/20 text-indigo-200 border border-indigo-400/30">
              <span>🧭</span> Bản đồ năng lực cá nhân hóa V4.0
            </div>
            <h2 className="text-2xl sm:text-3xl font-black tracking-tight text-white">
              Hồ Sơ Năng Lực {subjectName}
            </h2>
            <p className="text-xs sm:text-sm text-indigo-200/80 max-w-xl">
              Hệ thống theo dõi độc lập 8 năng lực toán học chuẩn dựa trên dữ liệu chẩn đoán thực tế,
              không giả định bất kỳ năng lực nào khi chưa có bằng chứng.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={onOpenGoalModal}
              className="px-5 py-2.5 rounded-xl bg-indigo-500 hover:bg-indigo-400 text-white text-xs sm:text-sm font-black shadow-lg shadow-indigo-500/25 transition-all cursor-pointer flex items-center gap-2"
            >
              <span>🎯</span>
              <span>Đặt mục tiêu & Nhận đề xuất</span>
            </button>
          </div>
        </div>

        {/* Quick summary counters */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-6 border-t border-indigo-800/50 mt-6 text-center">
          <div className="rounded-2xl bg-white/5 p-3.5 border border-white/10 backdrop-blur-xs">
            <span className="text-[11px] font-bold text-emerald-300 block">Đã làm chủ / Xác nhận</span>
            <span className="text-2xl font-black text-white">{provenCount} / {skills.length}</span>
          </div>
          <div className="rounded-2xl bg-white/5 p-3.5 border border-white/10 backdrop-blur-xs">
            <span className="text-[11px] font-bold text-amber-300 block">Cần hỗ trợ</span>
            <span className="text-2xl font-black text-white">{supportCount}</span>
          </div>
          <div className="rounded-2xl bg-white/5 p-3.5 border border-white/10 backdrop-blur-xs">
            <span className="text-[11px] font-bold text-rose-300 block">Đến hạn ôn tập</span>
            <span className="text-2xl font-black text-white">{reviewCount}</span>
          </div>
          <div className="rounded-2xl bg-white/5 p-3.5 border border-white/10 backdrop-blur-xs">
            <span className="text-[11px] font-bold text-slate-300 block">Chưa có bằng chứng</span>
            <span className="text-2xl font-black text-white">{unknownCount}</span>
          </div>
        </div>
      </div>

      {/* Unknown Skills Prompt (AC V4-06) */}
      {unknownCount > 0 && (
        <div className="rounded-2xl bg-amber-50 dark:bg-amber-950/40 p-4 sm:p-5 border border-amber-200 dark:border-amber-800 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-amber-900 dark:text-amber-200 font-bold text-sm">
              <span>⚠️</span>
              <span>Phát hiện {unknownCount} kỹ năng chưa có dữ liệu chẩn đoán (Unknown Baseline)</span>
            </div>
            <p className="text-xs text-amber-700 dark:text-amber-300 leading-relaxed">
              Theo nguyên tắc chuẩn đoán V4: hệ thống không tự động cho qua khi chưa có bằng chứng.
              Bạn có thể làm bài chẩn đoán nhanh (Diagnostic Probe) hoặc chọn học ngay.
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <button
              type="button"
              onClick={() => onStartDiagnostic("MATH-EQ-01")}
              className="px-4 py-2 rounded-xl bg-amber-600 hover:bg-amber-700 text-white text-xs font-black shadow-xs transition-colors cursor-pointer"
            >
              📝 Làm bài chẩn đoán nhanh
            </button>
          </div>
        </div>
      )}

      {/* Skills Grid (8 Standard Pilot Skills) */}
      <div>
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-base sm:text-lg font-black text-slate-900 dark:text-white flex items-center gap-2">
            <span>📚</span> 8 Kỹ Năng Trọng Tâm (Chuẩn V4 Pilot)
          </h3>
          <span className="text-xs font-bold text-slate-500 dark:text-slate-400">
            Tiến độ hoàn thành: {capabilityMap?.overallProgressRate || 0}%
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {skills.map((skill: SkillCapabilityStatusDto) => {
            const isBlocked = skill.availability === "PrerequisiteBlocked";
            return (
              <div
                key={skill.skillId}
                className={`rounded-2xl p-5 border transition-all flex flex-col justify-between ${
                  isBlocked
                    ? "bg-slate-50/70 dark:bg-slate-900/40 border-slate-200 dark:border-slate-800 opacity-80"
                    : "bg-white dark:bg-[#0f172a] border-slate-200 dark:border-slate-800 shadow-xs hover:border-indigo-400 dark:hover:border-indigo-600 hover:shadow-md"
                }`}
              >
                <div className="space-y-3">
                  <div className="flex items-start justify-between gap-2">
                    <span className="text-[10px] font-black uppercase tracking-wider px-2 py-0.5 rounded-md bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 border border-indigo-100 dark:border-indigo-900">
                      {skill.skillId}
                    </span>
                    {renderStateBadge(skill.state)}
                  </div>

                  <div>
                    <h4 className="text-sm font-black text-slate-900 dark:text-white leading-snug">
                      {skill.skillName}
                    </h4>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5 font-medium">
                      Chủ đề: {skill.topicName}
                    </p>
                    <p className="text-xs text-slate-600 dark:text-slate-300 mt-2 line-clamp-2 leading-relaxed">
                      {skill.description}
                    </p>
                  </div>

                  <div className="pt-2 border-t border-slate-100 dark:border-slate-800/80 space-y-1">
                    <div className="flex items-center justify-between text-[11px]">
                      <span className="text-slate-400 font-medium">Điểm làm chủ:</span>
                      <span className="font-bold text-slate-700 dark:text-slate-300">
                        {skill.masteryScore !== null && skill.masteryScore !== undefined
                          ? `${Math.round(skill.masteryScore * 100)}%`
                          : "Chưa có"}
                      </span>
                    </div>
                    <div className="flex items-center justify-between text-[11px]">
                      <span className="text-slate-400 font-medium">Số bằng chứng:</span>
                      <span className="font-bold text-slate-700 dark:text-slate-300">
                        {skill.evidenceCount} lần kiểm tra
                      </span>
                    </div>
                    {renderAvailabilityBadge(skill.availability)}
                  </div>
                </div>

                <div className="pt-4 mt-2">
                  {skill.state === "Unknown" ? (
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => onStartDiagnostic(skill.skillId)}
                        className="flex-1 py-2 px-3 rounded-xl bg-amber-500 hover:bg-amber-600 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer text-center"
                      >
                        Chẩn đoán
                      </button>
                      <button
                        type="button"
                        disabled={isBlocked}
                        onClick={() => onLearnSkill(skill.skillId)}
                        className="py-2 px-3 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 hover:bg-indigo-100 dark:hover:bg-indigo-900 text-indigo-700 dark:text-indigo-300 text-xs font-bold border border-indigo-200 dark:border-indigo-800 transition-colors disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer text-center"
                      >
                        Học luôn
                      </button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      disabled={isBlocked}
                      onClick={() => onLearnSkill(skill.skillId)}
                      className={`w-full py-2 px-3 rounded-xl text-xs font-bold transition-all text-center ${
                        isBlocked
                          ? "bg-slate-100 dark:bg-slate-800 text-slate-400 cursor-not-allowed"
                          : skill.state === "ReviewDue"
                          ? "bg-rose-600 hover:bg-rose-700 text-white shadow-xs cursor-pointer"
                          : "bg-indigo-600 hover:bg-indigo-700 text-white shadow-xs cursor-pointer"
                      }`}
                    >
                      {skill.state === "ReviewDue"
                        ? "🔄 Ôn tập ngay"
                        : skill.state === "Confirmed"
                        ? "⭐ Luyện nâng cao"
                        : "▶ Bắt đầu luyện tập"}
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
