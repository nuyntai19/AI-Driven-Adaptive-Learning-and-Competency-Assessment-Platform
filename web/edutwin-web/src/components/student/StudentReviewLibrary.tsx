import { useMemo, useReducer, useState } from "react";
import type { QuestionBookmarkDto } from "../../api/questionBookmarksApi";
import { emptyRecallSession, notebookSearchText, recallSessionReducer, reviewableBookmarks } from "../../utils/learningNotebook";
import { RichMathText } from "../math/RichMathText";

export function StudentReviewLibrary({ bookmarks, subjectId }: { bookmarks: QuestionBookmarkDto[]; subjectId: string }) {
  const [search, setSearch] = useState("");
  const [topic, setTopic] = useState("");
  const [session, dispatch] = useReducer(recallSessionReducer, emptyRecallSession);
  const [studying, setStudying] = useState(false);
  const saved = bookmarks.filter(card => card.subjectId === subjectId && card.answered && card.questionText);
  const topics = [...new Set(saved.map(card => card.topicName).filter((name): name is string => !!name))];
  const visible = saved.filter(card => (!topic || card.topicName === topic) && notebookSearchText(`${card.questionText} ${card.topicName}`).includes(notebookSearchText(search)));
  const eligible = reviewableBookmarks(visible, subjectId);
  const byId = useMemo(() => new Map(saved.map(card => [card.questionId, card])), [saved]);
  const card = byId.get(session.queue[0]);
  const complete = studying && session.total > 0 && !session.queue.length;
  const start = () => { dispatch({ type: "start", ids: eligible.map(item => item.questionId) }); setStudying(true); };

  return <div className="nb-library">
    <section className="nb-review-studio" aria-labelledby="review-studio-heading">
      <div className="nb-review-title"><div><p className="nb-kicker">RECALL STUDIO / NHỚ LẠI CHỦ ĐỘNG</p><h2 id="review-studio-heading">Đừng chỉ đọc lại.<br /><em>Thử nhớ lại.</em></h2></div><span className="nb-review-flower" aria-hidden="true">✳</span></div>
      {!studying ? <><p className="nb-review-description">Một lượt tối đa 10 thẻ. Tự trả lời trước, đối chiếu sau. Thẻ chưa nhớ sẽ quay lại ở cuối lượt.</p><button type="button" className="nb-light-button" disabled={!eligible.length} onClick={start}>Ôn {Math.min(10, eligible.length)} thẻ theo bộ lọc <span aria-hidden="true">↗</span></button><p className="nb-small-print">Chỉ dùng câu đã trả lời và có đáp án được công bố.</p></>
        : complete ? <div className="nb-session-complete" role="status"><span aria-hidden="true">✓</span><h3>Bạn vừa dành thời gian cho điều khó.</h3><p>Tự đánh giá đã nhớ: <b>{session.remembered}</b> thẻ · Bỏ qua: <b>{session.skipped}</b> thẻ.</p><button type="button" className="nb-light-button" disabled={!eligible.length} onClick={start}>Ôn một lượt nữa</button><button type="button" className="nb-quiet-button" onClick={() => setStudying(false)}>Về thư viện</button></div>
        : card ? <div className="nb-recall-session">
          <div className="nb-session-meta"><span>{card.topicName || "Thẻ của bạn"}</span><span>{session.remembered + session.skipped}/{session.total} thẻ đã khép lại</span></div>
          <progress max={session.total} value={session.remembered + session.skipped} aria-label="Tiến độ lượt ôn" />
          <div className="nb-flashcard"><RichMathText text={card.questionText} /></div>
          {session.revealed && <div className="nb-flashcard-answer"><p>ĐÁP ÁN ĐỐI CHIẾU</p><RichMathText text={card.correctAnswer} />{card.solution && <div className="mt-3 whitespace-pre-wrap"><RichMathText text={card.solution} /></div>}</div>}
          <div className="nb-review-actions">{!session.revealed ? <button type="button" className="nb-light-button" onClick={() => dispatch({ type: "reveal" })}>Mình đã thử trả lời · Mở đáp án</button> : <><button type="button" className="nb-light-button" onClick={() => dispatch({ type: "remember" })}>Mình đã nhớ ✓</button><button type="button" className="nb-quiet-button" onClick={() => dispatch({ type: "again" })}>Ôn lại cuối lượt ↺</button></>}<button type="button" className="nb-quiet-button" onClick={() => dispatch({ type: "skip" })}>Bỏ qua thẻ</button><button type="button" className="nb-quiet-button" onClick={() => setStudying(false)}>Kết thúc lượt ôn</button></div>
        </div> : <div role="status"><p>Thẻ này không còn trong danh sách đã tải. Hãy chọn lại bộ thẻ.</p><button type="button" className="nb-light-button" onClick={() => setStudying(false)}>Về thư viện</button></div>}
      <p className="nb-small-print">Tự đánh giá trong lượt ôn hiện tại; không thay đổi điểm, chuỗi học hay năng lực chính thức.</p>
    </section>
    <section className="nb-panel" aria-labelledby="saved-library-heading">
      <div className="nb-section-heading"><div><p className="nb-kicker">BỘ SƯU TẬP KIẾN THỨC</p><h2 id="saved-library-heading">Những câu bạn muốn giữ lại</h2></div><span className="nb-counter">{saved.length} câu</span></div>
      <div className="nb-filters"><label><span>Tìm trong câu đã lưu</span><input value={search} onChange={e => setSearch(e.target.value)} placeholder="Nội dung hoặc chủ đề…" type="search" /></label><label><span>Chủ đề</span><select value={topic} onChange={e => setTopic(e.target.value)}><option value="">Tất cả chủ đề đã lưu</option>{topics.map(name => <option key={name}>{name}</option>)}</select></label></div>
      {!visible.length ? <div className="nb-empty"><span aria-hidden="true">◇</span><h3>{saved.length ? "Chưa có câu khớp bộ lọc" : "Sổ tay đang chờ câu đầu tiên"}</h3><p>{saved.length ? "Thử từ khóa ngắn hơn hoặc chọn một chủ đề khác." : "Đánh dấu câu khi làm bài. Câu đã trả lời sẽ xuất hiện ở đây để bạn xem lại."}</p>{saved.length > 0 && <button type="button" className="nb-outline-button" onClick={() => { setSearch(""); setTopic(""); }}>Xóa bộ lọc</button>}</div>
        : <div className="nb-saved-list">{visible.map(item => <details key={item.questionId} className="nb-saved-card"><summary><span className="nb-topic-label">{item.topicName || "Câu đã lưu"}</span><span className="nb-question-preview"><RichMathText text={item.questionText} /></span><span className="nb-text-link">Mở thẻ +</span></summary><div className="nb-saved-answer">{item.correctAnswer ? <><p className="nb-kicker">ĐÁP ÁN ĐƯỢC CÔNG BỐ</p><RichMathText text={item.correctAnswer} /></> : <p>Đáp án chưa được công bố. Hãy quay lại sau khi bài tập kết thúc.</p>}{item.correctAnswer && item.solution && <div className="mt-3 whitespace-pre-wrap"><RichMathText text={item.solution} /></div>}</div></details>)}</div>}
    </section>
  </div>;
}
