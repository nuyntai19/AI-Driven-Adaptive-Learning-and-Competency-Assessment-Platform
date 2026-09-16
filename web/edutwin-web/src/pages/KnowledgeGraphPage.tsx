import React, { useState, useMemo, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { organizationApi } from "../api/organizationApi";
import { knowledgeGraphApi } from "../api/knowledgeGraphApi";
import { KnowledgeNodeCreatePanel } from "../components/KnowledgeNodeCreatePanel";
import { KnowledgeEdgeCreatePanel } from "../components/KnowledgeEdgeCreatePanel";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import type {
  KnowledgeGraphNodeDto,
  KnowledgeGraphEdgeDto,
  KnowledgeNodeType,
  KnowledgeRelationType,
  CreateKnowledgeNodeRequest,
  CreateKnowledgeEdgeRequest,
  UpdateKnowledgeNodeRequest,
  UpdateKnowledgeEdgeRequest,
} from "../types/knowledgeGraph";
import type { ProblemDetails } from "../types/auth";
import { isConcurrencyConflict, mapSafeOperationalError } from "../utils/problemDetails";
import {
  CenterManagerThemeScope,
  PageHeader,
  SafeErrorPanel,
  Skeleton,
  Modal,
  Drawer,
} from "../components/centerManager";

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

const nodeTypeLayerOrder: Record<KnowledgeNodeType, number> = {
  Subject: 0,
  Chapter: 1,
  Topic: 2,
  Skill: 3,
  Concept: 4,
};

const nodeTypeBadgeStyles: Record<KnowledgeNodeType, string> = {
  Subject: "bg-blue-500/10 text-blue-300 border-blue-500/30",
  Chapter: "bg-purple-500/10 text-purple-300 border-purple-500/30",
  Topic: "bg-cyan-500/10 text-cyan-300 border-cyan-500/30",
  Skill: "bg-emerald-500/10 text-emerald-300 border-emerald-500/30",
  Concept: "bg-amber-500/10 text-amber-300 border-amber-500/30",
};

const relationTypeColors: Record<KnowledgeRelationType, string> = {
  PrerequisiteOf: "#06b6d4", // Cyan
  PartOf: "#8b5cf6",         // Purple
  RelatedTo: "#64748b",      // Slate
  CausesErrorIn: "#f43f5e",  // Rose
};

/**
 * Modern Dark Enterprise SaaS view for CenterManager on KnowledgeGraphPage
 */
const CenterManagerKnowledgeGraphView: React.FC = () => {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const [searchParams, setSearchParams] = useSearchParams();
  const urlSubjectId = searchParams.get("subjectId") || "";

  const [selectedSubjectId, setSelectedSubjectId] = useState<string>(urlSubjectId);

  // Permission flags
  const canReadSubjects = hasPermission(permissions.subjectsRead);
  const canCreateNodes = hasPermission(permissions.nodesCreate);
  const canUpdateNodes = hasPermission(permissions.nodesUpdate);
  const canDeleteNodes = hasPermission(permissions.nodesDelete);
  const canCreateEdges = hasPermission(permissions.edgesCreate);
  const canUpdateEdges = hasPermission(permissions.edgesUpdate);
  const canDeleteEdges = hasPermission(permissions.edgesDelete);

  // Selection state
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"canvas" | "table">("canvas");
  const [zoomLevel, setZoomLevel] = useState<number>(1);
  const [isMobileInspectorOpen, setIsMobileInspectorOpen] = useState(false);

  // Creation Modals
  const [isCreateNodeOpen, setIsCreateNodeOpen] = useState(false);
  const [createNodeParentId, setCreateNodeParentId] = useState<string>("");
  const [createNodeType, setCreateNodeType] = useState<KnowledgeNodeType>("Topic");
  const [createNodeCode, setCreateNodeCode] = useState<string>("");
  const [createNodeName, setCreateNodeName] = useState<string>("");
  const [createNodeDescription, setCreateNodeDescription] = useState<string>("");
  const [createNodeOrderIndex, setCreateNodeOrderIndex] = useState<string>("0");
  const [createNodeExamImportance, setCreateNodeExamImportance] = useState<string>("0");
  const [createNodeEstimatedMinutes, setCreateNodeEstimatedMinutes] = useState<string>("30");
  const [createNodeIsActive, setCreateNodeIsActive] = useState<boolean>(true);
  const [createNodeError, setCreateNodeError] = useState<string | null>(null);

  const [isCreateEdgeOpen, setIsCreateEdgeOpen] = useState(false);
  const [createEdgeSourceId, setCreateEdgeSourceId] = useState<string>("");
  const [createEdgeTargetId, setCreateEdgeTargetId] = useState<string>("");
  const [createEdgeRelationType, setCreateEdgeRelationType] = useState<KnowledgeRelationType>("PrerequisiteOf");
  const [createEdgeWeight, setCreateEdgeWeight] = useState<string>("1.0");
  const [createEdgeError, setCreateEdgeError] = useState<string | null>(null);

  // Node Edit State (in Inspector)
  const [editNodeName, setEditNodeName] = useState("");
  const [editNodeParentId, setEditNodeParentId] = useState<string>("");
  const [editNodeDescription, setEditNodeDescription] = useState("");
  const [editNodeOrderIndex, setEditNodeOrderIndex] = useState("0");
  const [editNodeExamImportance, setEditNodeExamImportance] = useState("0");
  const [editNodeEstimatedMinutes, setEditNodeEstimatedMinutes] = useState("30");
  const [editNodeIsActive, setEditNodeIsActive] = useState(true);
  const [editNodeError, setEditNodeError] = useState<string | null>(null);

  // Node Delete Modal
  const [deletingNode, setDeletingNode] = useState<KnowledgeGraphNodeDto | null>(null);
  const [deleteNodeError, setDeleteNodeError] = useState<string | null>(null);

  // Edge Edit State (in Inspector)
  const [editEdgeWeight, setEditEdgeWeight] = useState("1.0");
  const [editEdgeError, setEditEdgeError] = useState<string | null>(null);

  // Edge Delete Modal
  const [deletingEdge, setDeletingEdge] = useState<KnowledgeGraphEdgeDto | null>(null);
  const [deleteEdgeError, setDeleteEdgeError] = useState<string | null>(null);

  const [globalSuccessMessage, setGlobalSuccessMessage] = useState<string | null>(null);

  // Query: Active subjects
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isError: isErrorSubjects,
    refetch: refetchSubjects,
  } = useQuery({
    queryKey: ["subjects", "knowledge-graph", user?.centerId, "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: Boolean(user?.centerId && canReadSubjects),
  });

  const activeSubjects = useMemo(() => subjectsData?.data ?? [], [subjectsData?.data]);

  // Sync selectedSubjectId with URL param when activeSubjects load
  useEffect(() => {
    if (urlSubjectId && activeSubjects.some((s) => s.subjectId === urlSubjectId)) {
      setSelectedSubjectId(urlSubjectId);
    } else if (!selectedSubjectId && activeSubjects.length > 0) {
      const defaultId = activeSubjects[0].subjectId;
      setSelectedSubjectId(defaultId);
      setSearchParams({ subjectId: defaultId }, { replace: true });
    }
  }, [urlSubjectId, activeSubjects, selectedSubjectId, setSearchParams]);

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

  // Query: Graph for selected subject
  const {
    data: graphData,
    isLoading: isLoadingGraph,
    isFetching: isFetchingGraph,
    isError: isErrorGraph,
    error: graphError,
    refetch: refetchGraph,
  } = useQuery({
    queryKey: ["knowledge-graph", user?.centerId, selectedSubjectId],
    queryFn: () => knowledgeGraphApi.getGraph(selectedSubjectId),
    enabled: Boolean(user?.centerId && selectedSubjectId.trim()),
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
      setEditNodeError(null);
    }
  }, [selectedNode]);

  // Populate edge edit form when selectedEdge changes
  useEffect(() => {
    if (selectedEdge) {
      setEditEdgeWeight(String(selectedEdge.weight ?? 1.0));
      setEditEdgeError(null);
    }
  }, [selectedEdge]);

  const invalidateGraph = async () => {
    if (user?.centerId && selectedSubjectId) {
      await queryClient.invalidateQueries({
        queryKey: ["knowledge-graph", user.centerId, selectedSubjectId],
      });
    }
  };

  // Node Mutations
  const updateNodeMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateKnowledgeNodeRequest }) =>
      knowledgeGraphApi.updateNode(id, req),
    onSuccess: async () => {
      await invalidateGraph();
      setEditNodeError(null);
      setGlobalSuccessMessage("Cập nhật nút kiến thức thành công!");
    },
    onError: async (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (isConcurrencyConflict(err)) {
          const refreshed = await refetchGraph();
          const latestNode = refreshed.data?.nodes.find((node) => node.nodeId === selectedNodeId);
          if (latestNode) {
            setEditNodeName(latestNode.nodeName);
            setEditNodeParentId(latestNode.parentNodeId || "");
            setEditNodeDescription(latestNode.description || "");
            setEditNodeOrderIndex(String(latestNode.orderIndex ?? 0));
            setEditNodeExamImportance(String(latestNode.examImportance ?? 0));
            setEditNodeEstimatedMinutes(String(latestNode.estimatedLearningMinutes ?? 30));
            setEditNodeIsActive(latestNode.isActive ?? true);
          }
          setEditNodeError(
            "Dữ liệu nút vừa thay đổi bởi người dùng khác. Hệ thống đã nạp RowVersion mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        if (err.response?.status === 409) {
          setEditNodeError(
            mapSafeOperationalError(
              err,
              "Không thể cập nhật nút do xung đột ràng buộc hoặc chu trình phụ thuộc."
            )
          );
          return;
        }
        setEditNodeError(mapSafeOperationalError(err, "Không thể cập nhật nút kiến thức. Vui lòng thử lại."));
        return;
      }
      setEditNodeError("Đã xảy ra lỗi khi cập nhật nút kiến thức.");
    },
  });

  const deleteNodeMutation = useMutation({
    mutationFn: (id: string) => knowledgeGraphApi.deleteNode(id),
    onSuccess: async () => {
      await invalidateGraph();
      setDeletingNode(null);
      setDeleteNodeError(null);
      setSelectedNodeId(null);
      setIsMobileInspectorOpen(false);
      setGlobalSuccessMessage("Đã xóa nút kiến thức thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (err.response?.status === 409) {
          setDeleteNodeError(
            mapSafeOperationalError(
              err,
              "Không thể xóa nút kiến thức vì đang có dữ liệu hoặc quan hệ liên kết trong hệ thống."
            )
          );
          return;
        }
        setDeleteNodeError(mapSafeOperationalError(err, "Không thể xóa nút kiến thức. Vui lòng thử lại."));
        return;
      }
      setDeleteNodeError("Đã xảy ra lỗi khi xóa nút kiến thức.");
    },
  });

  const createNodeMutation = useMutation({
    mutationFn: (req: CreateKnowledgeNodeRequest) => knowledgeGraphApi.createNode(req),
    onSuccess: async () => {
      await invalidateGraph();
      setIsCreateNodeOpen(false);
      setCreateNodeCode("");
      setCreateNodeName("");
      setCreateNodeDescription("");
      setCreateNodeOrderIndex("0");
      setCreateNodeExamImportance("0");
      setCreateNodeEstimatedMinutes("30");
      setCreateNodeParentId("");
      setCreateNodeError(null);
      setGlobalSuccessMessage("Tạo nút kiến thức mới thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        setCreateNodeError(mapSafeOperationalError(err, "Không thể tạo nút kiến thức. Vui lòng thử lại."));
        return;
      }
      setCreateNodeError("Đã xảy ra lỗi khi tạo nút kiến thức.");
    },
  });

  // Edge Mutations
  const updateEdgeMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateKnowledgeEdgeRequest }) =>
      knowledgeGraphApi.updateEdge(id, req),
    onSuccess: async () => {
      await invalidateGraph();
      setEditEdgeError(null);
      setGlobalSuccessMessage("Cập nhật liên kết thành công!");
    },
    onError: async (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (isConcurrencyConflict(err)) {
          const refreshed = await refetchGraph();
          const latestEdge = refreshed.data?.edges.find((edge) => edge.edgeId === selectedEdgeId);
          if (latestEdge) {
            setEditEdgeWeight(String(latestEdge.weight ?? 1.0));
          }
          setEditEdgeError(
            "Dữ liệu liên kết vừa thay đổi bởi người dùng khác. Hệ thống đã nạp RowVersion mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        setEditEdgeError(mapSafeOperationalError(err, "Không thể cập nhật liên kết. Vui lòng thử lại."));
        return;
      }
      setEditEdgeError("Đã xảy ra lỗi khi cập nhật liên kết.");
    },
  });

  const deleteEdgeMutation = useMutation({
    mutationFn: (id: string) => knowledgeGraphApi.deleteEdge(id),
    onSuccess: async () => {
      await invalidateGraph();
      setDeletingEdge(null);
      setDeleteEdgeError(null);
      setSelectedEdgeId(null);
      setIsMobileInspectorOpen(false);
      setGlobalSuccessMessage("Đã xóa liên kết thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        setDeleteEdgeError(mapSafeOperationalError(err, "Không thể xóa liên kết. Vui lòng thử lại."));
        return;
      }
      setDeleteEdgeError("Đã xảy ra lỗi khi xóa liên kết.");
    },
  });

  const createEdgeMutation = useMutation({
    mutationFn: (req: CreateKnowledgeEdgeRequest) => knowledgeGraphApi.createEdge(req),
    onSuccess: async () => {
      await invalidateGraph();
      setIsCreateEdgeOpen(false);
      setCreateEdgeSourceId("");
      setCreateEdgeTargetId("");
      setCreateEdgeWeight("1.0");
      setCreateEdgeError(null);
      setGlobalSuccessMessage("Tạo liên kết kiến thức mới thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (err.response?.status === 409) {
          setCreateEdgeError(
            mapSafeOperationalError(
              err,
              "Không thể tạo liên kết vì sẽ tạo chu trình phụ thuộc (DAG cycle) hoặc trùng lặp liên kết."
            )
          );
          return;
        }
        setCreateEdgeError(mapSafeOperationalError(err, "Không thể tạo liên kết kiến thức. Vui lòng thử lại."));
        return;
      }
      setCreateEdgeError("Đã xảy ra lỗi khi tạo liên kết kiến thức.");
    },
  });

  // Handlers for Save Node / Edge from Inspector
  const handleEditNodeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedNode) return;
    setEditNodeError(null);

    const trimmedName = editNodeName.trim();
    if (!trimmedName) {
      setEditNodeError("Tên nút không được để trống.");
      return;
    }

    const orderIdx = parseInt(editNodeOrderIndex, 10);
    if (isNaN(orderIdx) || orderIdx < 0) {
      setEditNodeError("Thứ tự phải là số nguyên không âm.");
      return;
    }

    const importance = parseFloat(editNodeExamImportance);
    if (isNaN(importance) || importance < 0 || importance > 100) {
      setEditNodeError("Mức quan trọng thi phải nằm trong khoảng từ 0 đến 100.");
      return;
    }

    const minutes = parseInt(editNodeEstimatedMinutes, 10);
    if (isNaN(minutes) || minutes < 1) {
      setEditNodeError("Thời gian học ước tính phải là số nguyên dương (tối thiểu 1 phút).");
      return;
    }

    updateNodeMutation.mutate({
      id: selectedNode.nodeId,
      req: {
        nodeName: trimmedName,
        parentNodeId: editNodeParentId.trim() || null,
        description: editNodeDescription.trim() || null,
        orderIndex: orderIdx,
        examImportance: importance,
        estimatedLearningMinutes: minutes,
        isActive: editNodeIsActive,
        rowVersion: selectedNode.rowVersion,
      },
    });
  };

  const handleEditEdgeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedEdge) return;
    setEditEdgeError(null);

    const weight = parseFloat(editEdgeWeight);
    if (isNaN(weight) || weight < 0 || weight > 1.0) {
      setEditEdgeError("Trọng số phải nằm trong khoảng từ 0.0 đến 1.0.");
      return;
    }

    updateEdgeMutation.mutate({
      id: selectedEdge.edgeId,
      req: {
        weight,
        rowVersion: selectedEdge.rowVersion,
      },
    });
  };

  const handleCreateNodeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCreateNodeError(null);

    const code = createNodeCode.trim();
    if (!code) {
      setCreateNodeError("Mã nút không được để trống.");
      return;
    }

    const name = createNodeName.trim();
    if (!name) {
      setCreateNodeError("Tên nút không được để trống.");
      return;
    }

    const orderIdx = parseInt(createNodeOrderIndex, 10);
    if (isNaN(orderIdx) || orderIdx < 0) {
      setCreateNodeError("Thứ tự phải là số nguyên không âm.");
      return;
    }

    const importance = parseFloat(createNodeExamImportance);
    if (isNaN(importance) || importance < 0 || importance > 100) {
      setCreateNodeError("Mức quan trọng thi phải nằm trong khoảng từ 0 đến 100.");
      return;
    }

    const minutes = parseInt(createNodeEstimatedMinutes, 10);
    if (isNaN(minutes) || minutes < 1) {
      setCreateNodeError("Thời gian học ước tính phải là số nguyên dương (tối thiểu 1 phút).");
      return;
    }

    createNodeMutation.mutate({
      subjectId: selectedSubjectId,
      parentNodeId: createNodeParentId.trim() || null,
      nodeType: createNodeType,
      nodeCode: code,
      nodeName: name,
      description: createNodeDescription.trim() || null,
      orderIndex: orderIdx,
      examImportance: importance,
      estimatedLearningMinutes: minutes,
      isActive: createNodeIsActive,
    });
  };

  const handleCreateEdgeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCreateEdgeError(null);

    if (!createEdgeSourceId.trim()) {
      setCreateEdgeError("Vui lòng chọn nút nguồn.");
      return;
    }

    if (!createEdgeTargetId.trim()) {
      setCreateEdgeError("Vui lòng chọn nút đích.");
      return;
    }

    if (createEdgeSourceId === createEdgeTargetId) {
      setCreateEdgeError("Nút nguồn và nút đích không được trùng nhau.");
      return;
    }

    const weight = parseFloat(createEdgeWeight);
    if (isNaN(weight) || weight < 0 || weight > 1.0) {
      setCreateEdgeError("Trọng số phải nằm trong khoảng từ 0.0 đến 1.0.");
      return;
    }

    createEdgeMutation.mutate({
      subjectId: selectedSubjectId,
      sourceNodeId: createEdgeSourceId,
      targetNodeId: createEdgeTargetId,
      relationType: createEdgeRelationType,
      weight,
    });
  };

  // Purely deterministic client-side topological SVG layout calculation
  // NEVER creates, stores, or mutates any fake coordinate fields
  const layout = useMemo(() => {
    if (nodes.length === 0) return { positions: new Map<string, { x: number; y: number }>(), width: 800, height: 600 };

    // Group nodes by layer: Subject(0), Chapter(1), Topic(2), Skill(3), Concept(4)
    const layers: KnowledgeGraphNodeDto[][] = [[], [], [], [], []];
    for (const node of nodes) {
      const layerIdx = nodeTypeLayerOrder[node.nodeType] ?? 2;
      layers[layerIdx].push(node);
    }

    // Sort nodes in each layer by orderIndex
    for (const layer of layers) {
      layer.sort((a, b) => a.orderIndex - b.orderIndex);
    }

    const columnWidth = 240;
    const rowHeight = 110;
    const startX = 60;
    const startY = 60;

    let maxNodesInLayer = 1;
    for (const layer of layers) {
      if (layer.length > maxNodesInLayer) maxNodesInLayer = layer.length;
    }

    const positions = new Map<string, { x: number; y: number }>();

    // Position each node deterministically
    layers.forEach((layer, layerIndex) => {
      const x = startX + layerIndex * columnWidth;
      const totalLayerHeight = layer.length * rowHeight;
      const baseOffsetY = startY + Math.max(0, ((maxNodesInLayer * rowHeight) - totalLayerHeight) / 4);

      layer.forEach((node, nodeIndex) => {
        const y = baseOffsetY + nodeIndex * rowHeight;
        positions.set(node.nodeId, { x, y });
      });
    });

    const totalWidth = Math.max(1000, startX + 5 * columnWidth + 80);
    const totalHeight = Math.max(650, startY + maxNodesInLayer * rowHeight + 120);

    return { positions, width: totalWidth, height: totalHeight };
  }, [nodes]);

  // Render Inspector Content
  const renderInspectorContent = () => {
    if (selectedNode) {
      return (
        <div className="space-y-6">
          <div className="border-b border-[var(--cm-border-subtle)] pb-4">
            <div className="flex items-center gap-2 mb-2">
              <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold border ${nodeTypeBadgeStyles[selectedNode.nodeType]}`}>
                {nodeTypeLabels[selectedNode.nodeType] ?? selectedNode.nodeType}
              </span>
              <span className="text-xs font-mono text-[var(--cm-cyan)]">{selectedNode.nodeCode}</span>
            </div>
            <h3 className="text-base font-bold text-[var(--cm-text)]">{selectedNode.nodeName}</h3>
            {selectedNode.description && (
              <p className="mt-1 text-xs text-[var(--cm-text-secondary)]">{selectedNode.description}</p>
            )}
          </div>

          {/* Node Details Meta */}
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-2.5 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block">Thứ tự hiển thị</span>
              <span className="font-semibold text-[var(--cm-text)]">{selectedNode.orderIndex}</span>
            </div>
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-2.5 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block">Trọng số thi</span>
              <span className="font-semibold text-[var(--cm-text)]">{selectedNode.examImportance}%</span>
            </div>
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-2.5 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block">Thời lượng học</span>
              <span className="font-semibold text-[var(--cm-text)]">{selectedNode.estimatedLearningMinutes} phút</span>
            </div>
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-2.5 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block">Trạng thái</span>
              <span className={`font-semibold ${selectedNode.isActive ? "text-emerald-400" : "text-slate-400"}`}>
                {selectedNode.isActive ? "Đang hoạt động" : "Đã ẩn"}
              </span>
            </div>
          </div>

          {/* Node Edit Form */}
          {canUpdateNodes ? (
            <form onSubmit={handleEditNodeSubmit} className="space-y-4 pt-2 border-t border-[var(--cm-border-subtle)]">
              <h4 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
                Chỉnh sửa nút kiến thức
              </h4>

              {editNodeError && (
                <div className="rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
                  {editNodeError}
                </div>
              )}

              <div>
                <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">
                  Tên nút <span className="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={editNodeName}
                  onChange={(e) => setEditNodeName(e.target.value)}
                  className="cm-input w-full text-xs"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">
                  Nút cha (phân cấp)
                </label>
                <select
                  value={editNodeParentId}
                  onChange={(e) => setEditNodeParentId(e.target.value)}
                  className="cm-select w-full text-xs"
                >
                  <option value="">-- Không có (Nút gốc) --</option>
                  {nodes
                    .filter((n) => n.nodeId !== selectedNode.nodeId)
                    .map((n) => (
                      <option key={n.nodeId} value={n.nodeId}>
                        {n.nodeCode} - {n.nodeName}
                      </option>
                    ))}
                </select>
              </div>

              <div className="grid grid-cols-3 gap-2">
                <div>
                  <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">Thứ tự</label>
                  <input
                    type="number"
                    min={0}
                    value={editNodeOrderIndex}
                    onChange={(e) => setEditNodeOrderIndex(e.target.value)}
                    className="cm-input w-full text-xs"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">Tỷ trọng thi</label>
                  <input
                    type="number"
                    min={0}
                    max={100}
                    step={0.1}
                    value={editNodeExamImportance}
                    onChange={(e) => setEditNodeExamImportance(e.target.value)}
                    className="cm-input w-full text-xs"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">Phút học</label>
                  <input
                    type="number"
                    min={1}
                    value={editNodeEstimatedMinutes}
                    onChange={(e) => setEditNodeEstimatedMinutes(e.target.value)}
                    className="cm-input w-full text-xs"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">Mô tả</label>
                <textarea
                  rows={2}
                  maxLength={1000}
                  value={editNodeDescription}
                  onChange={(e) => setEditNodeDescription(e.target.value)}
                  className="cm-input w-full text-xs"
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  id="cm-edit-node-active"
                  type="checkbox"
                  checked={editNodeIsActive}
                  onChange={(e) => setEditNodeIsActive(e.target.checked)}
                  className="rounded border-[var(--cm-border)] bg-[var(--cm-surface)] text-[var(--cm-cyan)]"
                />
                <label htmlFor="cm-edit-node-active" className="text-xs text-[var(--cm-text-secondary)] select-none">
                  Nút kiến thức đang hoạt động
                </label>
              </div>

              <div className="flex items-center justify-between gap-2 pt-2">
                {canDeleteNodes && (
                  <button
                    type="button"
                    onClick={() => {
                      setDeletingNode(selectedNode);
                      setDeleteNodeError(null);
                    }}
                    className="cm-danger-button text-xs py-1.5 px-3"
                  >
                    Xóa nút
                  </button>
                )}
                <button
                  type="submit"
                  disabled={updateNodeMutation.isPending}
                  className="cm-primary-button text-xs py-1.5 px-4 ml-auto"
                >
                  {updateNodeMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          ) : (
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)] text-xs text-[var(--cm-text-muted)]">
              Chế độ chỉ đọc: Bạn không có quyền chỉnh sửa nút kiến thức.
            </div>
          )}
        </div>
      );
    }

    if (selectedEdge) {
      const sourceNode = nodeMap.get(selectedEdge.sourceNodeId);
      const targetNode = nodeMap.get(selectedEdge.targetNodeId);

      return (
        <div className="space-y-6">
          <div className="border-b border-[var(--cm-border-subtle)] pb-4">
            <span
              className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold mb-2"
              style={{
                backgroundColor: `${relationTypeColors[selectedEdge.relationType]}22`,
                color: relationTypeColors[selectedEdge.relationType],
                borderColor: `${relationTypeColors[selectedEdge.relationType]}44`,
              }}
            >
              {relationTypeLabels[selectedEdge.relationType] ?? selectedEdge.relationType}
            </span>
            <h3 className="text-base font-bold text-[var(--cm-text)]">Chi tiết liên kết</h3>
            <p className="mt-1 text-xs text-[var(--cm-text-secondary)]">
              Mối quan hệ phụ thuộc giữa hai nút kiến thức trong đồ thị.
            </p>
          </div>

          <div className="space-y-3 text-xs">
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block mb-1">Nút nguồn</span>
              <span className="font-semibold text-[var(--cm-text)]">
                {sourceNode ? `${sourceNode.nodeCode} - ${sourceNode.nodeName}` : selectedEdge.sourceNodeId}
              </span>
            </div>
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block mb-1">Nút đích</span>
              <span className="font-semibold text-[var(--cm-text)]">
                {targetNode ? `${targetNode.nodeCode} - ${targetNode.nodeName}` : selectedEdge.targetNodeId}
              </span>
            </div>
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)]">
              <span className="text-[var(--cm-text-muted)] block mb-1">Trọng số liên kết hiện tại</span>
              <span className="font-semibold text-[var(--cm-cyan)]">{selectedEdge.weight}</span>
            </div>
          </div>

          {/* Edge Edit Form */}
          {canUpdateEdges ? (
            <form onSubmit={handleEditEdgeSubmit} className="space-y-4 pt-2 border-t border-[var(--cm-border-subtle)]">
              <h4 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
                Chỉnh sửa trọng số liên kết
              </h4>

              {editEdgeError && (
                <div className="rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
                  {editEdgeError}
                </div>
              )}

              <div>
                <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">
                  Trọng số (0.00 – 1.00) <span className="text-rose-400">*</span>
                </label>
                <input
                  type="number"
                  min={0}
                  max={1.0}
                  step={0.01}
                  required
                  value={editEdgeWeight}
                  onChange={(e) => setEditEdgeWeight(e.target.value)}
                  className="cm-input w-full text-xs"
                />
              </div>

              <div className="flex items-center justify-between gap-2 pt-2">
                {canDeleteEdges && (
                  <button
                    type="button"
                    onClick={() => {
                      setDeletingEdge(selectedEdge);
                      setDeleteEdgeError(null);
                    }}
                    className="cm-danger-button text-xs py-1.5 px-3"
                  >
                    Xóa liên kết
                  </button>
                )}
                <button
                  type="submit"
                  disabled={updateEdgeMutation.isPending}
                  className="cm-primary-button text-xs py-1.5 px-4 ml-auto"
                >
                  {updateEdgeMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          ) : (
            <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)] text-xs text-[var(--cm-text-muted)]">
              Chế độ chỉ đọc: Bạn không có quyền chỉnh sửa liên kết kiến thức.
            </div>
          )}
        </div>
      );
    }

    // Default overview when nothing is selected
    const activeSubject = activeSubjects.find((s) => s.subjectId === selectedSubjectId);
    return (
      <div className="space-y-6">
        <div className="border-b border-[var(--cm-border-subtle)] pb-4">
          <span className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-cyan)]">TỔNG QUAN ĐỒ THỊ</span>
          <h3 className="text-base font-bold text-[var(--cm-text)] mt-1">
            {activeSubject ? activeSubject.subjectName : "Môn học đã chọn"}
          </h3>
          <p className="mt-1 text-xs text-[var(--cm-text-secondary)]">
            Chọn bất kỳ nút kiến thức hoặc đường liên kết nào trên sơ đồ để xem thông số và chỉnh sửa.
          </p>
        </div>

        <div className="grid grid-cols-2 gap-3 text-xs">
          <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)]">
            <span className="text-[var(--cm-text-muted)] block">Tổng số nút</span>
            <span className="text-xl font-bold text-[var(--cm-text)] mt-1 block">{nodes.length}</span>
          </div>
          <div className="rounded-lg bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)]">
            <span className="text-[var(--cm-text-muted)] block">Tổng số liên kết</span>
            <span className="text-xl font-bold text-[var(--cm-cyan)] mt-1 block">{edges.length}</span>
          </div>
        </div>

        <div className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
            Phân bố loại nút
          </h4>
          <div className="space-y-1.5">
            {(["Subject", "Chapter", "Topic", "Skill", "Concept"] as KnowledgeNodeType[]).map((type) => {
              const count = nodes.filter((n) => n.nodeType === type).length;
              return (
                <div key={type} className="flex items-center justify-between text-xs py-1 px-2.5 rounded bg-[var(--cm-surface-subtle)]">
                  <span className="text-[var(--cm-text-secondary)]">{nodeTypeLabels[type]}</span>
                  <span className="font-semibold text-[var(--cm-text)]">{count}</span>
                </div>
              );
            })}
          </div>
        </div>

        <div className="pt-4 border-t border-[var(--cm-border-subtle)] flex flex-col gap-2">
          {canCreateNodes && (
            <button
              type="button"
              onClick={() => setIsCreateNodeOpen(true)}
              className="cm-secondary-button text-xs justify-center"
            >
              + Tạo nút kiến thức mới
            </button>
          )}
          {canCreateEdges && nodes.length >= 2 && (
            <button
              type="button"
              onClick={() => setIsCreateEdgeOpen(true)}
              className="cm-secondary-button text-xs justify-center"
            >
              + Tạo liên kết mới
            </button>
          )}
        </div>
      </div>
    );
  };

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="Học liệu & Nội dung"
          title="Đồ thị kiến thức"
          description="Quản trị mạng lưới kiến thức, phân cấp chủ đề và cây quan hệ phụ thuộc theo môn học của trung tâm."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Đồ thị kiến thức" },
          ]}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              {canReadSubjects && (
                <Link to="/quan-ly/mon-hoc" className="cm-secondary-button text-sm">
                  Quản lý môn học
                </Link>
              )}
              {canCreateNodes && selectedSubjectId && (
                <button
                  type="button"
                  id="btn-open-create-node"
                  onClick={() => setIsCreateNodeOpen(true)}
                  className="cm-primary-button text-sm"
                >
                  + Thêm nút kiến thức
                </button>
              )}
              {canCreateEdges && selectedSubjectId && nodes.length >= 2 && (
                <button
                  type="button"
                  id="btn-open-create-edge"
                  onClick={() => setIsCreateEdgeOpen(true)}
                  className="cm-secondary-button text-sm"
                >
                  + Thêm liên kết
                </button>
              )}
            </div>
          }
        />

        {/* Global Success Notification */}
        {globalSuccessMessage && (
          <div className="flex items-center justify-between rounded-xl bg-emerald-500/10 p-4 border border-emerald-500/30 text-sm font-medium text-emerald-300">
            <span>{globalSuccessMessage}</span>
            <button
              type="button"
              onClick={() => setGlobalSuccessMessage(null)}
              className="text-xs font-semibold text-emerald-400 hover:text-emerald-200"
            >
              Đóng
            </button>
          </div>
        )}

        {/* Subject Selector & Toolbar Bar */}
        <div className="flex flex-col gap-4 rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-1 flex-col gap-3 sm:flex-row sm:items-center">
            <div className="w-full sm:max-w-xs">
              <label
                htmlFor="cm-subject-select"
                className="mb-1 block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]"
              >
                Môn học
              </label>
              {isLoadingSubjects ? (
                <div className="h-9 rounded-lg bg-[var(--cm-surface-subtle)] animate-pulse" />
              ) : isErrorSubjects ? (
                <div className="text-xs text-rose-400">
                  Lỗi tải môn học.{" "}
                  <button type="button" onClick={() => refetchSubjects()} className="underline font-bold">
                    Thử lại
                  </button>
                </div>
              ) : (
                <select
                  id="cm-subject-select"
                  value={selectedSubjectId}
                  onChange={(e) => handleSelectSubject(e.target.value)}
                  className="cm-select w-full text-sm"
                >
                  <option value="">-- Chọn một môn học --</option>
                  {activeSubjects.map((sub) => (
                    <option key={sub.subjectId} value={sub.subjectId}>
                      {sub.subjectCode} - {sub.subjectName}
                    </option>
                  ))}
                </select>
              )}
            </div>

            {selectedSubjectId && (
              <div className="flex items-center gap-3 pt-3 sm:pt-4 text-xs text-[var(--cm-text-secondary)]">
                <span>
                  Nút: <strong className="text-[var(--cm-text)]">{nodes.length}</strong>
                </span>
                <span>•</span>
                <span>
                  Liên kết: <strong className="text-[var(--cm-cyan)]">{edges.length}</strong>
                </span>
                {isFetchingGraph && (
                  <>
                    <span>•</span>
                    <span className="text-[var(--cm-cyan)] animate-pulse">Đang đồng bộ...</span>
                  </>
                )}
              </div>
            )}
          </div>

          {selectedSubjectId && (
            <div className="flex items-center gap-2">
              <div className="flex rounded-lg border border-[var(--cm-border)] bg-[var(--cm-surface-subtle)] p-0.5">
                <button
                  type="button"
                  onClick={() => setViewMode("canvas")}
                  className={`rounded-md px-2.5 py-1 text-xs font-medium transition ${
                    viewMode === "canvas"
                      ? "bg-[var(--cm-indigo)] text-white"
                      : "text-[var(--cm-text-secondary)] hover:text-[var(--cm-text)]"
                  }`}
                >
                  Bản đồ (Canvas)
                </button>
                <button
                  type="button"
                  onClick={() => setViewMode("table")}
                  className={`rounded-md px-2.5 py-1 text-xs font-medium transition ${
                    viewMode === "table"
                      ? "bg-[var(--cm-indigo)] text-white"
                      : "text-[var(--cm-text-secondary)] hover:text-[var(--cm-text)]"
                  }`}
                >
                  Bảng dữ liệu
                </button>
              </div>

              <button
                type="button"
                onClick={() => refetchGraph()}
                className="cm-secondary-button text-xs py-1.5 px-3"
              >
                Làm mới
              </button>

              {/* Mobile Inspector Toggle */}
              <button
                type="button"
                onClick={() => setIsMobileInspectorOpen(true)}
                className="lg:hidden cm-secondary-button text-xs py-1.5 px-3"
              >
                Chi tiết
              </button>
            </div>
          )}
        </div>

        {/* Main Work Area */}
        {!selectedSubjectId ? (
          <div className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-12 text-center">
            <svg
              className="mx-auto h-12 w-12 text-[var(--cm-text-muted)]"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={1.5}
                d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19 14.5M14.25 3.104c.251.023.501.05.75.082M19 14.5l-3.293-3.293a1 1 0 00-.707-.293H8.707a1 1 0 00-.707.293L5 14.5m14 0v3.75a2.25 2.25 0 01-2.25 2.25H7.25A2.25 2.25 0 015 18.25V14.5"
              />
            </svg>
            <h3 className="mt-3 text-base font-semibold text-[var(--cm-text)]">Chưa chọn môn học</h3>
            <p className="mt-1 text-sm text-[var(--cm-text-secondary)]">
              Vui lòng chọn môn học từ danh sách phía trên để nạp đồ thị kiến thức và thiết lập quan hệ.
            </p>
          </div>
        ) : isLoadingGraph ? (
          <div className="space-y-4">
            <Skeleton className="h-96 w-full rounded-2xl" />
          </div>
        ) : isErrorGraph ? (
          <SafeErrorPanel
            error={graphError}
            fallback="Đã xảy ra lỗi khi lấy dữ liệu đồ thị từ hệ thống."
            onRetry={() => refetchGraph()}
          />
        ) : (
          <div className="flex flex-col lg:flex-row gap-6 items-start">
            {/* Main Visualizer Area */}
            <div className="flex-1 w-full min-w-0 rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] overflow-hidden shadow-xl">
              {viewMode === "canvas" ? (
                <div className="relative flex flex-col">
                  {/* Canvas Controls Bar */}
                  <div className="flex items-center justify-between border-b border-[var(--cm-border-subtle)] px-4 py-2.5 bg-[var(--cm-surface-subtle)] text-xs">
                    <div className="flex items-center gap-4 text-[var(--cm-text-secondary)]">
                      <span className="flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-full bg-[#06b6d4]" /> Tiên quyết
                      </span>
                      <span className="flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-full bg-[#8b5cf6]" /> Thuộc về
                      </span>
                      <span className="flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-full bg-[#64748b]" /> Liên quan
                      </span>
                      <span className="flex items-center gap-1.5">
                        <span className="h-2.5 w-2.5 rounded-full bg-[#f43f5e]" /> Gây lỗi
                      </span>
                    </div>

                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        onClick={() => setZoomLevel((z) => Math.max(0.6, z - 0.1))}
                        className="cm-icon-button h-7 w-7 text-xs border border-[var(--cm-border)]"
                        title="Thu nhỏ"
                      >
                        -
                      </button>
                      <span className="px-2 text-xs font-mono text-[var(--cm-text-muted)]">
                        {Math.round(zoomLevel * 100)}%
                      </span>
                      <button
                        type="button"
                        onClick={() => setZoomLevel((z) => Math.min(1.5, z + 0.1))}
                        className="cm-icon-button h-7 w-7 text-xs border border-[var(--cm-border)]"
                        title="Phóng to"
                      >
                        +
                      </button>
                      <button
                        type="button"
                        onClick={() => setZoomLevel(1)}
                        className="cm-icon-button h-7 px-2 text-xs border border-[var(--cm-border)] ml-1"
                        title="Đặt lại zoom"
                      >
                        100%
                      </button>
                    </div>
                  </div>

                  {/* SVG Canvas */}
                  <div className="overflow-auto max-h-[720px] min-h-[560px] p-6 bg-[var(--cm-bg)] flex justify-center items-start">
                    {nodes.length === 0 ? (
                      <div className="my-auto text-center py-16">
                        <p className="text-sm text-[var(--cm-text-muted)]">Môn học này chưa có nút kiến thức nào.</p>
                        {canCreateNodes && (
                          <button
                            type="button"
                            onClick={() => setIsCreateNodeOpen(true)}
                            className="cm-primary-button text-xs mt-3"
                          >
                            + Tạo nút đầu tiên
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
                          className="select-none"
                        >
                          <defs>
                            {/* Arrow markers for directed edges */}
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

                          {/* Edges Paths */}
                          {edges.map((edge) => {
                            const sourcePos = layout.positions.get(edge.sourceNodeId);
                            const targetPos = layout.positions.get(edge.targetNodeId);
                            if (!sourcePos || !targetPos) return null;

                            // Card dimensions
                            const cardW = 190;
                            const cardH = 75;

                            const startX = sourcePos.x + cardW;
                            const startY = sourcePos.y + cardH / 2;
                            const endX = targetPos.x;
                            const endY = targetPos.y + cardH / 2;

                            const isSelected = selectedEdgeId === edge.edgeId;
                            const strokeColor = relationTypeColors[edge.relationType] || "#64748b";
                            const midX = (startX + endX) / 2;
                            const midY = (startY + endY) / 2;

                            // Smooth bezier curve
                            const pathData = `M ${startX} ${startY} C ${startX + 50} ${startY}, ${endX - 50} ${endY}, ${endX} ${endY}`;

                            return (
                              <g
                                key={edge.edgeId}
                                className="cursor-pointer group"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setSelectedEdgeId(edge.edgeId);
                                  setSelectedNodeId(null);
                                  setIsMobileInspectorOpen(true);
                                }}
                              >
                                {/* Invisible wide path for easy clicking */}
                                <path d={pathData} fill="none" stroke="transparent" strokeWidth={16} />

                                {/* Visible edge path */}
                                <path
                                  d={pathData}
                                  fill="none"
                                  stroke={strokeColor}
                                  strokeWidth={isSelected ? 3.5 : Math.max(1.5, (edge.weight ?? 1) * 2.5)}
                                  strokeDasharray={edge.relationType === "RelatedTo" ? "4,4" : undefined}
                                  markerEnd={`url(#arrow-${edge.relationType})`}
                                  className="transition-all duration-150"
                                  style={{
                                    filter: isSelected ? `drop-shadow(0 0 6px ${strokeColor})` : undefined,
                                    opacity: selectedNodeId || (selectedEdgeId && !isSelected) ? 0.4 : 0.85,
                                  }}
                                />

                                {/* Weight badge in the middle */}
                                <rect
                                  x={midX - 18}
                                  y={midY - 9}
                                  width={36}
                                  height={18}
                                  rx={4}
                                  fill="#0f172a"
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

                          {/* Nodes Cards */}
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
                                className="cursor-pointer"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setSelectedNodeId(node.nodeId);
                                  setSelectedEdgeId(null);
                                  setIsMobileInspectorOpen(true);
                                }}
                              >
                                {/* Card Background Box */}
                                <rect
                                  width={cardW}
                                  height={cardH}
                                  rx={10}
                                  fill="#1e293b"
                                  stroke={isSelected ? "#06b6d4" : "#334155"}
                                  strokeWidth={isSelected ? 2 : 1}
                                  className="transition-all duration-150"
                                  style={{
                                    filter: isSelected
                                      ? "drop-shadow(0 0 10px rgba(6, 182, 212, 0.45))"
                                      : "drop-shadow(0 4px 6px rgba(0, 0, 0, 0.2))",
                                  }}
                                />

                                {/* Top Accent Bar by NodeType */}
                                <rect
                                  width={cardW}
                                  height={3}
                                  rx={1.5}
                                  fill={
                                    node.nodeType === "Subject"
                                      ? "#3b82f6"
                                      : node.nodeType === "Chapter"
                                      ? "#a855f7"
                                      : node.nodeType === "Topic"
                                      ? "#06b6d4"
                                      : node.nodeType === "Skill"
                                      ? "#10b981"
                                      : "#f59e0b"
                                  }
                                />

                                {/* Node Type & Code */}
                                <text x={12} y={22} fontSize={10} fontWeight="bold" fill="#06b6d4" fontFamily="monospace">
                                  {node.nodeCode}
                                </text>
                                <text
                                  x={cardW - 12}
                                  y={22}
                                  textAnchor="end"
                                  fontSize={9}
                                  fill="#94a3b8"
                                  fontWeight="600"
                                >
                                  {nodeTypeLabels[node.nodeType] ?? node.nodeType}
                                </text>

                                {/* Node Name */}
                                <text
                                  x={12}
                                  y={44}
                                  fontSize={12}
                                  fontWeight="600"
                                  fill="#f8fafc"
                                  className="truncate"
                                >
                                  {node.nodeName.length > 20
                                    ? `${node.nodeName.substring(0, 20)}...`
                                    : node.nodeName}
                                </text>

                                {/* Node Footer Info */}
                                <text x={12} y={63} fontSize={9} fill="#64748b">
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
                <div className="p-6 space-y-8">
                  {/* Nodes Section */}
                  <div>
                    <div className="flex items-center justify-between pb-3 border-b border-[var(--cm-border-subtle)] mb-4">
                      <div>
                        <h3 className="text-sm font-bold text-[var(--cm-text)]">
                          Danh sách nút kiến thức ({nodes.length})
                        </h3>
                        <p className="text-xs text-[var(--cm-text-secondary)]">
                          Các khái niệm, chủ đề, kỹ năng trong môn học.
                        </p>
                      </div>
                    </div>

                    <div className="overflow-x-auto">
                      <table className="w-full text-left text-xs" id="cm-knowledge-nodes-table">
                        <thead className="bg-[var(--cm-surface-subtle)] text-[var(--cm-text-muted)] uppercase tracking-wider">
                          <tr>
                            <th className="py-2.5 px-3">Mã nút</th>
                            <th className="py-2.5 px-3">Tên nút</th>
                            <th className="py-2.5 px-3">Loại nút</th>
                            <th className="py-2.5 px-3">Thứ tự</th>
                            <th className="py-2.5 px-3">Trọng số thi</th>
                            <th className="py-2.5 px-3 text-right">Thao tác</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--cm-border-subtle)] text-[var(--cm-text-secondary)]">
                          {nodes.map((node) => (
                            <tr
                              key={node.nodeId}
                              className={`hover:bg-[var(--cm-surface-subtle)] cursor-pointer transition ${
                                selectedNodeId === node.nodeId ? "bg-[var(--cm-surface-subtle)]" : ""
                              }`}
                              onClick={() => {
                                setSelectedNodeId(node.nodeId);
                                setSelectedEdgeId(null);
                                setIsMobileInspectorOpen(true);
                              }}
                            >
                              <td className="py-3 px-3 font-mono font-bold text-[var(--cm-cyan)]">
                                {node.nodeCode}
                              </td>
                              <td className="py-3 px-3 font-semibold text-[var(--cm-text)]">
                                {node.nodeName}
                              </td>
                              <td className="py-3 px-3">
                                <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${nodeTypeBadgeStyles[node.nodeType]}`}>
                                  {nodeTypeLabels[node.nodeType] ?? node.nodeType}
                                </span>
                              </td>
                              <td className="py-3 px-3">{node.orderIndex}</td>
                              <td className="py-3 px-3">{node.examImportance}%</td>
                              <td className="py-3 px-3 text-right space-x-2">
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setSelectedNodeId(node.nodeId);
                                    setSelectedEdgeId(null);
                                    setIsMobileInspectorOpen(true);
                                  }}
                                  className="cm-secondary-button text-[11px] py-1 px-2.5"
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

                  {/* Edges Section */}
                  <div>
                    <div className="flex items-center justify-between pb-3 border-b border-[var(--cm-border-subtle)] mb-4">
                      <div>
                        <h3 className="text-sm font-bold text-[var(--cm-text)]">
                          Danh sách liên kết kiến thức ({edges.length})
                        </h3>
                        <p className="text-xs text-[var(--cm-text-secondary)]">
                          Quan hệ tiên quyết, chứa, mở rộng giữa các nút.
                        </p>
                      </div>
                    </div>

                    <div className="overflow-x-auto">
                      <table className="w-full text-left text-xs" id="cm-knowledge-edges-table">
                        <thead className="bg-[var(--cm-surface-subtle)] text-[var(--cm-text-muted)] uppercase tracking-wider">
                          <tr>
                            <th className="py-2.5 px-3">Nút nguồn</th>
                            <th className="py-2.5 px-3">Loại quan hệ</th>
                            <th className="py-2.5 px-3">Nút đích</th>
                            <th className="py-2.5 px-3">Trọng số</th>
                            <th className="py-2.5 px-3 text-right">Thao tác</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--cm-border-subtle)] text-[var(--cm-text-secondary)]">
                          {edges.map((edge) => {
                            const source = nodeMap.get(edge.sourceNodeId);
                            const target = nodeMap.get(edge.targetNodeId);
                            return (
                              <tr
                                key={edge.edgeId}
                                className={`hover:bg-[var(--cm-surface-subtle)] cursor-pointer transition ${
                                  selectedEdgeId === edge.edgeId ? "bg-[var(--cm-surface-subtle)]" : ""
                                }`}
                                onClick={() => {
                                  setSelectedEdgeId(edge.edgeId);
                                  setSelectedNodeId(null);
                                  setIsMobileInspectorOpen(true);
                                }}
                              >
                                <td className="py-3 px-3 font-medium text-[var(--cm-text)]">
                                  {source ? `${source.nodeCode} - ${source.nodeName}` : edge.sourceNodeId}
                                </td>
                                <td className="py-3 px-3">
                                  <span
                                    className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border"
                                    style={{
                                      backgroundColor: `${relationTypeColors[edge.relationType]}18`,
                                      color: relationTypeColors[edge.relationType],
                                      borderColor: `${relationTypeColors[edge.relationType]}40`,
                                    }}
                                  >
                                    {relationTypeLabels[edge.relationType] ?? edge.relationType}
                                  </span>
                                </td>
                                <td className="py-3 px-3 font-medium text-[var(--cm-text)]">
                                  {target ? `${target.nodeCode} - ${target.nodeName}` : edge.targetNodeId}
                                </td>
                                <td className="py-3 px-3 font-mono text-[var(--cm-cyan)]">{edge.weight}</td>
                                <td className="py-3 px-3 text-right">
                                  <button
                                    type="button"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setSelectedEdgeId(edge.edgeId);
                                      setSelectedNodeId(null);
                                      setIsMobileInspectorOpen(true);
                                    }}
                                    className="cm-secondary-button text-[11px] py-1 px-2.5"
                                  >
                                    Chi tiết
                                  </button>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </div>
              )}
            </div>

            {/* Desktop Inspector Sidebar */}
            <aside className="hidden lg:block w-96 shrink-0 rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 shadow-xl sticky top-6">
              {renderInspectorContent()}
            </aside>
          </div>
        )}

        {/* Mobile Inspector Drawer */}
        <Drawer
          isOpen={isMobileInspectorOpen}
          title="Chi tiết thực thể"
          description="Xem và quản trị thông tin thuộc tính"
          onClose={() => setIsMobileInspectorOpen(false)}
        >
          {renderInspectorContent()}
        </Drawer>

        {/* Modal: Create Knowledge Node */}
        <Modal
          isOpen={isCreateNodeOpen}
          title="Tạo nút kiến thức mới"
          description="Bổ sung khái niệm, chủ đề hoặc kỹ năng vào đồ thị môn học"
          onClose={() => setIsCreateNodeOpen(false)}
        >
          {createNodeError && (
            <div className="mb-4 rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
              {createNodeError}
            </div>
          )}

          <form onSubmit={handleCreateNodeSubmit} className="space-y-4 text-xs">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                  Loại nút <span className="text-rose-400">*</span>
                </label>
                <select
                  value={createNodeType}
                  onChange={(e) => setCreateNodeType(e.target.value as KnowledgeNodeType)}
                  className="cm-select w-full"
                >
                  {(["Subject", "Chapter", "Topic", "Skill", "Concept"] as KnowledgeNodeType[]).map((t) => (
                    <option key={t} value={t}>
                      {nodeTypeLabels[t]}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                  Mã nút <span className="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  required
                  maxLength={64}
                  placeholder="VD: MATH.LOG.01"
                  value={createNodeCode}
                  onChange={(e) => setCreateNodeCode(e.target.value)}
                  className="cm-input w-full font-mono"
                />
              </div>
            </div>

            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                Tên nút kiến thức <span className="text-rose-400">*</span>
              </label>
              <input
                type="text"
                required
                maxLength={200}
                placeholder="VD: Định nghĩa và tính chất cơ bản của Logarit"
                value={createNodeName}
                onChange={(e) => setCreateNodeName(e.target.value)}
                className="cm-input w-full"
              />
            </div>

            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">Nút cha (phân cấp)</label>
              <select
                value={createNodeParentId}
                onChange={(e) => setCreateNodeParentId(e.target.value)}
                className="cm-select w-full"
              >
                <option value="">-- Không có (Nút gốc) --</option>
                {nodes.map((n) => (
                  <option key={n.nodeId} value={n.nodeId}>
                    {n.nodeCode} - {n.nodeName}
                  </option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">Thứ tự</label>
                <input
                  type="number"
                  min={0}
                  value={createNodeOrderIndex}
                  onChange={(e) => setCreateNodeOrderIndex(e.target.value)}
                  className="cm-input w-full"
                />
              </div>
              <div>
                <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">Trọng số thi (%)</label>
                <input
                  type="number"
                  min={0}
                  max={100}
                  step={0.1}
                  value={createNodeExamImportance}
                  onChange={(e) => setCreateNodeExamImportance(e.target.value)}
                  className="cm-input w-full"
                />
              </div>
              <div>
                <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">Thời lượng (phút)</label>
                <input
                  type="number"
                  min={1}
                  value={createNodeEstimatedMinutes}
                  onChange={(e) => setCreateNodeEstimatedMinutes(e.target.value)}
                  className="cm-input w-full"
                />
              </div>
            </div>

            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">Mô tả</label>
              <textarea
                rows={2}
                maxLength={1000}
                placeholder="Mô tả nội dung trọng tâm của nút kiến thức..."
                value={createNodeDescription}
                onChange={(e) => setCreateNodeDescription(e.target.value)}
                className="cm-input w-full"
              />
            </div>

            <div className="flex items-center gap-2 pt-1">
              <input
                id="cm-create-node-active"
                type="checkbox"
                checked={createNodeIsActive}
                onChange={(e) => setCreateNodeIsActive(e.target.checked)}
                className="rounded border-[var(--cm-border)] bg-[var(--cm-surface)] text-[var(--cm-cyan)]"
              />
              <label htmlFor="cm-create-node-active" className="text-xs text-[var(--cm-text-secondary)] select-none">
                Kích hoạt nút kiến thức ngay sau khi tạo
              </label>
            </div>

            <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--cm-border-subtle)]">
              <button
                type="button"
                onClick={() => setIsCreateNodeOpen(false)}
                className="cm-secondary-button text-xs py-2 px-4"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={createNodeMutation.isPending}
                className="cm-primary-button text-xs py-2 px-5"
              >
                {createNodeMutation.isPending ? "Đang tạo..." : "Tạo nút kiến thức"}
              </button>
            </div>
          </form>
        </Modal>

        {/* Modal: Create Knowledge Edge */}
        <Modal
          isOpen={isCreateEdgeOpen}
          title="Tạo liên kết kiến thức mới"
          description="Thiết lập quan hệ tiên quyết hoặc bổ trợ giữa hai nút kiến thức"
          onClose={() => setIsCreateEdgeOpen(false)}
        >
          {createEdgeError && (
            <div className="mb-4 rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
              {createEdgeError}
            </div>
          )}

          <form onSubmit={handleCreateEdgeSubmit} className="space-y-4 text-xs">
            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                Nút nguồn <span className="text-rose-400">*</span>
              </label>
              <select
                value={createEdgeSourceId}
                onChange={(e) => setCreateEdgeSourceId(e.target.value)}
                className="cm-select w-full"
                required
              >
                <option value="">-- Chọn nút nguồn --</option>
                {nodes.map((n) => (
                  <option key={n.nodeId} value={n.nodeId}>
                    {n.nodeCode} - {n.nodeName}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                Loại quan hệ <span className="text-rose-400">*</span>
              </label>
              <select
                value={createEdgeRelationType}
                onChange={(e) => setCreateEdgeRelationType(e.target.value as KnowledgeRelationType)}
                className="cm-select w-full"
              >
                {(["PrerequisiteOf", "PartOf", "RelatedTo", "CausesErrorIn"] as KnowledgeRelationType[]).map((r) => (
                  <option key={r} value={r}>
                    {relationTypeLabels[r]} ({r})
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                Nút đích <span className="text-rose-400">*</span>
              </label>
              <select
                value={createEdgeTargetId}
                onChange={(e) => setCreateEdgeTargetId(e.target.value)}
                className="cm-select w-full"
                required
              >
                <option value="">-- Chọn nút đích --</option>
                {nodes
                  .filter((n) => n.nodeId !== createEdgeSourceId)
                  .map((n) => (
                    <option key={n.nodeId} value={n.nodeId}>
                      {n.nodeCode} - {n.nodeName}
                    </option>
                  ))}
              </select>
            </div>

            <div>
              <label className="block font-medium text-[var(--cm-text-secondary)] mb-1">
                Trọng số liên kết (0.00 – 1.00) <span className="text-rose-400">*</span>
              </label>
              <input
                type="number"
                min={0}
                max={1.0}
                step={0.01}
                required
                value={createEdgeWeight}
                onChange={(e) => setCreateEdgeWeight(e.target.value)}
                className="cm-input w-full"
              />
              <p className="mt-1 text-[11px] text-[var(--cm-text-muted)]">
                Độ mạnh của quan hệ phụ thuộc. Mặc định là 1.00.
              </p>
            </div>

            <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--cm-border-subtle)]">
              <button
                type="button"
                onClick={() => setIsCreateEdgeOpen(false)}
                className="cm-secondary-button text-xs py-2 px-4"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={createEdgeMutation.isPending}
                className="cm-primary-button text-xs py-2 px-5"
              >
                {createEdgeMutation.isPending ? "Đang tạo..." : "Tạo liên kết"}
              </button>
            </div>
          </form>
        </Modal>

        {/* Modal: Confirm Delete Node */}
        <Modal
          isOpen={Boolean(deletingNode)}
          title="Xác nhận xóa nút kiến thức"
          description="Thao tác xóa mềm nút khỏi đồ thị môn học"
          onClose={() => setDeletingNode(null)}
        >
          {deleteNodeError && (
            <div className="mb-4 rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
              {deleteNodeError}
            </div>
          )}

          <p className="text-sm text-[var(--cm-text-secondary)] mb-3">
            Bạn có chắc chắn muốn xóa nút kiến thức{" "}
            <strong className="text-[var(--cm-text)]">
              {deletingNode?.nodeCode} — {deletingNode?.nodeName}
            </strong>
            ?
          </p>

          <div className="rounded-lg bg-amber-500/10 p-3 border border-amber-500/30 text-xs text-amber-300 mb-5">
            <strong>Lưu ý:</strong> Thao tác này sẽ xóa mềm nút kiến thức. Nếu nút đang có dữ liệu liên kết hoặc quan hệ đồ thị, hệ thống sẽ từ chối để bảo vệ toàn vẹn lịch sử.
          </div>

          <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--cm-border-subtle)]">
            <button
              type="button"
              onClick={() => setDeletingNode(null)}
              disabled={deleteNodeMutation.isPending}
              className="cm-secondary-button text-xs py-2 px-4"
            >
              Hủy
            </button>
            <button
              type="button"
              id="btn-confirm-delete-node"
              onClick={() => deletingNode && deleteNodeMutation.mutate(deletingNode.nodeId)}
              disabled={deleteNodeMutation.isPending}
              className="cm-danger-button text-xs py-2 px-5"
            >
              {deleteNodeMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
            </button>
          </div>
        </Modal>

        {/* Modal: Confirm Delete Edge */}
        <Modal
          isOpen={Boolean(deletingEdge)}
          title="Xác nhận xóa liên kết kiến thức"
          description="Hủy bỏ mối quan hệ phụ thuộc giữa hai nút kiến thức"
          onClose={() => setDeletingEdge(null)}
        >
          {deleteEdgeError && (
            <div className="mb-4 rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
              {deleteEdgeError}
            </div>
          )}

          <p className="text-sm text-[var(--cm-text-secondary)] mb-4">
            Bạn có chắc chắn muốn xóa liên kết giữa{" "}
            <strong className="text-[var(--cm-text)]">
              {deletingEdge && (nodeMap.get(deletingEdge.sourceNodeId)?.nodeName ?? deletingEdge.sourceNodeId)}
            </strong>{" "}
            và{" "}
            <strong className="text-[var(--cm-text)]">
              {deletingEdge && (nodeMap.get(deletingEdge.targetNodeId)?.nodeName ?? deletingEdge.targetNodeId)}
            </strong>
            ?
          </p>

          <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--cm-border-subtle)]">
            <button
              type="button"
              onClick={() => setDeletingEdge(null)}
              disabled={deleteEdgeMutation.isPending}
              className="cm-secondary-button text-xs py-2 px-4"
            >
              Hủy
            </button>
            <button
              type="button"
              id="btn-confirm-delete-edge"
              onClick={() => deletingEdge && deleteEdgeMutation.mutate(deletingEdge.edgeId)}
              disabled={deleteEdgeMutation.isPending}
              className="cm-danger-button text-xs py-2 px-5"
            >
              {deleteEdgeMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
            </button>
          </div>
        </Modal>
      </div>
    </CenterManagerThemeScope>
  );
};

/**
 * Legacy view preserved 100% for Teacher and other non-CenterManager accounts
 */
const LegacyKnowledgeGraphPage: React.FC = () => {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const [searchParams, setSearchParams] = useSearchParams();
  const urlSubjectId = searchParams.get("subjectId") || "";

  const [selectedSubjectId, setSelectedSubjectId] = useState<string>(urlSubjectId);

  // Permission flags
  const canReadSubjects = hasPermission(permissions.subjectsRead);
  const canCreateNodes = hasPermission(permissions.nodesCreate);
  const canUpdateNodes = hasPermission(permissions.nodesUpdate);
  const canDeleteNodes = hasPermission(permissions.nodesDelete);
  const canCreateEdges = hasPermission(permissions.edgesCreate);
  const canUpdateEdges = hasPermission(permissions.edgesUpdate);
  const canDeleteEdges = hasPermission(permissions.edgesDelete);

  // Modals state
  const [editingNode, setEditingNode] = useState<KnowledgeGraphNodeDto | null>(null);
  const [editNodeName, setEditNodeName] = useState("");
  const [editNodeParentId, setEditNodeParentId] = useState<string>("");
  const [editNodeDescription, setEditNodeDescription] = useState("");
  const [editNodeOrderIndex, setEditNodeOrderIndex] = useState("0");
  const [editNodeExamImportance, setEditNodeExamImportance] = useState("0");
  const [editNodeEstimatedMinutes, setEditNodeEstimatedMinutes] = useState("30");
  const [editNodeIsActive, setEditNodeIsActive] = useState(true);
  const [editNodeError, setEditNodeError] = useState<string | null>(null);

  const [deletingNode, setDeletingNode] = useState<KnowledgeGraphNodeDto | null>(null);
  const [deleteNodeError, setDeleteNodeError] = useState<string | null>(null);

  const [editingEdge, setEditingEdge] = useState<KnowledgeGraphEdgeDto | null>(null);
  const [editEdgeWeight, setEditEdgeWeight] = useState("1.0");
  const [editEdgeError, setEditEdgeError] = useState<string | null>(null);

  const [deletingEdge, setDeletingEdge] = useState<KnowledgeGraphEdgeDto | null>(null);
  const [deleteEdgeError, setDeleteEdgeError] = useState<string | null>(null);

  const [globalSuccessMessage, setGlobalSuccessMessage] = useState<string | null>(null);

  // Query: Active subjects
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isError: isErrorSubjects,
    refetch: refetchSubjects,
  } = useQuery({
    queryKey: ["subjects", "knowledge-graph", user?.centerId, "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: Boolean(user?.centerId),
  });

  const activeSubjects = useMemo(() => subjectsData?.data ?? [], [subjectsData?.data]);

  useEffect(() => {
    if (urlSubjectId && activeSubjects.some((s) => s.subjectId === urlSubjectId)) {
      setSelectedSubjectId(urlSubjectId);
    } else if (!selectedSubjectId && activeSubjects.length > 0) {
      const defaultId = activeSubjects[0].subjectId;
      setSelectedSubjectId(defaultId);
      setSearchParams({ subjectId: defaultId }, { replace: true });
    }
  }, [urlSubjectId, activeSubjects, selectedSubjectId, setSearchParams]);

  const handleSelectSubject = (newSubjectId: string) => {
    setSelectedSubjectId(newSubjectId);
    if (newSubjectId) {
      setSearchParams({ subjectId: newSubjectId });
    } else {
      setSearchParams({});
    }
  };

  // Query: Graph for selected subject
  const {
    data: graphData,
    isLoading: isLoadingGraph,
    isFetching: isFetchingGraph,
    isError: isErrorGraph,
    refetch: refetchGraph,
  } = useQuery({
    queryKey: ["knowledge-graph", user?.centerId, selectedSubjectId],
    queryFn: () => knowledgeGraphApi.getGraph(selectedSubjectId),
    enabled: Boolean(user?.centerId && selectedSubjectId.trim()),
  });

  const nodeMap = useMemo(() => {
    const map = new Map<string, string>();
    if (graphData?.nodes) {
      for (const node of graphData.nodes) {
        map.set(node.nodeId, `${node.nodeCode} - ${node.nodeName}`);
      }
    }
    return map;
  }, [graphData?.nodes]);

  const invalidateGraph = async () => {
    if (user?.centerId && selectedSubjectId) {
      await queryClient.invalidateQueries({
        queryKey: ["knowledge-graph", user.centerId, selectedSubjectId],
      });
    }
  };

  // Node Mutations
  const updateNodeMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateKnowledgeNodeRequest }) =>
      knowledgeGraphApi.updateNode(id, req),
    onSuccess: async () => {
      await invalidateGraph();
      setEditingNode(null);
      setEditNodeError(null);
      setGlobalSuccessMessage("Cập nhật nút kiến thức thành công!");
    },
    onError: async (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (isConcurrencyConflict(err)) {
          const refreshed = await refetchGraph();
          const latestNode = refreshed.data?.nodes.find((node) => node.nodeId === editingNode?.nodeId);
          if (latestNode) setEditingNode(latestNode);
          setEditNodeError(
            "Dữ liệu nút vừa thay đổi bởi người dùng khác. Hệ thống đã nạp RowVersion mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        if (err.response?.status === 409) {
          setEditNodeError(
            mapSafeOperationalError(err, "Không thể cập nhật nút do xung đột ràng buộc hoặc chu trình phụ thuộc.")
          );
          return;
        }
        setEditNodeError(mapSafeOperationalError(err, "Không thể cập nhật nút kiến thức. Vui lòng thử lại."));
        return;
      }
      setEditNodeError("Đã xảy ra lỗi khi cập nhật nút kiến thức.");
    },
  });

  const deleteNodeMutation = useMutation({
    mutationFn: (id: string) => knowledgeGraphApi.deleteNode(id),
    onSuccess: async () => {
      await invalidateGraph();
      setDeletingNode(null);
      setDeleteNodeError(null);
      setGlobalSuccessMessage("Đã xóa nút kiến thức thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (err.response?.status === 409) {
          setDeleteNodeError(
            mapSafeOperationalError(
              err,
              "Không thể xóa nút kiến thức vì đang có dữ liệu hoặc quan hệ liên kết trong hệ thống."
            )
          );
          return;
        }
        setDeleteNodeError(mapSafeOperationalError(err, "Không thể xóa nút kiến thức. Vui lòng thử lại."));
        return;
      }
      setDeleteNodeError("Đã xảy ra lỗi khi xóa nút kiến thức.");
    },
  });

  // Edge Mutations
  const updateEdgeMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateKnowledgeEdgeRequest }) =>
      knowledgeGraphApi.updateEdge(id, req),
    onSuccess: async () => {
      await invalidateGraph();
      setEditingEdge(null);
      setEditEdgeError(null);
      setGlobalSuccessMessage("Cập nhật liên kết thành công!");
    },
    onError: async (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (isConcurrencyConflict(err)) {
          const refreshed = await refetchGraph();
          const latestEdge = refreshed.data?.edges.find((edge) => edge.edgeId === editingEdge?.edgeId);
          if (latestEdge) setEditingEdge(latestEdge);
          setEditEdgeError(
            "Dữ liệu liên kết vừa thay đổi bởi người dùng khác. Hệ thống đã nạp RowVersion mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        setEditEdgeError(mapSafeOperationalError(err, "Không thể cập nhật liên kết. Vui lòng thử lại."));
        return;
      }
      setEditEdgeError("Đã xảy ra lỗi khi cập nhật liên kết.");
    },
  });

  const deleteEdgeMutation = useMutation({
    mutationFn: (id: string) => knowledgeGraphApi.deleteEdge(id),
    onSuccess: async () => {
      await invalidateGraph();
      setDeletingEdge(null);
      setDeleteEdgeError(null);
      setGlobalSuccessMessage("Đã xóa liên kết thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        setDeleteEdgeError(mapSafeOperationalError(err, "Không thể xóa liên kết. Vui lòng thử lại."));
        return;
      }
      setDeleteEdgeError("Đã xảy ra lỗi khi xóa liên kết.");
    },
  });

  // Open Handlers
  const handleOpenEditNode = (node: KnowledgeGraphNodeDto) => {
    setEditingNode(node);
    setEditNodeName(node.nodeName);
    setEditNodeParentId(node.parentNodeId || "");
    setEditNodeDescription(node.description || "");
    setEditNodeOrderIndex(String(node.orderIndex ?? 0));
    setEditNodeExamImportance(String(node.examImportance ?? 0));
    setEditNodeEstimatedMinutes(String(node.estimatedLearningMinutes ?? 30));
    setEditNodeIsActive(node.isActive ?? true);
    setEditNodeError(null);
  };

  const handleEditNodeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingNode) return;
    setEditNodeError(null);

    const trimmedName = editNodeName.trim();
    if (!trimmedName) {
      setEditNodeError("Tên nút không được để trống.");
      return;
    }

    const orderIdx = parseInt(editNodeOrderIndex, 10);
    if (isNaN(orderIdx) || orderIdx < 0) {
      setEditNodeError("Thứ tự phải là số nguyên không âm.");
      return;
    }

    const importance = parseFloat(editNodeExamImportance);
    if (isNaN(importance) || importance < 0 || importance > 100) {
      setEditNodeError("Mức quan trọng thi phải nằm trong khoảng từ 0 đến 100.");
      return;
    }

    const minutes = parseInt(editNodeEstimatedMinutes, 10);
    if (isNaN(minutes) || minutes < 1) {
      setEditNodeError("Thời gian học ước tính phải là số nguyên dương (tối thiểu 1 phút).");
      return;
    }

    updateNodeMutation.mutate({
      id: editingNode.nodeId,
      req: {
        nodeName: trimmedName,
        parentNodeId: editNodeParentId.trim() || null,
        description: editNodeDescription.trim() || null,
        orderIndex: orderIdx,
        examImportance: importance,
        estimatedLearningMinutes: minutes,
        isActive: editNodeIsActive,
        rowVersion: editingNode.rowVersion,
      },
    });
  };

  const handleOpenEditEdge = (edge: KnowledgeGraphEdgeDto) => {
    setEditingEdge(edge);
    setEditEdgeWeight(String(edge.weight ?? 1.0));
    setEditEdgeError(null);
  };

  const handleEditEdgeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingEdge) return;
    setEditEdgeError(null);

    const weight = parseFloat(editEdgeWeight);
    if (isNaN(weight) || weight < 0 || weight > 1.0) {
      setEditEdgeError("Trọng số phải nằm trong khoảng từ 0.0 đến 1.0.");
      return;
    }

    updateEdgeMutation.mutate({
      id: editingEdge.edgeId,
      req: {
        weight,
        rowVersion: editingEdge.rowVersion,
      },
    });
  };

  return (
    <div className="min-h-screen bg-slate-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-slate-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Đồ thị kiến thức
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Quản lý các nút kiến thức và mối quan hệ phụ thuộc theo môn học của trung tâm.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            {canReadSubjects && (
              <Link
                to="/quan-ly/mon-hoc"
                id="link-manage-subjects"
                className="inline-flex items-center rounded-lg bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
              >
                Quản lý môn học
              </Link>
            )}
            <Link
              to="/"
              className="inline-flex items-center rounded-lg bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Trang chủ
            </Link>
          </div>
        </div>

        {/* Global Success Notification */}
        {globalSuccessMessage && (
          <div className="mb-6 rounded-lg bg-emerald-50 p-4 ring-1 ring-emerald-200">
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-emerald-800">{globalSuccessMessage}</p>
              <button
                type="button"
                onClick={() => setGlobalSuccessMessage(null)}
                className="text-xs font-semibold text-emerald-700 hover:text-emerald-900"
              >
                Đóng
              </button>
            </div>
          </div>
        )}

        {/* Subject Selector Card */}
        <div className="mb-8 rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex-1 max-w-lg">
              <label
                htmlFor="subject-select"
                className="block text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1"
              >
                Chọn môn học để xem đồ thị
              </label>
              {isLoadingSubjects ? (
                <div className="animate-pulse h-10 bg-slate-100 rounded-lg" />
              ) : isErrorSubjects ? (
                <div className="text-sm text-red-600">
                  Lỗi tải danh sách môn học.{" "}
                  <button
                    type="button"
                    onClick={() => refetchSubjects()}
                    className="font-bold underline"
                  >
                    Thử lại
                  </button>
                </div>
              ) : (
                <select
                  id="subject-select"
                  value={selectedSubjectId}
                  onChange={(e) => handleSelectSubject(e.target.value)}
                  className="block w-full rounded-lg border-0 py-2.5 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm font-medium"
                >
                  <option value="">-- Chọn một môn học --</option>
                  {activeSubjects.map((sub) => (
                    <option key={sub.subjectId} value={sub.subjectId}>
                      {sub.subjectCode} - {sub.subjectName}
                    </option>
                  ))}
                </select>
              )}
            </div>

            {selectedSubjectId && (
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => refetchGraph()}
                  className="inline-flex items-center rounded-lg bg-white px-3 py-2 text-xs font-bold text-indigo-600 shadow-sm ring-1 ring-inset ring-indigo-200 hover:bg-indigo-50"
                >
                  Làm mới đồ thị
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Main Body */}
        {!selectedSubjectId ? (
          <div className="rounded-xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <svg
              className="mx-auto h-12 w-12 text-slate-400"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={1.5}
                d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19 14.5M14.25 3.104c.251.023.501.05.75.082M19 14.5l-3.293-3.293a1 1 0 00-.707-.293H8.707a1 1 0 00-.707.293L5 14.5m14 0v3.75a2.25 2.25 0 01-2.25 2.25H7.25A2.25 2.25 0 015 18.25V14.5"
              />
            </svg>
            <h3 className="mt-2 text-sm font-semibold text-slate-900">
              Chưa chọn môn học
            </h3>
            <p className="mt-1 text-sm text-slate-500">
              Vui lòng chọn một môn học ở danh sách phía trên để xem và quản trị đồ thị kiến thức.
            </p>
          </div>
        ) : isLoadingGraph ? (
          <div className="rounded-xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-indigo-600 border-r-transparent align-[-0.125em]" />
            <p className="mt-4 text-sm font-medium text-slate-700">
              Đang tải dữ liệu đồ thị kiến thức...
            </p>
          </div>
        ) : isErrorGraph ? (
          <div className="rounded-xl bg-red-50 p-6 shadow-sm ring-1 ring-red-200" role="alert">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
              <div>
                <h3 className="text-sm font-medium text-red-800">
                  Không thể tải đồ thị kiến thức
                </h3>
                <p className="mt-1 text-sm text-red-700">
                  Đã xảy ra lỗi khi lấy dữ liệu đồ thị từ hệ thống. Vui lòng kiểm tra lại kết nối hoặc thử lại.
                </p>
              </div>
              <button
                type="button"
                onClick={() => refetchGraph()}
                className="inline-flex items-center justify-center rounded-lg bg-red-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-red-500"
              >
                Thử lại
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-8">
            {/* Create Panels */}
            {user?.centerId && (
              <>
                {canCreateNodes && (
                  <KnowledgeNodeCreatePanel
                    key={`knowledge-node-create:${user.centerId}:${selectedSubjectId}`}
                    subjectId={selectedSubjectId}
                    centerId={user.centerId}
                    nodes={graphData?.nodes ?? []}
                  />
                )}
                {canCreateEdges && (
                  <KnowledgeEdgeCreatePanel
                    key={`knowledge-edge-create:${user.centerId}:${selectedSubjectId}`}
                    subjectId={selectedSubjectId}
                    centerId={user.centerId}
                    nodes={graphData?.nodes ?? []}
                  />
                )}
              </>
            )}

            {/* Background fetching indicator */}
            {isFetchingGraph && (
              <div className="flex items-center justify-end">
                <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
                  <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-indigo-600" />
                  Đang đồng bộ...
                </span>
              </div>
            )}

            {/* Section 1: Nodes Table */}
            <div className="overflow-hidden bg-white shadow-sm ring-1 ring-slate-200 sm:rounded-xl">
              <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
                <div>
                  <h2 className="text-base font-bold text-slate-900">
                    Danh sách nút kiến thức ({graphData?.nodes.length ?? 0})
                  </h2>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Các khái niệm, chủ đề, kỹ năng trong môn học.
                  </p>
                </div>
              </div>

              {graphData?.nodes.length === 0 ? (
                <div className="p-8 text-center text-sm text-slate-500">
                  Chưa có nút kiến thức trong môn học này.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-slate-200" id="knowledge-nodes-table">
                    <thead className="bg-slate-50">
                      <tr>
                        <th scope="col" className="py-3.5 pl-6 pr-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Mã nút
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Tên nút
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Loại nút
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Thứ tự
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Mức quan trọng thi
                        </th>
                        <th scope="col" className="py-3.5 pl-3 pr-6 text-right text-xs font-bold uppercase tracking-wider text-slate-500">
                          Thao tác
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200 bg-white">
                      {graphData?.nodes.map((node) => (
                        <tr key={node.nodeId} className="hover:bg-slate-50 transition-colors">
                          <td className="whitespace-nowrap py-4 pl-6 pr-3 text-sm font-bold text-indigo-700 font-mono">
                            {node.nodeCode}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm font-semibold text-slate-900">
                            {node.nodeName}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-slate-600">
                            <span className="inline-flex items-center rounded-md bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700 ring-1 ring-inset ring-slate-600/10">
                              {nodeTypeLabels[node.nodeType] ?? node.nodeType}
                            </span>
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-slate-500">
                            {node.orderIndex}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-slate-500">
                            {node.examImportance}
                          </td>
                          <td className="whitespace-nowrap py-4 pl-3 pr-6 text-right text-sm font-medium space-x-2">
                            {canUpdateNodes && (
                              <button
                                type="button"
                                id={`btn-edit-node-${node.nodeId}`}
                                onClick={() => handleOpenEditNode(node)}
                                className="inline-flex items-center rounded bg-amber-50 px-2.5 py-1.5 text-xs font-semibold text-amber-700 ring-1 ring-inset ring-amber-300 hover:bg-amber-100"
                              >
                                Sửa
                              </button>
                            )}
                            {canDeleteNodes && (
                              <button
                                type="button"
                                id={`btn-delete-node-${node.nodeId}`}
                                onClick={() => {
                                  setDeletingNode(node);
                                  setDeleteNodeError(null);
                                }}
                                className="inline-flex items-center rounded bg-red-50 px-2.5 py-1.5 text-xs font-semibold text-red-700 ring-1 ring-inset ring-red-300 hover:bg-red-100"
                              >
                                Xóa
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            {/* Section 2: Edges Table */}
            <div className="overflow-hidden bg-white shadow-sm ring-1 ring-slate-200 sm:rounded-xl">
              <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
                <div>
                  <h2 className="text-base font-bold text-slate-900">
                    Danh sách liên kết kiến thức ({graphData?.edges.length ?? 0})
                  </h2>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Quan hệ tiên quyết, chứa, mở rộng giữa các nút.
                  </p>
                </div>
              </div>

              {graphData?.edges.length === 0 ? (
                <div className="p-8 text-center text-sm text-slate-500">
                  Chưa có liên kết kiến thức.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-slate-200" id="knowledge-edges-table">
                    <thead className="bg-slate-50">
                      <tr>
                        <th scope="col" className="py-3.5 pl-6 pr-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Nút nguồn
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Loại quan hệ
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Nút đích
                        </th>
                        <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                          Trọng số
                        </th>
                        <th scope="col" className="py-3.5 pl-3 pr-6 text-right text-xs font-bold uppercase tracking-wider text-slate-500">
                          Thao tác
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200 bg-white">
                      {graphData?.edges.map((edge) => (
                        <tr key={edge.edgeId} className="hover:bg-slate-50 transition-colors">
                          <td className="whitespace-nowrap py-4 pl-6 pr-3 text-sm font-medium text-slate-900">
                            {nodeMap.get(edge.sourceNodeId) ?? edge.sourceNodeId}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-slate-600">
                            <span className="inline-flex items-center rounded-md bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
                              {relationTypeLabels[edge.relationType] ?? edge.relationType}
                            </span>
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm font-medium text-slate-900">
                            {nodeMap.get(edge.targetNodeId) ?? edge.targetNodeId}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-slate-500">
                            {edge.weight}
                          </td>
                          <td className="whitespace-nowrap py-4 pl-3 pr-6 text-right text-sm font-medium space-x-2">
                            {canUpdateEdges && (
                              <button
                                type="button"
                                id={`btn-edit-edge-${edge.edgeId}`}
                                onClick={() => handleOpenEditEdge(edge)}
                                className="inline-flex items-center rounded bg-amber-50 px-2.5 py-1.5 text-xs font-semibold text-amber-700 ring-1 ring-inset ring-amber-300 hover:bg-amber-100"
                              >
                                Sửa
                              </button>
                            )}
                            {canDeleteEdges && (
                              <button
                                type="button"
                                id={`btn-delete-edge-${edge.edgeId}`}
                                onClick={() => {
                                  setDeletingEdge(edge);
                                  setDeleteEdgeError(null);
                                }}
                                className="inline-flex items-center rounded bg-red-50 px-2.5 py-1.5 text-xs font-semibold text-red-700 ring-1 ring-inset ring-red-300 hover:bg-red-100"
                              >
                                Xóa
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Modal 1: Edit Knowledge Node */}
        {editingNode && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-slate-900">
                  Cập nhật nút kiến thức: {editingNode.nodeCode}
                </h3>
                <button
                  type="button"
                  onClick={() => setEditingNode(null)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {editNodeError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {editNodeError}
                </div>
              )}

              <form onSubmit={handleEditNodeSubmit} className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-1">
                      Mã nút (không thể đổi)
                    </label>
                    <input
                      type="text"
                      disabled
                      value={editingNode.nodeCode}
                      className="block w-full rounded-lg border-0 py-2 px-3 bg-slate-100 text-slate-500 font-mono sm:text-sm cursor-not-allowed"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-1">
                      Loại nút
                    </label>
                    <input
                      type="text"
                      disabled
                      value={nodeTypeLabels[editingNode.nodeType] ?? editingNode.nodeType}
                      className="block w-full rounded-lg border-0 py-2 px-3 bg-slate-100 text-slate-500 sm:text-sm cursor-not-allowed"
                    />
                  </div>
                </div>

                <div>
                  <label htmlFor="edit-node-name" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Tên nút <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="edit-node-name"
                    type="text"
                    required
                    maxLength={200}
                    value={editNodeName}
                    onChange={(e) => setEditNodeName(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="edit-node-parent" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Nút cha (phân cấp)
                  </label>
                  <select
                    id="edit-node-parent"
                    value={editNodeParentId}
                    onChange={(e) => setEditNodeParentId(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  >
                    <option value="">-- Không có (Nút gốc) --</option>
                    {graphData?.nodes
                      ?.filter((n) => n.nodeId !== editingNode.nodeId)
                      .map((n) => (
                        <option key={n.nodeId} value={n.nodeId}>
                          {n.nodeCode} - {n.nodeName}
                        </option>
                      ))}
                  </select>
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label htmlFor="edit-node-order" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                      Thứ tự
                    </label>
                    <input
                      id="edit-node-order"
                      type="number"
                      min={0}
                      value={editNodeOrderIndex}
                      onChange={(e) => setEditNodeOrderIndex(e.target.value)}
                      className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                    />
                  </div>
                  <div>
                    <label htmlFor="edit-node-importance" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                      Trọng số thi
                    </label>
                    <input
                      id="edit-node-importance"
                      type="number"
                      min={0}
                      max={100}
                      step={0.1}
                      value={editNodeExamImportance}
                      onChange={(e) => setEditNodeExamImportance(e.target.value)}
                      className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                    />
                  </div>
                  <div>
                    <label htmlFor="edit-node-minutes" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                      Thời gian (phút)
                    </label>
                    <input
                      id="edit-node-minutes"
                      type="number"
                      min={1}
                      value={editNodeEstimatedMinutes}
                      onChange={(e) => setEditNodeEstimatedMinutes(e.target.value)}
                      className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                    />
                  </div>
                </div>

                <div>
                  <label htmlFor="edit-node-desc" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Mô tả
                  </label>
                  <textarea
                    id="edit-node-desc"
                    rows={3}
                    maxLength={1000}
                    value={editNodeDescription}
                    onChange={(e) => setEditNodeDescription(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                  <p className="mt-1 text-xs text-slate-400">{editNodeDescription.length}/1000 ký tự</p>
                </div>

                <div className="flex items-center gap-3 pt-2">
                  <input
                    id="edit-node-active"
                    type="checkbox"
                    checked={editNodeIsActive}
                    onChange={(e) => setEditNodeIsActive(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-600"
                  />
                  <label htmlFor="edit-node-active" className="text-sm font-medium text-slate-700 select-none">
                    Nút kiến thức đang hoạt động
                  </label>
                </div>

                <div className="mt-6 flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setEditingNode(null)}
                    disabled={updateNodeMutation.isPending}
                    className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-edit-node"
                    disabled={updateNodeMutation.isPending}
                    className="inline-flex items-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateNodeMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal 2: Delete Knowledge Node */}
        {deletingNode && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-red-600">Xác nhận xóa nút kiến thức</h3>
                <button
                  type="button"
                  onClick={() => setDeletingNode(null)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {deleteNodeError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {deleteNodeError}
                </div>
              )}

              <p className="text-sm text-slate-700 mb-3">
                Bạn có chắc chắn muốn xóa nút kiến thức{" "}
                <span className="font-bold text-slate-900">
                  {deletingNode.nodeCode} — {deletingNode.nodeName}
                </span>
                ?
              </p>

              <div className="rounded-lg bg-amber-50 p-3 ring-1 ring-amber-200 text-xs text-amber-800 mb-6">
                <strong>Lưu ý:</strong> Thao tác này sẽ xóa mềm nút kiến thức. Nếu nút đang có dữ liệu liên kết hoặc quan hệ đồ thị, hệ thống sẽ từ chối để bảo vệ toàn vẹn lịch sử.
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setDeletingNode(null)}
                  disabled={deleteNodeMutation.isPending}
                  className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-confirm-delete-node"
                  onClick={() => deleteNodeMutation.mutate(deletingNode.nodeId)}
                  disabled={deleteNodeMutation.isPending}
                  className="inline-flex items-center rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-red-500 disabled:opacity-50"
                >
                  {deleteNodeMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Modal 3: Edit Knowledge Edge */}
        {editingEdge && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-slate-900">Cập nhật liên kết kiến thức</h3>
                <button
                  type="button"
                  onClick={() => setEditingEdge(null)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {editEdgeError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {editEdgeError}
                </div>
              )}

              <form onSubmit={handleEditEdgeSubmit} className="space-y-4">
                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Nút nguồn
                  </span>
                  <p className="mt-0.5 text-sm font-semibold text-slate-900">
                    {nodeMap.get(editingEdge.sourceNodeId) ?? editingEdge.sourceNodeId}
                  </p>
                </div>

                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Loại quan hệ
                  </span>
                  <p className="mt-0.5 text-sm font-semibold text-indigo-700">
                    {relationTypeLabels[editingEdge.relationType] ?? editingEdge.relationType}
                  </p>
                </div>

                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Nút đích
                  </span>
                  <p className="mt-0.5 text-sm font-semibold text-slate-900">
                    {nodeMap.get(editingEdge.targetNodeId) ?? editingEdge.targetNodeId}
                  </p>
                </div>

                <div>
                  <label htmlFor="edit-edge-weight" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Trọng số liên kết (0.00 – 1.00) <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="edit-edge-weight"
                    type="number"
                    min={0}
                    max={1.0}
                    step={0.01}
                    required
                    value={editEdgeWeight}
                    onChange={(e) => setEditEdgeWeight(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>

                <div className="mt-6 flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setEditingEdge(null)}
                    disabled={updateEdgeMutation.isPending}
                    className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-edit-edge"
                    disabled={updateEdgeMutation.isPending}
                    className="inline-flex items-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateEdgeMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal 4: Delete Knowledge Edge */}
        {deletingEdge && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-red-600">Xác nhận xóa liên kết</h3>
                <button
                  type="button"
                  onClick={() => setDeletingEdge(null)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {deleteEdgeError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {deleteEdgeError}
                </div>
              )}

              <p className="text-sm text-slate-700 mb-4">
                Bạn có chắc chắn muốn xóa liên kết giữa{" "}
                <span className="font-bold text-slate-900">
                  {nodeMap.get(deletingEdge.sourceNodeId) ?? deletingEdge.sourceNodeId}
                </span>{" "}
                và{" "}
                <span className="font-bold text-slate-900">
                  {nodeMap.get(deletingEdge.targetNodeId) ?? deletingEdge.targetNodeId}
                </span>
                ?
              </p>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setDeletingEdge(null)}
                  disabled={deleteEdgeMutation.isPending}
                  className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-confirm-delete-edge"
                  onClick={() => deleteEdgeMutation.mutate(deletingEdge.edgeId)}
                  disabled={deleteEdgeMutation.isPending}
                  className="inline-flex items-center rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-red-500 disabled:opacity-50"
                >
                  {deleteEdgeMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export const KnowledgeGraphPage: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? (
    <CenterManagerKnowledgeGraphView />
  ) : (
    <LegacyKnowledgeGraphPage />
  );
};
