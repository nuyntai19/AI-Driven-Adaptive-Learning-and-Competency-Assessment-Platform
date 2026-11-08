import { useQuestionBookmarks } from "../../utils/useQuestionBookmarks";

/** Lets a student flag a hard question and come back to it once the answer is available. */
export function BookmarkButton({ questionId }: { questionId: string | number }) {
  const { bookmarkedIds, toggleBookmark } = useQuestionBookmarks();
  const id = String(questionId);
  const marked = bookmarkedIds.includes(id);
  return (
    <button
      type="button"
      aria-pressed={marked}
      onClick={() => toggleBookmark(id)}
      title="Sau khi có đáp án, xem lại câu này trong trang Luyện tập thích ứng"
      className={`shrink-0 rounded-lg border px-2.5 py-1.5 text-xs font-semibold cursor-pointer transition-[background-color,color,transform] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none ${
        marked
          ? "border-amber-500 bg-amber-500 text-white hover:bg-amber-600"
          : "border-slate-300 text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
      }`}
    >
      {marked ? "Đã đánh dấu câu khó" : "Đánh dấu câu khó"}
    </button>
  );
}
