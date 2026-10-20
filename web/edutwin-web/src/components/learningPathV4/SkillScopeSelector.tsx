import type { SkillCapabilityStatusDto } from "../../types/learningPath";
import { groupLearningSkills } from "../../utils/learningScope";

export function SkillScopeSelector({ skills, selected, onChange }: {
  skills: SkillCapabilityStatusDto[]; selected: string[]; onChange: (ids: string[]) => void;
}) {
  const groups = groupLearningSkills(skills);
  return <fieldset className="space-y-3"><legend className="text-sm font-semibold">Em muốn học những phần nào?</legend>
    <p className="text-xs text-slate-500">Chọn những phần em muốn học hoặc còn yếu. Với nền tảng đã học ở trường, em có thể xác nhận để bỏ qua trước khi lập tuyến.</p>
    <div className="flex gap-4 text-xs font-medium text-indigo-600"><button type="button" onClick={() => onChange(skills.map(s => s.skillId))}>Chọn tất cả</button>
      <button type="button" onClick={() => onChange([])}>Bỏ chọn tất cả</button></div>
    <div className="max-h-72 space-y-3 overflow-y-auto">{groups.map(group => {
      const ids = group.skills.map(s => s.skillId);
      const count = ids.filter(id => selected.includes(id)).length;
      return <div className="rounded-xl border border-slate-200 p-3 dark:border-slate-700" key={group.id}>
        <label className="flex items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={count === ids.length}
          ref={el => { if (el) el.indeterminate = count > 0 && count < ids.length; }}
          onChange={e => onChange(e.target.checked ? [...new Set([...selected, ...ids])] : selected.filter(id => !ids.includes(id)))} />
          {group.name}<span className="ml-auto text-xs font-normal text-slate-500">{count}/{ids.length} kỹ năng</span></label>
        {!(group.skills.length === 1 && group.skills[0].skillName === group.name) && <div className="mt-2 grid gap-2 sm:grid-cols-2">{group.skills.map(skill => <label key={skill.skillId} className="flex items-start gap-2 rounded-lg bg-slate-50 p-2 text-sm dark:bg-slate-800">
          <input type="checkbox" checked={selected.includes(skill.skillId)} onChange={e => onChange(e.target.checked ? [...selected, skill.skillId] : selected.filter(id => id !== skill.skillId))} />
          <span>{skill.skillName}{skill.state === "Confirmed" && <span className="block text-xs text-emerald-700">Đã có bằng chứng · rút gọn luyện cơ bản</span>}</span>
        </label>)}</div>}
      </div>;
    })}</div>
  </fieldset>;
}
