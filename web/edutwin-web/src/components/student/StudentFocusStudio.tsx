import { useCallback, useEffect, useRef, useState } from "react";
import { formatFocusTime, initialFocusTimer, nextFocusAlarmDeadline, remainingFocusSeconds, restoreFocusTimer } from "../../utils/focusTimer";
import type { FocusTimerState } from "../../utils/focusTimer";
import { FocusAlarmAudio } from "../../utils/focusAlarmAudio";
import { useAuthStore } from "../../stores/authStore";
import { useModalAccessibility } from "../../utils/useModalAccessibility";

function readFocusTimer(key: string): FocusTimerState {
  try { return restoreFocusTimer(JSON.parse(window.sessionStorage.getItem(key) ?? "null"), Date.now()); }
  catch { return { ...initialFocusTimer }; }
}

export function StudentFocusStudio() {
  const user = useAuthStore(state => state.user);
  const storageKey = `edutwin:focus-timer:${user?.centerId}:${user?.userId}`;
  const [open, setOpen] = useState(false);
  const [timer, setTimer] = useState(() => readFocusTimer(storageKey));
  const { minutes, deadline, finished, mode, alarmTime, soundEnabled } = timer;
  const audio = useRef<FocusAlarmAudio | null>(null);
  const playedDeadline = useRef<number | null>(null);
  const [audioNotice, setAudioNotice] = useState("");
  const prepareSound = useCallback(async (preview = false) => {
    audio.current ??= new FocusAlarmAudio();
    const ready = preview ? await audio.current.play() : await audio.current.prepare();
    setAudioNotice(ready ? "" : "Trình duyệt chưa phát được âm thanh. Khi đến giờ, cảnh báo trên màn hình vẫn xuất hiện.");
  }, []);
  useEffect(() => () => { audio.current?.close(); }, []);
  const [clock, setClock] = useState(Date.now);
  const alarmDeadline = nextFocusAlarmDeadline(alarmTime, Date.now());
  const remaining = deadline !== null ? remainingFocusSeconds(deadline, clock)
    : mode === "alarm" && !finished && alarmDeadline !== null ? remainingFocusSeconds(alarmDeadline, Date.now()) : timer.remaining;
  const alarmLabel = (deadline ?? alarmDeadline) === null ? "" : new Date((deadline ?? alarmDeadline)!).toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric" });
  useEffect(() => {
    try { window.sessionStorage.setItem(storageKey, JSON.stringify(timer)); }
    catch { /* The timer still works when storage is unavailable. */ }
  }, [storageKey, timer]);
  const panel = useRef<HTMLDivElement>(null);
  useModalAccessibility({ isOpen: open, onClose: () => setOpen(false), containerRef: panel });

  useEffect(() => {
    if (deadline === null) return;
    const tick = () => {
      const now = Date.now();
      setClock(now);
      if (remainingFocusSeconds(deadline, now) === 0 && playedDeadline.current !== deadline) {
        playedDeadline.current = deadline;
        if (soundEnabled) void prepareSound(true);
        setTimer(current => current.deadline === deadline ? { ...current, deadline: null, remaining: 0, finished: true, acknowledged: false } : current);
      }
    };
    tick();
    const interval = window.setInterval(tick, 500);
    window.addEventListener("focus", tick);
    document.addEventListener("visibilitychange", tick);
    return () => {
      window.clearInterval(interval);
      window.removeEventListener("focus", tick);
      document.removeEventListener("visibilitychange", tick);
    };
  }, [deadline, soundEnabled, prepareSound]);

  const reset = (duration = minutes) => setTimer(current => ({ ...current, minutes: duration, totalSeconds: duration * 60, remaining: duration * 60, deadline: null, finished: false, acknowledged: false }));
  const progress = mode === "alarm" && deadline === null && !finished ? 0 : Math.min(100, Math.max(0, (1 - remaining / timer.totalSeconds) * 100));
  const completionMessage = mode === "alarm" ? `Đã đến giờ báo thức ${alarmTime}. Hãy nghỉ một chút nhé.` : "Hoàn thành khoảng tập trung. Hãy đứng dậy, uống nước và nghỉ một chút nhé.";

  return <>
    <button type="button" className={`st-focus-launcher ${deadline !== null ? "is-running" : ""}`} onClick={() => setOpen(true)} aria-label={`Mở góc tập trung${deadline !== null ? `, còn ${formatFocusTime(remaining)}` : ""}`}>
      <svg aria-hidden="true" width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M9 2h6M12 7v5l3 2M18 5l2 2" /><circle cx="12" cy="14" r="8" /></svg>
      <span>{deadline !== null ? formatFocusTime(remaining) : "Góc tập trung"}</span>
    </button>
    {finished && !timer.acknowledged && !open && <div role="alert" className="st-focus-alert">
      <strong>🔔 {mode === "alarm" ? "Đã đến giờ báo thức" : "Đã đến giờ nghỉ"}</strong>
      <p>{completionMessage}</p>
      <div className="flex gap-3 mt-2">
        <button type="button" className="font-bold underline" onClick={() => setOpen(true)}>Mở góc tập trung</button>
        <button type="button" className="underline" onClick={() => setTimer(current => ({ ...current, acknowledged: true }))}>Đóng cảnh báo</button>
      </div>
    </div>}
    {open && <div className="st-studio-overlay" onClick={() => setOpen(false)}>
      <div ref={panel} role="dialog" aria-modal="true" aria-labelledby="focus-studio-title" className="st-studio-panel" onClick={e => e.stopPropagation()} tabIndex={-1}>
        <div className="flex items-center justify-between gap-3">
          <p className="st-eyebrow">Một khoảng riêng để học</p>
          <button type="button" className="st-studio-close" onClick={() => setOpen(false)} aria-label="Thu gọn góc tập trung">×</button>
        </div>
        <h2 id="focus-studio-title" className="text-2xl font-extrabold">Từng phút, thêm một bước.</h2>
        <p className="mt-2 text-sm text-[var(--student-muted)]">Chọn một việc nhỏ. Dành trọn sự chú ý cho nó.</p>
        <div className="st-focus-durations" role="group" aria-label="Cách đặt giờ">
          <button type="button" aria-pressed={mode === "duration"} disabled={deadline !== null} onClick={() => setTimer(current => ({ ...current, mode: "duration", totalSeconds: minutes * 60, remaining: minutes * 60, finished: false, acknowledged: false }))}>Đếm ngược</button>
          <button type="button" aria-pressed={mode === "alarm"} disabled={deadline !== null} onClick={() => setTimer(current => ({ ...current, mode: "alarm", finished: false, acknowledged: false }))}>Giờ báo thức</button>
        </div>
        {mode === "alarm" ? <div className="st-focus-alarm-setting">
          <label htmlFor="focus-alarm-time">Báo thức lúc (giờ trên thiết bị)</label>
          <input id="focus-alarm-time" type="time" value={alarmTime} disabled={deadline !== null} onChange={e => setTimer(current => ({ ...current, alarmTime: e.target.value, finished: false, acknowledged: false }))} aria-describedby="focus-alarm-date" />
          <p id="focus-alarm-date" className="mt-2 text-sm">{finished ? `Đã báo lúc ${alarmTime}.` : alarmDeadline === null ? "Chọn giờ và phút bạn muốn được nhắc." : `Sẽ báo lúc ${alarmLabel}${deadline === null && new Date(alarmDeadline).getDate() !== new Date().getDate() ? " (ngày mai)" : ""}.`}</p>
        </div> : <div className="st-focus-durations" aria-label="Thời lượng tập trung">
          {[10, 25, 45].map(n => <button type="button" key={n} aria-pressed={minutes === n} disabled={deadline !== null} onClick={() => reset(n)}>{n} phút</button>)}
        </div>}
        <div className="st-focus-sound-setting">
          <label><input type="checkbox" checked={soundEnabled} onChange={e => { const enabled = e.target.checked; setTimer(current => ({ ...current, soundEnabled: enabled })); if (enabled) void prepareSound(); }} /> Phát chuông khi đến giờ</label>
          <button type="button" onClick={() => void prepareSound(true)}>Nghe thử chuông</button>
        </div>
        {audioNotice && <p role="status" className="mt-2 text-sm">{audioNotice}</p>}
        <div className="st-focus-clock" style={{ background: `conic-gradient(var(--student-brand) ${progress}%, var(--student-border) 0)` }}>
          <div><span className="st-focus-time">{mode === "alarm" && alarmDeadline === null ? "--:--" : formatFocusTime(remaining)}</span><span className="text-sm text-[var(--student-muted)]">{finished ? mode === "alarm" ? "Đã đến giờ báo thức" : "Đã đến giờ nghỉ" : deadline !== null ? mode === "alarm" ? `Báo lúc ${alarmTime}` : "Bạn đang tập trung" : "Sẵn sàng khi bạn muốn"}</span></div>
        </div>
        <p role={finished ? "alert" : "status"} className="min-h-6 text-center text-sm text-[var(--student-ink-secondary)]">{finished ? completionMessage : "Bạn có thể thu gọn và tiếp tục học ở bất kỳ trang nào."}</p>
        <div className="mt-5 flex justify-center gap-3">
          <button type="button" className="st-btn-primary rounded-full px-6 py-3 font-bold" disabled={mode === "alarm" && deadline === null && alarmDeadline === null} onClick={() => {
            const now = Date.now();
            if (deadline !== null) {
              if (mode === "alarm") { reset(); return; }
              const seconds = remainingFocusSeconds(deadline, now);
              setTimer(current => ({ ...current, deadline: null, remaining: seconds, finished: seconds === 0 }));
            } else {
              const target = mode === "alarm" ? nextFocusAlarmDeadline(alarmTime, now) : null;
              if (mode === "alarm" && target === null) return;
              if (soundEnabled) void prepareSound();
              const seconds = target !== null ? remainingFocusSeconds(target, now) : remaining || minutes * 60;
              playedDeadline.current = null;
              setClock(now);
              setTimer(current => ({ ...current, remaining: seconds, totalSeconds: mode === "alarm" ? seconds : minutes * 60, deadline: target ?? now + seconds * 1000, finished: false, acknowledged: false }));
            }
          }}>{mode === "alarm" ? deadline !== null ? "Hủy báo thức" : "Đặt báo thức" : deadline !== null ? "Tạm dừng" : finished ? "Bắt đầu lượt mới" : remaining < minutes * 60 ? "Tiếp tục" : "Bắt đầu tập trung"}</button>
          <button type="button" className="rounded-full border border-[var(--student-border)] px-4 py-3 font-semibold" onClick={() => reset()}>Đặt lại</button>
        </div>
        <p className="mt-5 text-center text-xs text-[var(--student-muted)]">Giữ tab mở để nhận chuông và cảnh báo. Bộ đếm không cộng điểm, chuỗi ngày hay bằng chứng năng lực.</p>
      </div>
    </div>}
  </>;
}
