import React, { useState, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import { knowledgeGraphApi } from "../api/knowledgeGraphApi";
import { useAuthStore } from "../stores/authStore";
import { KnowledgeNodeCreatePanel } from "../components/KnowledgeNodeCreatePanel";
import { KnowledgeEdgeCreatePanel } from "../components/KnowledgeEdgeCreatePanel";
import type { KnowledgeNodeType, KnowledgeRelationType } from "../types/knowledgeGraph";

const nodeTypeLabels: Record<KnowledgeNodeType, string> = {
  Subject: "Môn học",
  Chapter: "Chương",
  Topic: "Chủ đề",
  Skill: "Kỹ năng",
  Concept: "Khái niệm",
};

const relationTypeLabels: Record<KnowledgeRelationType, string> = {
  PrerequisiteOf: "Tiên quyết của",
  RelatedTo: "Liên quan đến",
  PartOf: "Là một phần của",
  CausesErrorIn: "Gây lỗi trong",
};

export const KnowledgeGraphPage: React.FC = () => {
  const { user } = useAuthStore();
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>("");

  const canManageGraph =
    user?.role === "Teacher" || user?.role === "CenterManager";

  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isFetching: isFetchingSubjects,
    isError: isErrorSubjects,
    refetch: refetchSubjects,
  } = useQuery({
    queryKey: ["subjects", "knowledge-graph", user?.centerId, "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: Boolean(user?.centerId),
  });

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

  const activeSubjects = subjectsData?.data ?? [];

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Đồ thị kiến thức
            </h1>
            <p className="mt-1 text-sm text-gray-500">
              Xem và theo dõi cấu trúc cây/đồ thị tri thức theo môn học.
            </p>
          </div>
          <div>
            <Link
              to="/"
              className="inline-flex items-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
            >
              Quay lại trang chính
            </Link>
          </div>
        </div>

        {/* Subject Selector Card */}
        <div className="mb-8 overflow-hidden rounded-lg bg-white shadow">
          <div className="p-6">
            <div className="max-w-xs">
              <label
                htmlFor="subject-select"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Môn học <span className="text-red-500">*</span>
              </label>
              <div className="mt-2">
                <select
                  id="subject-select"
                  name="subjectSelect"
                  value={selectedSubjectId}
                  onChange={(e) => setSelectedSubjectId(e.target.value)}
                  disabled={isLoadingSubjects || isFetchingSubjects}
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <option value="">Chọn môn học</option>
                  {(isLoadingSubjects || isFetchingSubjects) && (
                    <option value="" disabled>
                      Đang tải danh sách môn học...
                    </option>
                  )}
                  {activeSubjects.map((subject) => (
                    <option key={subject.subjectId} value={subject.subjectId}>
                      {subject.subjectCode} — {subject.subjectName}
                    </option>
                  ))}
                </select>
              </div>

              {isErrorSubjects && (
                <div className="mt-2 flex items-center gap-2">
                  <p className="text-xs text-red-600">
                    Không thể tải danh sách môn học.
                  </p>
                  <button
                    type="button"
                    onClick={() => refetchSubjects()}
                    className="text-xs font-semibold text-indigo-600 hover:text-indigo-500 underline"
                  >
                    Thử lại
                  </button>
                </div>
              )}

              {!isLoadingSubjects &&
                !isErrorSubjects &&
                activeSubjects.length === 0 && (
                  <p className="mt-2 text-xs text-amber-600">
                    Hiện chưa có môn học nào đang hoạt động trong trung tâm.
                  </p>
                )}
            </div>
          </div>
        </div>

        {/* Content Body States */}
        {!selectedSubjectId ? (
          <div className="rounded-lg bg-white p-12 text-center shadow">
            <svg
              className="mx-auto h-12 w-12 text-gray-400"
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
            <h3 className="mt-2 text-sm font-semibold text-gray-900">
              Chưa chọn môn học
            </h3>
            <p className="mt-1 text-sm text-gray-500">
              Vui lòng chọn một môn học ở danh sách phía trên để xem đồ thị kiến thức.
            </p>
          </div>
        ) : isLoadingGraph ? (
          <div className="rounded-lg bg-white p-12 text-center shadow">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-indigo-600 border-r-transparent align-[-0.125em] motion-reduce:animate-[spin_1.5s_linear_infinite]" />
            <p className="mt-4 text-sm font-medium text-gray-700">
              Đang tải đồ thị kiến thức...
            </p>
          </div>
        ) : isErrorGraph ? (
          <div className="rounded-lg bg-red-50 p-6 shadow ring-1 ring-red-200" role="alert">
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
                className="inline-flex items-center justify-center rounded-md bg-red-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-red-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600"
              >
                Thử lại
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-8">
            {/* Mutation Panels for Teacher and CenterManager */}
            {canManageGraph && user?.centerId && (
              <>
                <KnowledgeNodeCreatePanel
                  key={`knowledge-node-create:${user.centerId}:${selectedSubjectId}`}
                  subjectId={selectedSubjectId}
                  centerId={user.centerId}
                  nodes={graphData?.nodes ?? []}
                />
                <KnowledgeEdgeCreatePanel
                  key={`knowledge-edge-create:${user.centerId}:${selectedSubjectId}`}
                  subjectId={selectedSubjectId}
                  centerId={user.centerId}
                  nodes={graphData?.nodes ?? []}
                />
              </>
            )}

            {/* Background fetching badge */}
            {isFetchingGraph && (
              <div className="flex items-center justify-end">
                <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
                  <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-indigo-600" />
                  Đang cập nhật...
                </span>
              </div>
            )}

            {/* Section 1: Nodes Table */}
            <div className="overflow-hidden bg-white shadow sm:rounded-lg">
              <div className="px-4 py-5 sm:px-6 border-b border-gray-200 flex items-center justify-between">
                <div>
                  <h2 className="text-lg font-semibold leading-6 text-gray-900">
                    Danh sách nút kiến thức
                  </h2>
                  <p className="mt-1 text-sm text-gray-500">
                    Tổng số {graphData?.nodes.length ?? 0} nút trong đồ thị.
                  </p>
                </div>
              </div>

              {graphData?.nodes.length === 0 ? (
                <div className="p-6 text-center text-sm text-gray-500">
                  Chưa có nút kiến thức trong môn học này.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-gray-300">
                    <thead className="bg-gray-50">
                      <tr>
                        <th
                          scope="col"
                          className="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6"
                        >
                          Mã nút
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Tên nút
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Loại nút
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Thứ tự
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Mức quan trọng thi
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200 bg-white">
                      {graphData?.nodes.map((node) => (
                        <tr key={node.nodeId} className="hover:bg-gray-50">
                          <td className="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                            {node.nodeCode}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-900 font-semibold">
                            {node.nodeName}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-600">
                            <span className="inline-flex items-center rounded-md bg-gray-100 px-2 py-1 text-xs font-medium text-gray-700 ring-1 ring-inset ring-gray-600/10">
                              {nodeTypeLabels[node.nodeType] ?? node.nodeType}
                            </span>
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                            {node.orderIndex}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                            {node.examImportance}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            {/* Section 2: Edges Table */}
            <div className="overflow-hidden bg-white shadow sm:rounded-lg">
              <div className="px-4 py-5 sm:px-6 border-b border-gray-200 flex items-center justify-between">
                <div>
                  <h2 className="text-lg font-semibold leading-6 text-gray-900">
                    Danh sách liên kết kiến thức
                  </h2>
                  <p className="mt-1 text-sm text-gray-500">
                    Tổng số {graphData?.edges.length ?? 0} liên kết trong đồ thị.
                  </p>
                </div>
              </div>

              {graphData?.edges.length === 0 ? (
                <div className="p-6 text-center text-sm text-gray-500">
                  Chưa có liên kết kiến thức.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-gray-300">
                    <thead className="bg-gray-50">
                      <tr>
                        <th
                          scope="col"
                          className="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6"
                        >
                          Nút nguồn
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Loại quan hệ
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Nút đích
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900"
                        >
                          Trọng số
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200 bg-white">
                      {graphData?.edges.map((edge) => (
                        <tr key={edge.edgeId} className="hover:bg-gray-50">
                          <td className="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                            {nodeMap.get(edge.sourceNodeId) ?? edge.sourceNodeId}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-600">
                            <span className="inline-flex items-center rounded-md bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
                              {relationTypeLabels[edge.relationType] ??
                                edge.relationType}
                            </span>
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm font-medium text-gray-900">
                            {nodeMap.get(edge.targetNodeId) ?? edge.targetNodeId}
                          </td>
                          <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                            {edge.weight}
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
      </div>
    </div>
  );
};
