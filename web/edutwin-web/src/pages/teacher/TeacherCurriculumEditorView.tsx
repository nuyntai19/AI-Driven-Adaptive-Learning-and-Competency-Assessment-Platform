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

export const TeacherCurriculumEditorView: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const isCreateMode = !id || id === "tao-moi";
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canPublish = hasPermission(permissions.curriculumsPublish);

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
      if (!title.trim()) throw new Error("Vui lòng nhập tên giáo trình");
      if (!subjectId) throw new Error("Vui lòng chọn môn học");

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
      if (!id || isCreateMode) throw new Error("Cần lưu giáo trình trước khi xuất bản");
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
          <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
            <Link to="/giao-vien/giao-trinh" className="th-button-secondary" style={{ textDecoration: "none" }}>
              Quay lại danh sách
            </Link>

            {!isCreateMode && canPublish && status === "Draft" && (
              <button
                className="th-button-secondary"
                onClick={() => publishMutation.mutate()}
                disabled={publishMutation.isPending || saveMutation.isPending}
              >
                Xuất Bản
              </button>
            )}

            <button
              className="th-primary-button"
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending}
            >
              {saveMutation.isPending ? "Đang lưu..." : isCreateMode ? "Tạo Giáo Trình" : "Lưu Thay Đổi"}
            </button>
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
                  <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                    Tên giáo trình / Khung chương trình *
                  </label>
                  <input
                    type="text"
                    className="th-input"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="Ví dụ: Khung Chương Trình Toán Đại Số 10 Nâng Cao"
                    required
                  />
                </div>

                <div>
                  <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                    Môn học *
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
                  <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                    Mô tả mục tiêu đào tạo
                  </label>
                  <textarea
                    className="th-textarea"
                    rows={4}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="Tóm tắt yêu cầu cần đạt, chuẩn đầu ra và cấu trúc phân bổ thời lượng..."
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
                        cursor: "pointer",
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => handleToggleClass(cls.classId)}
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
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
