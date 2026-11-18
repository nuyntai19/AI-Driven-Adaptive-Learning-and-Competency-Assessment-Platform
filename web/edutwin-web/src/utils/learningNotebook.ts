import type { LearningInsightsDto } from "../types/learningPath";
import type { QuestionBookmarkDto } from "../api/questionBookmarksApi";

export type NotebookAnswerFilter = "all" | "incorrect" | "pending" | "correct";
export function notebookSearchText(value: string): string {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase().trim();
}

/** These are the server's latest references, not every attempt or a new diagnosis. */
export function notebookAnswers(insights: LearningInsightsDto, filter: NotebookAnswerFilter, search: string) {
  const term = notebookSearchText(search);
  const seen = new Set<string>();
  return insights.skills.flatMap(skill => skill.recentAnswers.map(answer => ({ ...answer, skillId: skill.skillId, skillName: skill.skillName })))
    .filter(answer => {
      if (seen.has(answer.attemptId)) return false;
      seen.add(answer.attemptId);
      return (filter === "all" || (filter === "incorrect" && answer.isCorrect === false) ||
        (filter === "correct" && answer.isCorrect === true) || (filter === "pending" && answer.isCorrect == null)) &&
        (!term || notebookSearchText(`${answer.questionText} ${answer.skillName}`).includes(term));
    }).sort((a, b) => Date.parse(b.submittedAt) - Date.parse(a.submittedAt));
}

export function notebookFocusSkills(insights: LearningInsightsDto) {
  return insights.skills.filter(skill => skill.wrongCount > 0 && skill.questionCount > 0)
    .sort((a, b) => b.wrongCount / b.questionCount - a.wrongCount / a.questionCount || b.wrongCount - a.wrongCount).slice(0, 3);
}

/** Practice choices are URL state, never authorization or a request to start a session. */
export function notebookPracticeUrl(params: URLSearchParams, topicId: string, minutes = 10) {
  const query = new URLSearchParams();
  for (const key of ["subjectId", "classId", "history"]) { const value = params.get(key); if (value) query.set(key, value); }
  query.set("topicId", topicId);
  query.set("minutes", String(minutes));
  return `/hoc-tap/luyen-tap?${query}`;
}

export function reviewableBookmarks(bookmarks: QuestionBookmarkDto[], subjectId: string) {
  return bookmarks.filter(card => card.subjectId === subjectId && card.answered && !!card.questionText?.trim() && !!card.correctAnswer?.trim());
}

export interface RecallSession {
  queue: string[]; total: number; remembered: number; skipped: number; revealed: boolean;
}
export type RecallAction = { type: "start"; ids: string[] } | { type: "reveal" | "again" | "remember" | "skip" };
export const emptyRecallSession: RecallSession = { queue: [], total: 0, remembered: 0, skipped: 0, revealed: false };

/** Self-assessment stays inside this browser session; it never changes academic evidence. */
export function recallSessionReducer(state: RecallSession, action: RecallAction): RecallSession {
  if (action.type === "start") {
    const queue = [...new Set(action.ids)].slice(0, 10);
    return { ...emptyRecallSession, queue, total: queue.length };
  }
  if (!state.queue.length) return state;
  if (action.type === "reveal") return { ...state, revealed: true };
  if (action.type === "skip") return { ...state, queue: state.queue.slice(1), skipped: state.skipped + 1, revealed: false };
  if (!state.revealed) return state;
  if (action.type === "again") return { ...state, queue: [...state.queue.slice(1), state.queue[0]], revealed: false };
  return { ...state, queue: state.queue.slice(1), remembered: state.remembered + 1, revealed: false };
}
