import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { addQuestionBookmark, listQuestionBookmarks, removeQuestionBookmark, type QuestionBookmarkDto } from "../api/questionBookmarksApi";
import { useAuthStore } from "../stores/authStore";

/** Hard-question marks of the signed-in student, kept on the server so they follow the account. */
export function useQuestionBookmarks() {
  const user = useAuthStore((state) => state.user);
  const queryClient = useQueryClient();
  const key = ["question-bookmarks", user?.centerId, user?.userId];
  const query = useQuery({ queryKey: key, queryFn: listQuestionBookmarks, enabled: !!user, staleTime: 60_000, retry: false });
  const bookmarks = query.data ?? [];

  const toggle = useMutation({
    mutationFn: ({ questionId, marked }: { questionId: string; marked: boolean }) =>
      marked ? removeQuestionBookmark(questionId) : addQuestionBookmark(questionId),
    onMutate: ({ questionId, marked }) => {
      const previous = queryClient.getQueryData<QuestionBookmarkDto[]>(key);
      queryClient.setQueryData<QuestionBookmarkDto[]>(key, (current = []) => marked
        ? current.filter((b) => b.questionId !== questionId)
        : [...current, { questionId, subjectId: "", answered: false, questionText: null, topicName: null, correctAnswer: null, solution: null, createdAt: new Date().toISOString() }]);
      return { previous };
    },
    onError: (_error, _variables, context) => queryClient.setQueryData(key, context?.previous),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: key }),
  });

  return {
    bookmarks,
    bookmarkedIds: bookmarks.map((b) => b.questionId),
    toggleBookmark: (questionId: string) => toggle.mutate({ questionId, marked: bookmarks.some((b) => b.questionId === questionId) }),
  };
}
