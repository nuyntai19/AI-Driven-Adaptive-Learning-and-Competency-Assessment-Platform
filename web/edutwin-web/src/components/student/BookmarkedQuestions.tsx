import { RichMathText } from "../math/RichMathText";
import { useQuestionBookmarks } from "../../utils/useQuestionBookmarks";

/** Marked questions the student has answered, ready to study again. */
export function BookmarkedQuestions({ subjectId }: { subjectId: string }) {
  const { bookmarks, toggleBookmark } = useQuestionBookmarks();
  const answered = bookmarks.filter((b) => b.answered && b.subjectId === subjectId);
  if (answered.length === 0) return null;

  return (
    <section aria-labelledby="bookmarks-heading" className="rounded-2xl border border-slate-200 bg-white p-4 sm:p-5 dark:border-slate-800 dark:bg-slate-900">
      <h2 id="bookmarks-heading" className="text-base font-bold text-slate-900 dark:text-white">Câu bạn đã đánh dấu</h2>
      <ul className="mt-2 divide-y divide-slate-100 dark:divide-slate-800">
        {answered.map((b) => (
          <li key={b.questionId} className="py-3">
            <details>
              <summary className="cursor-pointer text-sm font-semibold text-slate-800 dark:text-slate-200">
                <RichMathText text={b.questionText} />
                {b.topicName && <span className="ml-2 text-xs font-normal text-slate-500 dark:text-slate-400">{b.topicName}</span>}
              </summary>
              <div className="mt-2 space-y-2 text-sm text-slate-700 dark:text-slate-300">
                {b.correctAnswer ? <p>Đáp án: <strong><RichMathText text={b.correctAnswer} /></strong></p>
                  : <p className="text-slate-500 dark:text-slate-400">Đáp án sẽ hiện sau khi bài tập chứa câu này kết thúc.</p>}
                {b.solution && <div className="whitespace-pre-wrap"><RichMathText text={b.solution} /></div>}
                <button type="button" onClick={() => toggleBookmark(b.questionId)} className="text-xs font-semibold text-slate-500 hover:text-slate-800 cursor-pointer dark:text-slate-400 dark:hover:text-slate-200">
                  Bỏ đánh dấu
                </button>
              </div>
            </details>
          </li>
        ))}
      </ul>
    </section>
  );
}
