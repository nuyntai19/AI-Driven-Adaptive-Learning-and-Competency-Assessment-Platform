import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { curriculumApi } from "../../api/curriculumApi";
import { useAuthStore } from "../../stores/authStore";
import { TeacherModal } from "./TeacherOverlays";
import { mapSafeOperationalError } from "../../utils/problemDetails";

export function CurriculumArchiveDialog({ isOpen, curriculumId, title, isPending, error, onClose, onConfirm }: {
  isOpen: boolean; curriculumId?: string; title: string; isPending: boolean; error?: string | null;
  onClose: () => void; onConfirm: (reason: string) => void;
}) {
  const [reason, setReason] = useState("");
  const actor = useAuthStore(s => s.user);
  useEffect(() => { if (isOpen) setReason(""); }, [isOpen, curriculumId]);
  const usage = useQuery({
    queryKey: ["curriculum-lifecycle-usage", actor?.centerId, actor?.userId, curriculumId],
    queryFn: () => curriculumApi.lifecycleUsage(curriculumId!),
    enabled: Boolean(isOpen && curriculumId), staleTime: 0,
  });
  const blocked = (usage.data?.data.length ?? 0) > 0;
  return <TeacherModal isOpen={isOpen} onClose={onClose} title={`Lưu trữ: ${title}`}
    description="Không tự ngắt giáo trình của lớp đang học. Bài làm, điểm và dữ liệu năng lực cũ được giữ nguyên."
    footer={<div className="flex justify-end gap-3">
      <button className="th-secondary-button" disabled={isPending} onClick={onClose}>Hủy</button>
      <button className="th-danger-button" disabled={isPending || usage.isFetching || !usage.data || usage.isError || blocked || !reason.trim()}
        onClick={() => onConfirm(reason.trim())}>{isPending ? "Đang lưu trữ..." : "Xác nhận lưu trữ"}</button>
    </div>}>
    {usage.isPending ? <p>Đang kiểm tra tất cả lớp đang áp dụng...</p> : usage.isError ? <p role="alert">
      {mapSafeOperationalError(usage.error, "Không kiểm tra được ràng buộc. Chưa thể lưu trữ.")}
      <button className="th-secondary-button ml-2" onClick={() => void usage.refetch()}>Tải lại</button>
    </p> : blocked ? <div role="alert">
      <p className="font-semibold">Chưa thể lưu trữ: các lớp sau vẫn đang sử dụng giáo trình, kể cả lớp của giáo viên khác:</p>
      <ul className="list-disc pl-6 mt-3">{usage.data.data.map(cls => <li key={cls.classId}>{cls.className}</li>)}</ul>
      <p className="mt-3">Giáo viên phụ trách cần thay/ngừng áp dụng và lưu lý do trước. Lớp đã lưu trữ không chặn thao tác này.</p>
    </div> : <p>Không còn lớp hoạt động đang áp dụng giáo trình này.</p>}
    <label className="block mt-4 font-semibold">Lý do lưu trữ
      <textarea className="th-input mt-2 w-full" rows={3} maxLength={500} value={reason} disabled={isPending || blocked}
        onChange={event => setReason(event.target.value)} placeholder="Ví dụ: giáo trình đã được thay bằng phiên bản mới." />
    </label>
    {error && <p className="mt-3" role="alert">{error}</p>}
  </TeacherModal>;
}
