import { useEffect, useState } from "react";

/** Live time left until a deadline: "2 ngày 4 giờ", or hh:mm:ss on the last day. */
export function DueCountdown({ dueAt }: { dueAt: string }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const seconds = Math.floor((new Date(dueAt).getTime() - now) / 1000);
  if (Number.isNaN(seconds)) return null;
  if (seconds <= 0) return <span>Đã đến hạn</span>;
  const days = Math.floor(seconds / 86400);
  if (days >= 1) return <span className="tabular-nums">{days} ngày {Math.floor((seconds % 86400) / 3600)} giờ</span>;
  const pad = (n: number) => String(n).padStart(2, "0");
  return <span className="tabular-nums">{pad(Math.floor(seconds / 3600))}:{pad(Math.floor((seconds % 3600) / 60))}:{pad(seconds % 60)}</span>;
}

export default DueCountdown;
