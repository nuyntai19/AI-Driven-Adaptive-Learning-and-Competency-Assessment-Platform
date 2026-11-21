/** Wall-clock deadlines keep the timer accurate when a browser tab is suspended. */
export function remainingFocusSeconds(deadline: number, now: number): number {
  return Math.max(0, Math.ceil((deadline - now) / 1000));
}

export function formatFocusTime(seconds: number): string {
  const safe = Math.max(0, Math.floor(seconds));
  return `${Math.floor(safe / 60).toString().padStart(2, "0")}:${(safe % 60).toString().padStart(2, "0")}`;
}

/** Next occurrence in the student's local timezone, including calendar/DST changes. */
export function nextFocusAlarmDeadline(time: string, now: number): number | null {
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time) || !Number.isFinite(now)) return null;
  const [hours, minutes] = time.split(":").map(Number);
  const target = new Date(now);
  target.setHours(hours, minutes, 0, 0);
  if (target.getTime() <= now) {
    target.setDate(target.getDate() + 1);
    target.setHours(hours, minutes, 0, 0);
  }
  return target.getTime();
}

export interface FocusTimerState {
  minutes: number;
  remaining: number;
  totalSeconds: number;
  deadline: number | null;
  finished: boolean;
  mode: "duration" | "alarm";
  alarmTime: string;
  soundEnabled: boolean;
  acknowledged: boolean;
}

export const initialFocusTimer: FocusTimerState = {
  minutes: 25, remaining: 1500, totalSeconds: 1500, deadline: null,
  finished: false, mode: "duration", alarmTime: "", soundEnabled: true, acknowledged: false,
};

export function restoreFocusTimer(value: unknown, now: number): FocusTimerState {
  if (!value || typeof value !== "object") return { ...initialFocusTimer };
  const saved = value as Partial<FocusTimerState>;
  const total = saved.totalSeconds ?? (saved.minutes ?? 0) * 60;
  if (![10, 25, 45].includes(saved.minutes ?? 0) || !Number.isInteger(total) || total <= 0 || total > 26 * 3600 ||
      !Number.isInteger(saved.remaining) || saved.remaining! < 0 || saved.remaining! > total ||
      (saved.deadline !== null && !Number.isFinite(saved.deadline)) || typeof saved.finished !== "boolean")
    return { ...initialFocusTimer };
  const mode = saved.mode === "alarm" ? "alarm" : "duration";
  if (mode === "alarm" && nextFocusAlarmDeadline(saved.alarmTime ?? "", now) === null)
    return { ...initialFocusTimer };
  const remaining = saved.deadline === null ? saved.remaining! : remainingFocusSeconds(saved.deadline!, now);
  if (remaining > total) return { ...initialFocusTimer };
  return {
    ...initialFocusTimer, minutes: saved.minutes!, totalSeconds: total, remaining,
    deadline: remaining > 0 ? saved.deadline! : null,
    finished: saved.deadline !== null ? remaining === 0 : saved.finished,
    mode, alarmTime: saved.alarmTime ?? "", soundEnabled: saved.soundEnabled !== false,
    acknowledged: saved.acknowledged === true,
  };
}
