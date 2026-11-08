import { httpClient } from "./httpClient";

export interface QuestionBookmarkDto {
  questionId: string;
  subjectId: string;
  answered: boolean;
  questionText: string | null;
  topicName: string | null;
  correctAnswer: string | null;
  solution: string | null;
  createdAt: string;
}

const BASE = "/students/me/question-bookmarks";

export const listQuestionBookmarks = async (): Promise<QuestionBookmarkDto[]> => (await httpClient.get<QuestionBookmarkDto[]>(BASE)).data;
export const addQuestionBookmark = async (questionId: string): Promise<void> => { await httpClient.put(`${BASE}/${encodeURIComponent(questionId)}`); };
export const removeQuestionBookmark = async (questionId: string): Promise<void> => { await httpClient.delete(`${BASE}/${encodeURIComponent(questionId)}`); };
