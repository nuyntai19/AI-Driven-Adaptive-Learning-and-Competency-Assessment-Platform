import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getLearningAgenda } from "../api/learningPathApi";
import { listQuestionBookmarks } from "../api/questionBookmarksApi";
import { useAuthStore } from "../stores/authStore";
import { useStudentLearningAccess } from "../hooks/useStudentLearningAccess";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { notebookAnswers, notebookFocusSkills, notebookPracticeUrl, type NotebookAnswerFilter } from "../utils/learningNotebook";
import { RichMathText } from "../components/math/RichMathText";
import { StudentReviewLibrary } from "../components/student/StudentReviewLibrary";
import { formatTimeDayMonth } from "../utils/dateInput";
import "../styles/student-notebook.css";

const VIEWS = [{ id: "overview", label: "Góc nhìn tiến bộ", icon: "◈" }, { id: "mistakes", label: "Câu cần xem lại", icon: "↺" }, { id: "saved", label: "Thư viện thẻ ôn", icon: "▤" }] as const;
type NotebookView = typeof VIEWS[number]["id"];
const FILTERS: { id: NotebookAnswerFilter; label: string }[] = [{ id: "incorrect", label: "Cần xem lại" }, { id: "pending", label: "Chưa có kết luận" }, { id: "correct", label: "Đã trả lời đúng" }, { id: "all", label: "Tất cả" }];

export function StudentNotebookPage() {
  const [params] = useSearchParams();
  const subjectId = params.get("subjectId") || "";
  return <StudentNotebookWorkspace key={subjectId} subjectId={subjectId} />;
}

function StudentNotebookWorkspace({ subjectId }: { subjectId: string }) {
  const [params, setParams] = useSearchParams();
  const user = useAuthStore(s => s.user);
  const canReadBookmarks = user?.permissions.includes("learning.attempts.submit") === true;
  const access = useStudentLearningAccess(subjectId);
  const requestedView = params.get("view");
  const view: NotebookView = VIEWS.find(item => item.id === requestedView)?.id ?? "overview";
  const [filter, setFilter] = useState<NotebookAnswerFilter>("incorrect");
  const [search, setSearch] = useState("");
  const agenda = useQuery({
    queryKey: ["learning-agenda", "notebook", user?.centerId, user?.userId, subjectId, access.classId],
    queryFn: () => getLearningAgenda(subjectId, undefined, access.classId || undefined),
    enabled: !!user && access.writable && !!access.classId,
    staleTime: 60_000,
  });
  const bookmarks = useQuery({ queryKey: ["question-bookmarks", user?.centerId, user?.userId], queryFn: listQuestionBookmarks, enabled: !!user && canReadBookmarks && !!subjectId && !access.history, staleTime: 60_000, retry: false });
  const changeView = (nextView: NotebookView) => { const next = new URLSearchParams(params); next.set("view", nextView); setParams(next, { replace: true }); };
  const insights = agenda.data?.insights;
  const focus = insights ? notebookFocusSkills(insights).slice(0, 3) : [];
  const answers = insights ? notebookAnswers(insights, filter, search) : [];
  const answeredCorrectly = insights?.skills.reduce((sum, skill) => sum + skill.correctCount, 0) ?? 0;
  const answeredIncorrectly = insights?.skills.reduce((sum, skill) => sum + skill.wrongCount, 0) ?? 0;
  const known = answeredCorrectly + answeredIncorrectly;
  const accuracy = known ? Math.round(answeredCorrectly / known * 100) : null;
  const pathUrl = studentScopeUrl("/hoc-tap/lo-trinh-hoc-tap", params);

  return <div className="student-shell nb-workspace w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6">
    <header className="nb-masthead"><div><p className="nb-kicker">EDUTWIN / LEARNING JOURNAL</p><h1>Học từ điều đã làm.<br /><em>Tiến từ điều đã hiểu.</em></h1><p>Một nơi cho câu khó, điều vừa nhận ra và bước học tiếp theo của bạn.</p></div><div className="nb-notebook-art" aria-hidden="true"><span>FIELD<br />NOTES.</span><i>✦</i><b>01 — HÀNH TRÌNH CỦA BẠN</b></div></header>
    <nav className="nb-view-nav" aria-label="Các phần sổ tay">{VIEWS.map(item => <button type="button" key={item.id} aria-pressed={view === item.id} onClick={() => changeView(item.id)}><span aria-hidden="true">{item.icon}</span>{item.label}</button>)}</nav>

    {!subjectId ? <NotebookEmpty title="Chọn môn để mở sổ tay" detail="Dùng bộ chọn môn phía trên để xem đúng hành trình học của bạn." />
      : access.history ? <NotebookEmpty title="Bạn đang xem lớp trong lịch sử" detail="Sổ tay tổng hợp hành động cho lớp đang học. Dữ liệu lịch sử vẫn được giữ trong Hồ sơ năng lực." link={studentScopeUrl("/hoc-tap/ho-so-nang-luc", params)} linkLabel="Xem hồ sơ trong phạm vi này" />
      : access.pending ? <div className="nb-loading" role="status">Đang xác minh lớp và mở sổ tay của bạn…</div>
      : access.readOnly || !access.classId ? <NotebookEmpty title="Chưa mở được phạm vi học hiện tại" detail={access.classId ? access.reason : "Chọn lớp có giáo trình đang áp dụng để mở phân tích bài làm."} link={pathUrl} linkLabel="Kiểm tra lộ trình" />
      : view === "saved" ? !canReadBookmarks ? <NotebookEmpty title="Thư viện chưa được cấp quyền" detail="Tài khoản hiện chưa có quyền học tập cần thiết để đọc câu đã lưu. Liên hệ trung tâm để kiểm tra quyền." /> : bookmarks.isPending ? <div role="status" className="nb-loading">Đang mở thư viện thẻ ôn…</div> : bookmarks.isError ? <NotebookError onRetry={() => void bookmarks.refetch()} text="Chưa tải được câu đã lưu. Các thẻ của bạn vẫn được giữ trên hệ thống." /> : <StudentReviewLibrary key={`${user?.centerId}:${user?.userId}:${subjectId}`} subjectId={subjectId} bookmarks={bookmarks.data ?? []} />
      : agenda.isPending ? <div role="status" className="nb-loading">Đang kết nối các mảnh tiến bộ…</div>
      : agenda.isError ? <NotebookError onRetry={() => void agenda.refetch()} text="Chưa tải được dữ liệu bài làm. Hãy thử lại để xem đúng tiến trình của bạn." />
      : !insights ? <NotebookEmpty title="Sổ tay chưa có phân tích" detail="Mở lộ trình để bắt đầu. Sổ tay sẽ tổng hợp khi có bài làm được ghi nhận." link={pathUrl} linkLabel="Mở lộ trình" />
      : view === "overview" ? <>
        <section className="nb-overview-grid" aria-label="Tổng quan bài làm">
          <div className="nb-summary-card"><div><p className="nb-kicker">{insights.windowDays} NGÀY GẦN ĐÂY / BÀI LÀM THỰC TẾ</p><h2>Mỗi lần thử đều<br />để lại một điều.</h2><p>{insights.summary || "Chưa có bài làm trong khoảng thời gian này. Một buổi học ngắn là điểm bắt đầu tốt."}</p></div><div className="nb-score-ring" role="img" aria-label={accuracy == null ? "Chưa có câu đủ kết quả để tính tỷ lệ đúng" : `Đúng ${answeredCorrectly} trên ${known} câu đã có kết luận, ${accuracy}%`} style={{ background: `conic-gradient(#e7bd62 ${(accuracy ?? 0)}%, #ffffff20 0)` }}><div><strong>{accuracy == null ? "—" : `${accuracy}%`}</strong><span>câu trả lời đúng</span></div></div></div>
          <div className="nb-metric-grid"><NotebookMetric value={insights.answerCount} label="lượt trả lời" detail="Có thể gồm nhiều lượt trên một câu" /><NotebookMetric value={insights.gradedQuestionCount} label="câu khác nhau có kết quả" detail="Dùng kết quả mới nhất của mỗi câu" /><NotebookMetric value={answeredIncorrectly} label="câu cần xem lại" detail="Trong các câu đã có kết luận" /><NotebookMetric value={insights.pendingCount} label="lượt đang xử lý" detail="Không tính như câu trả lời sai" /></div>
        </section>
        <p className="nb-data-note">Tỷ lệ đúng phản ánh các câu đã có kết luận trong cửa sổ dữ liệu; không phải điểm thi, điểm cuối do giáo viên chốt, mức độ thành thạo hay bằng chứng tự làm độc lập.</p>
        <div className="nb-content-grid"><section className="nb-panel"><div className="nb-section-heading"><div><p className="nb-kicker">BƯỚC NHỎ TIẾP THEO</p><h2>Chọn đúng phần để tiến.</h2></div><span aria-hidden="true" className="nb-section-symbol">↗</span></div>
          {focus.length ? <div className="nb-focus-list">{focus.map((skill, i) => <article key={skill.skillId} className="nb-focus-item"><span className="nb-index">0{i + 1}</span><div><h3>{skill.skillName}</h3><p>{skill.observation}</p><div className="nb-skill-track" role="img" aria-label={`Đúng ${skill.correctCount}/${skill.questionCount} câu có kết quả`}><span style={{ width: `${Math.min(100, Math.max(0, skill.correctCount / skill.questionCount * 100))}%` }} /></div><span className="nb-skill-caption">Tỷ lệ đúng trên các câu đã chấm</span></div><Link className="nb-round-link" to={notebookPracticeUrl(params, skill.skillId, 10)} aria-label={`Luyện 10 phút chủ đề ${skill.skillName}`}>↗</Link></article>)}</div>
            : <div className="nb-empty"><span aria-hidden="true">✧</span><h3>{insights.answerCount ? "Chưa thấy phần sai rõ ràng" : "Chưa có bằng chứng để chọn phần yếu"}</h3><p>Tiếp tục lộ trình hoặc kiểm tra phần chưa học để có thêm dữ liệu phù hợp.</p><Link to={pathUrl} className="nb-outline-button">Chọn bước học tiếp theo →</Link></div>}
        </section><aside className="nb-editorial-note"><p className="nb-kicker">MỘT THÓI QUEN NHỎ</p><span aria-hidden="true">“</span><h2>Không cần học lại tất cả.<br /><em>Hãy hiểu chỗ mình vấp.</em></h2><p>Mở một câu cần xem lại, thử giải thích cách làm bằng lời của mình, rồi luyện thêm một phiên ngắn.</p><button type="button" className="nb-outline-button" onClick={() => changeView("mistakes")}>Mở câu cần xem lại ↗</button><button type="button" className="nb-text-link" onClick={() => changeView("saved")}>Hoặc ôn bằng thẻ đã lưu →</button></aside></div>
      </> : <section className="nb-panel nb-answer-workspace" aria-labelledby="notebook-answers-heading"><div className="nb-section-heading"><div><p className="nb-kicker">ĐỐI CHIẾU / HIỂU LẠI / THỬ LẠI</p><h2 id="notebook-answers-heading">Mỗi câu khó là một đầu mối.</h2><p className="nb-subtitle">Các tham chiếu mới nhất được hệ thống cung cấp, tối đa 5 câu mỗi kỹ năng trong {insights.windowDays} ngày.</p></div></div>
        <div className="nb-answer-controls"><div className="nb-filter-chips" aria-label="Lọc kết quả">{FILTERS.map(item => <button key={item.id} type="button" aria-pressed={filter === item.id} onClick={() => setFilter(item.id)}>{item.label}</button>)}</div><label className="nb-search-label"><span className="sr-only">Tìm câu hoặc kỹ năng</span><input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Tìm câu hoặc kỹ năng…" /></label></div>
        <p role="status" className="nb-match-count">{answers.length} câu trong danh sách tham chiếu khớp bộ lọc</p>
        {answers.length ? <div className="nb-answer-list">{answers.map(answer => <article key={answer.attemptId} className="nb-answer-card"><div className="nb-answer-meta"><span className={`nb-result-pill ${answer.isCorrect === false ? "needs-work" : answer.isCorrect === true ? "is-correct" : "is-pending"}`}>{answer.isCorrect === false ? "Cần xem lại" : answer.isCorrect === true ? "Đã trả lời đúng" : "Chưa có kết luận"}</span><span>{answer.skillName}</span><time dateTime={answer.submittedAt}>{formatTimeDayMonth(answer.submittedAt)}</time></div><div className="nb-answer-question"><RichMathText text={answer.questionText} /></div>{answer.guided && <p className="nb-guided-note">Câu này được làm sau phản hồi hoặc đã mở lời giải; không xem là bằng chứng độc lập.</p>}<details className="nb-reflection-prompt"><summary>Thử tự giải thích trước khi luyện lại</summary><ol><li>Mình đã dùng quy tắc hoặc kiến thức nào?</li><li>Bước nào mình chưa giải thích được?</li><li>Nếu đổi dữ kiện, mình có làm lại được không?</li></ol></details><Link to={notebookPracticeUrl(params, answer.skillId, 10)} className="nb-text-link">Luyện lại chủ đề này · 10 phút ↗</Link></article>)}</div>
          : <div className="nb-empty"><span aria-hidden="true">◎</span><h3>Chưa có câu khớp bộ lọc này</h3><p>{answeredIncorrectly && filter === "incorrect" ? "Có câu cần xem lại trong tổng hợp nhưng chúng có thể nằm ngoài danh sách tham chiếu mới nhất. Chọn chủ đề ở Góc nhìn tiến bộ để luyện thêm." : "Thử đổi bộ lọc, bỏ từ khóa hoặc quay lại sau một buổi học."}</p><button type="button" className="nb-outline-button" onClick={() => { setSearch(""); setFilter("all"); }}>Xem mọi câu được cung cấp</button></div>}
      </section>}
  </div>;
}

function NotebookMetric({ value, label, detail }: { value: number; label: string; detail: string }) {
  return <div className="nb-metric"><strong>{value}</strong><span>{label}</span><small>{detail}</small></div>;
}
function NotebookEmpty({ title, detail, link, linkLabel }: { title: string; detail: string; link?: string; linkLabel?: string }) {
  return <section className="nb-panel nb-empty"><span aria-hidden="true">✧</span><h2>{title}</h2><p>{detail}</p>{link && <Link to={link} className="nb-outline-button">{linkLabel} →</Link>}</section>;
}
function NotebookError({ text, onRetry }: { text: string; onRetry: () => void }) {
  return <div className="nb-panel nb-empty" role="alert"><h2>Chưa thể mở dữ liệu</h2><p>{text}</p><button type="button" className="nb-outline-button" onClick={onRetry}>Thử tải lại</button></div>;
}
