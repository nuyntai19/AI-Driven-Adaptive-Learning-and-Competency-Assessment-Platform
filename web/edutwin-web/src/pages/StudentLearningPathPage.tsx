import React, { useState, useEffect } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import {
  getLearningPathTopics,
  getLearningPathPreferences,
  generateLearningPath,
  getDetailedLearningPath,
  updateLearningPathSession,
} from "../api/learningPathApi";
import type {
  GenerateLearningPathRequest,
  LearningPathTopicNodeDto,
  DetailedLearningPathDto,
} from "../types/learningPath";

export const StudentLearningPathPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const activeSubjectId = searchParams.get("subjectId") || "";

  // 1. Fetch available subjects
  const { data: subjectsData } = useQuery({
    queryKey: ["subjects", "student-learning-path"],
    queryFn: () => organizationApi.listSubjects(true),
    staleTime: 5 * 60 * 1000,
  });

  const subjects = subjectsData?.data || [];
  const currentSubject = subjects.find((s) => s.subjectId === activeSubjectId) || subjects[0];
  const effectiveSubjectId = currentSubject?.subjectId || "";

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
    queryKey: ["learning-path-topics", effectiveSubjectId],
    queryFn: () => getLearningPathTopics(effectiveSubjectId),
    enabled: !!effectiveSubjectId,
    staleTime: 60 * 1000,
  });

  const topics: LearningPathTopicNodeDto[] = topicsData?.data || [];

  // 3. Fetch existing preferences
  const { data: prefData } = useQuery({
    queryKey: ["learning-path-preferences", effectiveSubjectId],
    queryFn: () => getLearningPathPreferences(effectiveSubjectId),
    enabled: !!effectiveSubjectId,
  });

  // 4. Fetch detailed learning path (with dynamic phases)
  const {
    data: detailedPathData,
    isLoading: isPathLoading,
  } = useQuery({
    queryKey: ["learning-path", effectiveSubjectId],
    queryFn: () => getDetailedLearningPath(effectiveSubjectId),
    enabled: !!effectiveSubjectId,
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
  }, [effectiveSubjectId]);

  // Sync state with saved preferences
  useEffect(() => {
    if (prefData?.data) {
      const p = prefData.data;
      setSelfAssessedLevel(p.selfAssessedLevel || "Medium");
      setWeakTopicIds(p.weakTopicNodeIds || []);
      setFocusTopicIds(p.focusTopicNodeIds || []);
      setGoalType(p.goalType || "Foundation");
      setTargetMastery(p.targetMastery || 80);
      setTargetWeeks(p.targetWeeks || 4);
      setMinutesPerDay(p.minutesPerDay || 30);
      setDaysPerWeek(p.daysPerWeek || 5);
      setPace(p.pace || "Moderate");
      setPreferredMode(p.preferredMode || "Balanced");
      setNote(p.note || "");
    }
  }, [prefData]);

  // If no path yet, open questionnaire by default
  useEffect(() => {
    if (!isPathLoading && !detailedPath && effectiveSubjectId) {
      setIsQuestionnaireOpen(true);
    }
  }, [isPathLoading, detailedPath, effectiveSubjectId]);

  // Generate Learning Path Mutation
  const generateMutation = useMutation({
    mutationFn: (req: GenerateLearningPathRequest) => generateLearningPath(req),
    onSuccess: (response) => {
      queryClient.setQueryData(["learning-path", effectiveSubjectId], response);
      void queryClient.invalidateQueries({ queryKey: ["learning-path", effectiveSubjectId] });
      void queryClient.invalidateQueries({ queryKey: ["learning-path-preferences", effectiveSubjectId] });
      setIsQuestionnaireOpen(false);
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
    setFormError(null);

    const payload: GenerateLearningPathRequest = {
      subjectId: effectiveSubjectId,
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

  const startSessionMutation = useMutation({
    mutationFn: (sessionId: string) => updateLearningPathSession(effectiveSubjectId, sessionId, "InProgress"),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["learning-path", effectiveSubjectId] }),
  });

  const startSession = (sessionId: string) => {
    startSessionMutation.mutate(sessionId);
    navigate(`/hoc-tap/luyen-tap?subjectId=${effectiveSubjectId}`);
  };

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-8 min-w-0">
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
            <span>{isQuestionnaireOpen ? "✕ Đóng khảo sát" : detailedPath ? "↻ Cập nhật lộ trình học" : "⚙ Tạo lộ trình học"}</span>
          </button>
        </div>
      </div>

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
              <textarea
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
                <span>{generateMutation.isPending ? "⏳ Đang tính toán..." : detailedPath ? "↻ Cập Nhật Lộ Trình Học" : "🚀 Tạo Lộ Trình Học Tập"}</span>
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
            <div className="rounded-3xl bg-indigo-600 text-white p-6 shadow-sm space-y-3">
              <div className="text-xs font-bold uppercase tracking-wider text-indigo-100">Tiếp theo bạn nên làm gì?</div>
              <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
                <div>
                  <h2 className="text-xl font-black">Hôm nay · {detailedPath.nextSession.estimatedMinutes} phút · {detailedPath.nextSession.title}</h2>
                  <p className="mt-1 text-sm text-indigo-100">{detailedPath.nextSession.objective}</p>
                  <ol className="mt-3 list-decimal list-inside text-sm space-y-1">
                    {detailedPath.nextSession.onlineActivities.slice(0, 3).map((activity) => <li key={activity}>{activity}</li>)}
                  </ol>
                </div>
                <button type="button" onClick={() => startSession(detailedPath.nextSession!.sessionId)} className="rounded-xl bg-white px-5 py-2.5 text-sm font-bold text-indigo-700">Bắt đầu học</button>
              </div>
            </div>
          )}

          {detailedPath.adaptationMessage && (
            <div className="rounded-2xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-200">
              <strong>Lộ trình đã được điều chỉnh.</strong> {detailedPath.adaptationMessage}
            </div>
          )}

          {detailedPath.recommendationRationale && (
            <div className="rounded-3xl bg-gradient-to-r from-indigo-500/10 via-purple-500/10 to-transparent p-6 border border-indigo-200/80 dark:border-indigo-900/60 space-y-2">
              <div className="flex items-center gap-2">
                <span className="text-base">💡</span>
                <h3 className="text-sm font-bold uppercase tracking-wider text-indigo-950 dark:text-indigo-300">
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
                    <div><h3 className="font-extrabold text-slate-900 dark:text-white">{phase.phaseName}</h3><p className="text-xs text-slate-500 mt-1">{phase.timeframe} · {phase.description}</p></div>
                    <span className="text-xs font-bold text-indigo-600">{phase.progressPercentage}%</span>
                  </summary>
                  <div className="mt-5 space-y-4">
                    {phase.weeks.map((week) => (
                      <details key={week.weekNumber} open={week.weekNumber === 1} className="rounded-2xl border border-slate-200 dark:border-slate-700 p-4">
                        <summary className="cursor-pointer list-none flex justify-between gap-3"><div><h4 className="text-sm font-bold text-slate-900 dark:text-white">{week.title}</h4><p className="text-xs text-slate-500 mt-1">{week.objective}</p></div><span className="text-xs text-slate-500">{week.sessions.length} buổi</span></summary>
                        <div className="mt-4 grid grid-cols-1 lg:grid-cols-2 gap-3">
                          {week.sessions.map((session, sessionIndex) => (
                            <article key={session.sessionId} className="rounded-xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 p-4 space-y-3">
                              <div className="flex justify-between gap-2"><div><span className="text-[10px] uppercase font-bold text-indigo-600">Buổi {sessionIndex + 1} · {session.estimatedMinutes} phút</span><h5 className="text-sm font-bold text-slate-900 dark:text-white">{session.title}</h5></div><span className="text-[10px] font-bold text-slate-500">{session.status === "Completed" ? "Đã xong" : session.status === "NeedsReview" ? "Cần ôn" : "Chưa học"}</span></div>
                              <p className="text-xs text-slate-600 dark:text-slate-300"><strong>Mục tiêu:</strong> {session.objective}</p>
                              <div className="text-xs text-slate-600 dark:text-slate-300"><strong>Trên EduTwin:</strong><ul className="mt-1 list-disc pl-4 space-y-1">{session.onlineActivities.map((a) => <li key={a}>{a}</li>)}</ul></div>
                              <div className="text-xs text-slate-600 dark:text-slate-300"><strong>Ngoài EduTwin:</strong><ul className="mt-1 list-disc pl-4 space-y-1">{session.offlineActivities.map((a) => <li key={a}>{a}</li>)}</ul></div>
                              <div className="text-xs text-slate-500">Điều kiện: {session.completionCriteria.join(" · ")}</div>
                              <button type="button" onClick={() => startSession(session.sessionId)} className="text-xs font-bold text-indigo-600 hover:underline">Bắt đầu học →</button>
                            </article>
                          ))}
                        </div>
                        <div className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-200"><strong>{week.checkpoint.type === "Monthly" ? "Checkpoint tháng" : "Checkpoint tuần"}:</strong> mục tiêu {week.checkpoint.targetMastery}% mastery, ≥ {Math.round(week.checkpoint.requiredCorrectRate * 100)}% đúng. {week.checkpoint.recommendation}</div>
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
            className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-xs transition-colors"
          >
            Bắt đầu khảo sát mục tiêu
          </button>
        </div>
      )}
    </div>
  );
};
