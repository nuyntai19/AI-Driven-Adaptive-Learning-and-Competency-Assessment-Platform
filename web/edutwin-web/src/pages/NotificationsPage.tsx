import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { notificationsApi } from "../api/notificationsApi";
import { useAuthStore } from "../stores/authStore";
import { useNotifications } from "../components/notifications/useNotifications";
import { NotificationIcon } from "../components/notifications/NotificationIcon";
import { NotificationComposer } from "../components/notifications/NotificationComposer";
import { notificationSendAccess } from "../utils/notificationHelpers";
import type { NotificationFilters, NotificationSent } from "../types/notifications";
import { NotificationFiltersBar, NotificationReceiptActions, NotificationSentActions } from "../components/notifications/NotificationInteractions";

import { NotificationOperations } from "../components/notifications/NotificationOperations";

const card = "min-w-0 overflow-hidden rounded-2xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900";
const button = "inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 px-3.5 py-2.5 text-sm font-medium transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-slate-700 dark:hover:bg-slate-800";
const date = (value: string) => new Date(value).toLocaleString("vi-VN");
export function NotificationsPage() {
  const user = useAuthStore((s) => s.user);
  return <OwnNotifications key={`${user?.centerId}:${user?.userId}`} />;
}
function OwnNotifications() {
  const { user, key, summary } = useNotifications();
  const access = notificationSendAccess(user);
  const operations = !!user?.permissions.includes(user.accountType === "PlatformAdmin" ? "platform.notifications.read_dispatches" : "notifications.dispatches.read");
  const client = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [localView, setLocalView] = useState<"received" | "sent" | "compose" | "operations">("received");
  // A related link can change the query while this page stays mounted.
  const view = params.has("sent") ? "sent" : localView;
  function setView(next: typeof localView) {
    setLocalView(next);
    if (params.has("sent") || params.has("notification")) {
      const clean = new URLSearchParams(params); clean.delete("sent"); clean.delete("notification");
      setParams(clean, { replace: true });
    }
  }
  const [success, setSuccess] = useState(false);
  const [correction, setCorrection] = useState<NotificationSent | null>(null);
  return <section className={`notification-surface w-full min-w-0 text-base text-slate-900 dark:text-slate-100 ${user?.accountType === "PlatformAdmin" ? "" : "mx-auto max-w-[1500px] p-4 sm:p-6 lg:p-8"}`}>
    <header className="mb-7 flex flex-wrap items-start justify-between gap-5">
      <div className="flex min-w-0 items-start gap-4"><span className="grid h-12 w-12 shrink-0 place-items-center rounded-2xl bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-400"><NotificationIcon className="h-6 w-6" /></span><div><h1 className="text-2xl font-bold tracking-tight sm:text-3xl">Trung tâm thông báo</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500 dark:text-slate-400">{access.send ? "Nhận cập nhật và gửi thông báo đến đúng người trong phạm vi của bạn." : "Hộp thư riêng của bạn. Thông báo không bị lọc mất khi đổi môn hoặc lớp."}</p></div></div>
      <div className="flex flex-wrap gap-2"><button type="button" onClick={() => void client.invalidateQueries({ queryKey: key })} className={button}><NotificationIcon kind="refresh" className="h-4 w-4" />Làm mới</button>{access.send && <button type="button" onClick={() => { setCorrection(null); setView("compose"); setSuccess(false); }} className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm shadow-indigo-500/20 hover:bg-indigo-700"><NotificationIcon kind="send" className="h-4 w-4" />Soạn thông báo</button>}</div>
    </header>
    {success && <p role="status" className="mb-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300">Thông báo đã được tiếp nhận. Hệ thống đang phân phối; bạn có thể xem trạng thái trong Đã gửi.</p>}
    <nav aria-label="Hộp thông báo" className="mb-5 flex flex-wrap gap-1 border-b border-slate-200 dark:border-slate-800">
      <button type="button" onClick={() => setView("received")} aria-current={view === "received" ? "page" : undefined} className={`border-b-2 px-4 py-3 text-sm font-semibold ${view === "received" ? "border-indigo-600 text-indigo-600 dark:text-indigo-400" : "border-transparent text-slate-500"}`}>Đã nhận {summary.data && summary.data.data.unreadCount > 0 && <span className="ml-2 rounded-full bg-indigo-100 px-2 py-0.5 text-xs dark:bg-indigo-500/20">{summary.data.data.unreadCount}</span>}</button>
      {access.sent && <button type="button" onClick={() => setView("sent")} aria-current={view === "sent" ? "page" : undefined} className={`border-b-2 px-4 py-3 text-sm font-semibold ${view === "sent" ? "border-indigo-600 text-indigo-600 dark:text-indigo-400" : "border-transparent text-slate-500"}`}>Đã gửi</button>}
      {operations && <button type="button" onClick={() => setView("operations")} aria-current={view === "operations" ? "page" : undefined} className={`border-b-2 px-4 py-3 text-sm font-semibold ${view === "operations" ? "border-indigo-600 text-indigo-600" : "border-transparent text-slate-500"}`}>Theo dõi gửi</button>}
    </nav>
    {view === "operations" && operations ? <NotificationOperations /> : view === "compose" && access.send ? <NotificationComposer key={correction?.messageId ?? "new"} correction={correction} queryKey={key} onCancel={() => setView("received")} onSent={() => { setSuccess(true); setView("sent"); void client.invalidateQueries({ queryKey: key }); }} /> : view === "sent" && access.sent ? <SentInbox key={params.get("sent") ?? "sent-inbox"} onCorrect={item => { setCorrection(item); setView("compose"); setSuccess(false); }} /> : <ReceivedInbox />}
  </section>;
}
function EmptyInbox({ sent = false, unread = false }: { sent?: boolean; unread?: boolean }) {
  return <div className={`${card} px-6 py-16 text-center sm:py-20`}><span className="mx-auto grid h-20 w-20 place-items-center rounded-3xl border border-indigo-100 bg-indigo-50 text-indigo-400 dark:border-indigo-500/20 dark:bg-indigo-500/10"><NotificationIcon kind={sent ? "send" : unread ? "check" : "bell"} className="h-9 w-9" /></span><h2 className="mt-6 text-lg font-semibold">{sent ? "Bạn chưa gửi thông báo nào" : unread ? "Bạn đã đọc hết thông báo" : "Hộp thư của bạn đang trống"}</h2><p className="mx-auto mt-2 max-w-md text-sm leading-6 text-slate-500 dark:text-slate-400">{sent ? "Chọn Soạn thông báo để gửi nội dung mới. Các bản đã gửi và trạng thái phân phối sẽ được giữ tại đây." : unread ? "Chọn Tất cả để xem lại những thông báo đã đọc." : "Chưa có thông báo được gửi đến bạn. Khi có cập nhật, chuông sẽ hiển thị số chưa đọc."}</p></div>;
}
function Paging({ page, totalPages, busy, onPage }: { page: number; totalPages: number; busy: boolean; onPage: (page: number) => void }) {
  return <div className="flex items-center justify-between gap-3 border-t border-slate-100 px-4 py-3 dark:border-slate-800"><button type="button" disabled={busy || page <= 1} onClick={() => onPage(page - 1)} className={button}>← Trước</button><span className="text-xs text-slate-500">Trang {page} / {Math.max(1, totalPages)}</span><button type="button" disabled={busy || page >= totalPages} onClick={() => onPage(page + 1)} className={button}>Sau →</button></div>;
}
function ReceivedInbox() {
  const { key, access, read, active } = useNotifications();
  const [params, setParams] = useSearchParams();
  const id = params.get("notification");
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [filters, setFilters] = useState<NotificationFilters>({});
  const list = useQuery({ queryKey: [...key, "list", page, unreadOnly, filters], queryFn: ({ signal }) => notificationsApi.list(page, 20, unreadOnly, signal, filters), enabled: access.read, retry: false });
  const detail = useQuery({ queryKey: [...key, "detail", id], queryFn: ({ signal }) => notificationsApi.detail(id!, signal), enabled: access.read && !!id, retry: false });
  const readStarted = useRef<string | null>(null);
  const markRead = read.mutate;
  useEffect(() => {
    if (active && access.update && id && detail.isSuccess && !detail.data.data.readAt && readStarted.current !== id) {
      readStarted.current = id; markRead([id]);
    }
  }, [active, access.update, id, detail.isSuccess, detail.data, markRead]);
  const visibleIds = list.data?.data.filter((item) => !item.readAt).map((item) => item.deliveryId) ?? [];
  const empty = list.isSuccess && list.data.data.length === 0;
  return <>
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3"><div className="inline-flex rounded-xl border border-slate-200 bg-white p-1 dark:border-slate-800 dark:bg-slate-900">{[{ label: "Tất cả", value: false }, { label: "Chưa đọc", value: true }].map(filter => <button key={filter.label} type="button" aria-pressed={unreadOnly === filter.value} onClick={() => { setUnreadOnly(filter.value); setPage(1); }} className={`rounded-lg px-3 py-2 text-sm font-medium ${unreadOnly === filter.value ? "bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-white" : "text-slate-500"}`}>{filter.label}</button>)}</div>{access.update && visibleIds.length > 0 && <button type="button" disabled={read.isPending} onClick={() => read.mutate(visibleIds)} className="inline-flex items-center gap-2 text-sm font-medium text-indigo-600 disabled:opacity-40 dark:text-indigo-400"><NotificationIcon kind="check" className="h-4 w-4" />Đọc các thông báo đang hiển thị</button>}</div>
    <NotificationFiltersBar filters={filters} onChange={value => { setFilters(value); setPage(1); }} />
    {read.isError && <p role="alert" className="mb-4 text-sm text-rose-600 dark:text-rose-400">Chưa đánh dấu đọc được. Bạn có thể thử lại; dữ liệu học tập không bị thay đổi.</p>}
    <div className={id ? "grid gap-5 lg:grid-cols-[minmax(300px,0.8fr)_minmax(0,1.2fr)]" : ""}>
      {empty ? <EmptyInbox unread={unreadOnly} /> : <div className={card}>
        {list.isPending && <p role="status" className="p-10 text-center text-sm text-slate-500">Đang tải hộp thư…</p>}
        {list.isError && <p role="alert" className="p-10 text-center text-sm text-rose-600">Không tải được hộp thư. Hãy thử làm mới.</p>}
        {list.isSuccess && <ul className="divide-y divide-slate-100 dark:divide-slate-800">{list.data.data.map(item => <li key={item.deliveryId}><button type="button" onClick={() => setParams({ notification: item.deliveryId })} aria-current={id === item.deliveryId ? "true" : undefined} className={`flex w-full gap-4 p-5 text-left transition-colors hover:bg-slate-50 dark:hover:bg-slate-800/70 ${id === item.deliveryId ? "bg-indigo-50 dark:bg-indigo-500/10" : ""}`}>
          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-400"><NotificationIcon /></span><div className="min-w-0 flex-1"><div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-1"><span className={`min-w-0 break-words text-sm ${item.readAt ? "font-medium" : "font-semibold"}`}>{item.title}</span><time className="shrink-0 text-xs text-slate-400" dateTime={item.deliveredAt}>{date(item.deliveredAt)}</time></div><span className="mt-1.5 block line-clamp-2 break-words text-sm leading-6 text-slate-500 dark:text-slate-400">{item.body}</span>{item.senderName && <span className="mt-2 block text-xs text-slate-400">Từ {item.senderName}</span>}</div>{!item.readAt && <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-indigo-500" aria-label="Chưa đọc" />}
        </button></li>)}</ul>}
        {list.isSuccess && <Paging page={page} totalPages={list.data.meta.totalPages ?? 0} busy={list.isFetching} onPage={setPage} />}
      </div>}
      {id && <article className={`${card} p-6`} aria-label="Chi tiết thông báo"><button type="button" onClick={() => setParams({})} className="mb-6 text-xs font-semibold text-indigo-600 dark:text-indigo-400">← Đóng chi tiết</button>
        {detail.isPending && <p role="status">Đang tải chi tiết…</p>}{detail.isError && <p role="alert">Thông báo không còn khả dụng hoặc bạn không có quyền xem.</p>}
        {detail.isSuccess && <><h2 className="break-words text-xl font-bold leading-8">{detail.data.data.title}</h2><p className="mt-3 border-b border-slate-100 pb-5 text-xs leading-6 text-slate-500 dark:border-slate-800">{detail.data.data.senderName ?? "Hệ thống"} · {date(detail.data.data.deliveredAt)}</p><p className="mt-5 whitespace-pre-wrap break-words text-sm leading-7">{detail.data.data.body}</p>{detail.data.data.relatedLink && <Link to={detail.data.data.relatedLink} className="mt-5 inline-flex text-sm font-semibold text-indigo-600 dark:text-indigo-400">Mở nội dung liên quan →</Link>}<p className="mt-8 text-xs text-slate-500">{detail.data.data.readAt ? "Đã đọc" : "Chưa đọc"}</p>{!detail.data.data.readAt && access.update && <button type="button" disabled={read.isPending} onClick={() => read.mutate([id])} className={`${button} mt-3`}>Đánh dấu đã đọc</button>}<NotificationReceiptActions key={id} item={detail.data.data} onHide={() => setParams({})} /></>}
      </article>}
    </div>
  </>;
}
function SentInbox({ onCorrect }: { onCorrect: (item: NotificationSent) => void }) {
  const { key } = useNotifications();
  const [page, setPage] = useState(1);
  const [params] = useSearchParams();
  const [selected, setSelected] = useState<string | null>(params.get("sent"));
  const list = useQuery({ queryKey: [...key, "sent", page], queryFn: ({ signal }) => notificationsApi.sent(page, signal), retry: false, staleTime: 0 });
  const detail = useQuery({ queryKey: [...key, "sent-detail", selected], queryFn: ({ signal }) => notificationsApi.sentDetail(selected!, signal), enabled: !!selected, retry: false });
  if (list.isSuccess && !list.data.data.length && !selected) return <EmptyInbox sent />;
  return <div className={selected ? "grid gap-5 lg:grid-cols-[minmax(300px,0.8fr)_minmax(0,1.2fr)]" : ""}><div className={card}>
    {list.isPending && <p role="status" className="p-10 text-center text-slate-500">Đang tải thông báo đã gửi…</p>}{list.isError && <p role="alert" className="p-10 text-center text-rose-600">Không tải được Đã gửi. Hãy thử làm mới.</p>}
    {list.isSuccess && <><ul className="divide-y divide-slate-100 dark:divide-slate-800">{list.data.data.map(item => <li key={item.messageId}><button type="button" onClick={() => setSelected(item.messageId)} className={`flex w-full gap-4 p-5 text-left hover:bg-slate-50 dark:hover:bg-slate-800 ${selected === item.messageId ? "bg-indigo-50 dark:bg-indigo-500/10" : ""}`}><span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400"><NotificationIcon kind="send" /></span><div className="min-w-0 flex-1"><p className="break-words text-sm font-semibold">{item.title}</p><p className="mt-1 line-clamp-2 break-words text-sm leading-6 text-slate-500">{item.body}</p><p className="mt-2 text-xs text-slate-400">{date(item.createdAt)} · {item.recipientCount} người nhận · {item.dispatchStatus === "Completed" ? "Đã phân phối" : item.dispatchStatus === "Failed" ? "Có lỗi phân phối" : "Đang phân phối"}</p></div></button></li>)}</ul><Paging page={page} totalPages={list.data.meta.totalPages ?? 0} busy={list.isFetching} onPage={setPage} /></>}
  </div>{selected && <article className={`${card} p-6`} aria-label="Chi tiết đã gửi"><button type="button" onClick={() => setSelected(null)} className="mb-5 text-xs font-semibold text-indigo-600">← Đóng chi tiết</button>{detail.isPending && <p role="status">Đang tải chi tiết…</p>}{detail.isError && <p role="alert">Bạn không có quyền xem bản gửi này hoặc dữ liệu không còn khả dụng.</p>}{detail.isSuccess && <><h2 className="break-words text-xl font-bold">{detail.data.data.title}</h2><p className="mt-3 text-xs text-slate-500">{date(detail.data.data.createdAt)}</p><p className="mt-6 whitespace-pre-wrap break-words text-sm leading-7">{detail.data.data.body}</p><div className="mt-8 grid grid-cols-2 gap-3">{[["Người nhận", detail.data.data.recipientCount], ["Đã phân phối", detail.data.data.deliveredCount], ["Đã đọc", detail.data.data.readCount], ["Không còn phù hợp", detail.data.data.suppressedCount]].map(([label, value]) => <div key={label} className="rounded-xl bg-slate-50 p-3 dark:bg-slate-800"><p className="text-xs text-slate-500">{label}</p><p className="mt-1 text-lg font-semibold">{value}</p></div>)}</div><p className="mt-4 text-xs leading-5 text-slate-500">Đã đọc không đồng nghĩa đã xác nhận hoặc đã xử lý. Nội dung bản đã gửi được giữ nguyên.</p><NotificationSentActions key={selected} item={detail.data.data} onCorrect={onCorrect} /></>}</article>}</div>;
}
