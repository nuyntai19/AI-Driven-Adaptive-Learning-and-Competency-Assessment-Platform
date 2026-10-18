import { httpClient } from "./httpClient";
import type {
  LearningPathTopicsResponse,
  StudentLearningPathPreferenceResponse,
  GenerateLearningPathRequest,
  DetailedLearningPathResponse,
  LearningPathSessionDto,
  StartSessionDto,
  StartSessionResponse,
  ResumeSessionDto,
  ResumeSessionResponse,
  EvaluateSessionRequest,
  EvaluateSessionDto,
  EvaluateSessionResponse,
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
  subjectId: string, classId?: string, pathId?: string
): Promise<DetailedLearningPathResponse> => {
  const response = await httpClient.get<DetailedLearningPathResponse>(
    "/students/me/learning-path/detailed",
    { params: { subjectId, classId:classId || undefined, pathId: pathId || undefined } }
  );
  return response.data;
};

export const updateLearningPathSession = async (subjectId: string, sessionId: string, status: string, classId?: string, history = false, expectedRowVersion?: string, acknowledgedActivities = false): Promise<void> => {
  await httpClient.patch(`/students/me/learning-path/sessions/${encodeURIComponent(sessionId)}`, { status, classId:classId || undefined, history, expectedRowVersion, acknowledgedActivities }, { params: { subjectId } });
};

export async function getLearningPathSession(subjectId: string, sessionId: string, classId?: string, history = false): Promise<LearningPathSessionDto> {
  const response = await httpClient.get<{ data: LearningPathSessionDto }>(`/students/me/learning-path/sessions/${encodeURIComponent(sessionId)}`,
    { params: { subjectId, classId: classId || undefined, history } });
  return response.data.data;
}

export const startLearningSession = async (
  sessionId: string,
  subjectId: string,
  classId?: string
): Promise<StartSessionDto> => {
  const response = await httpClient.post<StartSessionResponse>(
    `/students/me/learning-path/sessions/${encodeURIComponent(sessionId)}/start`,
    { subjectId, classId: classId || undefined }
  );
  return response.data.data;
};

export const resumeLearningSession = async (
  sessionId: string,
  subjectId: string,
  classId?: string
): Promise<ResumeSessionDto> => {
  const response = await httpClient.get<ResumeSessionResponse>(
    `/students/me/learning-path/sessions/${encodeURIComponent(sessionId)}/resume`,
    { params: { subjectId, classId: classId || undefined } }
  );
  return response.data.data;
};

export const evaluateLearningSession = async (
  sessionId: string,
  request: EvaluateSessionRequest
): Promise<EvaluateSessionDto> => {
  const response = await httpClient.post<EvaluateSessionResponse>(
    `/students/me/learning-path/sessions/${encodeURIComponent(sessionId)}/evaluate`,
    request
  );
  return response.data.data;
};

// ==========================================
// Learning Path V4 Endpoints
// ==========================================

export const getLearningGoal = async (
  subjectId: string
): Promise<import("../types/learningPath").LearningGoalDto> => {
  const response = await httpClient.get<import("../types/learningPath").LearningGoalDto>(
    "/students/me/learning-goals",
    { params: { subjectId } }
  );
  return response.data;
};

export const createOrUpdateLearningGoal = async (
  request: import("../types/learningPath").CreateLearningGoalRequest
): Promise<import("../types/learningPath").LearningGoalDto> => {
  const response = await httpClient.post<import("../types/learningPath").LearningGoalDto>(
    "/students/me/learning-goals",
    { ...request, classId: request.classId || undefined }
  );
  return response.data;
};

export const generatePathProposal = async (
  request: import("../types/learningPath").GeneratePathProposalRequest
): Promise<import("../types/learningPath").PathProposalDto> => {
  const response = await httpClient.post<import("../types/learningPath").PathProposalDto>(
    "/students/me/path-proposals",
    { ...request, classId: request.classId || undefined }
  );
  return response.data;
};

export const customizePathProposal = async (
  proposalId: string,
  request: import("../types/learningPath").CustomizePathProposalRequest
): Promise<import("../types/learningPath").PathProposalDto> => {
  const response = await httpClient.post<import("../types/learningPath").PathProposalDto>(
    `/students/me/path-proposals/${encodeURIComponent(proposalId)}/customize`, request
  );
  return response.data;
};

export const acceptPathProposal = async (
  proposalId: string
): Promise<import("../types/learningPath").AcceptPathProposalResponse> => {
  const response = await httpClient.post<import("../types/learningPath").AcceptPathProposalResponse>(
    `/students/me/path-proposals/${encodeURIComponent(proposalId)}/accept`
  );
  return response.data;
};

export const getCapabilityMap = async (
  subjectId: string,
  classId?: string
): Promise<import("../types/learningPath").CapabilityMapDto> => {
  const response = await httpClient.get<import("../types/learningPath").CapabilityMapDto>(
    "/students/me/learning-paths/capability-map",
    { params: { subjectId, classId: classId || undefined } }
  );
  return response.data;
};

export const getLearningAgenda = async (
  subjectId: string,
  availableMinutes: number = 15,
  classId?: string
): Promise<import("../types/learningPath").LearningAgendaDto> => {
  const response = await httpClient.get<import("../types/learningPath").LearningAgendaDto>(
    "/students/me/learning-agenda",
    { params: { subjectId, availableMinutes, classId: classId || undefined } }
  );
  return response.data;
};

export const startLearningMission = async (
  missionId: string,
  options: { retry?: boolean; questionCount?: number } = {}
): Promise<import("../types/learningPath").MissionRunDto> => {
  const response = await httpClient.post<import("../types/learningPath").MissionRunDto>(
    `/students/me/missions/${encodeURIComponent(missionId)}/start`,
    undefined,
    { params: { retry: options.retry || undefined, questionCount: options.questionCount || undefined } }
  );
  return response.data;
};

/** Cancels an unfinished run. Submitted answers are kept; the mission can be started again. */
export const abandonMissionRun = async (missionId: string, runId: number): Promise<import("../types/learningPath").MissionRunDto> => {
  const response = await httpClient.post<import("../types/learningPath").MissionRunDto>(
    `/students/me/missions/${encodeURIComponent(missionId)}/runs/${runId}/abandon`
  );
  return response.data;
};

export const getMissionRun = async (missionId: string, runId: number): Promise<import("../types/learningPath").MissionRunDto> => {
  const response = await httpClient.get<import("../types/learningPath").MissionRunDto>(`/students/me/missions/${encodeURIComponent(missionId)}/runs/${runId}`);
  return response.data;
};
export const getActivityAiFeedback = async (id: number): Promise<import("../types/learningPath").LearningTutorFeedbackDto> => {
  const response = await httpClient.post<import("../types/learningPath").LearningTutorFeedbackDto>(`/students/me/activity-runs/${id}/ai-feedback`);
  return response.data;
};

export const getActivityRun = async (
  activityRunId: number
): Promise<import("../types/learningPath").ActivityRunDto> => {
  const response = await httpClient.get<import("../types/learningPath").ActivityRunDto>(
    `/students/me/activity-runs/${activityRunId}`
  );
  return response.data;
};

export const saveActivityDraft = async (
  activityRunId: number,
  request: import("../types/learningPath").SaveActivityDraftRequest
): Promise<import("../types/learningPath").SaveActivityDraftResponse> => {
  const response = await httpClient.put<import("../types/learningPath").SaveActivityDraftResponse>(
    `/students/me/activity-runs/${activityRunId}/draft`,
    request
  );
  return response.data;
};

export const requestActivityAssistance = async (
  activityRunId: number,
  request: import("../types/learningPath").RequestAssistanceRequest
): Promise<import("../types/learningPath").AssistanceResponseDto> => {
  const response = await httpClient.post<import("../types/learningPath").AssistanceResponseDto>(
    `/students/me/activity-runs/${activityRunId}/assistance`,
    request
  );
  return response.data;
};

export const submitActivity = async (
  activityRunId: number,
  request: import("../types/learningPath").SubmitActivityRequest
): Promise<import("../types/learningPath").ActivitySubmissionResponseDto> => {
  const response = await httpClient.post<import("../types/learningPath").ActivitySubmissionResponseDto>(
    `/students/me/activity-runs/${activityRunId}/submissions`,
    request
  );
  return response.data;
};

export const evaluateMissionRunV4 = async (
  sessionRunId: number
): Promise<import("../types/learningPath").V4SessionEvaluationResultDto> => {
  const response = await httpClient.post<import("../types/learningPath").V4SessionEvaluationResultDto>(
    `/students/me/mission-runs/${sessionRunId}/evaluate-v4`
  );
  return response.data;
};
