import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { extendPathWithCheck } from "../../api/learningPathApi";
import type { SkillCapabilityStatusDto } from "../../types/learningPath";
import { extractProblemDetails } from "../../utils/problemDetails";

/** Tests more parts inside the current route; the route and its progress are kept. */
export function ExtendCheckModal({ subjectId, classId, subjectName, candidates, onClose, onStarted }: {
  subjectId: string; classId?: string; subjectName: string;
  candidates: (SkillCapabilityStatusDto & { note?: string })[];
  onClose: () => void; onStarted: (missionIds: string[]) => void;
}) {
  const [selected, setSelected] = useState(candidates.map(s => s.skillId));
  const [unavailable, setUnavailable] = useState<string[]>([]);
  const start = useMutation({
    mutationFn: () => extendPathWithCheck({ subjectId, classId, skillIds: selected }),
    onSuccess: res => {
      setUnavailable(res.unavailableSkillIds);
      if (res.missionIds.length) onStarted(res.missionIds);
    }
  });
  const nameOf = (id: string) => candidates.find(s => s.skillId === id)?.skillName ?? id;
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="extend-title" className="flex max-h-[90vh] w-full max-w-xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 text-slate-900 shadow-2xl dark:bg-slate-900 dark:text-slate-100">
      <header className="flex items-start justify-between gap-4 border-b border-slate-100 pb-4">
        <div><h2 id="extend-title" className="text-2xl font-semibold">Kiểm tra thêm phần khác · {subjectName}</h2>
          <p className="mt-2 text-sm text-slate-500">Lộ trình đang học được giữ nguyên. Mỗi phần có 5 câu mới; đạt thì giữ lịch ôn, chưa đạt sẽ thêm bộ rèn lại vào lộ trình hiện tại.</p></div>
        <button onClick={onClose} aria-label="Đóng" className="rounded-lg border px-3 py-1">×</button>
      </header>
      <div className="min-h-0 space-y-3 overflow-y-auto py-4">
        {unavailable.length > 0 && <p role="alert" className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">Chưa đủ câu hỏi mới để kiểm tra {unavailable.map(nameOf).join(", ")} (cần 5 câu chưa làm). Hãy báo giáo viên bổ sung câu hỏi.</p>}
        {start.error && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(start.error).message}</p>}
        <fieldset className="space-y-2"><legend className="text-sm font-semibold">Chọn phần muốn kiểm tra</legend>
          {candidates.map(s => <label key={s.skillId} className="flex items-start gap-2 rounded-xl border border-slate-200 p-3 text-sm dark:border-slate-700">
            <input type="checkbox" checked={selected.includes(s.skillId)} onChange={e => { setUnavailable([]);
              setSelected(e.target.checked ? [...selected, s.skillId] : selected.filter(id => id !== s.skillId)); }} />
            <span><span className="font-medium">{s.skillName}</span>{s.note && <span className="block text-xs text-slate-500">{s.note}</span>}</span>
          </label>)}
        </fieldset>
        <p className="text-sm text-slate-500">Khoảng {selected.length * 10} phút. Các phần nối tiếp nhau; có thể lưu và làm tiếp sau.</p>
      </div>
      <footer className="border-t border-slate-100 pt-4">
        <button disabled={!selected.length || start.isPending} onClick={() => start.mutate()} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{start.isPending ? "Đang tạo bài kiểm tra…" : "Bắt đầu kiểm tra"}</button>
      </footer>
    </section>
  </div>;
}
