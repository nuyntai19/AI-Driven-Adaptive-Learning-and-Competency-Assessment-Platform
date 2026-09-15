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
  UpdateKnowledgeNodeRequest,
  UpdateKnowledgeEdgeRequest,
} from "../types/knowledgeGraph";
import type { ProblemDetails } from "../types/auth";
import { extractProblemDetails, isConcurrencyConflict } from "../utils/problemDetails";

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

export const KnowledgeGraphPage: React.FC = () => {
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
        const details = extractProblemDetails(err);
        if (isConcurrencyConflict(err)) {
          const refreshed = await refetchGraph();
          const latestNode = refreshed.data?.nodes.find(
            (node) => node.nodeId === editingNode?.nodeId
          );
          if (latestNode) setEditingNode(latestNode);
          setEditNodeError(
            "Dữ liệu nút vừa thay đổi bởi người dùng khác. Hệ thống đã nạp RowVersion mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        if (err.response?.status === 409) {
          setEditNodeError(
            details?.detail || "Không thể cập nhật nút do xung đột ràng buộc hoặc chu trình phụ thuộc."
          );
          return;
        }
        setEditNodeError(details?.detail || "Không thể cập nhật nút kiến thức. Vui lòng thử lại.");
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
        const details = extractProblemDetails(err);
        if (err.response?.status === 409) {
          setDeleteNodeError(
            details?.detail ||
              "Không thể xóa nút kiến thức vì đang có dữ liệu hoặc quan hệ liên kết trong hệ thống."
          );
          return;
        }
        setDeleteNodeError(details?.detail || "Không thể xóa nút kiến thức. Vui lòng thử lại.");
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
        const details = extractProblemDetails(err);
        if (isConcurrencyConflict(err)) {
          const refreshed = await refetchGraph();
          const latestEdge = refreshed.data?.edges.find(
            (edge) => edge.edgeId === editingEdge?.edgeId
          );
          if (latestEdge) setEditingEdge(latestEdge);
          setEditEdgeError(
            "Dữ liệu liên kết vừa thay đổi bởi người dùng khác. Hệ thống đã nạp RowVersion mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        setEditEdgeError(details?.detail || "Không thể cập nhật liên kết. Vui lòng thử lại.");
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
        const details = extractProblemDetails(err);
        setDeleteEdgeError(details?.detail || "Không thể xóa liên kết. Vui lòng thử lại.");
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
