import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { getReconciledStudentAcademicContext } from "../api/dashboardsApi";
import { useAuthStore } from "../stores/authStore";

export function useStudentLearningAccess(subjectId: string) {
  const [params] = useSearchParams();
  const user = useAuthStore(s => s.user);
  const history = params.get("history") === "true";
  const classId = params.get("classId") || "";
  const query = useQuery({ queryKey: ["student-academic-context", user?.centerId, user?.userId, subjectId, classId, history],
    queryFn: () => getReconciledStudentAcademicContext(subjectId, classId, history),
    enabled: !!user && !!subjectId && !history, staleTime: 30_000 });
  const hasClasses = !!query.data?.classes.length;
  const noCurrentClass = hasClasses && !query.data?.classes.some(c => !c.isHistorical);
  const noCurriculum = hasClasses && !noCurrentClass && query.data?.curriculums.length === 0;
  const readOnly = history || query.isError || noCurrentClass || noCurriculum;
  // Wait for the header to replace a rejected bookmark before enabling writes.
  const selectionChanging = !!classId && !!query.data && query.data.selectedClassId !== classId;
  const pending = !history && !!subjectId && (query.isPending || selectionChanging);
  return { history, classId, pending, readOnly, writable: !pending && !readOnly && !!subjectId,
    reason: history ? "Bạn đang xem lịch sử lớp học. Không thể tạo lộ trình, bắt đầu buổi học hoặc nộp bài mới trong phạm vi này."
      : query.isError ? "Chưa xác minh được lớp đang học. Không gửi bài mới khi phạm vi lớp chưa hợp lệ."
      : query.data?.message || "Lớp đã lưu trữ hoặc bạn đã rời lớp. Chỉ được xem lại dữ liệu cũ." };
}
