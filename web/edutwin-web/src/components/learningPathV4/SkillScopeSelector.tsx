import type { SkillCapabilityStatusDto } from "../../types/learningPath";
import { groupLearningSkills } from "../../utils/learningScope";

export function SkillScopeSelector({ skills, selected, onChange, placement = false, allowedIds, lockedNote }: {
  skills: SkillCapabilityStatusDto[]; selected: string[]; onChange: (ids: string[]) => void; placement?: boolean;
  /** When set, only these parts can be picked (e.g. the parts just tested); the others are shown locked. */
  allowedIds?: string[];
  /** Why the other parts are locked and how to add them. */
  lockedNote?: string;
}) {
  const groups = groupLearningSkills(skills);
  const allowed = (id: string) => !allowedIds || allowedIds.includes(id);
  const pick = (ids: string[]) => onChange(ids.filter(allowed));
  return <fieldset className="space-y-3"><legend className="text-sm font-semibold">{placement ? "Em muốn kiểm tra những phần nào?" : "Em muốn học những phần nào?"}</legend>
    <p className="text-xs text-slate-500">{placement ? "Chọn một, vài hoặc tất cả các phần. Phần không chọn giữ trạng thái chưa được đánh giá; hệ thống không tự coi là đã vững." : "Chọn những phần em muốn học hoặc còn yếu. Với nền tảng đã học ở trường, em có thể xác nhận để bỏ qua trước khi lập tuyến."}</p>
    {allowedIds && lockedNote && <p className="rounded-lg bg-sky-50 p-3 text-xs text-sky-950 dark:bg-sky-950/40 dark:text-sky-100">{lockedNote}</p>}
    <div className="flex gap-4 text-xs font-medium text-indigo-600"><button type="button" onClick={() => pick(skills.map(s => s.skillId))}>Chọn tất cả</button>
      <button type="button" onClick={() => onChange([])}>Bỏ chọn tất cả</button></div>
    <div className="max-h-72 space-y-3 overflow-y-auto">{groups.map(group => {
      const ids = group.skills.map(s => s.skillId);
      const open = ids.filter(allowed);
      const count = ids.filter(id => selected.includes(id)).length;
      const locked = open.length === 0;
      return <div className={`rounded-xl border border-slate-200 p-3 dark:border-slate-700 ${locked ? "bg-slate-50 opacity-70 dark:bg-slate-800/50" : ""}`} key={group.id}>
        <label className="flex items-center gap-2 text-sm font-semibold"><input type="checkbox" disabled={locked} checked={count > 0 && count === open.length}
          ref={el => { if (el) el.indeterminate = count > 0 && count < open.length; }}
          onChange={e => onChange(e.target.checked ? [...new Set([...selected, ...open])] : selected.filter(id => !ids.includes(id)))} />
          {group.name}{locked && <span className="text-xs font-normal text-slate-500">· chưa kiểm tra đầu vào</span>}<span className="ml-auto text-xs font-normal text-slate-500">{count}/{ids.length} kỹ năng</span></label>
        {!(group.skills.length === 1 && group.skills[0].skillName === group.name) && <div className="mt-2 grid gap-2 sm:grid-cols-2">{group.skills.map(skill => <label key={skill.skillId} className={`flex items-start gap-2 rounded-lg bg-slate-50 p-2 text-sm dark:bg-slate-800 ${allowed(skill.skillId) ? "" : "opacity-70"}`}>
          <input type="checkbox" disabled={!allowed(skill.skillId)} checked={selected.includes(skill.skillId)} onChange={e => onChange(e.target.checked ? [...selected, skill.skillId] : selected.filter(id => id !== skill.skillId))} />
          <span>{skill.skillName}{!allowed(skill.skillId) ? <span className="block text-xs text-slate-500">Chưa kiểm tra đầu vào</span>
            : skill.state === "Confirmed" && <span className="block text-xs text-emerald-700">{placement ? "Đã có bằng chứng · vẫn kiểm tra lại nếu chọn" : "Đã có bằng chứng · rút gọn luyện cơ bản"}</span>}</span>
        </label>)}</div>}
      </div>;
    })}</div>
  </fieldset>;
}
