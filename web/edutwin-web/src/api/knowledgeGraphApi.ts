import { httpClient } from "./httpClient";
import type {
  KnowledgeGraphDto,
  KnowledgeGraphResponse,
  KnowledgeNodeDto,
  KnowledgeNodeResponse,
  CreateKnowledgeNodeRequest,
  KnowledgeEdgeDto,
  KnowledgeEdgeResponse,
  CreateKnowledgeEdgeRequest,
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

export const knowledgeGraphApi = {
  getGraph,
  createNode,
  createEdge,
};
