import { Link, useSearchParams } from "react-router-dom";
import { studentScopeUrl } from "../../utils/studentAcademicNavigation";

export function StudentLearningReadOnlyNotice({reason}:{reason:string}) {
  const [params] = useSearchParams();
  const current = new URLSearchParams(params); current.delete("history"); current.delete("classId");
  return <section role="status" className="max-w-4xl mx-auto my-6 rounded-2xl border border-[var(--student-border)] bg-[var(--student-surface)] p-6 space-y-4">
    <h1 className="text-xl font-bold">Phạm vi chỉ xem — không bắt đầu học mới</h1>
    <p>{reason}</p>
    <p>Bài làm, điểm, lời giải và năng lực cũ vẫn được giữ nguyên. Lộ trình cá nhân theo môn không được coi là bản lưu lịch sử riêng của lớp này.</p>
    <div className="flex flex-wrap gap-4">
      <Link className="font-semibold text-[var(--student-brand)]" to={studentScopeUrl("/hoc-tap/bai-tap",params)}>Xem bài làm đã lưu →</Link>
      <Link className="font-semibold text-[var(--student-brand)]" to={studentScopeUrl("/hoc-tap/tong-quan",current)}>Chọn lớp đang học →</Link>
    </div>
  </section>;
}
