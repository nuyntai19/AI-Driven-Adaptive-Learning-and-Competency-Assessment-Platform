import { useEffect, useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { httpClient } from "../../api/httpClient";
import { organizationApi } from "../../api/organizationApi";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import { extractProblemDetails } from "../../utils/problemDetails";
import { groupCurriculumClasses, needsNewGradeException, activeApplicationClassCount } from "../../utils/curriculumApplicationClasses";
import type { ClassDto } from "../../types/organization";

interface Application { applicationId: string; classId: string; className: string; pausedByClass?: boolean; applicationRole: string; startedAt: string; endedAt: string | null; changeReason: string | null; endReason: string | null; gradeMismatchReason: string | null }
interface Response { data: Application[]; rowVersion: string }
interface Props { curriculumId: string; subjectId: string; gradeLevel?: number | null; readOnly?: boolean }

export const CurriculumApplicationPanel = ({ curriculumId, subjectId, gradeLevel, readOnly = false }: Props) => {
  const user = useAuthStore(s => s.user);
  const actor = user?.userId;
  const hasPermission = useAuthStore(s => s.hasPermission);
  const editable = !readOnly && hasPermission(permissions.curriculumsUpdate);
  const client = useQueryClient();
  const [role, setRole] = useState("Primary");
  const [selected, setSelected] = useState<string[]>([]);
  const [baseVersion, setBaseVersion] = useState("");
  const [dirty, setDirty] = useState(false);
  const [showExceptions, setShowExceptions] = useState(false);
  const [reason, setReason] = useState("");
  const [gradeReason, setGradeReason] = useState("");
  const [feedback, setFeedback] = useState("");
  const key = ["curriculum-applications", curriculumId, user?.centerId, actor];
  const applications = useQuery({ queryKey:key,
    queryFn: async () => (await httpClient.get<Response>(`/curriculums/${curriculumId}/applications`)).data });
  const classes = useQuery({ queryKey: ["teacherClassesList", "application", user?.centerId, actor, subjectId],
    enabled: !!actor && editable, queryFn: async () => {
    const filter = { pageSize:100, teacherId:actor, subjectId, status:"Active" as const };
    const first = await organizationApi.listClasses({ ...filter, page:1 });
    const result = [...first.data];
    for(let page=2; page<=first.meta.totalPages; page++) result.push(...(await organizationApi.listClasses({ ...filter, page })).data);
    return result;
  } });
  const savedIds = useMemo(() => applications.data?.data.filter(a => !a.endedAt && a.applicationRole === role)
    .map(a => a.classId) ?? [], [applications.data, role]);
  const groups = useMemo(() => groupCurriculumClasses(classes.data ?? [], actor, subjectId, gradeLevel, savedIds),
    [classes.data, actor, subjectId, gradeLevel, savedIds]);
  useEffect(() => {
    if (dirty || !applications.data || (editable && !classes.data)) return;
    setSelected(savedIds.filter(id => groups.eligible.some(c => c.classId === id)));
    setBaseVersion(applications.data.rowVersion);
    if (groups.exceptions.some(c => savedIds.includes(c.classId))) setShowExceptions(true);
  }, [applications.data, classes.data, savedIds, groups, dirty, editable]);
  const stale = dirty && !!applications.data && baseVersion !== applications.data.rowVersion;
  const requiresGradeReason = needsNewGradeException(groups.eligible, selected, savedIds, gradeLevel);
  const save = useMutation({ mutationFn: async () => {
    if (!editable || !applications.data || !classes.data || classes.isError || applications.isError)
      throw new Error("Chưa tải đầy đủ dữ liệu hoặc không có quyền áp dụng.");
    if (stale) throw new Error("Phiên bản áp dụng đã thay đổi. Hủy lựa chọn chưa lưu và xem lại trước khi lưu.");
    if (requiresGradeReason && !gradeReason.trim()) throw new Error("Chọn lớp khác khối cần lý do ngoại lệ.");
    return (await httpClient.put<Response>(`/curriculums/${curriculumId}/applications`, { classIds:selected,
      rowVersion:baseVersion, applicationRole:role, changeReason:reason.trim() || null, gradeMismatchReason:gradeReason.trim() || null })).data;
  }, onSuccess: (data) => {
    client.setQueryData(key, data);
    client.invalidateQueries({queryKey:["teacherCurriculumDetail",curriculumId]});
    client.invalidateQueries({queryKey:["teacherCurriculums"]});
    client.invalidateQueries({queryKey:["student-academic-context"]});
    client.invalidateQueries({queryKey:["studentDashboard"]});
    setDirty(false); setFeedback("Đã lưu áp dụng giáo trình và lịch sử thay đổi."); setReason(""); setGradeReason("");
  }, onError: (error) => { setFeedback(extractProblemDetails(error).detail || (error as Error).message); } });
  const cancel = () => { setDirty(false); setReason(""); setGradeReason(""); setFeedback(""); };
  const renderClass = (c: ClassDto) => <label key={c.classId} className="flex gap-2 items-center text-sm">
    <input type="checkbox" disabled={save.isPending} checked={selected.includes(c.classId)} onChange={e => {
      setDirty(true); setFeedback("");
      setSelected(prev => e.target.checked ? [...prev,c.classId] : prev.filter(id => id !== c.classId));
    }} />
    {c.className} · {c.gradeLevel != null ? `Khối ${c.gradeLevel}` : "Chưa phân khối — áp dụng hiện có"}
  </label>;
  return <section className="th-surface rounded-xl p-5 mb-5 space-y-4" aria-label="Áp dụng giáo trình cho lớp">
    <h2 className="text-lg font-bold">{readOnly ? "Áp dụng và lịch sử giáo trình" : "Áp dụng giáo trình cho lớp phụ trách"} ({applications.data ? `${activeApplicationClassCount(applications.data.data)} lớp đang áp dụng` : "đang tải"})</h2>
    <p className="text-sm">Mỗi lớp chỉ có một giáo trình chính, có thể có nhiều giáo trình bổ trợ. Thay/ngừng áp dụng cần lý do; lịch sử được giữ lại. Đổi lớp áp dụng không cần nhân bản giáo trình.</p>
    {applications.isError ? <p role="alert">Không tải được lịch sử áp dụng. <button onClick={() => applications.refetch()}>Tải lại</button></p> : editable ? <>
      <label className="block text-sm">Vai trò áp dụng
        <select className="th-select block mt-1" aria-label="Vai trò áp dụng giáo trình" value={role}
          disabled={save.isPending || dirty} onChange={e => {setRole(e.target.value); setFeedback("");}}>
          <option value="Primary">Giáo trình chính</option><option value="Supplemental">Giáo trình bổ trợ</option>
        </select>
      </label>
      {dirty && <p className="text-sm">Lưu hoặc hủy lựa chọn trước khi đổi vai trò áp dụng.</p>}
      {stale && <p role="alert">Phiên bản server đã thay đổi; lựa chọn của bạn chưa bị ghi đè. Hủy lựa chọn chưa lưu để tải phiên bản mới.</p>}
      {classes.isError && <p role="alert">Chưa tải đầy đủ danh sách lớp. Không thể lưu thay đổi; <button onClick={() => classes.refetch()}>tải lại</button>.</p>}
      <h3 className="font-semibold">Lớp cùng khối {gradeLevel ?? "chưa xác định"}</h3>
      <div className="grid sm:grid-cols-2 gap-2">{groups.matching.map(renderClass)}</div>
      {!classes.isPending && groups.matching.length === 0 && <p>Chưa có lớp cùng môn, cùng khối đang học do bạn phụ trách.</p>}
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={showExceptions}
        disabled={save.isPending} onChange={e => setShowExceptions(e.target.checked)} />Hiện lớp khác khối (ngoại lệ)</label>
      {showExceptions && <div className="space-y-3 rounded-lg border border-amber-500/50 p-4">
        <p className="text-sm">Chỉ chọn khi có mục đích sư phạm rõ ràng. Lớp mới khác khối cần nhập lý do; ngoại lệ hiện có được giữ nguyên.</p>
        <div className="grid sm:grid-cols-2 gap-2">{groups.exceptions.map(renderClass)}</div>
        {groups.exceptions.length === 0 && <p>Không có lớp ngoại lệ phù hợp.</p>}
      </div>}
      {!showExceptions && groups.exceptions.some(c => selected.includes(c.classId)) &&
        <p className="text-sm">Có lớp ngoại lệ đang được chọn; ẩn danh sách không bỏ gán lớp.</p>}
      <label className="block text-sm">Lý do thay/ngừng áp dụng (bắt buộc nếu thay hoặc ngừng giáo trình cũ)
        <textarea className="th-input block w-full mt-1" disabled={save.isPending} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} />
      </label>
      {(showExceptions || requiresGradeReason) && <label className="block text-sm">Lý do ngoại lệ khác khối {requiresGradeReason ? "(bắt buộc)" : "(chỉ khi gán ngoại lệ mới)"}
        <textarea className="th-input block w-full mt-1" disabled={save.isPending} required={requiresGradeReason} maxLength={500}
          value={gradeReason} onChange={e => setGradeReason(e.target.value)} />
      </label>}
      <div className="flex flex-wrap gap-3">
        <button type="button" className="th-primary-button"
          disabled={save.isPending || applications.isPending || classes.isPending || classes.isError || stale || !dirty || (requiresGradeReason && !gradeReason.trim())}
          onClick={() => save.mutate()}>{save.isPending ? "Đang lưu..." : "Lưu áp dụng giáo trình"}</button>
        {dirty && <button type="button" className="th-secondary-button" disabled={save.isPending} onClick={cancel}>Hủy lựa chọn chưa lưu</button>}
      </div>
    </> : <ul className="space-y-2 text-sm">{applications.data?.data.filter(a => !a.endedAt).map(a =>
      <li key={a.applicationId}>{a.className} · {a.applicationRole === "Primary" ? "Chính" : "Bổ trợ"} · {a.pausedByClass ? "Tạm ngừng — lớp đã lưu trữ" : "Đang áp dụng"}</li>)}</ul>}
    {feedback && <p role="status">{feedback}</p>}
    <details><summary className="font-semibold cursor-pointer">Lịch sử áp dụng ({applications.data?.data.length ?? 0})</summary>
      <ul className="mt-3 space-y-2 text-sm">{applications.data?.data.map(a => <li key={a.applicationId}>
        {a.className} · {a.applicationRole === "Primary" ? "Chính" : "Bổ trợ"} · {a.endedAt ? "Đã ngừng" : a.pausedByClass ? "Tạm ngừng — lớp đã lưu trữ" : "Đang áp dụng"} · {new Date(a.startedAt).toLocaleString("vi-VN")}
        {a.endedAt && ` → ${new Date(a.endedAt).toLocaleString("vi-VN")}`}{a.changeReason && ` · Bắt đầu: ${a.changeReason}`}{a.endReason && ` · Ngừng: ${a.endReason}`}{a.gradeMismatchReason && ` · Ngoại lệ: ${a.gradeMismatchReason}`}
      </li>)}</ul>
    </details>
  </section>;
};
