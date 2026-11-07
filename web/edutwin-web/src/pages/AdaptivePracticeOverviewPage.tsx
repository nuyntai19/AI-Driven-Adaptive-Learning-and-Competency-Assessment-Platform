import React, { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../api/adaptivePracticeApi";
import { organizationApi } from "../api/organizationApi";
import type { AdaptivePracticeMode } from "../types/adaptivePractice";

export const AdaptivePracticeOverviewPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const selectedSubjectId = searchParams.get("subjectId") || "";
  const [selectedMode, setSelectedMode] = useState<AdaptivePracticeMode>("Weakness");
  const [selectedTopicId, setSelectedTopicId] = useState<string>("");
  const [timeBudget, setTimeBudget] = useState<number>(15);

  // 1. Fetch Subjects list
  const subjectsQuery = useQuery({
    queryKey: ["subjects", "adaptive-practice"],
    queryFn: () => organizationApi.listSubjects(true),
    staleTime: 5 * 60 * 1000,
  });
  const subjects = subjectsQuery.data?.data || [];
  const effectiveSubjectId = selectedSubjectId || (subjects.length > 0 ? subjects[0].subjectId : "");

  // 2. Fetch Active Session if any
  const activeSessionQuery = useQuery({
    queryKey: ["adaptive-practice", "active-session", effectiveSubjectId],
    queryFn: () => adaptivePracticeApi.getActiveSession(effectiveSubjectId || undefined),
    enabled: !!effectiveSubjectId,
    refetchOnWindowFocus: true,
  });
  const activeSession = activeSessionQuery.data;

  // 3. Fetch Practice Proposal
  const proposalQuery = useQuery({
    queryKey: ["adaptive-practice", "proposal", effectiveSubjectId, selectedTopicId, selectedMode],
    queryFn: () => adaptivePracticeApi.getProposal(effectiveSubjectId, selectedTopicId || undefined, selectedMode),
    enabled: !!effectiveSubjectId,
    staleTime: 30 * 1000,
  });
  const proposal = proposalQuery.data;

  // 4. Fetch Practice History
  const historyQuery = useQuery({
    queryKey: ["adaptive-practice", "history", effectiveSubjectId],
    queryFn: () => adaptivePracticeApi.getHistory(effectiveSubjectId || undefined, 8),
    enabled: !!effectiveSubjectId,
  });
  const history = historyQuery.data || [];

  // Start Session Mutation
  const createSessionMutation = useMutation({
    mutationFn: () =>
      adaptivePracticeApi.createSession({
        subjectId: effectiveSubjectId,
        topicNodeId: selectedTopicId || proposal?.topicNodeId || "",
        practiceMode: selectedMode,
        timeBudgetMinutes: timeBudget,
        customTargetMastery: proposal?.targetMastery,
      }),
    onSuccess: (session) => {
      queryClient.invalidateQueries({ queryKey: ["adaptive-practice"] });
      navigate(`/hoc-tap/luyen-tap/phien/${session.sessionId}`);
    },
  });

  const handleSubjectChange = (subjectId: string) => {
    const next = new URLSearchParams(searchParams);
    next.set("subjectId", subjectId);
    setSearchParams(next);
    setSelectedTopicId("");
  };

  const handleResumeSession = () => {
    if (activeSession) {
      navigate(`/hoc-tap/luyen-tap/phien/${activeSession.sessionId}`);
    }
  };

  return (
    <div className="w-full max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-8 min-w-0">
      {/* Header & Subject Selector */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-stone-200 dark:border-stone-800">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
              Luyện tập thích ứng
            </h1>
            <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
              AI-Driven Adaptive Engine 1.0
            </span>
          </div>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Tổ chức phiên luyện tập cá nhân hóa có mục tiêu rõ ràng, tự động điều chỉnh độ khó và tổng hợp bằng chứng năng lực.
          </p>
        </div>

        {/* Subject Switcher */}
        {subjects.length > 0 && (
          <div className="flex items-center gap-2 self-start sm:self-auto">
            <label htmlFor="subject-select" className="text-xs font-medium text-slate-600 dark:text-slate-400">
              Môn học:
            </label>
            <select
              id="subject-select"
              value={effectiveSubjectId}
              onChange={(e) => handleSubjectChange(e.target.value)}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200 focus:outline-indigo-500 shadow-2xs"
            >
              {subjects.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  {sub.subjectName} ({sub.subjectCode})
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Region A: Active Session Banner (if present) */}
      {activeSession && (
        <div className="p-5 rounded-2xl bg-gradient-to-r from-amber-500/10 via-amber-500/5 to-transparent border border-amber-300 dark:border-amber-700/60 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className="inline-block w-2.5 h-2.5 rounded-full bg-amber-500 animate-pulse" />
              <span className="text-xs font-bold uppercase tracking-wider text-amber-800 dark:text-amber-300">
                Bạn có phiên luyện tập đang thực hiện
              </span>
            </div>
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {activeSession.topicName} ({activeSession.subjectName})
            </h3>
            <p className="text-xs text-slate-600 dark:text-slate-400">
              Tiến độ: Đã làm {activeSession.questionCount} câu · Thời lượng ngân sách: {activeSession.timeBudgetMinutes} phút · Năng lực hiện tại: {activeSession.currentMastery.toFixed(0)}%
            </p>
          </div>

          <button
            type="button"
            onClick={handleResumeSession}
            className="inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl bg-amber-600 hover:bg-amber-700 text-white font-bold text-sm shadow-md hover:shadow-lg transition-all cursor-pointer shrink-0"
          >
            <span>Tiếp tục luyện tập</span>
            <span>→</span>
          </button>
        </div>
      )}

      {/* Region B & C: Main Proposal & Configuration Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column (8 cols): Today Mission / Proposal */}
        <div className="lg:col-span-8 space-y-6">
          <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-5">
            {/* Mission Header */}
            <div className="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-slate-100 dark:border-slate-800">
              <div>
                <span className="text-[11px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
                  Mục tiêu luyện tập đề xuất
                </span>
                <h2 className="text-xl font-bold text-slate-900 dark:text-white mt-0.5">
                  {proposal ? proposal.topicName : "Đang tải đề xuất..."}
                </h2>
              </div>

              {proposal?.isDiagnosticRecommended && (
                <span className="px-3 py-1 rounded-full text-xs font-bold bg-amber-100 text-amber-800 dark:bg-amber-950/80 dark:text-amber-300 border border-amber-300 dark:border-amber-700">
                  Khuyên dùng: Luyện chẩn đoán
                </span>
              )}
            </div>

            {/* Rationale & Explanation */}
            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/70 dark:border-slate-700/60 space-y-2">
              <div className="flex items-center gap-2 text-xs font-bold text-slate-700 dark:text-slate-300">
                <svg className="w-4 h-4 text-indigo-600 dark:text-indigo-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>Vì sao EduTwin đề xuất nội dung này?</span>
              </div>
              <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
                {proposal?.selectionReason || "Hệ thống đang tổng hợp dữ liệu từ Digital Twin và kết quả học tập gần đây..."}
              </p>
            </div>

            {/* Competency Gap Visualizer */}
            {proposal && (
              <div className="space-y-2">
                <div className="flex justify-between text-xs font-semibold">
                  <span className="text-slate-600 dark:text-slate-400">
                    Năng lực ước lượng: <strong className="text-slate-900 dark:text-white">{proposal.currentMastery.toFixed(0)}%</strong>
                  </span>
                  <span className="text-indigo-600 dark:text-indigo-400">
                    Mục tiêu phiên: <strong>{proposal.targetMastery.toFixed(0)}%</strong>
                  </span>
                </div>
                <div className="h-3 w-full bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden flex">
                  <div
                    className="bg-slate-400 dark:bg-slate-600 transition-all duration-500 rounded-l-full"
                    style={{ width: `${Math.min(100, proposal.currentMastery)}%` }}
                  />
                  <div
                    className="bg-indigo-600 dark:bg-indigo-500 opacity-80 transition-all duration-500"
                    style={{
                      width: `${Math.max(0, Math.min(100, proposal.targetMastery) - proposal.currentMastery)}%`,
                    }}
                  />
                </div>
                <p className="text-[11px] text-slate-400 dark:text-slate-500 italic">
                  *Thanh màu xám: năng lực đã xác nhận · Thanh màu tím: khoảng tiến bộ dự kiến đạt được sau phiên.
                </p>
              </div>
            )}

            {/* Time Budget Selector */}
            <div className="space-y-2 pt-2 border-t border-slate-100 dark:border-slate-800">
              <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
                Chọn ngân sách thời gian dự kiến:
              </label>
              <div className="grid grid-cols-3 gap-3">
                {[
                  { minutes: 10, label: "10 phút", desc: "4 – 8 câu" },
                  { minutes: 15, label: "15 phút", desc: "5 – 12 câu (Khuyên dùng)" },
                  { minutes: 20, label: "20 phút", desc: "6 – 15 câu" },
                ].map((item) => (
                  <button
                    key={item.minutes}
                    type="button"
                    onClick={() => setTimeBudget(item.minutes)}
                    className={`p-3 rounded-xl border text-left transition-all cursor-pointer ${
                      timeBudget === item.minutes
                        ? "border-indigo-600 bg-indigo-50/50 dark:bg-indigo-950/40 text-indigo-900 dark:text-indigo-200 ring-2 ring-indigo-500/20"
                        : "border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 text-slate-700 dark:text-slate-300"
                    }`}
                  >
                    <div className="text-sm font-bold">{item.label}</div>
                    <div className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">{item.desc}</div>
                  </button>
                ))}
              </div>
            </div>

            {/* CTA Button */}
            <div className="pt-2">
              <button
                type="button"
                onClick={() => createSessionMutation.mutate()}
                disabled={createSessionMutation.isPending || !proposal}
                className="w-full sm:w-auto px-8 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm shadow-md hover:shadow-indigo-500/25 transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
              >
                {createSessionMutation.isPending ? (
                  <span>Đang khởi tạo phiên...</span>
                ) : (
                  <>
                    <span>Bắt đầu luyện tập ngay</span>
                    <span className="text-base">→</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>

        {/* Right Column (4 cols): Modes & Custom Topic Selector */}
        <div className="lg:col-span-4 space-y-6">
          <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-4">
            <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">
              Chế độ luyện tập
            </h3>

            <div className="space-y-2">
              {[
                { mode: "Weakness" as AdaptivePracticeMode, label: "Luyện theo điểm yếu", desc: "Tập trung cải thiện các kỹ năng chưa vững" },
                { mode: "LearningPath" as AdaptivePracticeMode, label: "Theo lộ trình học (LP)", desc: "Bám sát mục tiêu chặng từ Learning Path" },
                { mode: "CustomTopic" as AdaptivePracticeMode, label: "Tự chọn chủ đề", desc: "Chủ động chọn chủ đề mong muốn ôn luyện" },
                { mode: "SpacedReview" as AdaptivePracticeMode, label: "Ôn tập kiến thức cũ", desc: "Ôn giãn cách các kiến thức từng học" },
              ].map((item) => (
                <button
                  key={item.mode}
                  type="button"
                  onClick={() => {
                    setSelectedMode(item.mode);
                    if (item.mode !== "CustomTopic") setSelectedTopicId("");
                  }}
                  className={`w-full p-3 rounded-xl border text-left transition-all cursor-pointer ${
                    selectedMode === item.mode
                      ? "border-indigo-600 bg-indigo-50/40 dark:bg-indigo-950/30 text-indigo-900 dark:text-indigo-200"
                      : "border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 text-slate-700 dark:text-slate-300"
                  }`}
                >
                  <div className="text-xs font-bold">{item.label}</div>
                  <div className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">{item.desc}</div>
                </button>
              ))}
            </div>

            {/* Custom Topic Dropdown (shown when CustomTopic mode active) */}
            {selectedMode === "CustomTopic" && proposal?.availableTopics && (
              <div className="space-y-1.5 pt-3 border-t border-slate-100 dark:border-slate-800">
                <label htmlFor="topic-dropdown" className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  Chọn chủ đề cụ thể:
                </label>
                <select
                  id="topic-dropdown"
                  value={selectedTopicId || proposal.topicNodeId}
                  onChange={(e) => setSelectedTopicId(e.target.value)}
                  className="w-full px-3 py-2 rounded-lg text-xs font-medium bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200 focus:outline-indigo-500"
                >
                  {proposal.availableTopics.map((top) => (
                    <option key={top.topicNodeId} value={top.topicNodeId}>
                      {top.topicName} ({top.currentMastery.toFixed(0)}%)
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Region D: Recent Practice History */}
      <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-base font-bold text-slate-900 dark:text-white">
            Lịch sử luyện tập gần đây
          </h3>
          <span className="text-xs text-slate-500 dark:text-slate-400">
            Hiển thị {history.length} phiên đã hoàn tất
          </span>
        </div>

        {history.length === 0 ? (
          <div className="py-8 text-center text-xs text-slate-500 dark:text-slate-400">
            Bạn chưa có phiên luyện tập nào hoàn thành trong môn học này. Hãy bắt đầu ngay phiên đầu tiên!
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 dark:border-slate-800 text-slate-500 dark:text-slate-400 font-semibold uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="py-3 px-4">Chủ đề & Môn</th>
                  <th className="py-3 px-4">Chế độ</th>
                  <th className="py-3 px-4">Số câu & Đúng</th>
                  <th className="py-3 px-4">Năng lực thay đổi</th>
                  <th className="py-3 px-4">Kết quả</th>
                  <th className="py-3 px-4">Thời gian</th>
                  <th className="py-3 px-4 text-right">Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {history.map((item) => (
                  <tr key={item.sessionId} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition-colors">
                    <td className="py-3 px-4 font-semibold text-slate-900 dark:text-slate-100">
                      <div>{item.topicName}</div>
                      <div className="text-[10px] text-slate-400 font-normal">{item.subjectName}</div>
                    </td>
                    <td className="py-3 px-4 text-slate-600 dark:text-slate-400">
                      {item.practiceMode === "Weakness" ? "Điểm yếu" : item.practiceMode === "LearningPath" ? "Lộ trình" : item.practiceMode === "CustomTopic" ? "Tự chọn" : "Ôn tập"}
                    </td>
                    <td className="py-3 px-4 text-slate-700 dark:text-slate-300 font-medium">
                      {item.correctCount}/{item.totalQuestions} câu đúng
                    </td>
                    <td className="py-3 px-4 font-semibold">
                      <span className="text-slate-400">{item.startingMastery.toFixed(0)}%</span>
                      <span className="mx-1 text-slate-300">→</span>
                      <span className={item.endingMastery && item.endingMastery > item.startingMastery ? "text-emerald-600 dark:text-emerald-400" : "text-slate-700 dark:text-slate-300"}>
                        {item.endingMastery ? item.endingMastery.toFixed(0) : item.startingMastery.toFixed(0)}%
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span
                        className={`inline-flex px-2 py-0.5 rounded text-[10px] font-bold ${
                          item.closeReason === "GoalAchievedWithEvidence"
                            ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300"
                            : item.closeReason === "RemediationNeeded"
                            ? "bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300"
                            : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {item.closeReason === "GoalAchievedWithEvidence"
                          ? "Đạt mục tiêu"
                          : item.closeReason === "RemediationNeeded"
                          ? "Cần bổ trợ"
                          : "Đã hoàn thành"}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-slate-500 dark:text-slate-400 text-[11px]">
                      {item.completedAt ? new Date(item.completedAt).toLocaleDateString("vi-VN") : "—"}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <Link
                        to={`/hoc-tap/luyen-tap/phien/${item.sessionId}/tong-ket`}
                        className="font-semibold text-indigo-600 dark:text-indigo-400 hover:underline"
                      >
                        Xem tổng kết
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
