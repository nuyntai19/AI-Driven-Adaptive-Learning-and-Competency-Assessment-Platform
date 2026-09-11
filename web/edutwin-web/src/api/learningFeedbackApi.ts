import { httpClient } from "./httpClient";
import type {
  SubmitAttemptRequest,
  SubmitAttemptDataDto,
  AnalysisJobStatusDataDto,
  AttemptFeedbackDataDto,
  NextQuestionDataDto,
} from "../types/learning";

interface ApiResponse<T> {
  data: T;
  meta: {
    traceId?: string;
    timestamp: string;
  };
}

export const submitAttempt = async (
  body: SubmitAttemptRequest
): Promise<SubmitAttemptDataDto> => {
  const response = await httpClient.post<ApiResponse<SubmitAttemptDataDto>>(
    "/learning/attempts",
    body
  );
  return response.data.data;
};

export const getAnalysisJobStatus = async (
  jobId: string | number
): Promise<AnalysisJobStatusDataDto> => {
  const response = await httpClient.get<ApiResponse<AnalysisJobStatusDataDto>>(
    `/learning/analysis-jobs/${encodeURIComponent(jobId.toString())}`
  );
  return response.data.data;
};

export const getAttemptFeedback = async (
  attemptId: string | number
): Promise<AttemptFeedbackDataDto> => {
  const response = await httpClient.get<ApiResponse<AttemptFeedbackDataDto>>(
    `/learning/attempts/${encodeURIComponent(attemptId.toString())}/feedback`
  );
  return response.data.data;
};

export const getNextQuestion = async (
  subjectId: string
): Promise<NextQuestionDataDto> => {
  const response = await httpClient.get<ApiResponse<NextQuestionDataDto>>(
    "/learning/next-question",
    {
      params: { subjectId },
    }
  );
  return response.data.data;
};

export const acceptRecommendation = async (
  recommendationId: string
): Promise<void> => {
  await httpClient.post(`/recommendations/${encodeURIComponent(recommendationId)}/accept`);
};

export const dismissRecommendation = async (
  recommendationId: string
): Promise<void> => {
  await httpClient.post(`/recommendations/${encodeURIComponent(recommendationId)}/dismiss`);
};
