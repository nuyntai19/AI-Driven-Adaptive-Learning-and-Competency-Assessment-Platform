import React, { useState, useMemo, useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { organizationApi } from "../../api/organizationApi";
import type {
  KnowledgeGraphNodeDto,
  KnowledgeGraphEdgeDto,
  KnowledgeNodeType,
  KnowledgeRelationType,
  CreateKnowledgeNodeRequest,
  CreateKnowledgeEdgeRequest,
  UpdateKnowledgeNodeRequest,
  UpdateKnowledgeEdgeRequest,
} from "../../types/knowledgeGraph";
import type { ProblemDetails } from "../../types/auth";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import { isConcurrencyConflict, mapSafeOperationalError, extractProblemDetails } from "../../utils/problemDetails";
import { computeDeterministicDagLayout, computeEdgePath } from "../../utils/knowledgeGraphLayout";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherModal, TeacherConfirmDialog } from "../../components/teacher/TeacherOverlays";

const nodeTypeLabels: Record<KnowledgeNodeType, string> = {
  Subject: "Môn học",
  Chapter: "Chương",
  Topic: "Chủ đề",
  Skill: "Kỹ năng",
  Concept: "Khái niệm",
};

const relationTypeLabels: Record<KnowledgeRelationType, string> = {
  PrerequisiteOf: "Tiên quyết",
  RelatedTo: "Liên quan đến",
  PartOf: "Thuộc về",
  CausesErrorIn: "Gây lỗi trong",
};

const nodeTypeColors: Record<KnowledgeNodeType, string> = {
  Subject: "#3b82f6",
  Chapter: "#a855f7",
  Topic: "#06b6d4",
  Skill: "#10b981",
  Concept: "#f59e0b",
};

const relationTypeColors: Record<KnowledgeRelationType, string> = {
  PrerequisiteOf: "#06b6d4",
  PartOf: "#8b5cf6",
  RelatedTo: "#64748b",
  CausesErrorIn: "#f43f5e",
};

// DAG Cycle Detection (DFS)
function checkDagCycles(nodes: KnowledgeGraphNodeDto[], edges: KnowledgeGraphEdgeDto[]): { isDag: boolean; cycleNodes: string[] } {
  const adj = new Map<string, string[]>();
  nodes.forEach((n) => adj.set(n.nodeId, []));
  edges.forEach((e) => {
    if (adj.has(e.sourceNodeId) && (e.relationType === "PrerequisiteOf" || e.relationType === "PartOf")) {
      adj.get(e.sourceNodeId)!.push(e.targetNodeId);
    }
  });

  const visited = new Map<string, number>(); // 0: unvisited, 1: visiting, 2: visited
  const cycleList: string[] = [];

  function dfs(nodeId: string, path: string[]): boolean {
    visited.set(nodeId, 1);
    const neighbors = adj.get(nodeId) || [];
    for (const n of neighbors) {
      if (visited.get(n) === 1) {
        cycleList.push(...path, n);
        return false;
      }
      if (!visited.get(n) && !dfs(n, [...path, n])) {
        return false;
      }
    }
    visited.set(nodeId, 2);
    return true;
  }

  for (const n of nodes) {
    if (!visited.get(n.nodeId)) {
      if (!dfs(n.nodeId, [n.nodeId])) {
        return { isDag: false, cycleNodes: cycleList };
      }
    }
  }

  return { isDag: true, cycleNodes: [] };
}

export const TeacherKnowledgeGraphView: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const urlSubjectId = searchParams.get("subjectId") || "";

  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canReadSubjects = hasPermission(permissions.subjectsRead);
  const canReadNodes = hasPermission(permissions.nodesRead);
  const canCreateNodes = hasPermission(permissions.nodesCreate);
  const canUpdateNodes = hasPermission(permissions.nodesUpdate);
  const canDeleteNodes = hasPermission(permissions.nodesDelete);
  const canReadEdges = hasPermission(permissions.edgesRead);
  const canCreateEdges = hasPermission(permissions.edgesCreate);
  const canUpdateEdges = hasPermission(permissions.edgesUpdate);
  const canDeleteEdges = hasPermission(permissions.edgesDelete);

  const [selectedSubjectId, setSelectedSubjectId] = useState<string>(urlSubjectId);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"canvas" | "table">("canvas");
  const [zoomLevel, setZoomLevel] = useState<number>(1);
  const [feedbackMsg, setFeedbackMsg] = useState<{ type: "success" | "error"; text: string; traceId?: string | null } | null>(null);

  // Modals state
  const [isAddNodeOpen, setIsAddNodeOpen] = useState(false);
  const [isAddEdgeOpen, setIsAddEdgeOpen] = useState(false);
  const [deletingNode, setDeletingNode] = useState<KnowledgeGraphNodeDto | null>(null);
  const [deletingEdge, setDeletingEdge] = useState<KnowledgeGraphEdgeDto | null>(null);

  // New Node Form State
  const [newNodeName, setNewNodeName] = useState("");
  const [newNodeCode, setNewNodeCode] = useState("");
  const [newNodeType, setNewNodeType] = useState<KnowledgeNodeType>("Topic");
  const [newParentNodeId, setNewParentNodeId] = useState<string>("");
  const [newExamImportance, setNewExamImportance] = useState<number>(3);
  const [newLearningMinutes, setNewLearningMinutes] = useState<number>(45);
  const [newNodeDesc, setNewNodeDesc] = useState("");

  // New Edge Form State
  const [edgeSourceId, setEdgeSourceId] = useState("");
  const [edgeTargetId, setEdgeTargetId] = useState("");
  const [edgeRelation, setEdgeRelation] = useState<KnowledgeRelationType>("PrerequisiteOf");
  const [edgeWeight, setEdgeWeight] = useState<number>(1.0);

  // Node Edit State (in Inspector)
  const [editNodeName, setEditNodeName] = useState("");
  const [editNodeParentId, setEditNodeParentId] = useState<string>("");
  const [editNodeDescription, setEditNodeDescription] = useState("");
  const [editNodeOrderIndex, setEditNodeOrderIndex] = useState("0");
  const [editNodeExamImportance, setEditNodeExamImportance] = useState("0");
  const [editNodeEstimatedMinutes, setEditNodeEstimatedMinutes] = useState("30");
  const [editNodeIsActive, setEditNodeIsActive] = useState(true);

  // Edge Edit State (in Inspector)
  const [editEdgeWeight, setEditEdgeWeight] = useState("1.0");

  // Query: Active subjects list
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isError: isErrorSubjects,
  } = useQuery({
    queryKey: ["teacherSubjectsList", user?.centerId],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: Boolean(user?.centerId && canReadSubjects),
  });

  const subjects = useMemo(() => subjectsData?.data ?? [], [subjectsData?.data]);

  // Sync selectedSubjectId with URL param
  useEffect(() => {
    if (urlSubjectId && subjects.some((s) => s.subjectId === urlSubjectId)) {
      setSelectedSubjectId(urlSubjectId);
    } else if (!selectedSubjectId && subjects.length > 0) {
      const defaultId = subjects[0].subjectId;
      setSelectedSubjectId(defaultId);
      setSearchParams({ subjectId: defaultId }, { replace: true });
    }
  }, [urlSubjectId, subjects, selectedSubjectId, setSearchParams]);

  const handleSelectSubject = (newSubjectId: string) => {
    setSelectedSubjectId(newSubjectId);
    setSelectedNodeId(null);
    setSelectedEdgeId(null);
    if (newSubjectId) {
      setSearchParams({ subjectId: newSubjectId });
    } else {
      setSearchParams({});
    }
  };

  // Query Knowledge Graph
  const {
    data: graphData,
    isLoading: isLoadingGraph,
    isFetching: isFetchingGraph,
    isError: isErrorGraph,
    error: graphError,
    refetch: refetchGraph,
  } = useQuery({
    queryKey: ["teacherKnowledgeGraph", user?.centerId, selectedSubjectId],
    queryFn: () => (selectedSubjectId ? knowledgeGraphApi.getGraph(selectedSubjectId) : null),
    enabled: Boolean(user?.centerId && selectedSubjectId.trim() && canReadNodes && canReadEdges),
  });

  const nodes = useMemo(() => graphData?.nodes ?? [], [graphData?.nodes]);
  const edges = useMemo(() => graphData?.edges ?? [], [graphData?.edges]);

  const nodeMap = useMemo(() => {
    const map = new Map<string, KnowledgeGraphNodeDto>();
    for (const node of nodes) {
      map.set(node.nodeId, node);
    }
    return map;
  }, [nodes]);

  const selectedNode = useMemo(
    () => (selectedNodeId ? nodeMap.get(selectedNodeId) || null : null),
    [selectedNodeId, nodeMap]
  );

  const selectedEdge = useMemo(
    () => (selectedEdgeId ? edges.find((e) => e.edgeId === selectedEdgeId) || null : null),
    [selectedEdgeId, edges]
  );

  // Populate node edit form when selectedNode changes
  useEffect(() => {
    if (selectedNode) {
      setEditNodeName(selectedNode.nodeName);
      setEditNodeParentId(selectedNode.parentNodeId || "");
      setEditNodeDescription(selectedNode.description || "");
      setEditNodeOrderIndex(String(selectedNode.orderIndex ?? 0));
      setEditNodeExamImportance(String(selectedNode.examImportance ?? 0));
      setEditNodeEstimatedMinutes(String(selectedNode.estimatedLearningMinutes ?? 30));
      setEditNodeIsActive(selectedNode.isActive ?? true);
    }
  }, [selectedNode]);

  // Populate edge edit form when selectedEdge changes
  useEffect(() => {
    if (selectedEdge) {
      setEditEdgeWeight(String(selectedEdge.weight ?? 1.0));
    }
  }, [selectedEdge]);

  // DAG Validation Check
  const dagValidation = useMemo(() => {
    return checkDagCycles(nodes, edges);
  }, [nodes, edges]);

  // Compute Deterministic DAG Topological Layout
  const layout = useMemo(() => {
    return computeDeterministicDagLayout(nodes, edges, {
      cardWidth: 190,
      cardHeight: 75,
      gapX: 80,
      gapY: 40,
      paddingX: 50,
      paddingY: 50,
      minWidth: 850,
      minHeight: 520,
    });
  }, [nodes, edges]);

  const invalidateGraph = async () => {
    if (user?.centerId && selectedSubjectId) {
      await queryClient.invalidateQueries({
        queryKey: ["teacherKnowledgeGraph", user.centerId, selectedSubjectId],
      });
    }
  };

  // Mutations
  const createNodeMutation = useMutation({
    mutationFn: (req: CreateKnowledgeNodeRequest) => knowledgeGraphApi.createNode(req),
    onSuccess: async () => {
      await invalidateGraph();
      setIsAddNodeOpen(false);
      resetNodeForm();
      setFeedbackMsg({ type: "success", text: "Tạo điểm tri thức mới thành công!" });
    },
    onError: (err: any) => {
      const details = extractProblemDetails(err);
      setFeedbackMsg({
        type: "error",
        text: mapSafeOperationalError(err, "Không thể tạo điểm tri thức. Vui lòng thử lại."),
        traceId: details.traceId,
      });
    },
  });

  const updateNodeMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateKnowledgeNodeRequest }) =>
      knowledgeGraphApi.updateNode(id, req),
    onSuccess: async () => {
      await invalidateGraph();
      setFeedbackMsg({ type: "success", text: "Cập nhật điểm tri thức thành công!" });
    },
    onError: async (err: any) => {
      const details = extractProblemDetails(err);
      if (isAxiosError<ProblemDetails>(err) && isConcurrencyConflict(err)) {
        await refetchGraph();
        setFeedbackMsg({
          type: "error",
          text: "Dữ liệu đã được cập nhật bởi phiên khác. Hệ thống đã nạp lại phiên bản mới nhất.",
          traceId: details.traceId,
        });
        return;
      }
      setFeedbackMsg({
        type: "error",
        text: mapSafeOperationalError(err, "Không thể cập nhật điểm tri thức."),
        traceId: details.traceId,
      });
    },
  });

  const deleteNodeMutation = useMutation({
    mutationFn: (id: string) => knowledgeGraphApi.deleteNode(id),
    onSuccess: async () => {
      await invalidateGraph();
      setDeletingNode(null);
      setSelectedNodeId(null);
      setFeedbackMsg({ type: "success", text: "Đã xóa điểm tri thức thành công!" });
    },
    onError: (err: any) => {
      const details = extractProblemDetails(err);
      setFeedbackMsg({
        type: "error",
        text: mapSafeOperationalError(err, "Không thể xóa điểm tri thức vì đang có ràng buộc liên kết."),
        traceId: details.traceId,
      });
    },
  });

  const createEdgeMutation = useMutation({
    mutationFn: (req: CreateKnowledgeEdgeRequest) => knowledgeGraphApi.createEdge(req),
    onSuccess: async () => {
      await invalidateGraph();
      setIsAddEdgeOpen(false);
      setFeedbackMsg({ type: "success", text: "Thiết lập liên kết tiên quyết thành công!" });
    },
    onError: (err: any) => {
      const details = extractProblemDetails(err);
      setFeedbackMsg({
        type: "error",
        text: mapSafeOperationalError(err, "Không thể tạo liên kết vì sẽ gây chu trình lặp (DAG Cycle) hoặc liên kết trùng lặp."),
        traceId: details.traceId,
      });
    },
  });

  const updateEdgeMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateKnowledgeEdgeRequest }) =>
      knowledgeGraphApi.updateEdge(id, req),
    onSuccess: async () => {
      await invalidateGraph();
      setFeedbackMsg({ type: "success", text: "Cập nhật trọng số liên kết thành công!" });
    },
    onError: (err: any) => {
      const details = extractProblemDetails(err);
      setFeedbackMsg({
        type: "error",
        text: mapSafeOperationalError(err, "Không thể cập nhật liên kết."),
        traceId: details.traceId,
      });
    },
  });

  const deleteEdgeMutation = useMutation({
    mutationFn: (id: string) => knowledgeGraphApi.deleteEdge(id),
    onSuccess: async () => {
      await invalidateGraph();
      setDeletingEdge(null);
      setSelectedEdgeId(null);
      setFeedbackMsg({ type: "success", text: "Đã xóa liên kết phụ thuộc thành công!" });
    },
    onError: (err: any) => {
      const details = extractProblemDetails(err);
      setFeedbackMsg({
        type: "error",
        text: mapSafeOperationalError(err, "Không thể xóa liên kết."),
        traceId: details.traceId,
      });
    },
  });

  const resetNodeForm = () => {
    setNewNodeName("");
    setNewNodeCode("");
    setNewNodeType("Topic");
    setNewParentNodeId("");
    setNewExamImportance(3);
    setNewLearningMinutes(45);
    setNewNodeDesc("");
  };

  const handleCreateNodeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newNodeName.trim() || !newNodeCode.trim()) return;

    createNodeMutation.mutate({
      subjectId: selectedSubjectId,
      parentNodeId: newParentNodeId || null,
      nodeType: newNodeType,
      nodeCode: newNodeCode.trim(),
      nodeName: newNodeName.trim(),
      description: newNodeDesc.trim() || null,
      orderIndex: nodes.length + 1,
      examImportance: newExamImportance,
      estimatedLearningMinutes: newLearningMinutes,
      isActive: true,
    });
  };

  const handleCreateEdgeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!edgeSourceId || !edgeTargetId || edgeSourceId === edgeTargetId) return;

    createEdgeMutation.mutate({
      subjectId: selectedSubjectId,
      sourceNodeId: edgeSourceId,
      targetNodeId: edgeTargetId,
      relationType: edgeRelation,
      weight: Number(edgeWeight),
    });
  };

  const handleEditNodeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedNode) return;

    const trimmedName = editNodeName.trim();
    if (!trimmedName) return;

    updateNodeMutation.mutate({
      id: selectedNode.nodeId,
      req: {
        nodeName: trimmedName,
        parentNodeId: editNodeParentId.trim() || null,
        description: editNodeDescription.trim() || null,
        orderIndex: parseInt(editNodeOrderIndex, 10) || 0,
        examImportance: parseFloat(editNodeExamImportance) || 0,
        estimatedLearningMinutes: parseInt(editNodeEstimatedMinutes, 10) || 30,
        isActive: editNodeIsActive,
        rowVersion: selectedNode.rowVersion,
      },
    });
  };

  const handleEditEdgeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedEdge) return;

    const weight = parseFloat(editEdgeWeight);
    if (isNaN(weight) || weight < 0 || weight > 1.0) return;

    updateEdgeMutation.mutate({
      id: selectedEdge.edgeId,
      req: {
        weight,
        rowVersion: selectedEdge.rowVersion,
      },
    });
  };

  const activeSubject = subjects.find((s) => s.subjectId === selectedSubjectId);

  return (
    <div className="th-page-container">
      <TeacherPageHeader
        title="Đồ Thị Tri Thức Môn Học (Knowledge Graph)"
        subtitle="Mô hình hóa quan hệ phụ thuộc tiên quyết (Prerequisites), phân tích cây tri thức DAG và kiểm soát lộ trình chuẩn"
        breadcrumbs={[
          { label: "Không gian Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Đồ thị tri thức" },
        ]}
        actions={
          <div style={{ display: "flex", gap: "10px", alignItems: "center", flexWrap: "wrap" }}>
            {canCreateEdges && selectedSubjectId && nodes.length >= 2 && (
              <button
                type="button"
                className="th-secondary-button"
                onClick={() => {
                  setEdgeSourceId(nodes[0]?.nodeId || "");
                  setEdgeTargetId(nodes[1]?.nodeId || "");
                  setIsAddEdgeOpen(true);
                }}
              >
                + Thêm Liên Kết Tiên Quyết
              </button>
            )}

            {canCreateNodes && selectedSubjectId && (
              <button
                type="button"
                className="th-primary-button"
                onClick={() => setIsAddNodeOpen(true)}
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M12 5v14M5 12h14" />
                </svg>
                Thêm Chủ Đề Mới
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
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            backgroundColor: feedbackMsg.type === "success" ? "rgba(16, 185, 129, 0.12)" : "rgba(239, 68, 68, 0.12)",
            border: `1px solid ${feedbackMsg.type === "success" ? "var(--th-primary)" : "var(--th-danger)"}`,
            color: feedbackMsg.type === "success" ? "var(--th-primary)" : "var(--th-danger)",
            fontSize: "0.85rem",
            fontWeight: 600,
          }}
        >
          <div>
            <span>{feedbackMsg.text}</span>
            {feedbackMsg.traceId && (
              <div style={{ fontSize: "0.75rem", fontFamily: "monospace", opacity: 0.8, marginTop: "2px" }}>
                Trace ID: {feedbackMsg.traceId}
              </div>
            )}
          </div>
          <button
            type="button"
            onClick={() => setFeedbackMsg(null)}
            style={{ background: "none", border: "none", color: "inherit", cursor: "pointer", fontWeight: "bold" }}
          >
            ✕
          </button>
        </div>
      )}

      {/* Stats and DAG Status */}
      <div className="th-stats-grid">
        <TeacherMetricCard
          label="Tổng Số Điểm Tri Thức"
          value={nodes.length}
          unit="nút"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="3" />
              <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-2 2 2 2 0 01-2-2v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83 0 2 2 0 010-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 01-2-2 2 2 0 012-2h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 010-2.83 2 2 0 012.83 0l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 012-2 2 2 0 012 2v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 0 2 2 0 010 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 012 2 2 2 0 01-2 2h-.09a1.65 1.65 0 00-1.51 1z" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Liên Kết Phụ Thuộc (Edges)"
          value={edges.length}
          unit="quan hệ"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="5" y1="12" x2="19" y2="12" />
              <polyline points="12 5 19 12 12 19" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Kiểm Định DAG Chuẩn"
          value={dagValidation.isDag ? "Hợp Lệ (DAG)" : "Có Vòng Lặp"}
          supportingText={dagValidation.isDag ? "Không có xung đột chu trình" : `Phát hiện chu trình tại: ${dagValidation.cycleNodes.length} nút`}
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              {dagValidation.isDag ? (
                <path d="M22 11.08V12a10 10 0 11-5.93-9.14" />
              ) : (
                <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              )}
            </svg>
          }
        />
        <TeacherMetricCard
          label="Thời Lượng Toàn Bộ"
          value={nodes.reduce((acc, curr) => acc + (curr.estimatedLearningMinutes || 0), 0)}
          unit="phút học"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
          }
        />
      </div>

      {/* Subject Selector & Toolbar Bar */}
      <div
        className="th-surface"
        style={{
          padding: "16px 20px",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "16px",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "16px", flexWrap: "wrap" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 700, color: "var(--th-text-muted)", textTransform: "uppercase", marginBottom: "4px" }}>
              Môn học đang chọn:
            </label>
            {isLoadingSubjects ? (
              <div style={{ height: "38px", width: "200px", borderRadius: "8px", background: "var(--th-surface-muted)" }} />
            ) : isErrorSubjects ? (
              <span style={{ fontSize: "0.85rem", color: "var(--th-danger)" }}>Lỗi tải môn học</span>
            ) : (
              <select
                className="th-select"
                value={selectedSubjectId}
                onChange={(e) => handleSelectSubject(e.target.value)}
                style={{ minWidth: "220px", fontWeight: 600 }}
              >
                <option value="">-- Chọn môn học --</option>
                {subjects.map((sub) => (
                  <option key={sub.subjectId} value={sub.subjectId}>
                    {sub.subjectCode} - {sub.subjectName}
                  </option>
                ))}
              </select>
            )}
          </div>

          {selectedSubjectId && (
            <div style={{ display: "flex", alignItems: "center", gap: "12px", fontSize: "0.85rem", color: "var(--th-text-secondary)", marginTop: "18px" }}>
              <span>Điểm tri thức: <strong style={{ color: "var(--th-text)" }}>{nodes.length}</strong></span>
              <span>•</span>
              <span>Liên kết: <strong style={{ color: "var(--th-teal)" }}>{edges.length}</strong></span>
              {isFetchingGraph && (
                <>
                  <span>•</span>
                  <span style={{ color: "var(--th-teal)" }}>Đang đồng bộ...</span>
                </>
              )}
            </div>
          )}
        </div>

        {selectedSubjectId && (
          <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
            {/* View Mode Toggle */}
            <div style={{ display: "flex", borderRadius: "8px", padding: "3px", backgroundColor: "var(--th-surface-raised)", border: "1px solid var(--th-border)" }}>
              <button
                type="button"
                onClick={() => setViewMode("canvas")}
                style={{
                  padding: "5px 12px",
                  borderRadius: "6px",
                  fontSize: "0.8rem",
                  fontWeight: 600,
                  border: "none",
                  cursor: "pointer",
                  backgroundColor: viewMode === "canvas" ? "var(--th-teal)" : "transparent",
                  color: viewMode === "canvas" ? "#ffffff" : "var(--th-text-secondary)",
                  transition: "all 0.15s ease",
                }}
              >
                Bản đồ (Canvas)
              </button>
              <button
                type="button"
                onClick={() => setViewMode("table")}
                style={{
                  padding: "5px 12px",
                  borderRadius: "6px",
                  fontSize: "0.8rem",
                  fontWeight: 600,
                  border: "none",
                  cursor: "pointer",
                  backgroundColor: viewMode === "table" ? "var(--th-teal)" : "transparent",
                  color: viewMode === "table" ? "#ffffff" : "var(--th-text-secondary)",
                  transition: "all 0.15s ease",
                }}
              >
                Bảng dữ liệu
              </button>
            </div>

            <button
              type="button"
              onClick={() => refetchGraph()}
              className="th-secondary-button"
              style={{ minHeight: "34px", padding: "0 12px", fontSize: "0.8rem" }}
            >
              Làm mới
            </button>
          </div>
        )}
      </div>

      {/* Main Graph Canvas & Inspector Area */}
      {!selectedSubjectId ? (
        <div className="th-surface" style={{ padding: "48px 24px", textAlign: "center" }}>
          <p style={{ fontSize: "1rem", color: "var(--th-text-muted)", margin: 0 }}>
            Vui lòng chọn một môn học ở danh sách phía trên để nạp đồ thị tri thức.
          </p>
        </div>
      ) : isLoadingGraph ? (
        <TeacherSkeleton height={560} />
      ) : isErrorGraph ? (
        <TeacherSafeErrorPanel
          error={graphError}
          title="Không thể nạp dữ liệu đồ thị tri thức"
          onRetry={() => refetchGraph()}
        />
      ) : (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 340px", gap: "20px", alignItems: "start" }}>
          {/* Main Visualizer or Table Area */}
          <div
            className="th-surface"
            style={{
              overflow: "hidden",
              minHeight: "600px",
              display: "flex",
              flexDirection: "column",
            }}
          >
            {viewMode === "canvas" ? (
              <div style={{ position: "relative", width: "100%", height: "100%" }}>
                {/* Canvas Controls Header: Legend on Left, Zoom Controls on Right */}
                <div
                  style={{
                    padding: "8px 16px",
                    borderBottom: "1px solid var(--th-border-subtle)",
                    backgroundColor: "var(--th-surface-raised)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: "16px",
                    fontSize: "0.75rem",
                    color: "var(--th-text-secondary)",
                    flexWrap: "wrap",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "16px", flexWrap: "wrap" }}>
                    <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                      <span style={{ width: "10px", height: "10px", borderRadius: "50%", backgroundColor: "#06b6d4" }} /> Tiên quyết (Prerequisite)
                    </span>
                    <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                      <span style={{ width: "10px", height: "10px", borderRadius: "50%", backgroundColor: "#8b5cf6" }} /> Thuộc về (PartOf)
                    </span>
                    <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                      <span style={{ width: "10px", height: "10px", borderRadius: "50%", backgroundColor: "#64748b" }} /> Liên quan (RelatedTo)
                    </span>
                    <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                      <span style={{ width: "10px", height: "10px", borderRadius: "50%", backgroundColor: "#f43f5e" }} /> Gây lỗi (CausesError)
                    </span>
                  </div>

                  {/* Zoom Controls inside Canvas Header */}
                  <div style={{ display: "flex", alignItems: "center", gap: "4px", backgroundColor: "var(--th-surface)", border: "1px solid var(--th-border)", borderRadius: "6px", padding: "2px 4px" }}>
                    <button
                      type="button"
                      onClick={() => setZoomLevel((z) => Math.max(0.6, z - 0.1))}
                      style={{ width: "24px", height: "24px", border: "none", background: "none", color: "var(--th-text)", cursor: "pointer", fontWeight: "bold", fontSize: "0.85rem" }}
                      title="Thu nhỏ"
                    >
                      -
                    </button>
                    <span style={{ fontSize: "0.75rem", fontFamily: "monospace", minWidth: "38px", textAlign: "center", color: "var(--th-text-secondary)" }}>
                      {Math.round(zoomLevel * 100)}%
                    </span>
                    <button
                      type="button"
                      onClick={() => setZoomLevel((z) => Math.min(1.5, z + 0.1))}
                      style={{ width: "24px", height: "24px", border: "none", background: "none", color: "var(--th-text)", cursor: "pointer", fontWeight: "bold", fontSize: "0.85rem" }}
                      title="Phóng to"
                    >
                      +
                    </button>
                    <button
                      type="button"
                      onClick={() => setZoomLevel(1)}
                      style={{ padding: "0 6px", height: "24px", border: "none", background: "none", color: "var(--th-text-muted)", cursor: "pointer", fontSize: "0.7rem" }}
                      title="Đặt lại 100%"
                    >
                      100%
                    </button>
                  </div>
                </div>

                {/* SVG Canvas Area */}
                <div
                  style={{
                    overflow: "auto",
                    maxHeight: "720px",
                    minHeight: "560px",
                    padding: "24px",
                    backgroundColor: "var(--th-bg)",
                    display: "flex",
                    justifyContent: "center",
                    alignItems: "flex-start",
                  }}
                  onClick={() => {
                    setSelectedNodeId(null);
                    setSelectedEdgeId(null);
                  }}
                >
                  {nodes.length === 0 ? (
                    <div style={{ margin: "auto", textAlign: "center", padding: "48px" }}>
                      <p style={{ color: "var(--th-text-muted)", fontSize: "0.9rem" }}>Môn học này chưa có điểm tri thức nào.</p>
                      {canCreateNodes && (
                        <button
                          type="button"
                          onClick={() => setIsAddNodeOpen(true)}
                          className="th-primary-button"
                          style={{ marginTop: "12px", fontSize: "0.8rem" }}
                        >
                          + Tạo Điểm Tri Thức Đầu Tiên
                        </button>
                      )}
                    </div>
                  ) : (
                    <div
                      style={{
                        transform: `scale(${zoomLevel})`,
                        transformOrigin: "top center",
                        transition: "transform 0.15s ease-out",
                      }}
                    >
                      <svg
                        width={layout.width}
                        height={layout.height}
                        viewBox={`0 0 ${layout.width} ${layout.height}`}
                        style={{ overflow: "visible", userSelect: "none" }}
                      >
                        <defs>
                          {(["PrerequisiteOf", "PartOf", "RelatedTo", "CausesErrorIn"] as KnowledgeRelationType[]).map(
                            (rel) => (
                              <marker
                                key={rel}
                                id={`arrow-${rel}`}
                                viewBox="0 0 10 10"
                                refX="9"
                                refY="5"
                                markerWidth="6"
                                markerHeight="6"
                                orient="auto-start-reverse"
                              >
                                <path d="M 0 1 L 10 5 L 0 9 z" fill={relationTypeColors[rel]} />
                              </marker>
                            )
                          )}
                        </defs>

                        {/* Render Edges */}
                        {edges.map((edge) => {
                          const sourcePos = layout.positions.get(edge.sourceNodeId);
                          const targetPos = layout.positions.get(edge.targetNodeId);
                          if (!sourcePos || !targetPos) return null;

                          const isSelected = selectedEdgeId === edge.edgeId;
                          const strokeColor = relationTypeColors[edge.relationType] || "#64748b";
                          const { d: pathData, midX, midY } = computeEdgePath(sourcePos, targetPos, 190, 75);

                          return (
                            <g
                              key={edge.edgeId}
                              style={{ cursor: "pointer" }}
                              onClick={(e) => {
                                e.stopPropagation();
                                setSelectedEdgeId(edge.edgeId);
                                setSelectedNodeId(null);
                              }}
                            >
                              {/* Wide transparent path for easy clicking */}
                              <path d={pathData} fill="none" stroke="transparent" strokeWidth={18} />

                              {/* Visible curve path */}
                              <path
                                d={pathData}
                                fill="none"
                                stroke={strokeColor}
                                strokeWidth={isSelected ? 3.5 : Math.max(1.8, (edge.weight ?? 1) * 2.5)}
                                strokeDasharray={edge.relationType === "RelatedTo" ? "5,5" : undefined}
                                markerEnd={`url(#arrow-${edge.relationType})`}
                                style={{
                                  filter: isSelected ? `drop-shadow(0 0 6px ${strokeColor})` : undefined,
                                  opacity: selectedNodeId || (selectedEdgeId && !isSelected) ? 0.35 : 0.85,
                                  transition: "all 0.15s ease",
                                }}
                              />

                              {/* Edge Weight Pill Badge */}
                              <rect
                                x={midX - 18}
                                y={midY - 9}
                                width={36}
                                height={18}
                                rx={4}
                                fill="var(--th-surface)"
                                stroke={strokeColor}
                                strokeWidth={isSelected ? 1.5 : 1}
                              />
                              <text
                                x={midX}
                                y={midY + 3.5}
                                textAnchor="middle"
                                fontSize={9}
                                fontWeight="bold"
                                fill={strokeColor}
                              >
                                {edge.weight}
                              </text>
                            </g>
                          );
                        })}

                        {/* Render Nodes Cards */}
                        {nodes.map((node) => {
                          const pos = layout.positions.get(node.nodeId);
                          if (!pos) return null;

                          const cardW = 190;
                          const cardH = 75;
                          const isSelected = selectedNodeId === node.nodeId;

                          return (
                            <g
                              key={node.nodeId}
                              transform={`translate(${pos.x}, ${pos.y})`}
                              style={{ cursor: "pointer" }}
                              onClick={(e) => {
                                e.stopPropagation();
                                setSelectedNodeId(node.nodeId);
                                setSelectedEdgeId(null);
                              }}
                            >
                              {/* Card Background Box */}
                              <rect
                                width={cardW}
                                height={cardH}
                                rx={10}
                                fill="var(--th-surface)"
                                stroke={isSelected ? "var(--th-teal)" : "var(--th-border)"}
                                strokeWidth={isSelected ? 2.5 : 1}
                                style={{
                                  filter: isSelected
                                    ? "drop-shadow(0 0 10px rgba(20, 184, 166, 0.5))"
                                    : "drop-shadow(0 4px 6px rgba(0, 0, 0, 0.25))",
                                  transition: "all 0.15s ease",
                                }}
                              />

                              {/* Top Accent Color Bar */}
                              <rect
                                width={cardW}
                                height={4}
                                rx={2}
                                fill={nodeTypeColors[node.nodeType] || "#06b6d4"}
                              />

                              {/* Node Code */}
                              <text
                                x={12}
                                y={22}
                                fontSize={10}
                                fontWeight="bold"
                                fill="var(--th-teal)"
                                fontFamily="monospace"
                              >
                                {node.nodeCode}
                              </text>

                              {/* Node Type Badge Text */}
                              <text
                                x={cardW - 12}
                                y={22}
                                textAnchor="end"
                                fontSize={9}
                                fill="var(--th-text-secondary)"
                                fontWeight="600"
                              >
                                {nodeTypeLabels[node.nodeType] ?? node.nodeType}
                              </text>

                              {/* Node Name */}
                              <text
                                x={12}
                                y={44}
                                fontSize={12}
                                fontWeight="700"
                                fill="var(--th-text)"
                              >
                                {node.nodeName.length > 20
                                  ? `${node.nodeName.substring(0, 19)}...`
                                  : node.nodeName}
                              </text>

                              {/* Footer Info: Order • Exam Weight • Learning Minutes */}
                              <text
                                x={12}
                                y={62}
                                fontSize={9}
                                fill="var(--th-text-muted)"
                              >
                                #{node.orderIndex} • Thi: {node.examImportance}% • {node.estimatedLearningMinutes}p
                              </text>
                            </g>
                          );
                        })}
                      </svg>
                    </div>
                  )}
                </div>
              </div>
            ) : (
              /* Structured Table View */
              <div style={{ padding: "20px" }}>
                <h3 style={{ fontSize: "0.95rem", fontWeight: 700, marginBottom: "12px", color: "var(--th-text)" }}>
                  Danh sách điểm tri thức ({nodes.length})
                </h3>
                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.825rem" }}>
                    <thead>
                      <tr style={{ borderBottom: "1px solid var(--th-border-subtle)", textAlign: "left", color: "var(--th-text-muted)" }}>
                        <th style={{ padding: "8px 10px" }}>Mã</th>
                        <th style={{ padding: "8px 10px" }}>Tên chủ đề</th>
                        <th style={{ padding: "8px 10px" }}>Phân loại</th>
                        <th style={{ padding: "8px 10px" }}>Thứ tự</th>
                        <th style={{ padding: "8px 10px" }}>Trọng số thi</th>
                        <th style={{ padding: "8px 10px" }}>Thời lượng</th>
                        <th style={{ padding: "8px 10px", textAlign: "right" }}>Thao tác</th>
                      </tr>
                    </thead>
                    <tbody>
                      {nodes.map((n) => (
                        <tr
                          key={n.nodeId}
                          style={{
                            borderBottom: "1px solid var(--th-border-subtle)",
                            backgroundColor: selectedNodeId === n.nodeId ? "var(--th-surface-raised)" : "transparent",
                            cursor: "pointer",
                          }}
                          onClick={() => {
                            setSelectedNodeId(n.nodeId);
                            setSelectedEdgeId(null);
                          }}
                        >
                          <td style={{ padding: "10px", fontFamily: "monospace", color: "var(--th-teal)", fontWeight: 600 }}>{n.nodeCode}</td>
                          <td style={{ padding: "10px", fontWeight: 600 }}>{n.nodeName}</td>
                          <td style={{ padding: "10px" }}>
                            <span className="th-badge th-badge-info">{nodeTypeLabels[n.nodeType] ?? n.nodeType}</span>
                          </td>
                          <td style={{ padding: "10px" }}>{n.orderIndex}</td>
                          <td style={{ padding: "10px" }}>{n.examImportance}%</td>
                          <td style={{ padding: "10px" }}>{n.estimatedLearningMinutes} phút</td>
                          <td style={{ padding: "10px", textAlign: "right" }}>
                            <button
                              type="button"
                              className="th-secondary-button"
                              style={{ minHeight: "28px", padding: "0 8px", fontSize: "0.75rem" }}
                              onClick={(e) => {
                                e.stopPropagation();
                                setSelectedNodeId(n.nodeId);
                                setSelectedEdgeId(null);
                              }}
                            >
                              Chi tiết
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>

          {/* Right Inspector Panel (340px) */}
          <div
            className="th-surface"
            style={{
              padding: "20px",
              position: "sticky",
              top: "20px",
            }}
          >
            {selectedNode ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", borderBottom: "1px solid var(--th-border-subtle)", paddingBottom: "12px" }}>
                  <div>
                    <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "4px" }}>
                      <span className="th-badge th-badge-info">{nodeTypeLabels[selectedNode.nodeType] ?? selectedNode.nodeType}</span>
                      <span style={{ fontSize: "0.75rem", fontFamily: "monospace", color: "var(--th-teal)", fontWeight: 700 }}>{selectedNode.nodeCode}</span>
                    </div>
                    <h3 style={{ fontSize: "1.05rem", fontWeight: 700, margin: "4px 0 0 0", color: "var(--th-text)" }}>{selectedNode.nodeName}</h3>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedNodeId(null)}
                    style={{ background: "none", border: "none", color: "var(--th-text-muted)", cursor: "pointer", fontSize: "1.1rem" }}
                  >
                    ✕
                  </button>
                </div>

                {selectedNode.description && (
                  <p style={{ fontSize: "0.8rem", color: "var(--th-text-secondary)", margin: 0, lineHeight: 1.4 }}>
                    {selectedNode.description}
                  </p>
                )}

                {/* Quick Pedagogy Action Buttons for Teachers */}
                <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                  <Link
                    to={`/giao-vien/cau-hoi?topicNodeId=${selectedNode.nodeId}`}
                    className="th-secondary-button"
                    style={{ textDecoration: "none", textAlign: "center", fontSize: "0.8rem", minHeight: "32px", padding: "0 10px" }}
                  >
                    📝 Xem Câu Hỏi Thuộc Chủ Đề Này
                  </Link>
                  <Link
                    to={`/giao-vien/bai-tap/tao-moi?topicNodeId=${selectedNode.nodeId}`}
                    className="th-primary-button"
                    style={{ textDecoration: "none", textAlign: "center", fontSize: "0.8rem", minHeight: "32px", padding: "0 10px" }}
                  >
                    🎯 Tạo Bài Tập Bù Đắp Lỗ Hổng
                  </Link>
                </div>

                {/* Connected Prerequisites */}
                {edges.filter((e) => e.sourceNodeId === selectedNode.nodeId || e.targetNodeId === selectedNode.nodeId).length > 0 && (
                  <div>
                    <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 700, color: "var(--th-text-muted)", textTransform: "uppercase", marginBottom: "6px" }}>
                      Quan hệ tiên quyết liên quan:
                    </label>
                    <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
                      {edges
                        .filter((e) => e.sourceNodeId === selectedNode.nodeId || e.targetNodeId === selectedNode.nodeId)
                        .map((e) => {
                          const isSource = e.sourceNodeId === selectedNode.nodeId;
                          const otherNodeId = isSource ? e.targetNodeId : e.sourceNodeId;
                          const otherNode = nodeMap.get(otherNodeId);

                          return (
                            <div
                              key={e.edgeId}
                              style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                padding: "6px 8px",
                                borderRadius: "6px",
                                backgroundColor: "var(--th-surface-raised)",
                                border: "1px solid var(--th-border)",
                                fontSize: "0.75rem",
                              }}
                            >
                              <span>
                                {isSource ? "Tiên quyết cho → " : "Phụ thuộc từ ← "}
                                <strong>{otherNode?.nodeName || otherNodeId}</strong>
                              </span>
                              {canDeleteEdges && (
                                <button
                                  type="button"
                                  onClick={() => setDeletingEdge(e)}
                                  style={{ background: "none", border: "none", color: "var(--th-danger)", cursor: "pointer", fontSize: "0.8rem", padding: "2px 4px" }}
                                  title="Xóa liên kết"
                                >
                                  ✕
                                </button>
                              )}
                            </div>
                          );
                        })}
                    </div>
                  </div>
                )}

                {/* Edit Form */}
                {canUpdateNodes ? (
                  <form onSubmit={handleEditNodeSubmit} style={{ display: "flex", flexDirection: "column", gap: "10px", borderTop: "1px solid var(--th-border-subtle)", paddingTop: "12px" }}>
                    <h4 style={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "var(--th-text-muted)", margin: 0 }}>
                      Chỉnh sửa điểm tri thức
                    </h4>

                    <div>
                      <label style={{ display: "block", fontSize: "0.75rem", color: "var(--th-text-secondary)", marginBottom: "4px" }}>Tên nút *</label>
                      <input
                        type="text"
                        className="th-input"
                        value={editNodeName}
                        onChange={(e) => setEditNodeName(e.target.value)}
                        required
                        style={{ width: "100%", fontSize: "0.8rem" }}
                      />
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px" }}>
                      <div>
                        <label style={{ display: "block", fontSize: "0.75rem", color: "var(--th-text-secondary)", marginBottom: "4px" }}>Trọng số thi (%)</label>
                        <input
                          type="number"
                          min={0}
                          max={100}
                          className="th-input"
                          value={editNodeExamImportance}
                          onChange={(e) => setEditNodeExamImportance(e.target.value)}
                          style={{ width: "100%", fontSize: "0.8rem" }}
                        />
                      </div>
                      <div>
                        <label style={{ display: "block", fontSize: "0.75rem", color: "var(--th-text-secondary)", marginBottom: "4px" }}>Phút học</label>
                        <input
                          type="number"
                          min={1}
                          className="th-input"
                          value={editNodeEstimatedMinutes}
                          onChange={(e) => setEditNodeEstimatedMinutes(e.target.value)}
                          style={{ width: "100%", fontSize: "0.8rem" }}
                        />
                      </div>
                    </div>

                    <div>
                      <label style={{ display: "block", fontSize: "0.75rem", color: "var(--th-text-secondary)", marginBottom: "4px" }}>Mô tả</label>
                      <textarea
                        rows={2}
                        className="th-textarea"
                        value={editNodeDescription}
                        onChange={(e) => setEditNodeDescription(e.target.value)}
                        style={{ fontSize: "0.8rem" }}
                      />
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "4px" }}>
                      {canDeleteNodes && (
                        <button
                          type="button"
                          className="th-danger-button"
                          onClick={() => setDeletingNode(selectedNode)}
                          style={{ minHeight: "32px", padding: "0 10px", fontSize: "0.75rem" }}
                        >
                          Xóa nút
                        </button>
                      )}
                      <button
                        type="submit"
                        className="th-primary-button"
                        disabled={updateNodeMutation.isPending}
                        style={{ minHeight: "32px", padding: "0 14px", fontSize: "0.75rem", marginLeft: "auto" }}
                      >
                        {updateNodeMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                      </button>
                    </div>
                  </form>
                ) : (
                  <div style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>
                    Chế độ xem thông tin: Không có quyền sửa điểm tri thức.
                  </div>
                )}
              </div>
            ) : selectedEdge ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", borderBottom: "1px solid var(--th-border-subtle)", paddingBottom: "12px" }}>
                  <div>
                    <span className="th-badge th-badge-warning">{relationTypeLabels[selectedEdge.relationType] ?? selectedEdge.relationType}</span>
                    <h3 style={{ fontSize: "1.05rem", fontWeight: 700, margin: "6px 0 0 0", color: "var(--th-text)" }}>Chi tiết liên kết</h3>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedEdgeId(null)}
                    style={{ background: "none", border: "none", color: "var(--th-text-muted)", cursor: "pointer", fontSize: "1.1rem" }}
                  >
                    ✕
                  </button>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: "8px", fontSize: "0.8rem" }}>
                  <div style={{ padding: "8px 10px", borderRadius: "6px", backgroundColor: "var(--th-surface-raised)", border: "1px solid var(--th-border)" }}>
                    <span style={{ color: "var(--th-text-muted)", display: "block", fontSize: "0.75rem" }}>Nút nguồn (Học trước):</span>
                    <strong>{nodeMap.get(selectedEdge.sourceNodeId)?.nodeName || selectedEdge.sourceNodeId}</strong>
                  </div>
                  <div style={{ padding: "8px 10px", borderRadius: "6px", backgroundColor: "var(--th-surface-raised)", border: "1px solid var(--th-border)" }}>
                    <span style={{ color: "var(--th-text-muted)", display: "block", fontSize: "0.75rem" }}>Nút đích (Học sau):</span>
                    <strong>{nodeMap.get(selectedEdge.targetNodeId)?.nodeName || selectedEdge.targetNodeId}</strong>
                  </div>
                </div>

                {canUpdateEdges ? (
                  <form onSubmit={handleEditEdgeSubmit} style={{ display: "flex", flexDirection: "column", gap: "10px", borderTop: "1px solid var(--th-border-subtle)", paddingTop: "12px" }}>
                    <div>
                      <label style={{ display: "block", fontSize: "0.75rem", color: "var(--th-text-secondary)", marginBottom: "4px" }}>
                        Trọng số phụ thuộc (0.00 – 1.00)
                      </label>
                      <input
                        type="number"
                        min={0}
                        max={1.0}
                        step={0.05}
                        className="th-input"
                        value={editEdgeWeight}
                        onChange={(e) => setEditEdgeWeight(e.target.value)}
                        style={{ width: "100%", fontSize: "0.8rem" }}
                        required
                      />
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "4px" }}>
                      {canDeleteEdges && (
                        <button
                          type="button"
                          className="th-danger-button"
                          onClick={() => setDeletingEdge(selectedEdge)}
                          style={{ minHeight: "32px", padding: "0 10px", fontSize: "0.75rem" }}
                        >
                          Xóa liên kết
                        </button>
                      )}
                      <button
                        type="submit"
                        className="th-primary-button"
                        disabled={updateEdgeMutation.isPending}
                        style={{ minHeight: "32px", padding: "0 14px", fontSize: "0.75rem", marginLeft: "auto" }}
                      >
                        {updateEdgeMutation.isPending ? "Đang lưu..." : "Lưu"}
                      </button>
                    </div>
                  </form>
                ) : null}
              </div>
            ) : (
              /* Overview When Nothing is Selected */
              <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
                <div style={{ borderBottom: "1px solid var(--th-border-subtle)", paddingBottom: "12px" }}>
                  <span style={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "var(--th-teal)" }}>TỔNG QUAN ĐỒ THỊ</span>
                  <h3 style={{ fontSize: "1.05rem", fontWeight: 700, margin: "4px 0 0 0", color: "var(--th-text)" }}>
                    {activeSubject ? activeSubject.subjectName : "Môn học đã chọn"}
                  </h3>
                  <p style={{ fontSize: "0.8rem", color: "var(--th-text-secondary)", margin: "4px 0 0 0" }}>
                    Nhấn vào bất kỳ điểm tri thức hoặc liên kết nào trên sơ đồ để xem chi tiết và thao tác.
                  </p>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                  <h4 style={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "var(--th-text-muted)", margin: 0 }}>
                    Phân bố loại điểm tri thức:
                  </h4>
                  {(["Chapter", "Topic", "Skill", "Concept"] as KnowledgeNodeType[]).map((type) => {
                    const count = nodes.filter((n) => n.nodeType === type).length;
                    return (
                      <div
                        key={type}
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          padding: "6px 10px",
                          borderRadius: "6px",
                          backgroundColor: "var(--th-surface-raised)",
                          fontSize: "0.8rem",
                        }}
                      >
                        <span style={{ color: "var(--th-text-secondary)" }}>{nodeTypeLabels[type]}</span>
                        <strong style={{ color: "var(--th-text)" }}>{count}</strong>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Modal: Add Node */}
      <TeacherModal
        isOpen={isAddNodeOpen}
        onClose={() => setIsAddNodeOpen(false)}
        title="Thêm Điểm Tri Thức Mới"
        maxWidth="520px"
      >
        <form onSubmit={handleCreateNodeSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Tên chủ đề / Khái niệm *
            </label>
            <input
              type="text"
              className="th-input"
              value={newNodeName}
              onChange={(e) => setNewNodeName(e.target.value)}
              placeholder="Ví dụ: Phương trình bậc hai một ẩn"
              required
              style={{ width: "100%" }}
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                Mã điểm tri thức *
              </label>
              <input
                type="text"
                className="th-input"
                value={newNodeCode}
                onChange={(e) => setNewNodeCode(e.target.value)}
                placeholder="Ví dụ: MATH10_PTBH"
                required
                style={{ width: "100%" }}
              />
            </div>
            <div>
              <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                Cấp độ phân loại
              </label>
              <select
                className="th-select"
                value={newNodeType}
                onChange={(e) => setNewNodeType(e.target.value as KnowledgeNodeType)}
                style={{ width: "100%" }}
              >
                <option value="Chapter">Chương (Chapter)</option>
                <option value="Topic">Chủ đề (Topic)</option>
                <option value="Skill">Kỹ năng (Skill)</option>
                <option value="Concept">Khái niệm (Concept)</option>
              </select>
            </div>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                Trọng số thi (%)
              </label>
              <input
                type="number"
                min="0"
                max="100"
                className="th-input"
                value={newExamImportance}
                onChange={(e) => setNewExamImportance(Number(e.target.value))}
                style={{ width: "100%" }}
              />
            </div>
            <div>
              <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
                Thời lượng (phút)
              </label>
              <input
                type="number"
                min="5"
                max="300"
                className="th-input"
                value={newLearningMinutes}
                onChange={(e) => setNewLearningMinutes(Number(e.target.value))}
                style={{ width: "100%" }}
              />
            </div>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Nút cha phân cấp (Tùy chọn)
            </label>
            <select
              className="th-select"
              value={newParentNodeId}
              onChange={(e) => setNewParentNodeId(e.target.value)}
              style={{ width: "100%" }}
            >
              <option value="">-- Không có (Nút gốc) --</option>
              {nodes.map((n) => (
                <option key={n.nodeId} value={n.nodeId}>
                  {n.nodeCode} - {n.nodeName}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Mô tả chi tiết
            </label>
            <textarea
              className="th-textarea"
              rows={3}
              value={newNodeDesc}
              onChange={(e) => setNewNodeDesc(e.target.value)}
              placeholder="Yêu cầu cần đạt, dạng bài tập điển hình..."
            />
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "10px" }}>
            <button
              type="button"
              className="th-secondary-button"
              onClick={() => setIsAddNodeOpen(false)}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="th-primary-button"
              disabled={createNodeMutation.isPending}
            >
              {createNodeMutation.isPending ? "Đang tạo..." : "Lưu Điểm Tri Thức"}
            </button>
          </div>
        </form>
      </TeacherModal>

      {/* Modal: Add Edge */}
      <TeacherModal
        isOpen={isAddEdgeOpen}
        onClose={() => setIsAddEdgeOpen(false)}
        title="Thiết Lập Liên Kết Tiên Quyết (Prerequisite Edge)"
        maxWidth="500px"
      >
        <form onSubmit={handleCreateEdgeSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Chủ đề Tiên quyết (Học trước) *
            </label>
            <select
              className="th-select"
              value={edgeSourceId}
              onChange={(e) => setEdgeSourceId(e.target.value)}
              required
              style={{ width: "100%" }}
            >
              {nodes.map((n) => (
                <option key={n.nodeId} value={n.nodeId}>
                  {n.nodeCode} - {n.nodeName}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Chủ đề Phụ thuộc (Học sau) *
            </label>
            <select
              className="th-select"
              value={edgeTargetId}
              onChange={(e) => setEdgeTargetId(e.target.value)}
              required
              style={{ width: "100%" }}
            >
              {nodes.map((n) => (
                <option key={n.nodeId} value={n.nodeId}>
                  {n.nodeCode} - {n.nodeName}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Loại quan hệ
            </label>
            <select
              className="th-select"
              value={edgeRelation}
              onChange={(e) => setEdgeRelation(e.target.value as KnowledgeRelationType)}
              style={{ width: "100%" }}
            >
              <option value="PrerequisiteOf">Tiên quyết bắt buộc (PrerequisiteOf)</option>
              <option value="PartOf">Là thành phần của (PartOf)</option>
              <option value="RelatedTo">Có liên quan / Bổ trợ (RelatedTo)</option>
              <option value="CausesErrorIn">Gây lỗi trong (CausesErrorIn)</option>
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: "6px" }}>
              Trọng số liên kết (0.10 - 1.00)
            </label>
            <input
              type="number"
              min="0.1"
              max="1.0"
              step="0.05"
              className="th-input"
              value={edgeWeight}
              onChange={(e) => setEdgeWeight(Number(e.target.value))}
              style={{ width: "100%" }}
            />
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "10px" }}>
            <button
              type="button"
              className="th-secondary-button"
              onClick={() => setIsAddEdgeOpen(false)}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="th-primary-button"
              disabled={createEdgeMutation.isPending || edgeSourceId === edgeTargetId}
            >
              {createEdgeMutation.isPending ? "Đang tạo..." : "Xác Nhận Liên Kết"}
            </button>
          </div>
        </form>
      </TeacherModal>

      {/* Delete Node Confirm Dialog */}
      <TeacherConfirmDialog
        isOpen={Boolean(deletingNode)}
        onClose={() => setDeletingNode(null)}
        onConfirm={() => {
          if (deletingNode) {
            deleteNodeMutation.mutate(deletingNode.nodeId);
          }
        }}
        title="Xác nhận xóa điểm tri thức"
        description={`Bạn có chắc chắn muốn xóa điểm tri thức "${deletingNode?.nodeName}" (${deletingNode?.nodeCode})? Tất cả liên kết liên quan sẽ bị gỡ bỏ.`}
        confirmLabel="Xóa vĩnh viễn"
        tone="danger"
        isConfirming={deleteNodeMutation.isPending}
      />

      {/* Delete Edge Confirm Dialog */}
      <TeacherConfirmDialog
        isOpen={Boolean(deletingEdge)}
        onClose={() => setDeletingEdge(null)}
        onConfirm={() => {
          if (deletingEdge) {
            deleteEdgeMutation.mutate(deletingEdge.edgeId);
          }
        }}
        title="Xác nhận gỡ bỏ liên kết"
        description="Bạn có chắc chắn muốn xóa liên kết phụ thuộc này khỏi sơ đồ tri thức?"
        confirmLabel="Xóa liên kết"
        tone="danger"
        isConfirming={deleteEdgeMutation.isPending}
      />
    </div>
  );
};
