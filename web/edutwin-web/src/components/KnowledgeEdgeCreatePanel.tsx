import React, { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { knowledgeGraphApi } from "../api/knowledgeGraphApi";
import type {
  KnowledgeGraphNodeDto,
  KnowledgeRelationType,
  CreateKnowledgeEdgeRequest,
} from "../types/knowledgeGraph";
import type { ProblemDetails } from "../types/auth";

interface KnowledgeEdgeCreatePanelProps {
  subjectId: string;
  centerId: string;
  nodes: KnowledgeGraphNodeDto[];
}

export const KnowledgeEdgeCreatePanel: React.FC<KnowledgeEdgeCreatePanelProps> = ({
  subjectId,
  centerId,
  nodes,
}) => {
  const queryClient = useQueryClient();

  const [sourceNodeId, setSourceNodeId] = useState<string>("");
  const [targetNodeId, setTargetNodeId] = useState<string>("");
  const [relationType, setRelationType] =
    useState<KnowledgeRelationType>("PrerequisiteOf");
  const [weight, setWeight] = useState<string>("1.0");

  const [validationError, setValidationError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const resetForm = () => {
    setSourceNodeId("");
    setTargetNodeId("");
    setRelationType("PrerequisiteOf");
    setWeight("1.0");
    setValidationError(null);
  };

  const createEdgeMutation = useMutation({
    mutationFn: (req: CreateKnowledgeEdgeRequest) => knowledgeGraphApi.createEdge(req),
    retry: false,
    onSuccess: async (_createdEdge, variables) => {
      await queryClient.invalidateQueries({
        queryKey: ["knowledge-graph", centerId, variables.subjectId],
      });
      setSuccessMessage("Tạo liên kết kiến thức thành công!");
      resetForm();
    },
  });

  const hasEnoughNodes = nodes.length >= 2;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createEdgeMutation.reset();
    setValidationError(null);
    setSuccessMessage(null);

    if (!sourceNodeId.trim()) {
      setValidationError("Vui lòng chọn nút nguồn.");
      return;
    }

    if (!targetNodeId.trim()) {
      setValidationError("Vui lòng chọn nút đích.");
      return;
    }

    if (sourceNodeId.trim() === targetNodeId.trim()) {
      setValidationError("Nút nguồn và nút đích không được giống nhau.");
      return;
    }

    const parsedWeight = Number(weight);
    if (!Number.isFinite(parsedWeight) || parsedWeight < 0 || parsedWeight > 1) {
      setValidationError("Trọng số liên kết phải là số từ 0.0 đến 1.0.");
      return;
    }

    const requestPayload: CreateKnowledgeEdgeRequest = {
      subjectId,
      sourceNodeId: sourceNodeId.trim(),
      targetNodeId: targetNodeId.trim(),
      relationType,
      weight: parsedWeight,
    };

    createEdgeMutation.mutate(requestPayload);
  };

  const renderApiError = () => {
    if (!createEdgeMutation.isError) return null;

    const err = createEdgeMutation.error;
    let message = "Không thể tạo liên kết kiến thức. Vui lòng thử lại.";

    if (isAxiosError<ProblemDetails>(err)) {
      const errorCode = err.response?.data?.errorCode;
      const detail = err.response?.data?.detail;

      if (errorCode === "VALIDATION_FAILED") {
        message = "Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại.";
      } else if (errorCode === "RESOURCE_NOT_FOUND") {
        message = "Môn học hoặc node liên quan không còn tồn tại hay không thể truy cập.";
      } else if (errorCode === "DUPLICATE_RESOURCE") {
        message = "Quan hệ giữa hai node đã tồn tại.";
      } else if (errorCode === "DAG_CYCLE_DETECTED") {
        message = "Không thể tạo quan hệ vì thao tác này sẽ tạo chu trình trong đồ thị.";
      } else if (detail) {
        message = detail;
      }
    }

    return (
      <div className="rounded-md bg-red-50 p-4 mb-4" role="alert">
        <p className="text-sm font-medium text-red-800">{message}</p>
      </div>
    );
  };

  return (
    <div className="overflow-hidden bg-white shadow sm:rounded-lg mb-8">
      <div className="px-4 py-5 sm:px-6 border-b border-gray-200">
        <h3 className="text-base font-semibold leading-6 text-gray-900">
          Tạo liên kết kiến thức mới
        </h3>
        <p className="mt-1 text-sm text-gray-500">
          Thiết lập mối quan hệ phụ thuộc (tiên quyết, liên quan,...) giữa hai nút trong đồ thị.
        </p>
      </div>

      <div className="p-6">
        {!hasEnoughNodes ? (
          <div className="rounded-md bg-amber-50 p-4 text-sm text-amber-800">
            Môn học cần ít nhất 2 nút kiến thức để tạo liên kết. Vui lòng tạo thêm nút kiến thức phía trên.
          </div>
        ) : (
          <>
            {successMessage && (
              <div className="rounded-md bg-green-50 p-4 mb-4" role="status">
                <p className="text-sm font-medium text-green-800">{successMessage}</p>
              </div>
            )}

            {validationError && (
              <div className="rounded-md bg-red-50 p-4 mb-4" role="alert">
                <p className="text-sm font-medium text-red-800">{validationError}</p>
              </div>
            )}

            {renderApiError()}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label
                    htmlFor="create-edge-source"
                    className="block text-sm font-medium leading-6 text-gray-900"
                  >
                    Nút nguồn <span className="text-red-500">*</span>
                  </label>
                  <div className="mt-1">
                    <select
                      id="create-edge-source"
                      value={sourceNodeId}
                      onChange={(e) => setSourceNodeId(e.target.value)}
                      disabled={createEdgeMutation.isPending}
                      className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                    >
                      <option value="">-- Chọn nút nguồn --</option>
                      {nodes.map((node) => (
                        <option key={node.nodeId} value={node.nodeId}>
                          {node.nodeCode} — {node.nodeName}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div>
                  <label
                    htmlFor="create-edge-target"
                    className="block text-sm font-medium leading-6 text-gray-900"
                  >
                    Nút đích <span className="text-red-500">*</span>
                  </label>
                  <div className="mt-1">
                    <select
                      id="create-edge-target"
                      value={targetNodeId}
                      onChange={(e) => setTargetNodeId(e.target.value)}
                      disabled={createEdgeMutation.isPending}
                      className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                    >
                      <option value="">-- Chọn nút đích --</option>
                      {nodes.map((node) => (
                        <option key={node.nodeId} value={node.nodeId}>
                          {node.nodeCode} — {node.nodeName}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div>
                  <label
                    htmlFor="create-edge-relation"
                    className="block text-sm font-medium leading-6 text-gray-900"
                  >
                    Loại quan hệ <span className="text-red-500">*</span>
                  </label>
                  <div className="mt-1">
                    <select
                      id="create-edge-relation"
                      value={relationType}
                      onChange={(e) =>
                        setRelationType(e.target.value as KnowledgeRelationType)
                      }
                      disabled={createEdgeMutation.isPending}
                      className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                    >
                      <option value="PrerequisiteOf">Tiên quyết của (PrerequisiteOf)</option>
                      <option value="RelatedTo">Liên quan đến (RelatedTo)</option>
                      <option value="PartOf">Là một phần của (PartOf)</option>
                      <option value="CausesErrorIn">Gây lỗi trong (CausesErrorIn)</option>
                    </select>
                  </div>
                </div>

                <div>
                  <label
                    htmlFor="create-edge-weight"
                    className="block text-sm font-medium leading-6 text-gray-900"
                  >
                    Trọng số (0.0 đến 1.0)
                  </label>
                  <div className="mt-1">
                    <input
                      type="number"
                      id="create-edge-weight"
                      min={0}
                      max={1}
                      step={0.1}
                      value={weight}
                      onChange={(e) => setWeight(e.target.value)}
                      disabled={createEdgeMutation.isPending}
                      className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                    />
                  </div>
                </div>
              </div>

              <div className="pt-2 flex justify-end">
                <button
                  type="submit"
                  disabled={createEdgeMutation.isPending}
                  className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:bg-indigo-400 disabled:cursor-not-allowed"
                >
                  {createEdgeMutation.isPending ? "Đang tạo..." : "Tạo liên kết kiến thức"}
                </button>
              </div>
            </form>
          </>
        )}
      </div>
    </div>
  );
};
