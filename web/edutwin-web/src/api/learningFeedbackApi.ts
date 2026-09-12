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
  const payload = {
    ...body,
    questionId: String(body.questionId),
  };
  const response = await httpClient.post<ApiResponse<SubmitAttemptDataDto>>(
    "/learning/attempts",
    payload
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
  interface NextQuestionApiDto {
    strategy: string;
    recommendationId?: string | null;
    topic: { nodeId: string; nodeName: string; mastery: number };
    question?: {
      questionId: string;
      questionType: string;
      difficulty: number;
      questionText: string;
      maxScore: number;
      estimatedTimeSeconds: number;
      reasoningRequired: boolean;
      languageCode: string;
      options: NextQuestionDataDto["options"];
    } | null;
    explanation: string;
  }

  const response = await httpClient.get<ApiResponse<NextQuestionApiDto>>(
    "/learning/next-question",
    {
      params: { subjectId },
    }
  );
  const data = response.data.data;
  if (!data.question) {
    throw new Error("Lộ trình hiện tại chưa có câu hỏi phù hợp.");
  }

  return {
    strategy: data.strategy,
    recommendationId: data.recommendationId,
    questionId: data.question.questionId,
    topicNodeId: data.topic.nodeId,
    topicName: data.topic.nodeName,
    topicMastery: data.topic.mastery,
    questionType: data.question.questionType,
    difficulty: data.question.difficulty,
    questionText: data.question.questionText,
    maxScore: data.question.maxScore,
    estimatedTimeSeconds: data.question.estimatedTimeSeconds,
    reasoningRequired: data.question.reasoningRequired,
    languageCode: data.question.languageCode,
    options: data.question.options ?? [],
    explanation: data.explanation,
  };
};

export const acceptRecommendation = async (
  recommendationId: string
): Promise<void> => {
  await httpClient.post(`/students/me/recommendation/${encodeURIComponent(recommendationId)}/accept`);
};

export const dismissRecommendation = async (
  recommendationId: string,
  reason?: string
): Promise<void> => {
  await httpClient.post(
    `/students/me/recommendation/${encodeURIComponent(recommendationId)}/dismiss`,
    { reason: reason?.trim() || null }
  );
};
