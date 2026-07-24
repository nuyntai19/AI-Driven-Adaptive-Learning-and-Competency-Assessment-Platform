import React, { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { knowledgeGraphApi } from "../api/knowledgeGraphApi";
import type {
  KnowledgeGraphNodeDto,
  KnowledgeNodeType,
  CreateKnowledgeNodeRequest,
} from "../types/knowledgeGraph";
import type { ProblemDetails } from "../types/auth";

interface KnowledgeNodeCreatePanelProps {
  subjectId: string;
  centerId: string;
  nodes: KnowledgeGraphNodeDto[];
}

export const KnowledgeNodeCreatePanel: React.FC<KnowledgeNodeCreatePanelProps> = ({
  subjectId,
  centerId,
  nodes,
}) => {
  const queryClient = useQueryClient();

  const [parentNodeId, setParentNodeId] = useState<string>("");
  const [nodeType, setNodeType] = useState<KnowledgeNodeType>("Topic");
  const [nodeCode, setNodeCode] = useState<string>("");
  const [nodeName, setNodeName] = useState<string>("");
  const [description, setDescription] = useState<string>("");
  const [orderIndex, setOrderIndex] = useState<string>("0");
  const [examImportance, setExamImportance] = useState<string>("0");
  const [estimatedLearningMinutes, setEstimatedLearningMinutes] = useState<string>("30");
  const [isActive, setIsActive] = useState<boolean>(true);

  const [validationError, setValidationError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const resetForm = () => {
    setParentNodeId("");
    setNodeType("Topic");
    setNodeCode("");
    setNodeName("");
    setDescription("");
    setOrderIndex("0");
    setExamImportance("0");
    setEstimatedLearningMinutes("30");
    setIsActive(true);
    setValidationError(null);
  };

  const createNodeMutation = useMutation({
    mutationFn: (req: CreateKnowledgeNodeRequest) => knowledgeGraphApi.createNode(req),
    retry: false,
    onSuccess: async (_createdNode, variables) => {
      await queryClient.invalidateQueries({
        queryKey: ["knowledge-graph", centerId, variables.subjectId],
      });
      setSuccessMessage("Tạo nút kiến thức thành công!");
      resetForm();
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createNodeMutation.reset();
    setValidationError(null);
    setSuccessMessage(null);

    // Frontend validation rules
    if (!nodeCode.trim()) {
      setValidationError("Mã nút không được để trống.");
      return;
    }
    if (nodeCode.length > 64) {
      setValidationError("Mã nút không được vượt quá 64 ký tự.");
      return;
    }

    if (!nodeName.trim()) {
      setValidationError("Tên nút không được để trống.");
      return;
    }
    if (nodeName.length > 200) {
      setValidationError("Tên nút không được vượt quá 200 ký tự.");
      return;
    }

    const parsedOrder = Number(orderIndex);
    if (!Number.isFinite(parsedOrder) || !Number.isInteger(parsedOrder) || parsedOrder < 0) {
      setValidationError("Thứ tự phải là số nguyên không âm.");
      return;
    }

    const parsedImportance = Number(examImportance);
    if (
      !Number.isFinite(parsedImportance) ||
      parsedImportance < 0 ||
      parsedImportance > 100
    ) {
      setValidationError("Mức quan trọng thi phải là số từ 0 đến 100.");
      return;
    }

    const parsedMinutes = Number(estimatedLearningMinutes);
    if (!Number.isFinite(parsedMinutes) || !Number.isInteger(parsedMinutes) || parsedMinutes < 1) {
      setValidationError("Thời lượng học ước tính phải là số nguyên dương (tối thiểu 1 phút).");
      return;
    }

    const requestPayload: CreateKnowledgeNodeRequest = {
      subjectId,
      parentNodeId: parentNodeId.trim() ? parentNodeId.trim() : null,
      nodeType,
      nodeCode,
      nodeName,
      description: description.trim() ? description.trim() : null,
      orderIndex: parsedOrder,
      examImportance: parsedImportance,
      estimatedLearningMinutes: parsedMinutes,
      isActive,
    };

    createNodeMutation.mutate(requestPayload);
  };

  const renderApiError = () => {
    if (!createNodeMutation.isError) return null;

    const err = createNodeMutation.error;
    let message = "Không thể tạo nút kiến thức. Vui lòng thử lại.";

    if (isAxiosError<ProblemDetails>(err)) {
      const errorCode = err.response?.data?.errorCode;
      const detail = err.response?.data?.detail;

      if (errorCode === "VALIDATION_FAILED") {
        message = "Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra lại.";
      } else if (errorCode === "RESOURCE_NOT_FOUND") {
        message = "Môn học hoặc node liên quan không còn tồn tại hay không thể truy cập.";
      } else if (errorCode === "DUPLICATE_RESOURCE") {
        message = "Mã node đã tồn tại trong môn học.";
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
          Tạo nút kiến thức mới
        </h3>
        <p className="mt-1 text-sm text-gray-500">
          Thêm chủ đề, chương, kỹ năng hoặc khái niệm vào môn học hiện tại.
        </p>
      </div>

      <div className="p-6">
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
                htmlFor="create-node-type"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Loại nút <span className="text-red-500">*</span>
              </label>
              <div className="mt-1">
                <select
                  id="create-node-type"
                  value={nodeType}
                  onChange={(e) => setNodeType(e.target.value as KnowledgeNodeType)}
                  disabled={createNodeMutation.isPending}
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                >
                  <option value="Chapter">Chương</option>
                  <option value="Topic">Chủ đề</option>
                  <option value="Skill">Kỹ năng</option>
                  <option value="Concept">Khái niệm</option>
                  <option value="Subject">Môn học</option>
                </select>
              </div>
            </div>

            <div>
              <label
                htmlFor="create-node-parent"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Nút cha (Không bắt buộc)
              </label>
              <div className="mt-1">
                <select
                  id="create-node-parent"
                  value={parentNodeId}
                  onChange={(e) => setParentNodeId(e.target.value)}
                  disabled={createNodeMutation.isPending}
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                >
                  <option value="">-- Không có nút cha (Gốc) --</option>
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
                htmlFor="create-node-code"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Mã nút <span className="text-red-500">*</span>
              </label>
              <div className="mt-1">
                <input
                  type="text"
                  id="create-node-code"
                  value={nodeCode}
                  onChange={(e) => setNodeCode(e.target.value)}
                  disabled={createNodeMutation.isPending}
                  placeholder="Ví dụ: MATH.LOG.01"
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="create-node-name"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Tên nút <span className="text-red-500">*</span>
              </label>
              <div className="mt-1">
                <input
                  type="text"
                  id="create-node-name"
                  value={nodeName}
                  onChange={(e) => setNodeName(e.target.value)}
                  disabled={createNodeMutation.isPending}
                  placeholder="Ví dụ: Định nghĩa Logarit"
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="create-node-order"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Thứ tự sắp xếp (orderIndex)
              </label>
              <div className="mt-1">
                <input
                  type="number"
                  id="create-node-order"
                  min={0}
                  value={orderIndex}
                  onChange={(e) => setOrderIndex(e.target.value)}
                  disabled={createNodeMutation.isPending}
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="create-node-importance"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Mức quan trọng thi (0 - 100)
              </label>
              <div className="mt-1">
                <input
                  type="number"
                  id="create-node-importance"
                  min={0}
                  max={100}
                  step={0.1}
                  value={examImportance}
                  onChange={(e) => setExamImportance(e.target.value)}
                  disabled={createNodeMutation.isPending}
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="create-node-minutes"
                className="block text-sm font-medium leading-6 text-gray-900"
              >
                Thời lượng học ước tính (phút)
              </label>
              <div className="mt-1">
                <input
                  type="number"
                  id="create-node-minutes"
                  min={1}
                  value={estimatedLearningMinutes}
                  onChange={(e) => setEstimatedLearningMinutes(e.target.value)}
                  disabled={createNodeMutation.isPending}
                  className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                />
              </div>
            </div>

            <div className="flex items-center pt-6">
              <input
                type="checkbox"
                id="create-node-active"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                disabled={createNodeMutation.isPending}
                className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-600 disabled:opacity-50"
              />
              <label
                htmlFor="create-node-active"
                className="ml-2 block text-sm font-medium leading-6 text-gray-900"
              >
                Kích hoạt nút này (isActive)
              </label>
            </div>
          </div>

          <div>
            <label
              htmlFor="create-node-desc"
              className="block text-sm font-medium leading-6 text-gray-900"
            >
              Mô tả chi tiết (Không bắt buộc)
            </label>
            <div className="mt-1">
              <textarea
                id="create-node-desc"
                rows={2}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                disabled={createNodeMutation.isPending}
                placeholder="Nhập ghi chú hoặc mô tả về kiến thức..."
                className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
              />
            </div>
          </div>

          <div className="pt-2 flex justify-end">
            <button
              type="submit"
              disabled={createNodeMutation.isPending}
              className="inline-flex items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:bg-indigo-400 disabled:cursor-not-allowed"
            >
              {createNodeMutation.isPending ? "Đang tạo..." : "Tạo nút kiến thức"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
