import { httpClient } from "./httpClient";
import type {
  TeacherReviewQueueQuery,
  TeacherReviewQueueResponse,
  TeacherOverrideRequest,
  TeacherOverrideResponse,
} from "../types/reviews";

export const listTeacherReviewQueue = async (
  query?: TeacherReviewQueueQuery
): Promise<TeacherReviewQueueResponse> => {
  const params: Record<string, string | number> = {};
  if (query?.classId) {
    params.classId = query.classId;
  }
  if (query?.page) {
    params.page = query.page;
  }
  if (query?.pageSize) {
    params.pageSize = query.pageSize;
  }

  const response = await httpClient.get<TeacherReviewQueueResponse>(
    "/teachers/me/review-queue",
    { params }
  );
  return response.data;
};

export const overrideReasoningAnalysis = async (
  analysisId: string | number,
  request: TeacherOverrideRequest
): Promise<TeacherOverrideResponse> => {
  const response = await httpClient.post<TeacherOverrideResponse>(
    `/teachers/me/reasoning-analyses/${analysisId}/override`,
    request
  );
  return response.data;
};
