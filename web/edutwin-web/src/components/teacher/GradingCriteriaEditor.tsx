import type { GradingCriteria } from "../../types/questions";
import { rubricDefinitionError } from "../../utils/rubric";

export function GradingCriteriaEditor({ value, onChange, maxScore }: {
  value: GradingCriteria; onChange: (value: GradingCriteria) => void; maxScore: number;
}) {
  const criteria = value.criteria || [];
  const error = rubricDefinitionError(criteria, maxScore);
  return <section className="space-y-4 rounded-xl border border-[var(--th-border)] p-4">
    <h3 className="font-bold text-base">Tiêu chí chấm & Rubric</h3>
    <p className="text-sm text-[var(--th-text-secondary)]">Chấp nhận cách giải khác hợp lệ. Hình minh họa có thể tùy chọn; nếu vẽ/ghi ký hiệu là mục tiêu cần chấm, cấu hình yêu cầu ảnh nháp riêng cho tiêu chí bên dưới.</p>
    {([['requiredIdeas', 'Ý chính / mục tiêu cần đạt'], ['commonErrors', 'Lỗi thực sự cần lưu ý']] as const).map(([key, label]) =>
      <label key={key} className="block text-sm font-semibold">{label} (mỗi dòng một mục)
        <textarea className="th-input w-full mt-1 text-sm" rows={2} value={value[key].join('\n')}
          onChange={e => onChange({ ...value, [key]: e.target.value.split('\n') })} />
      </label>)}
    <label className="block text-sm font-semibold">Ghi chú chấm / phương pháp tương đương
      <textarea className="th-input w-full mt-1 text-sm" rows={3} value={value.scoringNotes}
        onChange={e => onChange({ ...value, scoringNotes: e.target.value })} />
    </label>
    <p className="text-sm">Rubric tính điểm (tùy chọn): tổng tiêu chí = {maxScore} điểm gốc; khi chấm, hiển thị quy đổi thang 10. Nếu không cấu hình, giáo viên vẫn chấm điểm tổng như trước.</p>
    {criteria.map((c, index) => <div key={c.criterionId} className="space-y-2 rounded-lg border border-[var(--th-border)] p-3">
      <div className="flex gap-2 items-center">
        <input aria-label={`Tên tiêu chí ${index + 1}`} className="th-input flex-1 min-w-0 text-sm" value={c.title} placeholder="Ví dụ: Phương pháp hợp lệ"
          onChange={e => onChange({ ...value, criteria: criteria.map((item, i) => i === index ? { ...item, title: e.target.value } : item) })} />
        <input aria-label={`Điểm tối đa tiêu chí ${index + 1}`} type="number" min="0.01" step="0.01" className="th-input w-24 text-sm" value={c.maxScore}
          onChange={e => onChange({ ...value, criteria: criteria.map((item, i) => i === index ? { ...item, maxScore: Number(e.target.value) } : item) })} />
        <button type="button" className="text-sm font-bold text-rose-500" onClick={() => onChange({ ...value, criteria: criteria.filter((_, i) => i !== index) })}>Xóa</button>
      </div>
      <textarea aria-label={`Mô tả tiêu chí ${index + 1}`} rows={2} className="th-input w-full text-sm" value={c.description} placeholder="Điều kiện cho điểm; chấp nhận phương pháp tương đương"
        onChange={e => onChange({ ...value, criteria: criteria.map((item, i) => i === index ? { ...item, description: e.target.value } : item) })} />
      <label className="block text-sm">Yêu cầu phải nhìn thấy trên ảnh nháp — tiêu chí {index + 1} (mỗi dòng một yêu cầu)
        <textarea aria-label={`Yêu cầu ảnh nháp tiêu chí ${index + 1}`} rows={2} maxLength={6000} className="th-input w-full text-sm mt-1"
          value={(c.visualRequirements || []).join('\n')} placeholder="Ví dụ: Có nhãn M tại trung điểm BC; có dấu góc vuông tại A"
          onChange={e => onChange({ ...value, criteria: criteria.map((item, i) => i === index ? { ...item, visualRequirements: e.target.value ? e.target.value.split('\n').map(line => line.trim()) : [] } : item) })} />
      </label>
      <p className="text-xs text-[var(--th-text-secondary)]">Để trống nếu tiêu chí không yêu cầu ảnh học sinh. Khi có yêu cầu, AI phải chỉ ra từng bằng chứng; đáp số đúng không thay thế hình còn thiếu.</p>
    </div>)}
    <button type="button" disabled={criteria.length >= 20} className="th-secondary-button text-sm"
      onClick={() => onChange({ ...value, schemaVersion: '2.0', criteria: [...criteria, { criterionId: crypto.randomUUID(), title: '', description: '', maxScore: 0.5 }] })}>+ Thêm tiêu chí có điểm</button>
    {error && <p role="alert" className="text-sm text-rose-500">{error}</p>}
  </section>;
}
