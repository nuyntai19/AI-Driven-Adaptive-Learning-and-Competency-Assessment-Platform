import { httpClient } from "./httpClient";
import type {
  LearningPathTopicsResponse,
  StudentLearningPathPreferenceResponse,
  GenerateLearningPathRequest,
  DetailedLearningPathResponse,
} from "../types/learningPath";

export const getLearningPathTopics = async (
  subjectId: string, classId?: string, history = false
): Promise<LearningPathTopicsResponse> => {
  const response = await httpClient.get<LearningPathTopicsResponse>(
    "/students/me/learning-path/topics",
    { params: { subjectId, classId: classId || undefined, history } }
  );
  return response.data;
};

export const getLearningPathPreferences = async (
  subjectId: string
): Promise<StudentLearningPathPreferenceResponse> => {
  const response = await httpClient.get<StudentLearningPathPreferenceResponse>(
    "/students/me/learning-path/preferences",
    { params: { subjectId } }
  );
  return response.data;
};

export const generateLearningPath = async (
  request: GenerateLearningPathRequest
): Promise<DetailedLearningPathResponse> => {
  const response = await httpClient.post<DetailedLearningPathResponse>(
    "/students/me/learning-path/generate",
    request
  );
  return response.data;
};

export const getDetailedLearningPath = async (
  subjectId: string, classId?: string
): Promise<DetailedLearningPathResponse> => {
  const response = await httpClient.get<DetailedLearningPathResponse>(
    "/students/me/learning-path/detailed",
    { params: { subjectId, classId:classId || undefined } }
  );
  return response.data;
};

export const updateLearningPathSession = async (subjectId: string, sessionId: string, status: string, classId?: string, history = false): Promise<void> => {
  await httpClient.patch(`/students/me/learning-path/sessions/${encodeURIComponent(sessionId)}`, { status, classId:classId || undefined, history }, { params: { subjectId } });
};
