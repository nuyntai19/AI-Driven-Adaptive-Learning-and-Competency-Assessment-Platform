import React, { useState, useEffect, useRef, useMemo } from "react";
import { useParams, useNavigate, useSearchParams, Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { curriculumApi } from "../../api/curriculumApi";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { organizationApi } from "../../api/organizationApi";
import type { Curriculum, CreateCurriculumRequest } from "../../types/curriculum";
import type { MaterialVisibility } from "../../types/questions";
import type { KnowledgeNodeDto } from "../../types/knowledgeGraph";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  TeacherPageHeader,
  TeacherSkeleton,
  TeacherStatusBadge,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherModal } from "../../components/teacher/TeacherOverlays";
import { CurriculumArchiveDialog } from "../../components/teacher/CurriculumArchiveDialog";
import { formatCurriculumSaveError } from "../curriculumEditorHelpers";
import { extractProblemDetails, mapSafeOperationalError } from "../../utils/problemDetails";
import { academicLifecycleError } from "../../utils/academicLifecycleError";
import { CurriculumApplicationPanel } from "../../components/teacher/CurriculumApplicationPanel";
import { curriculumGraphLinkScope, incompatibleKnowledgeNodes, knowledgeKeys, knowledgeScopeSearch } from "../../utils/knowledgeScope";

export const TeacherCurriculumEditorView: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const isCreateMode = !id || id === "tao-moi";
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const actorId = useAuthStore(state => state.user?.userId);
  const centerId = useAuthStore(state => state.user?.centerId);
  const [visibility, setVisibility] = useState<MaterialVisibility>("Private");

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canPublish = hasPermission(permissions.curriculumsPublish);
  const canCreate = hasPermission(permissions.curriculumsCreate);

  // Form State
  const [title, setTitle] = useState("");
  const [subjectId, setSubjectId] = useState(searchParams.get("subjectId") || "");
  const [gradeLevel, setGradeLevel] = useState<number | "">([10,11,12].includes(Number(searchParams.get("gradeLevel"))) ? Number(searchParams.get("gradeLevel")) : "");
  const [graphVersionId, setGraphVersionId] = useState(searchParams.get("graphVersionId") || "");
  const nodeLabels = useRef(new Map<string, KnowledgeNodeDto>());
  const [description, setDescription] = useState("");
  const [selectedNodeIds, setSelectedNodeIds] = useState<string[]>([]);
  const [selectedClassIds, setSelectedClassIds] = useState<string[]>([]);
  const [rowVersion, setRowVersion] = useState<string>("");
  const [status, setStatus] = useState<string>("Draft");
  const [nodeSearch, setNodeSearch] = useState("");
  const [feedbackMsg, setFeedbackMsg] = useState<{ type: "success" | "error"; text: string } | null>(null);

  // Archive & Clone dialog states
  const [isArchiveDialogOpen, setIsArchiveDialogOpen] = useState(false);
  const [isCloneModalOpen, setIsCloneModalOpen] = useState(false);
  const [cloneTitle, setCloneTitle] = useState("");

  // Fetch subjects
  const { data: subjectsData } = useQuery({
    queryKey: ["teacherSubjectsList", centerId, actorId],
    queryFn: () => organizationApi.listSubjects(true),
  });
  const subjects = useMemo(() => subjectsData?.data ?? [], [subjectsData]);

  // Fetch classes
  const { data: classesData, isFetching: isFetchingClasses, isError: isClassesError } = useQuery({
    queryKey: ["teacherClassesList", "curriculum-plan", centerId, actorId],
    enabled: Boolean(actorId),
    queryFn: async () => {
      const params = {pageSize:100, teacherId:actorId};
      const first = await organizationApi.listClasses({...params,page:1});
      const all = [...first.data];
      for(let page=2;page<=first.meta.totalPages;page++) all.push(...(await organizationApi.listClasses({...params,page})).data);
      return {...first,data:all};
    },
  });
  const classes = classesData?.data ?? [];

  // Fetch curriculum details if in edit mode
  const {
    data: curriculumData,
    isLoading: isLoadingCurriculum,
    isError: isCurriculumError,
    error: curriculumError,
  } = useQuery({
    queryKey: ["teacherCurriculumDetail", id, centerId, actorId],
    queryFn: () => (id && !isCreateMode ? curriculumApi.getById(id) : null),
    enabled: !isCreateMode && Boolean(id),
  });

  // Populate state from curriculum details
  useEffect(() => {
    if (curriculumData?.data) {
      const c = curriculumData.data;
      setTitle(c.title || "");
      setSubjectId(c.subjectId || "");
      setGradeLevel(c.gradeLevel ?? "");
      setGraphVersionId(c.graphVersionId || "");
      setDescription(c.description || "");
      setSelectedNodeIds(c.nodeIds || []);
      setSelectedClassIds(c.classIds || []);
      setRowVersion(c.rowVersion || "");
      setStatus(c.reviewStatus || "Draft");
      setVisibility(c.visibility || "Private");
    } else if (isCreateMode && subjects.length > 0 && !subjectId) {
      setSubjectId(subjects[0].subjectId);
    }
  }, [curriculumData, isCreateMode, subjects, subjectId]);

  const versionsQuery = useQuery({
    queryKey: knowledgeKeys.versions(centerId, actorId, subjectId, gradeLevel || undefined),
    queryFn: () => knowledgeGraphApi.listVersions(subjectId, gradeLevel || undefined),
    enabled: Boolean(subjectId && gradeLevel),
  });
  const approvedVersions = (versionsQuery.data ?? []).filter(v => v.status === "Published" && !v.isLegacy);
  const selectedVersion = (versionsQuery.data ?? []).find(v => v.graphVersionId === graphVersionId);
  const editGraphMutation = useMutation({
    mutationFn: async () => {
      if (!subjectId || !gradeLevel) throw new Error("Chọn môn học và khối trước khi chỉnh sửa khung tri thức.");
      const existingDraft = versionsQuery.data?.find(v => v.status === "Draft" && v.createdBy === actorId && !v.isLegacy);
      if (existingDraft) return existingDraft;
      const source = selectedVersion?.status === "Published" ? selectedVersion :
        approvedVersions.find(v => v.isCurrent) || approvedVersions[0];
      return knowledgeGraphApi.createVersion({ subjectId, gradeLevel: Number(gradeLevel),
        title: `Khung tri thức ${gradeLevel} - bản nháp`, sourceGraphVersionId: source?.graphVersionId });
    },
    onSuccess: async version => {
      await queryClient.invalidateQueries({ queryKey: knowledgeKeys.versions(centerId, actorId, subjectId, Number(gradeLevel)) });
      const params = new URLSearchParams(knowledgeScopeSearch(subjectId,
        { gradeLevel: Number(gradeLevel), graphVersionId: version.graphVersionId }));
      const classContextId = selectedClassIds[0] || curriculumData?.data?.classIds[0];
      if (classContextId) params.set("classContextId", classContextId);
      if (id) params.set("returnCurriculumId", id);
      navigate(`/giao-vien/do-thi-tri-thuc?${params}`);
    },
    onError: err => setFeedbackMsg({ type: "error", text: extractProblemDetails(err).detail || "Không tạo được bản nháp khung tri thức." }),
  });
  useEffect(() => {
    if (!graphVersionId && gradeLevel && versionsQuery.data) {
      const current = versionsQuery.data.find(v => v.isCurrent && v.status === "Published" && !v.isLegacy);
      if (current) setGraphVersionId(current.graphVersionId);
    }
  }, [graphVersionId, gradeLevel, versionsQuery.data]);
  const nodeScope = { gradeLevel: gradeLevel || undefined, graphVersionId: graphVersionId || undefined };
  const { data: availableNodes = [], isLoading: isLoadingNodes, isFetching: isFetchingNodes, isError: isNodesError, refetch: refetchNodes } = useQuery<KnowledgeNodeDto[]>({
    queryKey: knowledgeKeys.nodes(centerId, actorId, subjectId, nodeScope),
    queryFn: () => knowledgeGraphApi.listNodes(subjectId, nodeScope),
    enabled: Boolean(subjectId && gradeLevel && graphVersionId),
  });
  useEffect(() => { availableNodes.forEach(node => nodeLabels.current.set(node.nodeId, node)); }, [availableNodes]);
  const incompatibleNodes = incompatibleKnowledgeNodes(selectedNodeIds, availableNodes);
  const incompatibleClasses = classes.filter(c => selectedClassIds.includes(c.classId) && (c.gradeLevel !== gradeLevel ||
    c.subject.subjectId !== subjectId || c.teacher.teacherId !== actorId || c.status !== "Active" || c.learningScope === "History"));
  const unresolvedClassIds = classesData ? selectedClassIds.filter(id => !classes.some(c => c.classId === id)) : [];
  const pendingScope = selectedNodeIds.length > 0 && (isFetchingNodes || versionsQuery.isFetching);
  const invalidSelection = selectedNodeIds.length > 0 && (!selectedVersion || selectedVersion.isLegacy || selectedVersion.status !== "Published" || isNodesError || incompatibleNodes.length > 0);
  useEffect(() => {
    if (!isCreateMode) return;
    const next = new URLSearchParams(searchParams);
    for (const [key, value] of Object.entries({ subjectId, gradeLevel, graphVersionId })) {
      if (value) next.set(key, String(value)); else next.delete(key);
    }
    if (next.toString() !== searchParams.toString()) setSearchParams(next, { replace: true });
  }, [subjectId, gradeLevel, graphVersionId, isCreateMode, searchParams, setSearchParams]);

  // Node toggle helpers
  const handleToggleNode = (nodeId: string) => {
    setSelectedNodeIds((prev) =>
      prev.includes(nodeId) ? prev.filter((i) => i !== nodeId) : [...prev, nodeId]
    );
  };

  const handleToggleClass = (classId: string) => {
    setSelectedClassIds((prev) =>
      prev.includes(classId) ? prev.filter((i) => i !== classId) : [...prev, classId]
    );
  };

  const handleMoveNode = (index: number, direction: "up" | "down") => {
    if (direction === "up" && index === 0) return;
    if (direction === "down" && index === selectedNodeIds.length - 1) return;

    const newNodes = [...selectedNodeIds];
    const targetIndex = direction === "up" ? index - 1 : index + 1;
    const temp = newNodes[index];
    newNodes[index] = newNodes[targetIndex];
    newNodes[targetIndex] = temp;
    setSelectedNodeIds(newNodes);
  };

  // Save Mutation
  const saveMutation = useMutation({
    mutationFn: async () => {
      setFeedbackMsg(null);
      if (!title.trim()) throw new Error("Vui lòng nhập tên giáo trình / khung chương trình.");
      if (title.trim().length > 200) throw new Error("Tên giáo trình không được vượt quá 200 ký tự.");
      if (!subjectId) throw new Error("Vui lòng chọn môn học cho giáo trình.");
      if (description.trim().length > 2000) throw new Error("Mô tả giáo trình không được vượt quá 2000 ký tự.");
      if (!gradeLevel || ![10,11,12].includes(Number(gradeLevel))) throw new Error("Vui lòng chọn khối học áp dụng.");
      if (pendingScope || invalidSelection) throw new Error("Các chủ đề đã chọn chưa phù hợp khối hoặc phiên bản khung tri thức. Hãy xử lý danh sách xung đột trước khi lưu.");
      if (selectedClassIds.length && (isFetchingClasses || isClassesError)) throw new Error("Chờ tải đầy đủ lớp dự kiến trước khi lưu.");
      if (incompatibleClasses.length || unresolvedClassIds.length) throw new Error("Có lớp dự kiến không còn phù hợp khối hoặc trạng thái. Bỏ chọn các lớp xung đột trước khi lưu.");

      if (isCreateMode) {
        if (![10, 11, 12].includes(Number(gradeLevel))) {
          throw new Error("Vui lòng chọn khối học áp dụng (Khối 10, 11 hoặc 12) cho giáo trình mới.");
        }
        const payload: CreateCurriculumRequest = {
          visibility,
          title: title.trim(),
          subjectId,
          gradeLevel: Number(gradeLevel),
          graphVersionId: graphVersionId || undefined,
          description: description.trim() || undefined,
          nodeIds: selectedNodeIds,
        };
        const res = await curriculumApi.create(payload);
        return res.data;
      } else {
        if (!id) throw new Error("Mã giáo trình không hợp lệ");
        // 1. Update basic info
        const updateRes = await curriculumApi.update(id, {
          visibility,
          title: title.trim(),
          gradeLevel: Number(gradeLevel),
          graphVersionId: graphVersionId || undefined,
          nodeIds: selectedNodeIds,
          classIds: selectedClassIds,
          description: description.trim() || undefined,
          rowVersion,
        });
        return updateRes.data;
      }
    },
    onSuccess: (data: Curriculum) => {
      setRowVersion(data.rowVersion);
      setFeedbackMsg({ type: "success", text: "Đã lưu thay đổi giáo trình thành công!" });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculumDetail", id] });
      queryClient.invalidateQueries({ queryKey: knowledgeKeys.all });

      if (isCreateMode && data?.curriculumId) {
        navigate(`/giao-vien/giao-trinh/${data.curriculumId}`);
      }
    },
    onError: (err: unknown) => {
      const scopeConflict = (err as { response?: { data?: { scopeConflict?: { message?: string; invalidNodeIds?: string[]; invalidClassIds?: string[] } } } })?.response?.data?.scopeConflict;
      if (scopeConflict) {
        setFeedbackMsg({ type: "error", text: [scopeConflict.message || "Lựa chọn không phù hợp phạm vi đã chọn.",
          scopeConflict.invalidNodeIds?.length ? `Chủ đề: ${scopeConflict.invalidNodeIds.map(n => nodeLabels.current.get(n)?.nodeName || `#${n}`).join(", ")}.` : "",
          scopeConflict.invalidClassIds?.length ? `Lớp: ${scopeConflict.invalidClassIds.map(id => { const c = classes.find(c => c.classId === id); return c ? `${c.className} (${c.academicYear})` : id; }).join(", ")}.` : ""].filter(Boolean).join(" ") });
        return;
      }
      const text = formatCurriculumSaveError(err, {
        classes,
        selectedClassIds,
        curriculumGradeLevel: gradeLevel,
      });
      setFeedbackMsg({ type: "error", text });
    },
  });

  // Publish Mutation
  const publishMutation = useMutation({
    mutationFn: async () => {
      setFeedbackMsg(null);
      if (!id || isCreateMode) throw new Error("Cần lưu giáo trình trước khi xuất bản.");
      if (selectedNodeIds.length === 0) throw new Error("Giáo trình cần có ít nhất 1 chủ đề / điểm tri thức trước khi xuất bản chính thức.");
      if (pendingScope || invalidSelection || incompatibleClasses.length || unresolvedClassIds.length || isFetchingClasses || isClassesError) throw new Error("Xử lý lựa chọn xung đột và lưu bản nháp trước khi xuất bản.");
      const saved = curriculumData?.data;
      if (!saved || saved.gradeLevel !== gradeLevel || (saved.graphVersionId || "") !== graphVersionId || saved.title !== title || (saved.description || "") !== description ||
        saved.nodeIds.join(",") !== selectedNodeIds.join(",") || [...saved.classIds].sort().join(",") !== [...selectedClassIds].sort().join(",")) {
        throw new Error("Lưu thay đổi bản nháp trước khi xuất bản.");
      }
      return await curriculumApi.publish(id, { rowVersion });
    },
    onSuccess: (res) => {
      setRowVersion(res.data.rowVersion);
      setStatus("Published");
      setFeedbackMsg({ type: "success", text: "Giáo trình đã được xuất bản chính thức!" });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculumDetail", id] });
      queryClient.invalidateQueries({ queryKey: knowledgeKeys.all });
    },
    onError: (err: unknown) => {
      const details = extractProblemDetails(err);
      const text = details.detail || mapSafeOperationalError(err, "Lỗi khi xuất bản giáo trình");
      setFeedbackMsg({ type: "error", text });
    },
  });

  // Archive Mutation
  const archiveMutation = useMutation({
    mutationFn: async (reason: string) => {
      setFeedbackMsg(null);
      if (!id || isCreateMode) throw new Error("Cần lưu giáo trình trước khi lưu trữ.");
      return await curriculumApi.archive(id, { rowVersion, reason });
    },
    onSuccess: (res) => {
      setIsArchiveDialogOpen(false);
      setRowVersion(res.data.rowVersion);
      setStatus("Archived");
      setFeedbackMsg({ type: "success", text: "Giáo trình đã được lưu trữ. Danh sách chủ đề và nội dung lịch sử được bảo toàn; thông số ưu tiên của đồ thị dùng chung vẫn có nhật ký riêng." });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculumDetail", id] });
      queryClient.invalidateQueries({ queryKey: knowledgeKeys.all });
    },
    onError: (err: any) => {
      setFeedbackMsg({ type: "error", text: academicLifecycleError(err, "Lỗi khi lưu trữ giáo trình") });
    },
  });

  // Clone Mutation
  const cloneMutation = useMutation({
    mutationFn: async (targetTitle: string) => {
      setFeedbackMsg(null);
      if (!id || isCreateMode) throw new Error("Không thể nhân bản giáo trình chưa được lưu.");
      return await curriculumApi.clone(id, { title: targetTitle });
    },
    onSuccess: (res) => {
      setIsCloneModalOpen(false);
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
      if (res?.data?.curriculumId) {
        navigate(`/giao-vien/giao-trinh/${res.data.curriculumId}`);
      }
    },
    onError: (err: any) => {
      setFeedbackMsg({ type: "error", text: err.message || "Lỗi khi nhân bản giáo trình" });
    },
  });

  const isOwned = isCreateMode || curriculumData?.data?.teacherId === actorId;
  const isDraft = isOwned && (isCreateMode || status === "Draft");
  const plannedClasses = classes.filter(c => selectedClassIds.includes(c.classId) || (c.teacher.teacherId === actorId && c.subject.subjectId === subjectId &&
    c.status === "Active" && c.learningScope !== "History" && c.gradeLevel === gradeLevel));
  const graphLinkScope = curriculumGraphLinkScope(!isCreateMode ? id : undefined,
    { gradeLevel: gradeLevel || undefined, graphVersionId, nodeIds: selectedNodeIds }, curriculumData?.data);
  const hasUnsavedComposition = !graphLinkScope.curriculumId;

  // Filtered available nodes
  const filteredAvailableNodes = availableNodes.filter((n) =>
    n.nodeName.toLowerCase().includes(nodeSearch.toLowerCase()) ||
    n.nodeCode.toLowerCase().includes(nodeSearch.toLowerCase())
  );

  return (
    <div className="th-page-container">
      <TeacherPageHeader
        title={isCreateMode ? "Soạn Giáo Trình Mới" : `${isDraft ? "Chỉnh Sửa Giáo Trình" : "Nội Dung Giáo Trình"}: ${title}`}
        subtitle="Xây dựng cấu trúc cây bài học, tích hợp chủ đề tri thức và phân bổ cho các lớp học"
        actions={
          <div style={{ display: "flex", gap: "10px", alignItems: "center", flexWrap: "wrap" }}>
            <Link to="/giao-vien/giao-trinh" className="th-button-secondary" style={{ textDecoration: "none" }}>
              Quay lại danh sách
            </Link>

            {!isCreateMode && isOwned && canPublish && status === "Draft" && (
              <button
                type="button"
                className="th-button-secondary"
                onClick={() => publishMutation.mutate()}
                disabled={publishMutation.isPending || saveMutation.isPending}
              >
                Xuất Bản
              </button>
            )}

            {!isCreateMode && isDraft && gradeLevel && subjectId && (
              <button type="button" className="th-button-secondary"
                disabled={editGraphMutation.isPending || versionsQuery.isFetching || versionsQuery.isError}
                onClick={() => editGraphMutation.mutate()}>
                {editGraphMutation.isPending ? "Đang mở khung tri thức..." : `Chỉnh sửa khung tri thức Khối ${gradeLevel}`}
              </button>
            )}

            {!isCreateMode && isOwned && canPublish && status === "Published" && (
              <button
                type="button"
                className="th-button-secondary"
                onClick={() => setIsArchiveDialogOpen(true)}
                disabled={archiveMutation.isPending}
                style={{ color: "var(--th-warning)" }}
              >
                Lưu Trữ (Archive)
              </button>
            )}

            {!isCreateMode && canCreate && (status === "Published" || status === "Archived") && (
              <button
                type="button"
                className="th-button-secondary"
                onClick={() => {
                  setCloneTitle(`${title} (V2)`);
                  setIsCloneModalOpen(true);
                }}
                disabled={cloneMutation.isPending}
              >
                Nhân Bản (Clone V2)
              </button>
            )}

            {isDraft && (
              <button
                type="button"
                className="th-primary-button"
                onClick={() => saveMutation.mutate()}
                disabled={saveMutation.isPending}
              >
                {saveMutation.isPending ? "Đang lưu..." : isCreateMode ? "Tạo Giáo Trình" : "Lưu Thay Đổi"}
              </button>
            )}
          </div>
        }
      />

      {feedbackMsg && (
        <div
          id="curriculum-feedback-banner"
          style={{
            padding: "12px 16px",
            borderRadius: "8px",
            marginBottom: "16px",
            backgroundColor: feedbackMsg.type === "success" ? "rgba(16, 185, 129, 0.12)" : "rgba(239, 68, 68, 0.12)",
            border: `1px solid ${feedbackMsg.type === "success" ? "var(--th-primary)" : "var(--th-danger)"}`,
            color: feedbackMsg.type === "success" ? "var(--th-primary)" : "var(--th-danger)",
            fontSize: "0.875rem",
            fontWeight: 600,
          }}
        >
          {feedbackMsg.text}
        </div>
      )}

      {status === "Published" && (
        <div
          style={{
            padding: "14px 18px",
            borderRadius: "10px",
            marginBottom: "20px",
            backgroundColor: "rgba(6, 182, 212, 0.08)",
            border: "1px solid var(--th-primary)",
            display: "flex",
            alignItems: "flex-start",
            gap: "12px",
          }}
        >
          <div style={{ fontSize: "1.2rem", lineHeight: 1 }}>🔒</div>
          <div style={{ flex: 1, fontSize: "0.85rem", color: "var(--th-text-primary)" }}>
            <strong>Giáo trình đã xuất bản (Published):</strong> Cấu trúc chủ đề được khóa để bảo toàn dữ liệu học tập. Việc áp dụng cho lớp được quản lý riêng ở khung bên dưới, có lịch sử khi thay/ngừng áp dụng. Muốn đổi nội dung, dùng <strong>Nhân Bản (Clone V2)</strong> để tạo bản thảo mới.
          </div>
        </div>
      )}

      {status === "Archived" && (
        <div
          style={{
            padding: "14px 18px",
            borderRadius: "10px",
            marginBottom: "20px",
            backgroundColor: "rgba(148, 163, 184, 0.12)",
            border: "1px solid var(--th-border-color)",
            display: "flex",
            alignItems: "flex-start",
            gap: "12px",
          }}
        >
          <div style={{ fontSize: "1.2rem", lineHeight: 1 }}>📦</div>
          <div style={{ flex: 1, fontSize: "0.85rem", color: "var(--th-text-primary)" }}>
            <strong>Giáo trình đã được lưu trữ (Archived):</strong> Danh sách chủ đề và phiên bản đồ thị lịch sử được bảo toàn. Nội dung, thông số và quan hệ đã chốt không được sửa trực tiếp. Bấm <strong>Nhân Bản (Clone V2)</strong> để tạo giáo trình nháp mới và chọn khung tri thức đã duyệt phù hợp.
          </div>
        </div>
      )}

      {isLoadingCurriculum && <TeacherSkeleton height={400} />}
      {!isCreateMode && id && !isLoadingCurriculum && !isCurriculumError && (status === "Published" || status === "Archived") &&
        <CurriculumApplicationPanel key={id} curriculumId={id} subjectId={subjectId}
          gradeLevel={gradeLevel === "" ? null : Number(gradeLevel)} readOnly={status !== "Published"} />}

      {isCurriculumError && (
        <TeacherSafeErrorPanel error={curriculumError} title="Không thể tải thông tin giáo trình" />
      )}

      {!isLoadingCurriculum && !isCurriculumError && (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "24px", alignItems: "start" }}>
          {/* Left Column: Basic Info & Class Assignment */}
          <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
            {/* General Information Card */}
            <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
                <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: 0 }}>
                  Thông Tin Chung
                </h3>
                {!isCreateMode && (
                  <TeacherStatusBadge
                    status={status === "Published" ? "success" : status === "Draft" ? "warning" : "neutral"}
                    label={status === "Published" ? "Đã Xuất Bản" : status === "Draft" ? "Bản Nháp" : "Lưu Trữ"}
                  />
                )}
              </div>

              <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
                <label className="text-sm font-semibold">Phạm vi học liệu
                  <select className="th-select w-full mt-1" disabled={!isDraft} value={visibility} onChange={e => setVisibility(e.target.value as MaterialVisibility)}>
                    <option value="Private">Private — của tôi</option><option value="Shared">Shared — dùng chung trong trung tâm</option>
                  </select>
                  <p className="text-sm font-normal mt-2">Giáo viên khác chỉ xem/sao chép giáo trình Shared đã xuất bản; không sửa bản gốc hoặc xem lớp của bạn. Bản sao mặc định Private.</p>
                </label>
                <div>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                    <label style={{ fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)" }}>
                      Tên giáo trình / Khung chương trình <span style={{ color: "var(--th-danger)" }}>*</span>
                    </label>
                    <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>{title.length}/200 ký tự</span>
                  </div>
                  <input
                    type="text"
                    maxLength={200}
                    className="th-input"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="Ví dụ: Khung Chương Trình Toán Đại Số 10 Nâng Cao"
                    required
                    disabled={!isDraft}
                  />
                </div>

                <div>
                  <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                    Môn học <span style={{ color: "var(--th-danger)" }}>*</span>
                  </label>
                  <select
                    className="th-select"
                    value={subjectId}
                    onChange={(e) => {
                      setSubjectId(e.target.value);
                      setSelectedNodeIds([]);
                      setGraphVersionId("");
                    }}
                    disabled={!isCreateMode}
                  >
                    {subjects.map((sub) => (
                      <option key={sub.subjectId} value={sub.subjectId}>
                        {sub.subjectName}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                    Khối học áp dụng
                  </label>
                  <select
                    className="th-select"
                    value={gradeLevel}
                    onChange={(e) => { setGradeLevel(e.target.value ? Number(e.target.value) : ""); setGraphVersionId(""); }}
                    disabled={!isDraft}
                  >
                    {isCreateMode ? (
                      <option value="">-- Chọn khối lớp (bắt buộc) --</option>
                    ) : (
                      <option value="">-- Chưa phân loại --</option>
                    )}
                    <option value="10">Khối 10</option>
                    <option value="11">Khối 11</option>
                    <option value="12">Khối 12</option>
                  </select>
                </div>

                <label className="block text-sm">Phiên bản khung tri thức
                  <select className="th-select block mt-1 w-full" disabled={!isDraft || !gradeLevel || versionsQuery.isFetching} value={graphVersionId} onChange={e => setGraphVersionId(e.target.value)}>
                    <option value="">{gradeLevel ? "Chưa có khung được duyệt" : "Chọn khối trước"}</option>
                    {selectedVersion?.isLegacy && <option value={selectedVersion.graphVersionId}>{selectedVersion.title} · Dữ liệu chuyển tiếp</option>}
                    {approvedVersions.map(v => <option key={v.graphVersionId} value={v.graphVersionId}>{v.title || `Khối ${v.gradeLevel}`}{v.isCurrent ? " · Hiện hành" : ""}</option>)}
                  </select>
                  {versionsQuery.isError && <span role="alert">Không tải được khung tri thức. <button type="button" onClick={() => versionsQuery.refetch()}>Tải lại</button></span>}
                  {gradeLevel && !versionsQuery.isFetching && !approvedVersions.length && <p className="text-sm mt-2">Khối này chưa có khung được duyệt. Có thể lưu bản nháp trống; mở đồ thị để tạo hoặc rà soát khung tri thức.</p>}
                </label>

                <div>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                    <label style={{ fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)" }}>
                      Mô tả mục tiêu đào tạo
                    </label>
                    <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>{description.length}/2000 ký tự</span>
                  </div>
                  <textarea
                    className="th-textarea"
                    rows={4}
                    maxLength={2000}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="Tóm tắt yêu cầu cần đạt, chuẩn đầu ra và cấu trúc phân bổ thời lượng..."
                    disabled={!isDraft}
                  />
                </div>
              </div>
            </div>

            {/* Draft planning is not an active curriculum application. */}
            {isDraft && !isCreateMode && <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
              <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 12px 0" }}>
                Lớp dự kiến áp dụng ({selectedClassIds.length})
              </h3>
              <p style={{ fontSize: "0.8rem", color: "var(--th-text-secondary)", margin: "0 0 14px 0" }}>
                Đây là kế hoạch của bản nháp, chưa áp dụng cho học sinh. Sau xuất bản, dùng khung áp dụng giáo trình để gán/thay/ngừng lớp có lịch sử. Ngoại lệ khác khối được quản lý sau xuất bản.
              </p>

              <div style={{ display: "flex", flexDirection: "column", gap: "8px", maxHeight: "220px", overflowY: "auto" }}>
                {plannedClasses.map((cls) => {
                  const isChecked = selectedClassIds.includes(cls.classId);
                  return (
                    <label
                      key={cls.classId}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "10px",
                        padding: "8px 12px",
                        borderRadius: "6px",
                        border: "1px solid var(--th-border-color)",
                        backgroundColor: isChecked ? "var(--th-primary-subtle)" : "var(--th-surface-ground)",
                        cursor: isDraft ? "pointer" : "not-allowed",
                        opacity: isDraft ? 1 : 0.85,
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => isDraft && handleToggleClass(cls.classId)}
                        disabled={!isDraft}
                        style={{ width: "16px", height: "16px", accentColor: "var(--th-primary)" }}
                      />
                      <div>
                        <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--th-text-primary)", display: "flex", alignItems: "center", gap: "6px" }}>
                          <span>{cls.className} ({cls.academicYear})</span>
                          <span className="th-badge th-badge-info" style={{ fontSize: "0.7rem", padding: "1px 6px" }}>
                            {cls.gradeLevel ? `Khối ${cls.gradeLevel}` : "Chưa phân loại"}
                          </span>
                        </div>
                        <div style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>
                          {cls.studentCount ?? 0} học sinh
                        </div>
                      </div>
                    </label>
                  );
                })}
              </div>
            </div>}
            {isCreateMode && <p className="text-sm">Lưu bản nháp trước để chọn lớp dự kiến, hoặc gán lớp sau khi xuất bản.</p>}
          </div>

          {/* Right Column: Knowledge Topics Tree & Organizer */}
          <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
              <div>
                <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: 0 }}>
                  Cây Chủ Đề Tri Thức ({selectedNodeIds.length} đã chọn)
                </h3>
                <span style={{ fontSize: "0.8rem", color: "var(--th-text-secondary)" }}>
                  Sắp xếp trình tự giảng dạy theo thứ tự tuyến tính từ trên xuống dưới
                </span>
              </div>
              <Link
                to={`/giao-vien/do-thi-tri-thuc?${knowledgeScopeSearch(subjectId, graphLinkScope)}`}
                style={{ fontSize: "0.8rem", color: "var(--th-primary)", fontWeight: 600, textDecoration: "none" }}
              >
                Xem đồ thị của giáo trình ↗
              </Link>
            </div>

            {hasUnsavedComposition && <p className="text-sm mb-3">Liên kết xem khung tri thức đã chọn; lưu giáo trình để thấy đúng các chủ đề đang soạn. Dùng nút “Chỉnh sửa khung tri thức” ở đầu trang để thêm chủ đề hoặc liên kết trong bản Draft.</p>}

            {isDraft && (invalidSelection || incompatibleClasses.length > 0 || unresolvedClassIds.length > 0) && !pendingScope && <div role="alert" className="rounded-lg border border-amber-500 p-3 mb-4 text-sm space-y-2">
              <p className="font-semibold">Lựa chọn cần xử lý trước khi lưu</p>
              {selectedVersion?.isLegacy && <p>Khung chuyển tiếp chưa được duyệt cho soạn mới. Chọn phiên bản đã rà soát.</p>}
              {incompatibleNodes.map(nodeId => <div key={nodeId} className="flex justify-between gap-2"><span>{nodeLabels.current.get(nodeId)?.nodeName || `Chủ đề #${nodeId}`} không thuộc khung đã chọn.</span><button type="button" className="th-secondary-button" onClick={() => handleToggleNode(nodeId)}>Bỏ chọn</button></div>)}
              {incompatibleClasses.map(c => <div key={c.classId} className="flex justify-between gap-2"><span>{c.className} · {c.academicYear} · Khối {c.gradeLevel} · Không phù hợp phạm vi hoặc trạng thái</span><button type="button" className="th-secondary-button" onClick={() => handleToggleClass(c.classId)}>Bỏ lớp dự kiến</button></div>)}
              {unresolvedClassIds.map(classId => <div key={classId} className="flex justify-between gap-2"><span>Lớp #{classId} không còn khả dụng.</span><button type="button" className="th-secondary-button" onClick={() => handleToggleClass(classId)}>Bỏ lớp dự kiến</button></div>)}
              {isNodesError && <button type="button" onClick={() => refetchNodes()}>Tải lại chủ đề</button>}
            </div>}

            {/* Selected Nodes List (Ordered) */}
            <div style={{ marginBottom: "20px" }}>
              <label style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--th-text-muted)", textTransform: "uppercase" }}>
                Thứ tự các chủ đề trong giáo trình
              </label>

              {selectedNodeIds.length === 0 ? (
                <div style={{ padding: "24px", textAlign: "center", border: "1px dashed var(--th-border-color)", borderRadius: "8px", marginTop: "8px", color: "var(--th-text-muted)", fontSize: "0.875rem" }}>
                  Chưa có chủ đề nào được thêm vào giáo trình. Chọn từ danh sách bên dưới!
                </div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: "8px", marginTop: "8px" }}>
                  {selectedNodeIds.map((nodeId, idx) => {
                    const node = availableNodes.find((n) => n.nodeId === nodeId) ?? nodeLabels.current.get(nodeId);
                    return (
                      <div
                        key={nodeId}
                        style={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "space-between",
                          padding: "8px 12px",
                          borderRadius: "8px",
                          border: "1px solid var(--th-border-color)",
                          backgroundColor: "var(--th-surface-ground)",
                        }}
                      >
                        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                          <span style={{ width: "22px", height: "22px", borderRadius: "50%", background: "var(--th-primary)", color: "#fff", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.75rem", fontWeight: 700 }}>
                            {idx + 1}
                          </span>
                          <div>
                            <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--th-text-primary)" }}>
                              {node?.nodeName || nodeId}
                            </div>
                            <div style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>
                              Mã: {node?.nodeCode || "N/A"} • {node?.estimatedLearningMinutes ?? 45} phút
                            </div>
                          </div>
                        </div>

                        {isDraft && (
                          <div style={{ display: "flex", gap: "4px" }}>
                            <button
                              type="button"
                              className="th-button-secondary"
                              onClick={() => handleMoveNode(idx, "up")}
                              disabled={idx === 0}
                              style={{ padding: "4px 8px", fontSize: "0.75rem" }}
                            >
                              ↑
                            </button>
                            <button
                              type="button"
                              className="th-button-secondary"
                              onClick={() => handleMoveNode(idx, "down")}
                              disabled={idx === selectedNodeIds.length - 1}
                              style={{ padding: "4px 8px", fontSize: "0.75rem" }}
                            >
                              ↓
                            </button>
                            <button
                              type="button"
                              onClick={() => handleToggleNode(nodeId)}
                              style={{ padding: "4px 8px", fontSize: "0.75rem", color: "var(--th-danger)", background: "none", border: "none", cursor: "pointer" }}
                            >
                              ✕
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Available Nodes Picker */}
            <div style={{ borderTop: "1px solid var(--th-border-color)", paddingTop: "16px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "10px" }}>
                <label style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--th-text-muted)", textTransform: "uppercase" }}>
                  Thư viện chủ đề tri thức có sẵn ({availableNodes.length})
                </label>
              </div>

              {!isDraft ? (
                <div style={{ padding: "16px", textAlign: "center", border: "1px dashed var(--th-border-color)", borderRadius: "8px", color: "var(--th-text-muted)", fontSize: "0.85rem" }}>
                  🔒 Cấu trúc giáo trình đã được khóa đóng băng ở trạng thái {status === "Published" ? "Đã Xuất Bản" : "Lưu Trữ"}. Vui lòng bấm <strong>Nhân Bản (Clone V2)</strong> ở góc trên nếu bạn muốn thêm, bớt hoặc điều chỉnh các chủ đề.
                </div>
              ) : (
                <>
                  <input
                    type="text"
                    className="th-input"
                    placeholder="Tìm chủ đề theo tên hoặc mã..."
                    value={nodeSearch}
                    onChange={(e) => setNodeSearch(e.target.value)}
                    style={{ width: "100%", fontSize: "0.85rem", marginBottom: "10px" }}
                  />

                  {isLoadingNodes ? (
                    <TeacherSkeleton height={150} />
                  ) : (
                    <div style={{ maxHeight: "250px", overflowY: "auto", display: "flex", flexDirection: "column", gap: "6px" }}>
                      {!gradeLevel && <p className="text-sm">Chọn môn và khối để xem chủ đề phù hợp.</p>}
                      {gradeLevel && !availableNodes.length && <p className="text-sm">Chưa có chủ đề thuộc phiên bản khung đã chọn.</p>}
                      {filteredAvailableNodes.map((n) => {
                        const isSelected = selectedNodeIds.includes(n.nodeId);
                        return (
                          <div
                            key={n.nodeId}
                            onClick={() => handleToggleNode(n.nodeId)}
                            style={{
                              display: "flex",
                              justifyContent: "space-between",
                              alignItems: "center",
                              padding: "8px 12px",
                              borderRadius: "6px",
                              border: isSelected ? "1px solid var(--th-primary)" : "1px solid var(--th-border-color)",
                              backgroundColor: isSelected ? "var(--th-primary-subtle)" : "var(--th-surface-ground)",
                              cursor: "pointer",
                            }}
                          >
                            <div>
                              <span style={{ fontWeight: 600, fontSize: "0.85rem", color: "var(--th-text-primary)" }}>
                                {n.nodeName}
                              </span>
                              <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)", marginLeft: "8px" }}>
                                ({n.nodeCode})
                              </span>
                            </div>
                            <button
                              type="button"
                              className={isSelected ? "th-button-secondary" : "th-primary-button"}
                              style={{ fontSize: "0.75rem", padding: "3px 8px" }}
                            >
                              {isSelected ? "Bỏ chọn" : "+ Thêm"}
                            </button>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Archive Confirm Dialog */}
      <CurriculumArchiveDialog
        isOpen={isArchiveDialogOpen}
        onClose={() => setIsArchiveDialogOpen(false)}
        onConfirm={(reason) => archiveMutation.mutate(reason)}
        curriculumId={id}
        title={title}
        isPending={archiveMutation.isPending}
        error={feedbackMsg?.type === "error" ? feedbackMsg.text : null}
      />

      {/* Clone Curriculum Modal */}
      <TeacherModal
        isOpen={isCloneModalOpen}
        onClose={() => setIsCloneModalOpen(false)}
        title="Nhân bản giáo trình (Clone V2)"
        description={`Tạo một bản sao mới dạng Bản nháp (Draft) từ "${title}". Tất cả các chủ đề tri thức đang hoạt động (${selectedNodeIds.length} nodes) sẽ được sao chép nguyên vẹn.`}
        maxWidth="max-w-md"
        footer={
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px" }}>
            <button
              type="button"
              className="th-button-secondary"
              onClick={() => setIsCloneModalOpen(false)}
              disabled={cloneMutation.isPending}
            >
              Hủy
            </button>
            <button
              type="button"
              className="th-primary-button"
              onClick={() => cloneMutation.mutate(cloneTitle)}
              disabled={cloneMutation.isPending || !cloneTitle.trim()}
            >
              {cloneMutation.isPending ? "Đang nhân bản..." : "Tạo bản sao V2"}
            </button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Tên giáo trình mới <span style={{ color: "var(--th-danger)" }}>*</span>
            </label>
            <input
              type="text"
              maxLength={200}
              className="th-input"
              value={cloneTitle}
              onChange={(e) => setCloneTitle(e.target.value)}
              placeholder="Nhập tên giáo trình mới..."
              style={{ width: "100%" }}
              autoFocus
            />
          </div>
          <p style={{ fontSize: "0.75rem", color: "var(--th-text-muted)", margin: 0 }}>
            💡 Gợi ý: Bản sao mới sẽ ở trạng thái Bản nháp (Draft). Bạn có thể tự do thêm, bớt và đổi thứ tự các chủ đề trước khi áp dụng cho khóa học sinh mới mà không ảnh hưởng đến lớp cũ.
          </p>
        </div>
      </TeacherModal>
    </div>
  );
};
