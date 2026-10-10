import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { getReconciledStudentAcademicContext } from "../../api/dashboardsApi";
import { useAuthStore } from "../../stores/authStore";
import { studentClassesInView, reconcileStudentClassParam } from "../../utils/studentAcademicScope";
import { studentScopeChangeUrl } from "../../utils/studentAcademicNavigation";

export const StudentAcademicContext = () => {
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
  return <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-2 border-t border-[var(--student-border)] text-sm space-y-2" data-testid="student-academic-context">
    <div className="flex flex-wrap items-center gap-3">
      <div className="inline-flex rounded-lg border border-[var(--student-border)] p-1 gap-1">
        <button type="button" aria-pressed={!history} onClick={() => changeMode(false)} className={`rounded-md px-3 py-1 font-semibold ${!history ? "bg-[var(--student-brand)] text-white" : "text-[var(--student-ink)]"}`}>Đang học</button>
        <button type="button" aria-pressed={history} onClick={() => changeMode(true)} className={`rounded-md px-3 py-1 font-semibold ${history ? "bg-[var(--student-brand)] text-white" : "text-[var(--student-ink)]"}`}>Xem lịch sử</button>
      </div>
      {subject && <label className="flex items-center gap-2 min-w-0">Lớp:
        <select aria-label="Lớp học đang xem" disabled={query.isPending || query.isError || classes.length === 0}
          value={classId || query.data?.selectedClassId || ""} onChange={e => { const p = new URLSearchParams(params); p.set("classId", e.target.value); navigate(studentScopeChangeUrl(location.pathname, p)); }}
          className="max-w-full sm:max-w-[360px] rounded-lg border border-[var(--student-border)] bg-[var(--student-surface)] px-3 py-1.5 font-semibold">
          {classes.length === 0 && <option value="">{history ? "Chưa có lịch sử lớp học" : "Chưa có lớp đang học"}</option>}
          {classes.map(c => <option value={c.classId} key={c.classId}>{c.className}{c.gradeLevel ? ` · Khối ${c.gradeLevel}` : ""}</option>)}
        </select>
      </label>}
      {query.isError && <span role="alert">Không thể tải phạm vi lớp. Hãy chọn lại môn/lớp hoặc tải lại trang.</span>}
    </div>
    {query.data?.curriculums.length ? <p className="text-[var(--student-ink-muted)]">
      {history ? "Giáo trình từng áp dụng" : "Giáo trình do giáo viên áp dụng"}: {query.data.curriculums.map(c => `${c.title}${c.applicationRole === "Supplemental" ? " (bổ trợ)" : ""}`).join("; ")}.
    </p> : query.data?.message && <p className="text-[var(--student-ink-muted)]">{query.data.message}</p>}
    {history && classes.length > 0 && <p className="text-[var(--student-ink-muted)]">Chỉ xem lớp đã kết thúc hoặc lớp bạn đã rời khỏi; không thay đổi trạng thái lớp, điểm hoặc năng lực đã tích lũy.</p>}
  </div>;
};
