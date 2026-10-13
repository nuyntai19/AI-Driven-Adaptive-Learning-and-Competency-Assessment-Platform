import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { notificationsApi } from "../../api/notificationsApi";
import { useAuthStore } from "../../stores/authStore";
import { notificationBadge, notificationPopoverPosition, notificationRoutes, notificationPreviewStorageKey, notificationPreviewVisible, notificationPreviewWatermark, notificationPreviewActions } from "../../utils/notificationHelpers";
import { useNotifications } from "./useNotifications";
import { NotificationIcon } from "./NotificationIcon";
import "./notificationSurfaces.css";

export function NotificationBell() {
  const user = useAuthStore((s) => s.user);
  return <OwnBell key={`${user?.centerId}:${user?.userId}`} />;
}
function OwnBell() {
  const { user, key, access, summary, read } = useNotifications();
  const storageKey = user ? notificationPreviewStorageKey(user.centerId, user.userId) : "";
  const [dismissedThrough, setDismissedThrough] = useState<string | null>(() => {
    try { return localStorage.getItem(storageKey); } catch { return null; }
  });
  const [storageError, setStorageError] = useState(false);
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({ width: 400, left: 12, top: 72, maxHeight: 500 });
  const trigger = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const preview = useQuery({ queryKey: [...key, "preview"],
    queryFn: ({ signal }) => notificationsApi.list(1, 5, false, signal), enabled: open && access.read, staleTime: 0, retry: false });
  const items = preview.data?.data.filter(item => notificationPreviewVisible(item.deliveryId, dismissedThrough)) ?? [];
  const unreadIds = items.filter(item => !item.readAt).map(item => item.deliveryId);
  const actions = notificationPreviewActions(items.length, unreadIds.length, access.update, preview.isFetching || read.isPending);
  const actionClass = "inline-flex min-h-11 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:border-slate-200 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100 dark:hover:bg-slate-800 dark:disabled:border-slate-700 dark:disabled:bg-slate-800 dark:disabled:text-slate-400";
  useEffect(() => {
    const sync = (event: StorageEvent) => { if (event.key === storageKey) setDismissedThrough(event.newValue); };
    window.addEventListener("storage", sync);
    return () => window.removeEventListener("storage", sync);
  }, [storageKey]);
  function savePreviewWatermark(next: string | null) {
    try {
      if (next === null) localStorage.removeItem(storageKey); else localStorage.setItem(storageKey, next);
      setDismissedThrough(next); setStorageError(false);
    } catch { setStorageError(true); }
  }
  useEffect(() => {
    if (!open) return;
    const place = () => {
      if (trigger.current) setPosition(notificationPopoverPosition(trigger.current.getBoundingClientRect(), { width: window.innerWidth, height: window.innerHeight }));
    };
    const outside = (event: PointerEvent) => {
      if (event.target instanceof Node && !panel.current?.contains(event.target) && !trigger.current?.contains(event.target)) setOpen(false);
    };
    const keyboard = (event: KeyboardEvent) => {
      if (event.key === "Escape") { event.preventDefault(); setOpen(false); trigger.current?.focus(); }
    };
    const focusOut = (event: FocusEvent) => {
      if (event.target instanceof Node && !panel.current?.contains(event.target) && !trigger.current?.contains(event.target)) setOpen(false);
    };
    const scrolled = (event: Event) => {
      if (event.target instanceof Node && panel.current?.contains(event.target)) return;
      setOpen(false);
    };
    panel.current?.focus();
    window.addEventListener("resize", place);
    window.addEventListener("scroll", scrolled, true);
    document.addEventListener("pointerdown", outside);
    document.addEventListener("keydown", keyboard);
    document.addEventListener("focusin", focusOut);
    return () => {
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", scrolled, true);
      document.removeEventListener("pointerdown", outside);
      document.removeEventListener("keydown", keyboard);
      document.removeEventListener("focusin", focusOut);
    };
  }, [open]);
  if (!user || !access.read) return null;
  const badge = notificationBadge(summary.isError ? undefined : summary.data?.data.unreadCount);
  const route = notificationRoutes[user.accountType];
  function toggle() {
    if (!open && trigger.current) setPosition(notificationPopoverPosition(trigger.current.getBoundingClientRect(), { width: window.innerWidth, height: window.innerHeight }));
    setOpen(!open);
  }
  return <>
    <button ref={trigger} type="button" onClick={toggle} aria-haspopup="dialog" aria-expanded={open}
      aria-label={summary.isError ? "Thông báo: chưa tải được số chưa đọc" : badge ? `Thông báo, ${summary.data?.data.unreadCount} chưa đọc` : "Thông báo"}
      className="relative grid h-10 w-10 shrink-0 place-items-center rounded-xl border border-slate-200 text-slate-600 transition-colors hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-indigo-500 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800">
      <NotificationIcon />
      {badge && <span className="absolute -right-1 -top-1 min-w-5 rounded-full bg-rose-600 px-1 text-center text-[11px] font-bold leading-5 text-white">{badge}</span>}
      {summary.isError && <span aria-hidden="true" className="absolute -right-1 -top-1 rounded-full bg-amber-500 px-1 text-xs text-black">!</span>}
    </button>
    {open && createPortal(<div ref={panel} role="dialog" aria-modal="false" aria-labelledby={titleId} tabIndex={-1}
      style={position} data-notification-popover="true"
      className={`notification-surface ${user.accountType === "PlatformAdmin" ? "platform-typography " : ""}fixed z-[100] flex flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white text-base text-slate-900 shadow-[0_18px_60px_-12px_rgba(15,23,42,0.28)] outline-none dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100`}>
      <div className="flex shrink-0 items-center justify-between gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800">
        <div><h2 id={titleId} className="text-lg font-bold">Thông báo</h2><p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{summary.isError ? "Chưa tải được số chưa đọc" : summary.data ? `${summary.data.data.unreadCount} chưa đọc trong hộp thư` : "Hộp thư của bạn"}</p></div>
        <button type="button" onClick={() => { setOpen(false); trigger.current?.focus(); }} className="grid h-8 w-8 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label="Đóng thông báo">✕</button>
      </div>
      <div className="shrink-0 space-y-2 border-b border-slate-100 px-5 py-3 dark:border-slate-800">
        <div className="flex flex-wrap gap-2">
        <button type="button" disabled={actions.readDisabled} onClick={() => { if (!actions.readDisabled) read.mutate(unreadIds); }} title={actions.readTitle} className={actionClass}><NotificationIcon kind="check" className="h-4 w-4 shrink-0" />Đã xem</button>
        <button type="button" disabled={actions.clearDisabled} onClick={() => savePreviewWatermark(notificationPreviewWatermark(items.map(item => item.deliveryId), dismissedThrough))} title="Chỉ dọn box trên trình duyệt này; không xóa thông báo trong hộp thư" className={actionClass}><svg className="h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" d="M4 7h16M10 11v6m4-6v6M6 7l1 13h10l1-13M9 7V4h6v3" /></svg>Xóa tất cả</button>
        {dismissedThrough && <button type="button" onClick={() => savePreviewWatermark(null)} className="rounded-lg px-3 py-2 text-sm font-semibold text-indigo-600 dark:text-indigo-400">Hiện lại</button>}
        </div>
        <p className="text-xs leading-6 text-slate-500 dark:text-slate-400" aria-live="polite">{actions.hint} Dọn box không xóa hộp thư.</p>
      </div>
      {storageError && <p role="alert" className="px-5 py-3 text-sm text-rose-600">Trình duyệt chưa lưu được lựa chọn dọn box. Thông báo vẫn được giữ nguyên.</p>}
      {read.isError && <p role="alert" className="px-5 py-3 text-sm text-rose-600">Chưa đánh dấu đã xem được. Hãy thử lại.</p>}
      <div className="min-h-0 overflow-y-auto">
        {preview.isPending && <p role="status" className="px-5 py-10 text-center text-sm text-slate-500">Đang tải thông báo…</p>}
        {preview.isError && <div role="alert" className="px-5 py-8 text-center text-sm"><p>Không tải được thông báo.</p><button type="button" onClick={() => void preview.refetch()} className="mt-3 font-semibold text-indigo-600 dark:text-indigo-400">Thử lại</button></div>}
        {preview.isSuccess && (items.length ? <ul className="divide-y divide-slate-100 dark:divide-slate-800">{items.map((item) => <li key={item.deliveryId}>
          <Link to={`${route}?notification=${item.deliveryId}`} onClick={() => setOpen(false)} className="flex gap-3 px-5 py-4 hover:bg-slate-50 dark:hover:bg-slate-800">
            <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-400"><NotificationIcon className="h-4 w-4" /></span>
            <div className="min-w-0 flex-1"><p className={`break-words text-sm ${item.readAt ? "font-medium" : "font-semibold"}`}>{item.title}</p><p className="mt-1 line-clamp-2 break-words text-sm text-slate-500 dark:text-slate-400">{item.body}</p><time dateTime={item.deliveredAt} className="mt-2 block text-xs text-slate-400">{new Date(item.deliveredAt).toLocaleString("vi-VN")}</time></div>
            {!item.readAt && <span className="mt-2 h-2 w-2 shrink-0 rounded-full bg-indigo-500" aria-label="Chưa đọc" />}
          </Link></li>)}</ul> : <div className="px-6 py-9 text-center"><span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-slate-100 text-slate-400 dark:bg-slate-800"><NotificationIcon className="h-6 w-6" /></span><p className="mt-4 text-sm font-semibold">{dismissedThrough ? "Đã dọn danh sách xem nhanh" : "Bạn chưa có thông báo"}</p><p className="mt-1 text-xs leading-6 text-slate-500 dark:text-slate-400">{dismissedThrough ? "Thông báo vẫn ở hộp thư đầy đủ. Thông báo mới sẽ tiếp tục hiện tại đây." : "Thông báo được gửi đến bạn sẽ xuất hiện tại đây."}</p></div>)}
      </div>
      <Link to={route} onClick={() => setOpen(false)} className="shrink-0 border-t border-slate-100 px-5 py-3.5 text-center text-sm font-semibold text-indigo-600 hover:bg-indigo-50 dark:border-slate-800 dark:text-indigo-400 dark:hover:bg-slate-800">Mở trung tâm thông báo →</Link>
    </div>, document.body)}
  </>;
}
