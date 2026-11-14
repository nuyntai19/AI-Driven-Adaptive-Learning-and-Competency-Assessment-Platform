import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { getReconciledStudentAcademicContext } from "../../api/dashboardsApi";
import { useAuthStore } from "../../stores/authStore";
import { studentClassesInView, reconcileStudentClassParam } from "../../utils/studentAcademicScope";
import { studentScopeChangeUrl } from "../../utils/studentAcademicNavigation";

/** `inline` renders inside the layout context row instead of as its own full-width row. */
export const StudentAcademicContext = ({ inline = false }: { inline?: boolean }) => {
  const [params, setParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const user = useAuthStore(s => s.user);
  const subject = params.get("subjectId") || "";
  const classId = params.get("classId") || "";
  const history = params.get("history") === "true";
  const query = useQuery({ queryKey: ["student-academic-context", user?.centerId, user?.userId, subject, classId, history],
    queryFn: () => getReconciledStudentAcademicContext(subject, classId, history),
    enabled: !!user, staleTime: 30_000 });
  useEffect(() => {
    if (!query.data) return;
    const next = reconcileStudentClassParam(params, query.data.selectedClassId);
    if (next) setParams(next, { replace: true });
  }, [subject, classId, query.data?.selectedClassId, params, setParams]);
  const changeMode = (next: boolean) => {
    if (next === history) return;
    const p = new URLSearchParams(params); p.delete("classId");
    if (next) p.set("history", "true"); else p.delete("history");
    navigate(studentScopeChangeUrl(location.pathname, p));
  };
  const classes = studentClassesInView(query.data?.classes ?? [], history);
  // The switch only matters to a student who has left a class or whose class has ended; otherwise it is noise.
  const hasPastClasses = (query.data?.classes ?? []).some(c => c.isHistorical);
  const showModeSwitch = history || hasPastClasses;
  const onlyClass = !history && classes.length === 1 ? classes[0] : null;
  return <div className={`${inline ? "contents" : "w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-2 flex flex-wrap items-center gap-x-4 gap-y-1.5"} text-[13px] text-[var(--student-ink-secondary)]`} data-testid="student-academic-context">
    <div className="flex flex-wrap items-center gap-3">
      {showModeSwitch && <div className="inline-flex rounded-full bg-[var(--student-surface-subtle)] p-1 gap-1" role="group" aria-label="Chọn lớp đang học hoặc lớp đã kết thúc">
        <button type="button" aria-pressed={!history} title="Lớp em đang theo học: làm bài, học lộ trình như bình thường" onClick={() => changeMode(false)} className={`rounded-full px-3 py-1 font-semibold transition-colors ${!history ? "st-nav-active" : "text-[var(--student-ink-secondary)] hover:text-[var(--student-ink)]"}`}>Lớp đang học</button>
        <button type="button" aria-pressed={history} title="Lớp đã kết thúc hoặc em đã rời: chỉ xem lại bài làm và kết quả cũ" onClick={() => changeMode(true)} className={`rounded-full px-3 py-1 font-semibold transition-colors ${history ? "st-nav-active" : "text-[var(--student-ink-secondary)] hover:text-[var(--student-ink)]"}`}>Lớp đã kết thúc</button>
      </div>}
      {subject && onlyClass && <p className="min-w-0 inline-flex items-center gap-1.5 rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-3 py-1">Lớp <strong className="font-semibold text-[var(--student-ink)]">{onlyClass.className}{onlyClass.gradeLevel ? ` · Khối ${onlyClass.gradeLevel}` : ""}</strong></p>}
      {subject && !onlyClass && <label className="flex items-center gap-2 min-w-0">Lớp:
        <select aria-label="Lớp học đang xem" disabled={query.isPending || query.isError || classes.length === 0}
          value={classId || query.data?.selectedClassId || ""} onChange={e => { const p = new URLSearchParams(params); p.set("classId", e.target.value); navigate(studentScopeChangeUrl(location.pathname, p)); }}
          className="max-w-full sm:max-w-[360px] rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-3 py-1 font-semibold text-[var(--student-ink)]">
          {classes.length === 0 && <option value="">{history ? "Chưa có lịch sử lớp học" : "Chưa có lớp đang học"}</option>}
          {classes.map(c => <option value={c.classId} key={c.classId}>{c.className}{c.gradeLevel ? ` · Khối ${c.gradeLevel}` : ""}</option>)}
        </select>
      </label>}
      {query.isError && <span role="alert">Không thể tải phạm vi lớp. Hãy chọn lại môn/lớp hoặc tải lại trang.</span>}
    </div>
    {query.data?.curriculums.length ? <p className="text-[var(--student-muted)] min-w-0">
      {history ? "Giáo trình từng áp dụng" : "Giáo trình do giáo viên áp dụng"}: {query.data.curriculums.map(c => `${c.title}${c.applicationRole === "Supplemental" ? " (bổ trợ)" : ""}`).join("; ")}.
    </p> : query.data?.message && <p className="text-[var(--student-muted)]">{query.data.message}</p>}
    {history && classes.length > 0 && <p className="text-[var(--student-muted)] basis-full">Chỉ xem lớp đã kết thúc hoặc lớp bạn đã rời khỏi; không thay đổi trạng thái lớp, điểm hoặc năng lực đã tích lũy.</p>}
  </div>;
};
