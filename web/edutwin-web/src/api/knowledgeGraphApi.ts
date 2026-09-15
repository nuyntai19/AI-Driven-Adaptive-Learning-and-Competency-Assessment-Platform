import { httpClient } from "./httpClient";
import type {
  KnowledgeGraphDto,
  KnowledgeGraphResponse,
  KnowledgeNodeDto,
  KnowledgeNodeResponse,
  KnowledgeNodeListResponse,
  CreateKnowledgeNodeRequest,
  UpdateKnowledgeNodeRequest,
  KnowledgeEdgeDto,
  KnowledgeEdgeResponse,
  CreateKnowledgeEdgeRequest,
  UpdateKnowledgeEdgeRequest,
} from "../types/knowledgeGraph";

const getGraph = async (subjectId: string): Promise<KnowledgeGraphDto> => {
  const trimmedSubjectId = subjectId.trim();
  if (!trimmedSubjectId) {
    throw new Error("Subject ID is required.");
  }

  const response = await httpClient.get<KnowledgeGraphResponse>(
    "/knowledge/graph",
    {
      params: { subjectId: trimmedSubjectId },
    }
  );
  return response.data.data;
};

const createNode = async (
  request: CreateKnowledgeNodeRequest
): Promise<KnowledgeNodeDto> => {
  const payload = {
    subjectId: request.subjectId.trim(),
    parentNodeId: request.parentNodeId?.trim() || null,
    nodeType: request.nodeType,
    nodeCode: request.nodeCode.trim(),
    nodeName: request.nodeName.trim(),
    description: request.description?.trim() || null,
    orderIndex: request.orderIndex,
    examImportance: request.examImportance,
    estimatedLearningMinutes: request.estimatedLearningMinutes,
    isActive: request.isActive,
  };

  const response = await httpClient.post<KnowledgeNodeResponse>(
    "/knowledge/nodes",
    payload
  );
  return response.data.data;
};

const updateNode = async (
  nodeId: string,
  request: UpdateKnowledgeNodeRequest
): Promise<KnowledgeNodeDto> => {
  const payload = {
    parentNodeId: request.parentNodeId?.trim() || null,
    nodeName: request.nodeName.trim(),
    description: request.description?.trim() || null,
    orderIndex: request.orderIndex,
    examImportance: request.examImportance,
    estimatedLearningMinutes: request.estimatedLearningMinutes,
    isActive: request.isActive,
    rowVersion: request.rowVersion,
  };

  const response = await httpClient.patch<KnowledgeNodeResponse>(
    `/knowledge/nodes/${nodeId}`,
    payload
  );
  return response.data.data;
};

const deleteNode = async (nodeId: string): Promise<void> => {
  await httpClient.delete(`/knowledge/nodes/${nodeId}`);
};

const createEdge = async (
  request: CreateKnowledgeEdgeRequest
): Promise<KnowledgeEdgeDto> => {
  const payload = {
    subjectId: request.subjectId.trim(),
    sourceNodeId: request.sourceNodeId.trim(),
    targetNodeId: request.targetNodeId.trim(),
    relationType: request.relationType,
    weight: request.weight,
  };

  const response = await httpClient.post<KnowledgeEdgeResponse>(
    "/knowledge/edges",
    payload
  );
  return response.data.data;
};

const updateEdge = async (
  edgeId: string,
  request: UpdateKnowledgeEdgeRequest
): Promise<KnowledgeEdgeDto> => {
  const payload = {
    weight: request.weight,
    rowVersion: request.rowVersion,
  };

  const response = await httpClient.patch<KnowledgeEdgeResponse>(
    `/knowledge/edges/${edgeId}`,
    payload
  );
  return response.data.data;
};

const deleteEdge = async (edgeId: string): Promise<void> => {
  await httpClient.delete(`/knowledge/edges/${edgeId}`);
};

const listNodes = async (subjectId: string): Promise<KnowledgeNodeDto[]> => {
  if (!subjectId) return [];
  const response = await httpClient.get<KnowledgeNodeListResponse>(
    "/knowledge/nodes",
    { params: { subjectId } }
  );
  return response.data.data ?? [];
};

export const knowledgeGraphApi = {
  getGraph,
  createNode,
  updateNode,
  deleteNode,
  createEdge,
  updateEdge,
  deleteEdge,
  listNodes,
};
