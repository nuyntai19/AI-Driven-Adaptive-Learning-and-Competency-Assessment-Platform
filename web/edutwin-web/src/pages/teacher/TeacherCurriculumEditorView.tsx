import React, { useState, useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { curriculumApi } from "../../api/curriculumApi";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { organizationApi } from "../../api/organizationApi";
import type { Curriculum, CreateCurriculumRequest } from "../../types/curriculum";
import type { KnowledgeNodeDto } from "../../types/knowledgeGraph";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  TeacherPageHeader,
  TeacherSkeleton,
  TeacherStatusBadge,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherModal, TeacherConfirmDialog } from "../../components/teacher/TeacherOverlays";

export const TeacherCurriculumEditorView: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const isCreateMode = !id || id === "tao-moi";
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canPublish = hasPermission(permissions.curriculumsPublish);
  const canCreate = hasPermission(permissions.curriculumsCreate);

  // Form State
  const [title, setTitle] = useState("");
  const [subjectId, setSubjectId] = useState("");
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
    queryKey: ["teacherSubjectsList"],
    queryFn: () => organizationApi.listSubjects(true),
  });
  const subjects = subjectsData?.data ?? [];

  // Fetch classes
  const { data: classesData } = useQuery({
    queryKey: ["teacherClassesList"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 50 }),
  });
  const classes = classesData?.data ?? [];

  // Fetch curriculum details if in edit mode
  const {
    data: curriculumData,
    isLoading: isLoadingCurriculum,
    isError: isCurriculumError,
    error: curriculumError,
  } = useQuery({
    queryKey: ["teacherCurriculumDetail", id],
    queryFn: () => (id && !isCreateMode ? curriculumApi.getById(id) : null),
    enabled: !isCreateMode && Boolean(id),
  });

  // Populate state from curriculum details
  useEffect(() => {
    if (curriculumData?.data) {
      const c = curriculumData.data;
      setTitle(c.title || "");
      setSubjectId(c.subjectId || "");
      setDescription(c.description || "");
      setSelectedNodeIds(c.nodeIds || []);
      setSelectedClassIds(c.classIds || []);
      setRowVersion(c.rowVersion || "");
      setStatus(c.reviewStatus || "Draft");
    } else if (isCreateMode && subjects.length > 0 && !subjectId) {
      setSubjectId(subjects[0].subjectId);
    }
  }, [curriculumData, isCreateMode, subjects]);

  // Fetch knowledge nodes for the chosen subject
  const { data: availableNodes = [], isLoading: isLoadingNodes } = useQuery<KnowledgeNodeDto[]>({
    queryKey: ["knowledgeNodes", subjectId],
    queryFn: () => (subjectId ? knowledgeGraphApi.listNodes(subjectId) : Promise.resolve([])),
    enabled: Boolean(subjectId),
  });

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

      if (isCreateMode) {
        const payload: CreateCurriculumRequest = {
          title: title.trim(),
          subjectId,
          description: description.trim() || undefined,
          nodeIds: selectedNodeIds,
        };
        const res = await curriculumApi.create(payload);
        return res.data;
      } else {
        if (!id) throw new Error("Mã giáo trình không hợp lệ");
        let currentVersion = rowVersion;

        // 1. Update basic info
        const updateRes = await curriculumApi.update(id, {
          title: title.trim(),
          description: description.trim() || undefined,
          rowVersion: currentVersion,
        });
        currentVersion = updateRes.data.rowVersion;

        // 2. Update nodes
        const nodesRes = await curriculumApi.updateNodes(id, {
          nodeIds: selectedNodeIds,
          rowVersion: currentVersion,
        });
        currentVersion = nodesRes.data.rowVersion;

        // 3. Update classes
        const classesRes = await curriculumApi.updateClasses(id, {
          classIds: selectedClassIds,
          rowVersion: currentVersion,
        });

        return classesRes.data;
      }
    },
    onSuccess: (data: Curriculum) => {
      setFeedbackMsg({ type: "success", text: "Đã lưu thay đổi giáo trình thành công!" });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculumDetail", id] });

      if (isCreateMode && data?.curriculumId) {
        navigate(`/giao-vien/giao-trinh/${data.curriculumId}`);
      }
    },
    onError: (err: any) => {
      setFeedbackMsg({ type: "error", text: err.message || "Không thể lưu giáo trình" });
    },
  });

  // Publish Mutation
  const publishMutation = useMutation({
    mutationFn: async () => {
      setFeedbackMsg(null);
      if (!id || isCreateMode) throw new Error("Cần lưu giáo trình trước khi xuất bản.");
      if (selectedNodeIds.length === 0) throw new Error("Giáo trình cần có ít nhất 1 chủ đề / điểm tri thức trước khi xuất bản chính thức.");
      return await curriculumApi.publish(id, { rowVersion });
    },
    onSuccess: (res) => {
      setRowVersion(res.data.rowVersion);
      setStatus("Published");
      setFeedbackMsg({ type: "success", text: "Giáo trình đã được xuất bản chính thức!" });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
    },
    onError: (err: any) => {
      setFeedbackMsg({ type: "error", text: err.message || "Lỗi khi xuất bản giáo trình" });
    },
  });

  // Archive Mutation
  const archiveMutation = useMutation({
    mutationFn: async () => {
      setFeedbackMsg(null);
      if (!id || isCreateMode) throw new Error("Cần lưu giáo trình trước khi lưu trữ.");
      return await curriculumApi.archive(id, { rowVersion });
    },
    onSuccess: (res) => {
      setIsArchiveDialogOpen(false);
      setRowVersion(res.data.rowVersion);
      setStatus("Archived");
      setFeedbackMsg({ type: "success", text: "Giáo trình đã được chuyển sang trạng thái Lưu trữ (Archived) và đóng băng hoàn toàn." });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculumDetail", id] });
    },
    onError: (err: any) => {
      setFeedbackMsg({ type: "error", text: err.message || "Lỗi khi lưu trữ giáo trình" });
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

  const isDraft = isCreateMode || status === "Draft";

  // Filtered available nodes
  const filteredAvailableNodes = availableNodes.filter((n) =>
    n.nodeName.toLowerCase().includes(nodeSearch.toLowerCase()) ||
    n.nodeCode.toLowerCase().includes(nodeSearch.toLowerCase())
  );

  return (
    <div className="th-page-container">
      <TeacherPageHeader
        title={isCreateMode ? "Soạn Giáo Trình Mới" : `Chỉnh Sửa Giáo Trình: ${title}`}
        subtitle="Xây dựng cấu trúc cây bài học, tích hợp chủ đề tri thức và phân bổ cho các lớp học"
        actions={
          <div style={{ display: "flex", gap: "10px", alignItems: "center", flexWrap: "wrap" }}>
            <Link to="/giao-vien/giao-trinh" className="th-button-secondary" style={{ textDecoration: "none" }}>
              Quay lại danh sách
            </Link>

            {!isCreateMode && canPublish && status === "Draft" && (
              <button
                type="button"
                className="th-button-secondary"
                onClick={() => publishMutation.mutate()}
                disabled={publishMutation.isPending || saveMutation.isPending}
              >
                Xuất Bản
              </button>
            )}

            {!isCreateMode && canPublish && status === "Published" && (
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
            <strong>Giáo trình đang áp dụng chính thức (Published):</strong> Cấu trúc các chủ đề tri thức và phân bổ lớp học được khóa đóng băng nhằm bảo đảm tính toàn vẹn dữ liệu học tập và mô hình AI Digital Twin của học sinh. Nếu cần cải cách chương trình đào tạo cho năm học mới, vui lòng bấm <strong>Nhân Bản (Clone V2)</strong> ở trên để tạo một bản thảo mới.
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
            <strong>Giáo trình đã được lưu trữ (Archived):</strong> Giáo trình này đã hoàn tất chu kỳ giảng dạy và đóng băng vĩnh viễn để bảo tồn lịch sử học tập. Bạn có thể bấm <strong>Nhân Bản (Clone V2)</strong> để tạo bản sao kế thừa cho các khóa học tiếp theo.
          </div>
        </div>
      )}

      {isLoadingCurriculum && <TeacherSkeleton height={400} />}

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

            {/* Target Classes Assignment */}
            <div className="th-surface" style={{ borderRadius: "12px", padding: "20px" }}>
              <h3 style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 12px 0" }}>
                Lớp Học Áp Dụng ({selectedClassIds.length})
              </h3>
              <p style={{ fontSize: "0.8rem", color: "var(--th-text-secondary)", margin: "0 0 14px 0" }}>
                Học sinh thuộc các lớp được chọn sẽ học và làm bài tập theo lộ trình của giáo trình này.
              </p>

              <div style={{ display: "flex", flexDirection: "column", gap: "8px", maxHeight: "220px", overflowY: "auto" }}>
                {classes.map((cls) => {
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
                        <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--th-text-primary)" }}>
                          {cls.className} ({cls.academicYear})
                        </div>
                        <div style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>
                          {cls.studentCount ?? 0} học sinh
                        </div>
                      </div>
                    </label>
                  );
                })}
              </div>
            </div>
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
                to={`/giao-vien/do-thi-tri-thuc?subjectId=${subjectId}`}
                style={{ fontSize: "0.8rem", color: "var(--th-primary)", fontWeight: 600, textDecoration: "none" }}
              >
                Mở Đồ Thị Tri Thức ↗
              </Link>
            </div>

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
                    const node = availableNodes.find((n) => n.nodeId === nodeId);
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
      <TeacherConfirmDialog
        isOpen={isArchiveDialogOpen}
        onClose={() => setIsArchiveDialogOpen(false)}
        onConfirm={() => archiveMutation.mutate()}
        title="Lưu trữ giáo trình (Archive)"
        description={`Bạn có chắc chắn muốn lưu trữ giáo trình "${title}"? Cấu trúc các điểm tri thức sẽ được đóng băng vĩnh viễn để bảo tồn lịch sử học tập. Bạn có thể sử dụng chức năng "Nhân bản (Clone V2)" để tạo phiên bản mới.`}
        confirmLabel="Xác nhận lưu trữ"
        tone="danger"
        isConfirming={archiveMutation.isPending}
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
