import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { permissions } from "../../auth/permissions";
import { useAuthStore } from "../../stores/authStore";
import { knowledgeKeys } from "../../utils/knowledgeScope";
import { academicLifecycleError } from "../../utils/academicLifecycleError";
import { TeacherModal } from "./TeacherOverlays";

/** The same reuse/fork flow as the curriculum editor, now available from the graph itself. */
export function OpenKnowledgeFrameworkButton({ subjectId, gradeLevel, disabled, onOpen }: {
  subjectId: string; gradeLevel?: number; disabled: boolean; onOpen: (id: string, grade: number) => void;
}) {
  const user = useAuthStore(s => s.user);
  const hasPermission = useAuthStore(s => s.hasPermission);
  const client = useQueryClient();
  const [chooseGrade, setChooseGrade] = useState(false);
  const [error, setError] = useState("");
  const active = useRef(true);
  useEffect(() => { active.current = true; return () => { active.current = false; }; }, []);
  const canEdit = user?.accountType === "Teacher" && hasPermission(permissions.nodesRead) && hasPermission(permissions.edgesRead)
    && hasPermission(permissions.nodesUpdate) && hasPermission(permissions.edgesUpdate);
  const canCreate = hasPermission(permissions.nodesCreate) && hasPermission(permissions.edgesCreate);
  const open = useMutation({ mutationFn: async (grade: number) => {
    const versions = await client.fetchQuery({ queryKey: knowledgeKeys.versions(user?.centerId, user?.userId, subjectId, grade), queryFn: () => knowledgeGraphApi.listVersions(subjectId, grade), staleTime: 0 });
    const draft = versions.find(v => v.status === "Draft" && v.createdBy === user?.userId && !v.isLegacy);
    if (draft) return draft;
    if (!active.current) return null;
    if (!canCreate) throw new Error("NO_FRAMEWORK_CREATE_PERMISSION");
    const approved = versions.filter(v => v.status === "Published" && !v.isLegacy);
    const source = approved.find(v => v.isCurrent) || approved[0];
    return knowledgeGraphApi.createVersion({ subjectId, gradeLevel: grade, title: `Khung tri thức ${grade} - bản nháp`, sourceGraphVersionId: source?.graphVersionId });
  }, onSuccess: async version => {
    if (!version || !active.current) return;
    await client.invalidateQueries({ queryKey: knowledgeKeys.versions(user?.centerId, user?.userId, subjectId, version.gradeLevel) });
    if (!active.current) return;
    setChooseGrade(false); onOpen(version.graphVersionId, version.gradeLevel);
  }, onError: err => {
    if (active.current) setError(err instanceof Error && err.message === "NO_FRAMEWORK_CREATE_PERMISSION"
      ? "Khối này chưa có bản nháp của bạn. Bạn cần quyền tạo chủ đề và liên kết để tạo bản nháp mới."
      : academicLifecycleError(err, "Không mở được khung tri thức. Kiểm tra kết nối và quyền truy cập rồi thử lại."));
  } });
  if (!canEdit) return null;
  return <div className="space-y-2">
    <button type="button" className="th-primary-button" disabled={disabled || !subjectId || open.isPending} data-testid="open-knowledge-framework"
      onClick={() => { setError(""); if (gradeLevel && [10, 11, 12].includes(gradeLevel)) open.mutate(gradeLevel); else setChooseGrade(true); }}>
      {open.isPending ? "Đang mở khung tri thức…" : `Chỉnh sửa khung tri thức${gradeLevel ? ` Khối ${gradeLevel}` : ""}`}
    </button>
    {error && !chooseGrade && <p role="alert" data-toast-mirror="off" className="text-sm text-[var(--th-danger)]">{error}</p>}
    <TeacherModal isOpen={chooseGrade} title="Chọn khối để chỉnh sửa khung tri thức" description="Mở bản nháp của bạn. Nếu chưa có, tạo bản nháp từ khung đã duyệt hoặc khung trống." onClose={() => !open.isPending && setChooseGrade(false)}>
      <div className="flex flex-wrap gap-3">{[10, 11, 12].map(grade => <button key={grade} type="button" className="th-primary-button" disabled={open.isPending} onClick={() => { setError(""); open.mutate(grade); }}>Khối {grade}</button>)}</div>
      {open.isPending && <p role="status" className="text-sm mt-3">Đang mở khung tri thức…</p>}
      {error && <p role="alert" data-toast-mirror="off" className="text-sm mt-3 text-[var(--th-danger)]">{error}</p>}
    </TeacherModal>
  </div>;
}
