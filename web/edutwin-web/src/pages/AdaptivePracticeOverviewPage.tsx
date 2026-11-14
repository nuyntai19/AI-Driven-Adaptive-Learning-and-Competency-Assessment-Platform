import React, { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../api/adaptivePracticeApi";
import { getReconciledStudentAcademicContext } from "../api/dashboardsApi";
import { useAuthStore } from "../stores/authStore";
import { practiceEndLabel } from "../utils/adaptivePracticeSummary";
import { BookmarkedQuestions } from "../components/student/BookmarkedQuestions";
import type { AdaptivePracticeMode } from "../types/adaptivePractice";

const CARD = "st-card st-spot p-5 sm:p-6";
const PRIMARY = "st-btn-primary group inline-flex items-center justify-center gap-2 rounded-full px-6 py-3 text-sm font-bold whitespace-nowrap [font-family:var(--student-font-display)]";
const OUTLINE = "inline-flex items-center justify-center rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-4 py-2 text-sm font-semibold text-[var(--student-ink)] transition-colors hover:border-[var(--student-brand)] hover:text-[var(--student-brand-ink)] cursor-pointer disabled:opacity-50";
const PILL = "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-bold leading-4";

const Icon = ({ d, className = "h-5 w-5" }: { d: string; className?: string }) => (
  <svg aria-hidden="true" className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={d} /></svg>
);
const ICON_PLAY = "M7 5v14l11-7z";
const ICON_TARGET = "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-4a5 5 0 1 0 0-10 5 5 0 0 0 0 10Zm0-4a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z";
const ICON_ROUTE = "M6 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4Zm12-10a2 2 0 1 0 0-4 2 2 0 0 0 0 4ZM8 17h7a3 3 0 0 0 0-6H9a3 3 0 0 1 0-6h7";
const ICON_SEARCH = "m20 20-4.3-4.3M17 10.5a6.5 6.5 0 1 1-13 0 6.5 6.5 0 0 1 13 0Z";
const ICON_REVIEW = "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5";
const ICON_ARROW = "M5 12h14m-5-5 5 5-5 5";
const ICON_INFO = "M12 16v-4m0-4h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z";

const MODES: { mode: AdaptivePracticeMode; label: string; desc: string; icon: string; tile: string }[] = [
  { mode: "Weakness", label: "Luyện theo điểm yếu", desc: "Tập trung cải thiện các kỹ năng chưa vững", icon: ICON_TARGET, tile: "bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300" },
  { mode: "LearningPath", label: "Theo lộ trình học", desc: "Bám sát mục tiêu chặng đang học", icon: ICON_ROUTE, tile: "bg-[var(--student-brand-soft)] text-[var(--student-brand-ink)]" },
  { mode: "CustomTopic", label: "Tự chọn chủ đề", desc: "Chủ động chọn chủ đề muốn ôn luyện", icon: ICON_SEARCH, tile: "bg-sky-50 text-sky-700 dark:bg-sky-950/40 dark:text-sky-300" },
  { mode: "SpacedReview", label: "Ôn tập kiến thức cũ", desc: "Ôn lại chủ đề đã học khi đến hạn", icon: ICON_REVIEW, tile: "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300" },
];

const DURATIONS = [
  { minutes: 10, label: "10 phút", desc: "4 – 8 câu" },
  { minutes: 15, label: "15 phút", desc: "5 – 12 câu · khuyên dùng" },
  { minutes: 20, label: "20 phút", desc: "6 – 15 câu" },
];

const modeShortLabel = (mode: AdaptivePracticeMode) =>
  mode === "Weakness" ? "Điểm yếu" : mode === "LearningPath" ? "Lộ trình" : mode === "CustomTopic" ? "Tự chọn" : "Ôn tập";

export const AdaptivePracticeOverviewPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // The subject is chosen once, in the student header.
  const effectiveSubjectId = searchParams.get("subjectId") || "";
  const urlClassId = searchParams.get("classId") || "";
  const user = useAuthStore((state) => state.user);
  // The header fills classId into the URL a moment later; resolve it here so
  // the first proposal is already limited to the class in view.
  const academicContextQuery = useQuery({
    queryKey: ["student-academic-context", user?.centerId, user?.userId, effectiveSubjectId, urlClassId, searchParams.get("history") === "true"],
    queryFn: () => getReconciledStudentAcademicContext(effectiveSubjectId, urlClassId, searchParams.get("history") === "true"),
    enabled: !!user && !!effectiveSubjectId,
    staleTime: 30_000,
  });
  const classId = urlClassId || academicContextQuery.data?.selectedClassId || "";
  const classResolved = !!urlClassId || academicContextQuery.isSuccess || academicContextQuery.isError;
  // A link from the learning path can preselect the topic to practise.
  const linkedTopicId = searchParams.get("topicId") || "";
  const [selectedMode, setSelectedMode] = useState<AdaptivePracticeMode>(linkedTopicId ? "CustomTopic" : "Weakness");
  const [selectedTopicId, setSelectedTopicId] = useState<string>(linkedTopicId);
  const [timeBudget, setTimeBudget] = useState<number>(15);

  // A topic only exists within its subject, so switching subject clears it.
  const [topicSubjectId, setTopicSubjectId] = useState(effectiveSubjectId);
  if (topicSubjectId !== effectiveSubjectId) {
    setTopicSubjectId(effectiveSubjectId);
    setSelectedTopicId("");
  }

  // 1. Fetch Active Session if any
  const activeSessionQuery = useQuery({
    queryKey: ["adaptive-practice", "active-session", effectiveSubjectId],
    queryFn: () => adaptivePracticeApi.getActiveSession(effectiveSubjectId || undefined),
    enabled: !!effectiveSubjectId,
    refetchOnWindowFocus: true,
  });
  const activeSession = activeSessionQuery.data;

  // 3. Fetch Practice Proposal
  const proposalQuery = useQuery({
    queryKey: ["adaptive-practice", "proposal", effectiveSubjectId, classId, selectedTopicId, selectedMode],
    queryFn: () => adaptivePracticeApi.getProposal(effectiveSubjectId, selectedTopicId || undefined, selectedMode, classId || undefined),
    enabled: !!effectiveSubjectId && classResolved,
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
        classId: classId || undefined,
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

  const stopActiveSessionMutation = useMutation({
    mutationFn: () => adaptivePracticeApi.closeSession(activeSession!.sessionId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["adaptive-practice"] }),
  });

  const handleResumeSession = () => {
    if (activeSession) {
      navigate(`/hoc-tap/luyen-tap/phien/${activeSession.sessionId}`);
    }
  };

  const recentQuestions = history.reduce((sum, item) => sum + item.totalQuestions, 0);
  const recentCorrect = history.reduce((sum, item) => sum + item.correctCount, 0);
  const recentAccuracy = recentQuestions > 0 ? Math.round((recentCorrect / recentQuestions) * 100) : null;

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5 min-w-0 student-shell">
      {/* Header */}
      <header className="st-rise">
        <p className="st-eyebrow">Phiên luyện tập cá nhân hoá</p>
        <h1 className="mt-1.5 text-2xl font-extrabold tracking-tight sm:text-3xl [font-family:var(--student-font-display)]">
          Luyện tập <span className="st-grad-text">thích ứng</span>
        </h1>
        <p className="mt-1.5 max-w-2xl text-sm text-[var(--student-ink-secondary)]">
          Độ khó tự điều chỉnh theo từng câu. Mỗi phiên kết thúc bằng báo cáo năng lực.
        </p>
      </header>

      {/* Active session banner */}
      {activeSession && (
        <div role="status" style={{ "--d": 1 } as React.CSSProperties} className="st-rise flex flex-col gap-4 rounded-[20px] border border-amber-300 bg-amber-50/80 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5 dark:border-amber-700/60 dark:bg-amber-950/30">
          <div className="flex min-w-0 items-center gap-3">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-300">
              <Icon d={ICON_PLAY} />
            </span>
            <div className="min-w-0">
              <p className="text-sm font-bold text-[var(--student-ink)]">Bạn có phiên luyện tập đang làm dở</p>
              <p className="truncate text-xs text-amber-800 dark:text-amber-300">
                {activeSession.topicName} ({activeSession.subjectName}) · đã làm {activeSession.questionCount} câu · {activeSession.timeBudgetMinutes} phút · năng lực {activeSession.currentMastery.toFixed(0)}%
              </p>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            <button type="button" onClick={handleResumeSession} className={`${PRIMARY} !px-5 !py-2.5`}>
              <span>Tiếp tục luyện tập</span>
              <span className="st-arrow"><Icon d={ICON_ARROW} className="h-4 w-4" /></span>
            </button>
            <button
              type="button"
              onClick={() => stopActiveSessionMutation.mutate()}
              disabled={stopActiveSessionMutation.isPending}
              className={OUTLINE}
            >
              {stopActiveSessionMutation.isPending ? "Đang dừng..." : "Dừng phiên này"}
            </button>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12 lg:items-start">
        <div className="min-w-0 space-y-5 lg:col-span-8">
          {proposalQuery.isError ? (
            <div role="alert" className={CARD}>
              <p className="font-bold text-[var(--student-ink)]">Không tải được đề xuất luyện tập.</p>
              <button type="button" onClick={() => void proposalQuery.refetch()} className={`${OUTLINE} mt-3`}>
                Thử lại
              </button>
            </div>
          ) : proposal?.unavailableReason ? (
            <div role="status" className={CARD}>
              <p className="st-eyebrow">Mục tiêu luyện tập đề xuất</p>
              <h2 className="mt-1 text-lg font-bold">Chưa có nội dung để luyện tập</h2>
              <p className="mt-1 text-sm leading-relaxed text-[var(--student-ink-secondary)]">{proposal.unavailableReason}</p>
              {selectedMode !== "Weakness" && (
                <button type="button" onClick={() => setSelectedMode("Weakness")} className={`${PRIMARY} mt-4 !py-2.5`}>
                  Luyện theo điểm yếu
                </button>
              )}
            </div>
          ) : (
            <article className={`${CARD} st-rise`} style={{ "--d": 2 } as React.CSSProperties} aria-labelledby="practice-proposal-title">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="st-eyebrow">Mục tiêu luyện tập đề xuất</p>
                  <h2 id="practice-proposal-title" className="mt-1 break-words text-xl font-extrabold tracking-tight [font-family:var(--student-font-display)]">
                    {proposal ? proposal.topicName : "Đang tải đề xuất..."}
                  </h2>
                </div>
                {proposal?.isDiagnosticRecommended && (
                  <span className={`${PILL} bg-amber-50 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300`}>Khuyên dùng: Luyện chẩn đoán</span>
                )}
              </div>

              <details open className="mt-4 rounded-2xl border border-[var(--student-border)] bg-[var(--student-surface-subtle)] px-4 py-3">
                <summary className="flex cursor-pointer items-center gap-2 text-xs font-bold text-[var(--student-ink)]">
                  <span className="text-[var(--student-brand)]"><Icon d={ICON_INFO} className="h-4 w-4" /></span>
                  Vì sao EduTwin đề xuất nội dung này?
                </summary>
                <p className="mt-2 text-sm leading-relaxed text-[var(--student-ink-secondary)]">
                  {proposal?.selectionReason || "Hệ thống đang tổng hợp dữ liệu từ Digital Twin và kết quả học tập gần đây..."}
                </p>
              </details>

              {proposal && (
                <div className="mt-5 space-y-2">
                  <div className="flex flex-wrap justify-between gap-2 text-sm font-semibold">
                    <span className="text-[var(--student-ink-secondary)]">
                      Năng lực ước lượng: <b className="text-[var(--student-ink)]">{proposal.isDiagnosticRecommended ? "Chưa có dữ liệu" : `${proposal.currentMastery.toFixed(0)}%`}</b>
                    </span>
                    <span className="text-[var(--student-brand-ink)]">Mục tiêu phiên: <b>{proposal.targetMastery.toFixed(0)}%</b></span>
                  </div>
                  <div
                    className="relative h-3 w-full rounded-full bg-[var(--student-surface-subtle)]"
                    role="img"
                    aria-label={`Năng lực ${proposal.currentMastery.toFixed(0)}%, mục tiêu phiên ${proposal.targetMastery.toFixed(0)}%`}
                  >
                    <div
                      className="st-bar-fill h-full rounded-full bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))]"
                      style={{ width: `${Math.min(100, Math.max(0, proposal.currentMastery))}%` }}
                    />
                    <span
                      className="absolute -top-1 h-5 w-0.5 -translate-x-1/2 rounded-full bg-[var(--student-ink)]"
                      style={{ left: `${Math.min(100, Math.max(0, proposal.targetMastery))}%` }}
                      title="Mục tiêu phiên"
                    />
                  </div>
                  <p className="text-[11px] text-[var(--student-muted)]">Thanh màu: năng lực đã xác nhận · Vạch đậm: mục tiêu cần đạt sau phiên.</p>
                </div>
              )}

              <p className="mb-2 mt-5 text-sm font-bold">Thời lượng phiên</p>
              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3" role="radiogroup" aria-label="Thời lượng phiên">
                {DURATIONS.map((item) => {
                  const isActive = timeBudget === item.minutes;
                  return (
                    <button
                      key={item.minutes}
                      type="button"
                      role="radio"
                      aria-checked={isActive}
                      onClick={() => setTimeBudget(item.minutes)}
                      className={`rounded-2xl border px-4 py-3 text-left transition-all cursor-pointer ${
                        isActive
                          ? "border-[var(--student-brand)] bg-[var(--student-brand-soft)] ring-4 ring-[var(--student-brand-soft)]"
                          : "border-[var(--student-border)] bg-[var(--student-surface)] hover:border-[var(--student-brand)]"
                      }`}
                    >
                      <b className={`block text-sm [font-family:var(--student-font-display)] ${isActive ? "text-[var(--student-brand-ink)]" : ""}`}>{item.label}</b>
                      <small className="mt-0.5 block text-xs text-[var(--student-muted)]">{item.desc}</small>
                    </button>
                  );
                })}
              </div>

              {proposal && (proposal.availableQuestionCount ?? 0) > 0 && (
                <p className="mt-3 text-xs text-[var(--student-muted)]">Chủ đề này hiện có {proposal.availableQuestionCount} câu để luyện tập.</p>
              )}

              <div className="mt-5 space-y-2">
                <button
                  type="button"
                  onClick={() => createSessionMutation.mutate()}
                  disabled={createSessionMutation.isPending || !proposal || !!activeSession}
                  className={`${PRIMARY} w-full sm:w-auto`}
                >
                  {createSessionMutation.isPending ? (
                    <span>Đang khởi tạo phiên...</span>
                  ) : (
                    <>
                      <span>Bắt đầu luyện tập ngay</span>
                      <span className="st-arrow"><Icon d={ICON_ARROW} className="h-4 w-4" /></span>
                    </>
                  )}
                </button>
                {activeSession && (
                  <p className="text-xs text-[var(--student-muted)]">
                    Mỗi môn chỉ có một phiên đang chạy. Hãy tiếp tục hoặc dừng phiên ở trên trước khi bắt đầu phiên mới.
                  </p>
                )}
              </div>
            </article>
          )}

          {/* Practice modes */}
          <section aria-labelledby="practice-modes-title">
            <h2 id="practice-modes-title" className="mb-2.5 text-sm font-bold">Hoặc chọn chế độ khác</h2>
            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
              {MODES.map((item) => {
                const isActive = selectedMode === item.mode;
                return (
                  <button
                    key={item.mode}
                    type="button"
                    aria-pressed={isActive}
                    onClick={() => {
                      setSelectedMode(item.mode);
                      if (item.mode !== "CustomTopic") setSelectedTopicId("");
                    }}
                    className={`st-card flex items-center gap-3 p-3.5 text-left transition-all cursor-pointer hover:-translate-y-0.5 ${
                      isActive ? "!border-[var(--student-brand)] ring-4 ring-[var(--student-brand-soft)]" : ""
                    }`}
                  >
                    <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${item.tile}`}><Icon d={item.icon} /></span>
                    <span className="min-w-0">
                      <b className="block text-sm">{item.label}</b>
                      <small className="block text-xs text-[var(--student-muted)]">{item.desc}</small>
                    </span>
                  </button>
                );
              })}
            </div>

            {selectedMode === "CustomTopic" && proposal?.availableTopics && (
              <div className="st-card mt-3 space-y-1.5 p-4">
                <label htmlFor="topic-dropdown" className="block text-xs font-bold text-[var(--student-ink)]">
                  Chọn chủ đề cụ thể
                </label>
                <select
                  id="topic-dropdown"
                  value={selectedTopicId || proposal.topicNodeId}
                  onChange={(e) => setSelectedTopicId(e.target.value)}
                  className="w-full rounded-xl border border-[var(--student-border)] bg-[var(--student-surface)] px-3 py-2.5 text-sm text-[var(--student-ink)] focus:border-[var(--student-brand)] focus:outline-none focus:ring-4 focus:ring-[var(--student-brand-soft)]"
                >
                  {proposal.availableTopics.map((top) => (
                    <option key={top.topicNodeId} value={top.topicNodeId}>
                      {top.topicName} ({top.currentMastery.toFixed(0)}%)
                    </option>
                  ))}
                </select>
              </div>
            )}
          </section>
        </div>

        {/* Right column: recent sessions */}
        <aside className="min-w-0 space-y-5 lg:col-span-4">
          <article className="st-card p-5" aria-labelledby="practice-history-title">
            <div className="flex items-center justify-between gap-2">
              <h2 id="practice-history-title" className="text-base font-bold">Phiên gần đây</h2>
              <span className="text-xs text-[var(--student-muted)]">{history.length} phiên đã xong</span>
            </div>

            {history.length === 0 ? (
              <p className="py-6 text-center text-sm text-[var(--student-muted)]">
                Bạn chưa có phiên luyện tập nào hoàn thành trong môn học này. Hãy bắt đầu ngay phiên đầu tiên!
              </p>
            ) : (
              <ul className="mt-3 divide-y divide-[var(--student-border)]">
                {history.map((item) => {
                  const ending = item.endingMastery ?? item.startingMastery;
                  const delta = Math.round(ending - item.startingMastery);
                  return (
                    <li key={item.sessionId} className="py-3 first:pt-1 last:pb-0">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <b className="block break-words text-sm">{item.topicName}</b>
                          <small className="text-xs text-[var(--student-muted)]">
                            {modeShortLabel(item.practiceMode)} · {item.completedAt ? new Date(item.completedAt).toLocaleDateString("vi-VN") : "—"} · {Math.max(1, Math.round(item.timeSpentSeconds / 60))} phút
                          </small>
                        </div>
                        <span
                          className={`${PILL} shrink-0 ${
                            delta > 0
                              ? "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300"
                              : delta < 0
                                ? "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300"
                                : "bg-[var(--student-surface-subtle)] text-[var(--student-muted)]"
                          }`}
                          title={`${item.startingMastery.toFixed(0)}% → ${ending.toFixed(0)}%`}
                        >
                          {delta > 0 ? `+${delta}%` : delta < 0 ? `−${Math.abs(delta)}%` : "±0%"}
                        </span>
                      </div>
                      <div className="mt-1 flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                        <small className="text-xs text-[var(--student-ink-secondary)]">
                          {item.correctCount}/{item.totalQuestions} câu đúng · {practiceEndLabel(item.closeReason)}
                        </small>
                        <Link
                          to={`/hoc-tap/luyen-tap/phien/${item.sessionId}/tong-ket`}
                          className="st-link inline-flex items-center gap-1 text-xs font-bold text-[var(--student-brand-ink)]"
                        >
                          Xem tổng kết <span className="st-arrow"><Icon d={ICON_ARROW} className="h-3.5 w-3.5" /></span>
                        </Link>
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </article>

          {history.length > 0 && (
            <article className="st-card p-5" aria-labelledby="practice-recent-stats">
              <h2 id="practice-recent-stats" className="text-base font-bold">Gần đây</h2>
              <dl className="mt-3 grid grid-cols-2 gap-3">
                <div className="flex flex-col-reverse rounded-2xl bg-[var(--student-surface-subtle)] p-3">
                  <dt className="text-xs text-[var(--student-muted)]">phiên luyện tập</dt>
                  <dd className="text-2xl font-extrabold [font-family:var(--student-font-display)]">{history.length}</dd>
                </div>
                <div className="flex flex-col-reverse rounded-2xl bg-[var(--student-surface-subtle)] p-3">
                  <dt className="text-xs text-[var(--student-muted)]">câu trả lời đúng</dt>
                  <dd className="text-2xl font-extrabold [font-family:var(--student-font-display)]">{recentAccuracy == null ? "–" : `${recentAccuracy}%`}</dd>
                </div>
              </dl>
            </article>
          )}
        </aside>
      </div>

      <BookmarkedQuestions subjectId={effectiveSubjectId} />
    </div>
  );
};
