import { useEffect, useRef, useState } from "react";
import { formatFocusTime, remainingFocusSeconds } from "../../utils/focusTimer";
import { useModalAccessibility } from "../../utils/useModalAccessibility";

/** A personal timer, deliberately separate from submitted study evidence. */
export function StudentFocusStudio() {
  const [open, setOpen] = useState(false);
  const [minutes, setMinutes] = useState(25);
  const [remaining, setRemaining] = useState(25 * 60);
  const [deadline, setDeadline] = useState<number | null>(null);
  const [finished, setFinished] = useState(false);
  const panel = useRef<HTMLDivElement>(null);
  useModalAccessibility({ isOpen: open, onClose: () => setOpen(false), containerRef: panel });

  useEffect(() => {
    if (deadline === null) return;
    const tick = () => {
      const next = remainingFocusSeconds(deadline, Date.now());
      setRemaining(next);
      if (next === 0) { setDeadline(null); setFinished(true); }
    };
    tick();
    const timer = window.setInterval(tick, 500);
    return () => window.clearInterval(timer);
  }, [deadline]);

  const pause = () => {
    if (deadline !== null) setRemaining(remainingFocusSeconds(deadline, Date.now()));
    setDeadline(null);
  };
  const reset = (duration = minutes) => {
    setDeadline(null); setFinished(false); setMinutes(duration); setRemaining(duration * 60);
  };
  const progress = Math.min(100, (1 - remaining / (minutes * 60)) * 100);

  return <>
    <button type="button" className={`st-focus-launcher ${deadline !== null ? "is-running" : ""}`} onClick={() => setOpen(true)} aria-label={`Mở góc tập trung${deadline !== null ? `, còn ${formatFocusTime(remaining)}` : ""}`}>
      <svg aria-hidden="true" width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M9 2h6M12 7v5l3 2M18 5l2 2" /><circle cx="12" cy="14" r="8" /></svg>
      <span>{deadline !== null ? formatFocusTime(remaining) : "Góc tập trung"}</span>
    </button>
    {open && <div className="st-studio-overlay" onClick={() => setOpen(false)}>
      <div ref={panel} role="dialog" aria-modal="true" aria-labelledby="focus-studio-title" className="st-studio-panel" onClick={e => e.stopPropagation()} tabIndex={-1}>
        <div className="flex items-center justify-between gap-3">
          <p className="st-eyebrow">Một khoảng riêng để học</p>
          <button type="button" className="st-studio-close" onClick={() => setOpen(false)} aria-label="Thu gọn góc tập trung">×</button>
        </div>
        <h2 id="focus-studio-title" className="text-2xl font-extrabold">Từng phút, thêm một bước.</h2>
        <p className="mt-2 text-sm text-[var(--student-muted)]">Chọn một việc nhỏ. Dành trọn sự chú ý cho nó.</p>
        <div className="st-focus-durations" aria-label="Thời lượng tập trung">
          {[10, 25, 45].map(n => <button type="button" key={n} aria-pressed={minutes === n} disabled={deadline !== null} onClick={() => reset(n)}>{n} phút</button>)}
        </div>
        <div className="st-focus-clock" style={{ background: `conic-gradient(var(--student-brand) ${progress}%, var(--student-border) 0)` }}>
          <div><span className="st-focus-time">{formatFocusTime(remaining)}</span><span className="text-sm text-[var(--student-muted)]">{finished ? "Đã đến giờ nghỉ" : deadline !== null ? "Bạn đang tập trung" : "Sẵn sàng khi bạn muốn"}</span></div>
        </div>
        <p role="status" className="min-h-6 text-center text-sm text-[var(--student-ink-secondary)]">{finished ? "Hoàn thành khoảng tập trung. Hãy đứng dậy, uống nước và nghỉ một chút nhé." : "Bạn có thể thu gọn và tiếp tục học ở bất kỳ trang nào."}</p>
        <div className="mt-5 flex justify-center gap-3">
          <button type="button" className="st-btn-primary rounded-full px-6 py-3 font-bold" onClick={() => {
            if (deadline !== null) pause();
            else { const seconds = remaining || minutes * 60; setRemaining(seconds); setFinished(false); setDeadline(Date.now() + seconds * 1000); }
          }}>{deadline !== null ? "Tạm dừng" : finished ? "Bắt đầu lượt mới" : remaining < minutes * 60 ? "Tiếp tục" : "Bắt đầu tập trung"}</button>
          <button type="button" className="rounded-full border border-[var(--student-border)] px-4 py-3 font-semibold" onClick={() => reset()}>Đặt lại</button>
        </div>
        <p className="mt-5 text-center text-xs text-[var(--student-muted)]">Bộ đếm cá nhân trong phiên mở trang; không cộng điểm, chuỗi ngày hay bằng chứng năng lực.</p>
      </div>
    </div>}
  </>;
}
