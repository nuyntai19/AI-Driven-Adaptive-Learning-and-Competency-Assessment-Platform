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

export interface SubmitAttemptHttpResult {
  data: SubmitAttemptDataDto;
  /** The draft can be cleared only after the server accepted or replayed submission. */
  status: number;
}

export const submitAttempt = async (
  body: SubmitAttemptRequest
): Promise<SubmitAttemptHttpResult> => {
  const payload = {
    ...body,
    questionId: String(body.questionId),
  };
  const response = await httpClient.post<ApiResponse<SubmitAttemptDataDto>>(
    "/learning/attempts",
    payload
  );
  return { data: response.data.data, status: response.status };
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
      answerEvaluationMode?: string;
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
    recommendationId:
      data.recommendationId == null ? null : String(data.recommendationId),
    questionId: String(data.question.questionId),
    topicNodeId: String(data.topic.nodeId),
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
    answerEvaluationMode:
      data.question.answerEvaluationMode ||
      (data.question.questionType === "Essay" ? "Manual" : "TextExact"),
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
