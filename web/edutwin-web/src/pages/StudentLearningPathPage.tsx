import React, { useState, useEffect } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { useStudentLearningAccess } from "../hooks/useStudentLearningAccess";
import { StudentLearningReadOnlyNotice } from "../components/student/StudentLearningReadOnlyNotice";
import { useAuthStore } from "../stores/authStore";
import {
  getLearningPathTopics,
  getLearningPathPreferences,
  generateLearningPath,
  getDetailedLearningPath,
  startLearningSession,
} from "../api/learningPathApi";
import type {
  GenerateLearningPathRequest,
  LearningPathTopicNodeDto,
  DetailedLearningPathDto,
  LearningPathSessionDto,
} from "../types/learningPath";
import { SessionPreparationModal } from "../components/student/SessionPreparationModal";

export const StudentLearningPathPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const activeSubjectId = searchParams.get("subjectId") || "";
  const savedPathId = searchParams.get("pathId") || undefined;

  // 1. Fetch available subjects
  const { data: subjectsData } = useQuery({
    queryKey: ["subjects", "student-learning-path"],
    queryFn: () => organizationApi.listSubjects(true),
    staleTime: 5 * 60 * 1000,
  });

  const subjects = subjectsData?.data || [];
  const currentSubject = subjects.find((s) => s.subjectId === activeSubjectId) || subjects[0];
  const effectiveSubjectId = currentSubject?.subjectId || "";
  const access = useStudentLearningAccess(effectiveSubjectId);
  const actor = useAuthStore(s => s.user);

  // Update URL if no subjectId in search params
  useEffect(() => {
    if (!activeSubjectId && effectiveSubjectId) {
      const p = new URLSearchParams(searchParams);
      p.set("subjectId", effectiveSubjectId);
      setSearchParams(p, { replace: true });
    }
  }, [activeSubjectId, effectiveSubjectId, searchParams, setSearchParams]);

  // 2. Fetch real topics from backend (Requirement 11: real topics from endpoint)
  const { data: topicsData, isLoading: isTopicsLoading } = useQuery({
    queryKey: ["learning-path-topics", actor?.centerId, actor?.userId, effectiveSubjectId, access.classId, access.history],
    queryFn: () => getLearningPathTopics(effectiveSubjectId, access.classId, access.history),
    enabled: !!effectiveSubjectId && access.writable,
    staleTime: 60 * 1000,
  });

  const topics: LearningPathTopicNodeDto[] = topicsData?.data || [];

  // 3. Fetch existing preferences
  const { data: prefData } = useQuery({
    queryKey: ["learning-path-preferences", effectiveSubjectId],
    queryFn: () => getLearningPathPreferences(effectiveSubjectId),
    enabled: !!effectiveSubjectId && access.writable,
  });

  // 4. Fetch detailed learning path (with dynamic phases)
  const {
    data: detailedPathData,
    isLoading: isPathLoading,
    isError: isPathError,
    error: pathError,
    refetch: refetchPath,
  } = useQuery({
    queryKey: ["learning-path", effectiveSubjectId, access.classId, savedPathId],
    queryFn: () => getDetailedLearningPath(effectiveSubjectId, access.classId, savedPathId),
    enabled: !!effectiveSubjectId && access.writable,
    retry: false,
  });

  const detailedPath: DetailedLearningPathDto | null = detailedPathData?.data || null;

  // Questionnaire form state
  const [isQuestionnaireOpen, setIsQuestionnaireOpen] = useState(false);
  const [selfAssessedLevel, setSelfAssessedLevel] = useState("Medium");
  const [weakTopicIds, setWeakTopicIds] = useState<number[]>([]);
  const [focusTopicIds, setFocusTopicIds] = useState<number[]>([]);
  const [goalType, setGoalType] = useState("Foundation");
  const [targetMastery, setTargetMastery] = useState(80);
  const [targetWeeks, setTargetWeeks] = useState(4);
  const [minutesPerDay, setMinutesPerDay] = useState(30);
  const [daysPerWeek, setDaysPerWeek] = useState(5);
  const [pace, setPace] = useState("Moderate");
  const [preferredMode, setPreferredMode] = useState("Balanced");
  const [note, setNote] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  // Each subject owns an independent questionnaire and persisted path.
  useEffect(() => {
    setSelfAssessedLevel("Medium");
    setWeakTopicIds([]);
    setFocusTopicIds([]);
    setGoalType("Foundation");
    setTargetMastery(80);
    setTargetWeeks(4);
    setMinutesPerDay(30);
    setDaysPerWeek(5);
    setPace("Moderate");
    setPreferredMode("Balanced");
    setNote("");
    setFormError(null);
    setIsQuestionnaireOpen(false);
  }, [effectiveSubjectId, access.classId]);

  // Sync state with saved preferences
  useEffect(() => {
    if (prefData?.data) {
      const p = prefData.data;
      setSelfAssessedLevel(p.selfAssessedLevel || "Medium");
      const allowed = new Set((topicsData?.data || []).map(t => Number(t.topicNodeId)));
      setWeakTopicIds((p.weakTopicNodeIds || []).filter(id => allowed.has(id)));
      setFocusTopicIds((p.focusTopicNodeIds || []).filter(id => allowed.has(id)));
      setGoalType(p.goalType || "Foundation");
      setTargetMastery(p.targetMastery || 80);
      setTargetWeeks(p.targetWeeks || 4);
      setMinutesPerDay(p.minutesPerDay || 30);
      setDaysPerWeek(p.daysPerWeek || 5);
      setPace(p.pace || "Moderate");
      setPreferredMode(p.preferredMode || "Balanced");
      setNote(p.note || "");
    }
  }, [prefData, topicsData, access.classId]);

  // If no path yet, open questionnaire by default
  useEffect(() => {
    if (access.writable && !isPathLoading && !detailedPath && effectiveSubjectId && (!isPathError || (pathError as { response?: { status?: number } })?.response?.status === 404)) {
      setIsQuestionnaireOpen(true);
    }
  }, [isPathLoading, detailedPath, effectiveSubjectId, access.writable, isPathError, pathError]);

  // Generate Learning Path Mutation
  const generateMutation = useMutation({
    mutationFn: (req: GenerateLearningPathRequest) => {
      if (!access.writable) throw new Error(access.reason);
      return generateLearningPath(req);
    },
    onSuccess: (response, request) => {
      queryClient.setQueryData(["learning-path", request.subjectId, request.classId || "", undefined], response);
      void queryClient.invalidateQueries({ queryKey: ["learning-path", request.subjectId] });
      void queryClient.invalidateQueries({ queryKey: ["learning-path-preferences", request.subjectId] });
      if (request.subjectId === effectiveSubjectId && (request.classId || "") === access.classId) {
        setIsQuestionnaireOpen(false);
        if (savedPathId) { const params = new URLSearchParams(searchParams); params.delete("pathId"); setSearchParams(params, { replace: true }); }
      }
    },
    onError: (err: unknown) => {
      setFormError(err instanceof Error ? err.message : "Không thể tạo lộ trình học tập.");
    },
  });

  const handleToggleWeakTopic = (topicIdNum: number) => {
    setWeakTopicIds((prev) =>
      prev.includes(topicIdNum) ? prev.filter((id) => id !== topicIdNum) : [...prev, topicIdNum]
    );
  };

  const handleToggleFocusTopic = (topicIdNum: number) => {
    setFocusTopicIds((prev) =>
      prev.includes(topicIdNum) ? prev.filter((id) => id !== topicIdNum) : [...prev, topicIdNum]
    );
  };

  const handleSubmitQuestionnaire = (e: React.FormEvent) => {
    e.preventDefault();
    if (!access.writable) return;
    setFormError(null);

    const payload: GenerateLearningPathRequest = {
      subjectId: effectiveSubjectId,
      classId: access.classId || undefined,
      history: access.history,
      selfAssessedLevel,
      weakTopicNodeIds: weakTopicIds,
      focusTopicNodeIds: focusTopicIds,
      goalType,
      targetMastery,
      targetWeeks,
      minutesPerDay,
      daysPerWeek,
      pace,
      preferredMode,
      note: note.trim() || null,
      forceRegenerate: Boolean(detailedPath),
    };

    generateMutation.mutate(payload);
  };

  const [prepSession, setPrepSession] = useState<LearningPathSessionDto | null>(null);
  const [isStartingSession, setIsStartingSession] = useState(false);
  const [prepError, setPrepError] = useState<string | null>(null);

  const startSessionMutation = useMutation({
    mutationFn: async (sessionId: string) => {
      if (!access.writable) throw new Error(access.reason);
      return startLearningSession(sessionId, effectiveSubjectId, access.classId);
    },
  });

  const handleStartSession = async (sessionId: string) => {
    if (!access.writable) return;
    setIsStartingSession(true);
    setPrepError(null);
    try {
      const startResult = await startSessionMutation.mutateAsync(sessionId);
      void queryClient.invalidateQueries({ queryKey: ["learning-path", effectiveSubjectId] });
      setPrepSession(null);
      const p = new URLSearchParams(searchParams);
      p.set("subjectId", effectiveSubjectId);
      p.set("sessionId", sessionId);
      if (startResult.sessionRunId) {
        p.set("sessionRunId", String(startResult.sessionRunId));
      }
      p.delete("assignmentId");
      p.delete("analysisJobId");
      navigate(`/hoc-tap/luyen-tap?${p}`);
    } catch (error) {
      setPrepError(error instanceof Error ? error.message : "Không thể khởi tạo phiên học.");
    } finally {
      setIsStartingSession(false);
    }
  };

  const handleResumeSession = (sessionId: string) => {
    const p = new URLSearchParams(searchParams);
    p.set("subjectId", effectiveSubjectId);
    p.set("sessionId", sessionId);
    p.delete("assignmentId");
    p.delete("analysisJobId");
    navigate(`/hoc-tap/luyen-tap?${p}`);
  };

  const renderSessionStatusBadge = (status: string) => {
    switch (status) {
      case "Completed":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            Đã hoàn thành
          </span>
        );
      case "InProgress":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-indigo-100 text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300 border border-indigo-300 dark:border-indigo-800 animate-pulse">
            <span className="w-1.5 h-1.5 rounded-full bg-indigo-500" />
            Đang học
          </span>
        );
      case "NeedsReview":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-300 dark:border-amber-800">
            <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
            Chờ giáo viên duyệt
          </span>
        );
      case "Grading":
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-sky-100 text-sky-800 dark:bg-sky-950/60 dark:text-sky-300 border border-sky-300 dark:border-sky-800">
            <span className="w-1.5 h-1.5 rounded-full bg-sky-500" />
            Đang chấm bài
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-medium bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700">
            Chưa học
          </span>
        );
    }
  };

  if (access.pending) return <p role="status" className="p-6">Đang kiểm tra lớp và giáo trình đang áp dụng…</p>;
  if (access.readOnly) return <StudentLearningReadOnlyNotice reason={access.reason} />;

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-8 min-w-0">
      {formError && !isQuestionnaireOpen && <p role="alert" className="text-red-700 dark:text-red-400">{formError}</p>}
      {isPathError && (pathError as { response?: { status?: number } })?.response?.status !== 404 && (
        <div role="alert"><p>Không thể tải lộ trình. Vui lòng thử lại.</p><button type="button" onClick={() => void refetchPath()}>Tải lại</button></div>
      )}
      {/* Header & Subject Selector */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-stone-200/80 dark:border-stone-800">
        <div>
          <div className="flex items-center gap-2.5">
            <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-indigo-600 text-white font-bold text-sm shadow-xs">
              🎯
            </span>
            <h1 className="text-xl sm:text-2xl font-black tracking-tight text-slate-900 dark:text-white">
              Lộ Trình Học Tập Cá Nhân Hóa
            </h1>
          </div>
          <p className="mt-1 text-xs sm:text-sm text-slate-500 dark:text-slate-400">
            Kế hoạch học tập thích ứng được tối ưu từ Đồ thị Tri thức và Hồ sơ Năng lực số (Digital Twin)
          </p>
        </div>

        {/* Subject Switcher & Action button */}
        <div className="flex items-center gap-3">
          {subjects.length > 0 && (
            <select
              value={effectiveSubjectId}
              onChange={(e) => {
                const p = new URLSearchParams(searchParams);
                p.set("subjectId", e.target.value);
                p.delete("pathId");
                setSearchParams(p);
              }}
              className="rounded-xl border border-stone-200 dark:border-stone-700 bg-white dark:bg-slate-800 px-3.5 py-2 text-xs sm:text-sm font-bold text-slate-800 dark:text-slate-200 shadow-2xs focus:border-indigo-500 focus:outline-none"
            >
              {subjects.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  Môn: {sub.subjectName}
                </option>
              ))}
            </select>
          )}

          <button
            type="button"
            onClick={() => setIsQuestionnaireOpen((prev) => !prev)}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs sm:text-sm font-bold bg-indigo-600 hover:bg-indigo-700 text-white shadow-xs transition-colors cursor-pointer"
          >
            <span>{isQuestionnaireOpen ? "✕ Đóng khảo sát" : detailedPath ? "↻ AI đề xuất lại lộ trình" : "✨ AI đề xuất lộ trình"}</span>
          </button>
        </div>
      </div>

      {savedPathId && detailedPath && <div role="status" className="rounded-xl bg-indigo-50 p-4 text-sm dark:bg-indigo-950">
        Bạn đang xem kế hoạch đã lưu v{detailedPath.version}, gồm tiến độ các buổi thuộc kế hoạch này.
        <button type="button" className="ml-3 underline" onClick={() => { const params = new URLSearchParams(searchParams); params.delete("pathId"); setSearchParams(params); }}>Xem lộ trình hiện tại</button>
      </div>}

      {/* QUESTIONNAIRE CARD (Survey form) */}
      {isQuestionnaireOpen && (
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-8 shadow-sm border border-indigo-200 dark:border-indigo-900 space-y-6">
          <div className="border-b border-slate-100 dark:border-slate-800 pb-4">
            <h2 className="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
              <span>📋</span> Khảo Sát Mục Tiêu & Nhịp Độ Học Tập ({currentSubject?.subjectName})
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
              Hãy trả lời 10 câu hỏi dưới đây để hệ thống EduTwin xây dựng lộ trình học tập tối ưu nhất cho bạn.
            </p>
          </div>

          {formError && (
            <div className="rounded-xl bg-rose-50 dark:bg-rose-950/40 p-4 border border-rose-200 dark:border-rose-800 text-xs text-rose-700 dark:text-rose-300">
              {formError}
            </div>
          )}

          <form onSubmit={handleSubmitQuestionnaire} className="space-y-6">
            {/* Q1: Tự đánh giá năng lực */}
            <div className="space-y-2">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                1. Bạn tự đánh giá mức độ hiện tại của mình ở môn học này:
              </label>
              <div className="grid grid-cols-2 sm:grid-cols-5 gap-2.5">
                {[
                  { value: "VeryWeak", label: "Rất yếu / Mất gốc" },
                  { value: "Weak", label: "Cần củng cố" },
                  { value: "Medium", label: "Trung bình" },
                  { value: "Good", label: "Khá vững" },
                  { value: "Excellent", label: "Xuất sắc" },
                ].map((lvl) => (
                  <button
                    key={lvl.value}
                    type="button"
                    onClick={() => setSelfAssessedLevel(lvl.value)}
                    className={`p-3 rounded-xl text-xs font-bold border transition-all text-center ${
                      selfAssessedLevel === lvl.value
                        ? "border-indigo-600 bg-indigo-50 dark:bg-indigo-950/60 text-indigo-900 dark:text-indigo-200 ring-2 ring-indigo-500/20"
                        : "border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/60 text-slate-700 dark:text-slate-300 hover:bg-slate-100"
                    }`}
                  >
                    {lvl.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Q2: Chủ đề cảm thấy còn yếu (Real topics from backend) */}
            <div className="space-y-2">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                2. Chọn các chủ đề bạn cảm thấy đang hổng hoặc cần ôn tập lại kỹ hơn:
              </label>
              {isTopicsLoading ? (
                <div className="text-xs text-slate-400 animate-pulse">Đang tải danh sách chủ đề...</div>
              ) : topics.length === 0 ? (
                <div className="text-xs text-slate-400">Chưa có danh mục chủ đề cho môn này.</div>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2.5 max-h-56 overflow-y-auto p-1">
                  {topics.map((t) => {
                    const idNum = Number(t.topicNodeId);
                    const isChecked = weakTopicIds.includes(idNum);
                    return (
                      <label
                        key={t.topicNodeId}
                        className={`flex items-start gap-2.5 p-3 rounded-xl border text-xs font-medium cursor-pointer transition-all ${
                          isChecked
                            ? "border-rose-400 bg-rose-50/70 dark:bg-rose-950/40 text-rose-900 dark:text-rose-200"
                            : "border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/60 text-slate-700 dark:text-slate-300 hover:bg-slate-50"
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => handleToggleWeakTopic(idNum)}
                          className="mt-0.5 rounded border-slate-300 text-rose-600 focus:ring-rose-500"
                        />
                        <div className="min-w-0 flex-1">
                          <p className="font-bold truncate">{t.nodeName}</p>
                          <span className="text-[10px] text-slate-400 block mt-0.5">
                            Thành thạo hiện tại: {t.currentMastery}%
                          </span>
                        </div>
                      </label>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Q3: Chủ đề mục tiêu trọng tâm */}
            <div className="space-y-2">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                3. Chọn các chủ đề trọng tâm bạn muốn ưu tiên làm chủ để bứt phá:
              </label>
              {topics.length > 0 && (
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2.5 max-h-56 overflow-y-auto p-1">
                  {topics.map((t) => {
                    const idNum = Number(t.topicNodeId);
                    const isChecked = focusTopicIds.includes(idNum);
                    return (
                      <label
                        key={t.topicNodeId}
                        className={`flex items-start gap-2.5 p-3 rounded-xl border text-xs font-medium cursor-pointer transition-all ${
                          isChecked
                            ? "border-indigo-500 bg-indigo-50/70 dark:bg-indigo-950/40 text-indigo-900 dark:text-indigo-200"
                            : "border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/60 text-slate-700 dark:text-slate-300 hover:bg-slate-50"
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => handleToggleFocusTopic(idNum)}
                          className="mt-0.5 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                        />
                        <div className="min-w-0 flex-1">
                          <p className="font-bold truncate">{t.nodeName}</p>
                          <span className="text-[10px] text-slate-400 block mt-0.5">
                            Mức độ hiện tại: {t.currentMastery}%
                          </span>
                        </div>
                      </label>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Q4 & Q5: Mục tiêu kỳ vọng & Độ thành thạo */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  4. Mục tiêu kỳ vọng cao nhất:
                </label>
                <select
                  value={goalType}
                  onChange={(e) => setGoalType(e.target.value)}
                  className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-3 text-xs font-bold text-slate-800 dark:text-slate-200"
                >
                  <option value="Foundation">Lấy lại căn bản & Nền tảng vững chắc</option>
                  <option value="KeepUp">Theo kịp tiến độ trên lớp học</option>
                  <option value="ImproveGrade">Cải thiện điểm số kiểm tra định kỳ</option>
                  <option value="ExamPrep">Luyện thi tốt nghiệp / Đại học / Chuyên</option>
                  <option value="Advanced">Làm chủ nâng cao & Vận dụng cao</option>
                </select>
              </div>

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  5. Độ thành thạo mục tiêu ({targetMastery}%):
                </label>
                <div className="flex items-center gap-3">
                  <input
                    type="range"
                    min="50"
                    max="100"
                    step="5"
                    value={targetMastery}
                    onChange={(e) => setTargetMastery(Number(e.target.value))}
                    className="w-full accent-indigo-600"
                  />
                  <span className="text-sm font-black text-indigo-600 w-12 text-right">{targetMastery}%</span>
                </div>
              </div>
            </div>

            {/* Q6, Q7, Q8: Kế hoạch thời gian */}
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  6. Thời gian hoàn thành:
                </label>
                <select
                  value={targetWeeks}
                  onChange={(e) => setTargetWeeks(Number(e.target.value))}
                  className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-3 text-xs font-bold text-slate-800 dark:text-slate-200"
                >
                  <option value={2}>2 tuần (Cấp tốc)</option>
                  <option value={4}>4 tuần (1 tháng)</option>
                  <option value={6}>6 tuần (1.5 tháng)</option>
                  <option value={8}>8 tuần (2 tháng)</option>
                  <option value={12}>12 tuần (1 học kỳ)</option>
                </select>
              </div>

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  7. Thời lượng học mỗi ngày:
                </label>
                <select
                  value={minutesPerDay}
                  onChange={(e) => setMinutesPerDay(Number(e.target.value))}
                  className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-3 text-xs font-bold text-slate-800 dark:text-slate-200"
                >
                  <option value={15}>15 phút / ngày</option>
                  <option value={30}>30 phút / ngày</option>
                  <option value={45}>45 phút / ngày</option>
                  <option value={60}>60 phút / ngày</option>
                  <option value={90}>90 phút / ngày</option>
                </select>
              </div>

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  8. Số ngày học trong tuần:
                </label>
                <select
                  value={daysPerWeek}
                  onChange={(e) => setDaysPerWeek(Number(e.target.value))}
                  className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-3 text-xs font-bold text-slate-800 dark:text-slate-200"
                >
                  <option value={3}>3 ngày / tuần</option>
                  <option value={4}>4 ngày / tuần</option>
                  <option value={5}>5 ngày / tuần</option>
                  <option value={6}>6 ngày / tuần</option>
                  <option value={7}>Cả tuần (7 ngày)</option>
                </select>
              </div>
            </div>

            {/* Q9, Q10: Nhịp độ & Phong cách học */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  9. Nhịp độ học tập ưu tiên:
                </label>
                <div className="grid grid-cols-3 gap-2">
                  {[
                    { value: "Gentle", label: "Nhẹ nhàng" },
                    { value: "Moderate", label: "Vừa phải" },
                    { value: "Accelerated", label: "Tăng tốc" },
                  ].map((p) => (
                    <button
                      key={p.value}
                      type="button"
                      onClick={() => setPace(p.value)}
                      className={`p-2.5 rounded-xl text-xs font-bold border transition-all text-center ${
                        pace === p.value
                          ? "border-indigo-600 bg-indigo-50 dark:bg-indigo-950/60 text-indigo-900 dark:text-indigo-200"
                          : "border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/60 text-slate-700 dark:text-slate-300"
                      }`}
                    >
                      {p.label}
                    </button>
                  ))}
                </div>
              </div>

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  10. Phong cách học tập:
                </label>
                <div className="grid grid-cols-3 gap-2">
                  {[
                    { value: "TheoryHeavy", label: "Lý thuyết" },
                    { value: "Balanced", label: "Cân bằng" },
                    { value: "PracticeHeavy", label: "Bài tập" },
                  ].map((m) => (
                    <button
                      key={m.value}
                      type="button"
                      onClick={() => setPreferredMode(m.value)}
                      className={`p-2.5 rounded-xl text-xs font-bold border transition-all text-center ${
                        preferredMode === m.value
                          ? "border-indigo-600 bg-indigo-50 dark:bg-indigo-950/60 text-indigo-900 dark:text-indigo-200"
                          : "border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/60 text-slate-700 dark:text-slate-300"
                      }`}
                    >
                      {m.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Ghi chú thêm */}
            <div className="space-y-2">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                Ghi chú riêng của bạn (tùy chọn):
              </label>
              <textarea maxLength={1000}
                rows={2}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Ví dụ: Cần tập trung ôn kỹ phần hình không gian cho bài kiểm tra giữa kỳ..."
                className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-3 text-xs font-medium text-slate-800 dark:text-slate-200"
              />
            </div>

            <div className="pt-2 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setIsQuestionnaireOpen(false)}
                className="px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                disabled={generateMutation.isPending}
                className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-xs transition-colors disabled:opacity-50 flex items-center gap-2"
              >
                <span>{generateMutation.isPending ? "⏳ AI đang xây dựng lộ trình..." : detailedPath ? "↻ AI Đề Xuất Lại Lộ Trình" : "✨ AI Đề Xuất Lộ Trình Học Tập"}</span>
              </button>
            </div>
          </form>
        </div>
      )}

      {/* ROADMAP CONTENT (When path is available) */}
      {isPathLoading ? (
        <div className="space-y-4 animate-pulse">
          <div className="h-32 bg-slate-100 dark:bg-slate-800 rounded-3xl" />
          <div className="h-64 bg-slate-100 dark:bg-slate-800 rounded-3xl" />
        </div>
      ) : detailedPath ? (
        <div className="space-y-7">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 shadow-xs border border-slate-200/80 dark:border-slate-800">
              <span className="text-xs font-bold text-slate-400 block">Độ thành thạo hiện tại</span>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black text-slate-900 dark:text-white">
                  {detailedPath.currentOverallMastery}%
                </span>
                <span className="text-xs text-slate-500">trung bình môn</span>
              </div>
            </div>

            <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 shadow-xs border border-slate-200/80 dark:border-slate-800">
              <span className="text-xs font-bold text-slate-400 block">Mục tiêu · {({ ExamPrep: "Ôn thi", Foundation: "Củng cố nền tảng", KeepUp: "Theo kịp lớp", ImproveGrade: "Cải thiện điểm", Advanced: "Nâng cao" } as Record<string, string>)[detailedPath.goalType] || "Học tập"}</span>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black text-indigo-600 dark:text-indigo-400">
                  {detailedPath.targetMastery}%
                </span>
                <span className="text-xs text-slate-500">trong {detailedPath.estimatedWeeks} tuần</span>
              </div>
            </div>

            <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 shadow-xs border border-slate-200/80 dark:border-slate-800">
              <span className="text-xs font-bold text-slate-400 block">Kế hoạch rèn luyện</span>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black text-slate-900 dark:text-white">
                  {detailedPath.minutesPerDay} phút
                </span>
                <span className="text-xs text-slate-500">/ ngày ({detailedPath.daysPerWeek} ngày/tuần)</span>
              </div>
            </div>

            <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 shadow-xs border border-slate-200/80 dark:border-slate-800">
              <span className="text-xs font-bold text-slate-400 block">Tiến độ hoàn thành</span>
              <div className="mt-2 flex items-baseline gap-2">
                <span className="text-2xl font-black text-emerald-600 dark:text-emerald-400">
                  {detailedPath.progressPercentage}%
                </span>
                <span className="text-xs text-slate-500">toàn bộ lộ trình</span>
              </div>
            </div>
          </div>

          {detailedPath.nextSession && (
            <div className="rounded-3xl bg-gradient-to-r from-indigo-600 via-indigo-700 to-purple-700 text-white p-6 sm:p-7 shadow-lg shadow-indigo-500/10 space-y-4">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold uppercase tracking-wider text-indigo-200 flex items-center gap-1.5">
                  <span>⚡</span> Phiên học trọng tâm tiếp theo
                </span>
                <span className="px-3 py-1 rounded-full text-xs font-bold bg-white/20 backdrop-blur-xs">
                  ⏱ {detailedPath.nextSession.estimatedMinutes} phút
                </span>
              </div>
              <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-5">
                <div className="space-y-2 max-w-2xl">
                  <h2 className="text-xl sm:text-2xl font-black text-white">
                    {detailedPath.nextSession.title}
                  </h2>
                  <p className="text-xs sm:text-sm text-indigo-100 leading-relaxed font-medium">
                    {detailedPath.nextSession.objective}
                  </p>
                  <div className="pt-1 flex flex-wrap items-center gap-3 text-xs text-indigo-100">
                    <span className="inline-flex items-center gap-1">
                      <span>🎯</span> Mục tiêu: {detailedPath.nextSession.targetMastery}% mastery
                    </span>
                    <span>•</span>
                    <span className="inline-flex items-center gap-1">
                      <span>🏁</span> Checkpoint đạt: ≥ {Math.round((detailedPath.nextSession.requiredCorrectRate || 0.8) * 100)}%
                    </span>
                  </div>
                </div>

                <div className="shrink-0">
                  {detailedPath.nextSession.status === "InProgress" ? (
                    <button
                      type="button"
                      onClick={() => handleResumeSession(detailedPath.nextSession!.sessionId)}
                      className="px-6 py-3 rounded-xl bg-white hover:bg-slate-50 text-indigo-900 text-xs sm:text-sm font-black shadow-md transition-all cursor-pointer flex items-center gap-2"
                    >
                      <span>⚡</span>
                      <span>Tiếp tục phiên đang làm →</span>
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={() => setPrepSession(detailedPath.nextSession!)}
                      className="px-6 py-3 rounded-xl bg-white hover:bg-slate-50 text-indigo-900 text-xs sm:text-sm font-black shadow-md transition-all cursor-pointer flex items-center gap-2"
                    >
                      <span>🚀</span>
                      <span>Chuẩn bị & Bắt đầu học</span>
                    </button>
                  )}
                </div>
              </div>
            </div>
          )}

          {detailedPath.adaptationMessage && (
            <div className="rounded-2xl border border-amber-300 bg-amber-50 p-4 text-xs sm:text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-200 flex items-start gap-3">
              <span className="text-lg">📢</span>
              <div>
                <strong>Lộ trình đã được tối ưu thích ứng:</strong> {detailedPath.adaptationMessage}
              </div>
            </div>
          )}

          {detailedPath.recommendationRationale && (
            <div className="rounded-3xl bg-gradient-to-r from-indigo-500/10 via-purple-500/10 to-transparent p-6 border border-indigo-200/80 dark:border-indigo-900/60 space-y-2">
              <div className="flex items-center gap-2">
                <span className="text-base">💡</span>
                <h3 className="text-xs sm:text-sm font-bold uppercase tracking-wider text-indigo-950 dark:text-indigo-300">
                  Vì sao EduTwin đề xuất lộ trình này?
                </h3>
              </div>
              <p className="text-xs sm:text-sm text-slate-700 dark:text-slate-300 leading-relaxed font-medium">
                {detailedPath.recommendationRationale}
              </p>
            </div>
          )}

          <div className="space-y-6">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span>🗺</span> Kế hoạch theo giai đoạn, tuần và buổi học
              </h2>
              <span className="text-xs font-semibold text-slate-500">
                Phiên bản lộ trình: v{detailedPath.version}
              </span>
            </div>

            <div className="space-y-6">
              {detailedPath.phases.map((phase) => (
                <details key={phase.phaseNumber} open={phase.phaseNumber === 1} className="group rounded-3xl bg-white dark:bg-[#0f172a] p-5 sm:p-6 border border-slate-200/80 dark:border-slate-800">
                  <summary className="cursor-pointer list-none flex items-center justify-between gap-4">
                    <div>
                      <h3 className="font-extrabold text-slate-900 dark:text-white">{phase.phaseName}</h3>
                      <p className="text-xs text-slate-500 mt-1">{phase.timeframe} · {phase.description}</p>
                    </div>
                    <span className="text-xs font-bold text-indigo-600">{phase.progressPercentage}%</span>
                  </summary>
                  <div className="mt-5 space-y-4">
                    {phase.weeks.map((week) => (
                      <details key={week.weekNumber} open={week.weekNumber === 1} className="rounded-2xl border border-slate-200 dark:border-slate-700 p-4">
                        <summary className="cursor-pointer list-none flex justify-between gap-3">
                          <div>
                            <h4 className="text-sm font-bold text-slate-900 dark:text-white">{week.title}</h4>
                            <p className="text-xs text-slate-500 mt-1">{week.objective}</p>
                          </div>
                          <span className="text-xs text-slate-500">{week.sessions.length} buổi</span>
                        </summary>
                        <div className="mt-4 grid grid-cols-1 lg:grid-cols-2 gap-4">
                          {week.sessions.map((session, sessionIndex) => (
                            <article key={session.sessionId} className="rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/90 dark:border-slate-700/80 p-5 space-y-4 flex flex-col justify-between">
                              <div className="space-y-3">
                                <div className="flex items-start justify-between gap-2">
                                  <div>
                                    <span className="text-[10px] uppercase font-bold text-indigo-600 dark:text-indigo-400 block mb-0.5">
                                      Buổi {sessionIndex + 1} · {session.estimatedMinutes} phút
                                    </span>
                                    <h5 className="text-sm font-bold text-slate-900 dark:text-white">
                                      {session.title}
                                    </h5>
                                  </div>
                                  <div className="shrink-0">
                                    {renderSessionStatusBadge(session.status)}
                                  </div>
                                </div>

                                <p className="text-xs text-slate-600 dark:text-slate-300 font-medium">
                                  <strong>Mục tiêu:</strong> {session.objective}
                                </p>

                                <div className="space-y-1.5 text-xs text-slate-600 dark:text-slate-400 bg-white/60 dark:bg-slate-900/40 p-3 rounded-xl border border-slate-200/60 dark:border-slate-800">
                                  <div><strong>Chuyên đề:</strong> {session.topicName}</div>
                                  <div><strong>Hoạt động:</strong> {session.onlineActivities.join(" • ") || "Luyện tập thích ứng"}</div>
                                  <div className="text-[11px] text-amber-700 dark:text-amber-400">
                                    <strong>Đánh giá:</strong> Checkpoint độc lập (≥ {Math.round((session.requiredCorrectRate || 0.8) * 100)}% đúng, không gợi ý)
                                  </div>
                                </div>
                              </div>

                              <div className="pt-2 border-t border-slate-200/60 dark:border-slate-700/60 flex items-center justify-between">
                                <span className="text-[11px] text-slate-400">
                                  Mục tiêu: {session.targetMastery}% mastery
                                </span>

                                {session.status === "InProgress" ? (
                                  <button
                                    type="button"
                                    onClick={() => handleResumeSession(session.sessionId)}
                                    className="px-3.5 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold transition-colors cursor-pointer flex items-center gap-1.5"
                                  >
                                    <span>Tiếp tục học</span>
                                    <span>→</span>
                                  </button>
                                ) : session.status === "Completed" ? (
                                  <button
                                    type="button"
                                    onClick={() => setPrepSession(session)}
                                    className="px-3 py-1.5 rounded-lg bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-slate-800 dark:text-slate-200 text-xs font-bold transition-colors cursor-pointer flex items-center gap-1.5"
                                  >
                                    <span>Luyện tập lại</span>
                                    <span>↺</span>
                                  </button>
                                ) : session.status === "NeedsReview" ? (
                                  <button
                                    type="button"
                                    onClick={() => handleResumeSession(session.sessionId)}
                                    className="px-3.5 py-1.5 rounded-lg bg-amber-500 hover:bg-amber-600 text-white text-xs font-bold transition-colors cursor-pointer flex items-center gap-1.5"
                                  >
                                    <span>Chờ duyệt</span>
                                    <span>⏳</span>
                                  </button>
                                ) : (
                                  <button
                                    type="button"
                                    onClick={() => setPrepSession(session)}
                                    className="px-3.5 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold transition-colors cursor-pointer flex items-center gap-1.5"
                                  >
                                    <span>Chuẩn bị & Bắt đầu</span>
                                    <span>→</span>
                                  </button>
                                )}
                              </div>
                            </article>
                          ))}
                        </div>
                        <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50/70 p-3.5 text-xs text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-200 flex items-start gap-2">
                          <span className="text-base">🏁</span>
                          <div>
                            <strong>{week.checkpoint.type === "Monthly" ? "Checkpoint tháng" : "Checkpoint tuần"}:</strong> mục tiêu {week.checkpoint.targetMastery}% mastery, yêu cầu ≥ {Math.round(week.checkpoint.requiredCorrectRate * 100)}% đúng. {week.checkpoint.recommendation}
                          </div>
                        </div>
                      </details>
                    ))}
                  </div>
                </details>
              ))}
            </div>
          </div>
        </div>
      ) : (
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-12 text-center border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
          <div className="text-4xl">🎯</div>
          <h3 className="text-lg font-bold text-slate-900 dark:text-white">
            Chưa có lộ trình học tập cho môn {currentSubject?.subjectName || "này"}
          </h3>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 max-w-md mx-auto">
            Vui lòng hoàn thành khảo sát mục tiêu ngắn để hệ thống EduTwin xây dựng lộ trình thích ứng riêng cho bạn.
          </p>
          <button
            type="button"
            onClick={() => setIsQuestionnaireOpen(true)}
            className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
          >
            Bắt đầu khảo sát mục tiêu
          </button>
        </div>
      )}

      {/* Preparation Modal */}
      <SessionPreparationModal
        isOpen={Boolean(prepSession)}
        onClose={() => {
          setPrepSession(null);
          setPrepError(null);
        }}
        session={prepSession}
        subjectName={currentSubject?.subjectName || "Môn học"}
        onStart={handleStartSession}
        isStarting={isStartingSession}
        error={prepError}
      />
    </div>
  );
};
