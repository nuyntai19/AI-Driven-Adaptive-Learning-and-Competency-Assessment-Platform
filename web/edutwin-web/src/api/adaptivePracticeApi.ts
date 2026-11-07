import { httpClient } from "./httpClient";
import type {
  AdaptivePracticeProposal,
  AdaptivePracticeMode,
  CreateAdaptivePracticeSessionRequest,
  AdaptivePracticeSession,
  AdaptivePracticeQuestion,
  AdaptivePracticeHint,
  SubmitAdaptivePracticeAnswerRequest,
  AdaptivePracticeAnswerFeedback,
  AdaptivePracticeSummary,
  AdaptivePracticeHistoryItem,
  TeacherAdaptivePracticeSession,
  TeacherAdaptivePracticeAlert,
  AdaptivePracticeCloseReason,
} from "../types/adaptivePractice";

export const adaptivePracticeApi = {
  getProposal: async (
    subjectId: string,
    customTopicId?: string,
    mode?: AdaptivePracticeMode
  ): Promise<AdaptivePracticeProposal> => {
    const response = await httpClient.get<AdaptivePracticeProposal>(
      "/adaptive-practice/proposal",
      {
        params: {
          subjectId,
          customTopicId: customTopicId || undefined,
          mode: mode || undefined,
        },
      }
    );
    return response.data;
  },

  getActiveSession: async (
    subjectId?: string
  ): Promise<AdaptivePracticeSession | null> => {
    const response = await httpClient.get<AdaptivePracticeSession | null>(
      "/adaptive-practice/active-session",
      {
        params: { subjectId: subjectId || undefined },
      }
    );
    return response.data;
  },

  getSessionById: async (
    sessionId: string
  ): Promise<AdaptivePracticeSession> => {
    const response = await httpClient.get<AdaptivePracticeSession>(
      `/adaptive-practice/sessions/${sessionId}`
    );
    return response.data;
  },

  createSession: async (
    request: CreateAdaptivePracticeSessionRequest
  ): Promise<AdaptivePracticeSession> => {
    const response = await httpClient.post<AdaptivePracticeSession>(
      "/adaptive-practice/sessions",
      request
    );
    return response.data;
  },

  resumeSession: async (
    sessionId: string
  ): Promise<AdaptivePracticeSession> => {
    const response = await httpClient.post<AdaptivePracticeSession>(
      `/adaptive-practice/sessions/${sessionId}/resume`
    );
    return response.data;
  },

  pauseSession: async (
    sessionId: string
  ): Promise<AdaptivePracticeSession> => {
    const response = await httpClient.post<AdaptivePracticeSession>(
      `/adaptive-practice/sessions/${sessionId}/pause`
    );
    return response.data;
  },

  closeSession: async (
    sessionId: string,
    closeReason: AdaptivePracticeCloseReason = "StudentStopped"
  ): Promise<AdaptivePracticeSummary> => {
    const response = await httpClient.post<AdaptivePracticeSummary>(
      `/adaptive-practice/sessions/${sessionId}/close`,
      null,
      { params: { closeReason } }
    );
    return response.data;
  },

  getNextQuestion: async (
    sessionId: string
  ): Promise<AdaptivePracticeQuestion | null> => {
    const response = await httpClient.get<AdaptivePracticeQuestion | null>(
      `/adaptive-practice/sessions/${sessionId}/next-question`
    );
    return response.data;
  },

  getHint: async (
    sessionId: string,
    sessionItemId: string,
    level: number = 1
  ): Promise<AdaptivePracticeHint> => {
    const response = await httpClient.get<AdaptivePracticeHint>(
      `/adaptive-practice/sessions/${sessionId}/hint`,
      {
        params: { sessionItemId, level },
      }
    );
    return response.data;
  },

  submitAnswer: async (
    sessionId: string,
    request: SubmitAdaptivePracticeAnswerRequest
  ): Promise<AdaptivePracticeAnswerFeedback> => {
    const response = await httpClient.post<AdaptivePracticeAnswerFeedback>(
      `/adaptive-practice/sessions/${sessionId}/submit`,
      request
    );
    return response.data;
  },

  getSummary: async (
    sessionId: string
  ): Promise<AdaptivePracticeSummary> => {
    const response = await httpClient.get<AdaptivePracticeSummary>(
      `/adaptive-practice/sessions/${sessionId}/summary`
    );
    return response.data;
  },

  getHistory: async (
    subjectId?: string,
    limit: number = 10
  ): Promise<AdaptivePracticeHistoryItem[]> => {
    const response = await httpClient.get<AdaptivePracticeHistoryItem[]>(
      "/adaptive-practice/history",
      {
        params: { subjectId: subjectId || undefined, limit },
      }
    );
    return response.data;
  },

  getTeacherSessions: async (
    classId?: string,
    subjectId?: string,
    limit: number = 20
  ): Promise<TeacherAdaptivePracticeSession[]> => {
    const response = await httpClient.get<TeacherAdaptivePracticeSession[]>(
      "/adaptive-practice/teacher/sessions",
      {
        params: { classId: classId || undefined, subjectId: subjectId || undefined, limit },
      }
    );
    return response.data;
  },

  getTeacherAlerts: async (
    classId?: string
  ): Promise<TeacherAdaptivePracticeAlert[]> => {
    const response = await httpClient.get<TeacherAdaptivePracticeAlert[]>(
      "/adaptive-practice/teacher/alerts",
      {
        params: { classId: classId || undefined },
      }
    );
    return response.data;
  },
};
