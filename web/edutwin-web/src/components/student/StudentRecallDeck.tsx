import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listQuestionBookmarks } from "../../api/questionBookmarksApi";
import { useAuthStore } from "../../stores/authStore";
import { RichMathText } from "../math/RichMathText";

/** Practice recall using only answer material already released by the server. */
export function StudentRecallDeck({ subjectId }: { subjectId: string }) {
  const user = useAuthStore(s => s.user);
  const query = useQuery({ queryKey: ["question-bookmarks", user?.centerId, user?.userId], queryFn: listQuestionBookmarks, enabled: !!user, staleTime: 60_000, retry: false });
  const cards = (query.data ?? []).filter(q => q.subjectId === subjectId && q.answered && q.questionText);
  const [index, setIndex] = useState(0);
  const [revealedId, setRevealedId] = useState<string | null>(null);
  const card = cards[index % (cards.length || 1)];
  return <section className="st-card st-recall" aria-labelledby="recall-heading">
    <div className="flex items-start justify-between gap-3">
      <div><p className="st-eyebrow">Nhớ lại trước khi xem</p><h2 id="recall-heading" className="mt-1 text-lg font-bold">Góc ôn của riêng bạn</h2></div>
      <span className="st-recall-mark" aria-hidden="true">↺</span>
    </div>
    {query.isPending ? <p role="status" className="mt-4 text-sm">Đang mở các câu đã lưu…</p>
      : query.isError ? <div className="mt-4"><p className="text-sm">Chưa tải được câu đã lưu.</p><button type="button" className="st-text-action" onClick={() => void query.refetch()}>Thử lại</button></div>
      : card ? <>
        <div className="mt-4 flex justify-between gap-2 text-xs text-[var(--student-muted)]"><span>{card.topicName || "Câu hỏi đã đánh dấu"}</span><span>{index % cards.length + 1}/{cards.length}</span></div>
        <div className="st-recall-question"><RichMathText text={card.questionText} /></div>
        <p className="text-xs text-[var(--student-muted)]">Thử tự trả lời hoặc viết ra nháp rồi đối chiếu.</p>
        {revealedId === card.questionId && <div className="st-recall-answer">
          {card.correctAnswer ? <><p className="mb-2 text-xs font-bold">Đáp án đối chiếu</p><RichMathText text={card.correctAnswer} /></> : <p>Đáp án chưa được công bố. Bạn có thể xem lại sau khi bài tập kết thúc.</p>}
          {card.solution && <div className="mt-3 whitespace-pre-wrap"><RichMathText text={card.solution} /></div>}
        </div>}
        <div className="mt-4 flex flex-wrap gap-3">
          <button type="button" aria-expanded={revealedId === card.questionId} className="st-text-action" onClick={() => setRevealedId(revealedId === card.questionId ? null : card.questionId)}>{revealedId === card.questionId ? "Ẩn đáp án" : "Đối chiếu đáp án"}</button>
          {cards.length > 1 && <button type="button" className="st-text-action" onClick={() => { setIndex(i => i + 1); setRevealedId(null); }}>Câu tiếp theo →</button>}
        </div>
      </> : <div className="st-recall-empty"><span aria-hidden="true">✧</span><p className="font-semibold">Biến câu khó thành điều bạn nhớ.</p><p className="mt-2 text-sm text-[var(--student-ink-secondary)]">Đánh dấu câu hỏi khi làm bài. Các câu đã trả lời của môn này sẽ trở thành thẻ ôn tại đây.</p></div>}
  </section>;
}
