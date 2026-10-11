import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { notificationsApi } from "../../api/notificationsApi";
import type { NotificationFilters, NotificationItem, NotificationSent } from "../../types/notifications";
import { useNotifications } from "./useNotifications";
import { notificationSendAccess, notificationDateBoundary, notificationDateInput, notificationContentError } from "../../utils/notificationHelpers";

const button = "rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium disabled:opacity-40 dark:border-slate-700";
const field = "rounded-xl border border-slate-200 bg-white p-3 text-sm dark:border-slate-700 dark:bg-slate-900";
export function NotificationFiltersBar({ filters, onChange }: { filters: NotificationFilters; onChange: (value: NotificationFilters) => void }) {
  return <details className="mb-4 rounded-xl border border-slate-200 p-4 dark:border-slate-800">
    <summary className="cursor-pointer text-sm font-semibold">Bộ lọc bổ sung</summary>
    <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <label className="grid gap-2 text-sm">Loại<select className={field} value={filters.category ?? ""} onChange={e => onChange({ ...filters, category: e.target.value || undefined })}><option value="">Tất cả</option><option value="Information">Thông tin</option><option value="Request">Yêu cầu</option><option value="System">Tự động</option></select></label>
      <label className="grid gap-2 text-sm">Ưu tiên<select className={field} value={filters.priority ?? ""} onChange={e => onChange({ ...filters, priority: e.target.value || undefined })}><option value="">Tất cả</option><option value="Normal">Thông thường</option><option value="Important">Quan trọng</option><option value="Urgent">Khẩn</option></select></label>
      <label className="grid gap-2 text-sm">Từ ngày<input type="date" className={field} value={notificationDateInput(filters.from)} onChange={e => onChange({ ...filters, from: notificationDateBoundary(e.target.value) })} /></label>
      <label className="grid gap-2 text-sm">Đến ngày<input type="date" className={field} value={notificationDateInput(filters.until)} onChange={e => onChange({ ...filters, until: notificationDateBoundary(e.target.value, true) })} /></label>
    </div>
    <label className="mt-4 flex items-center gap-2 text-sm"><input type="checkbox" checked={filters.needsAcknowledgement ?? false} onChange={e => onChange({ ...filters, needsAcknowledgement: e.target.checked })} />Cần tôi xác nhận nhận</label>
    <button className={`${button} mt-3`} type="button" onClick={() => onChange({})}>Xóa bộ lọc</button>
  </details>;
}

export function NotificationReceiptActions({ item, onHide }: { item: NotificationItem; onHide: () => void }) {
  const { key, access } = useNotifications();
  const client = useQueryClient();
  const [body, setBody] = useState("");
  const replyId = useRef(crypto.randomUUID());
  const refresh = () => void client.invalidateQueries({ queryKey: key });
  const ack = useMutation({ mutationFn: () => notificationsApi.acknowledge(item.deliveryId), onSuccess: refresh });
  const hide = useMutation({ mutationFn: () => notificationsApi.hide(item.deliveryId), onSuccess: () => { refresh(); onHide(); } });
  const reply = useMutation({ mutationFn: () => notificationsApi.reply(item.deliveryId, replyId.current, body.trim()), onSuccess: refresh });
  const busy = ack.isPending || hide.isPending || reply.isPending;
  return <div className="mt-6 space-y-4 border-t border-slate-100 pt-5 dark:border-slate-800">
    {item.lifecycleStatus !== "Active" && <p role="status" className="rounded-xl bg-amber-50 p-4 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">{item.lifecycleStatus === "Retracted" ? "Bản gửi đã thu hồi, không còn hành động cần thực hiện." : "Bản gửi này đã được thay thế. Xem bản đính chính mới trong hộp thư nếu bạn thuộc phạm vi nhận."}</p>}
    <p className="text-xs leading-6 text-slate-500">Ưu tiên: {item.priority === "Urgent" ? "Khẩn" : item.priority === "Important" ? "Quan trọng" : "Thông thường"}. Đã đọc, đã xác nhận nhận và đã xử lý là các trạng thái độc lập.</p>
    {item.acknowledgedAt ? <p className="text-sm text-emerald-600">Đã xác nhận nhận · {new Date(item.acknowledgedAt).toLocaleString("vi-VN")}</p> : item.requiresAcknowledgement && access.acknowledge && <button className={button} type="button" disabled={busy} onClick={() => ack.mutate()}>Xác nhận tôi đã nhận thông báo</button>}
    {item.ownReply ? <div className="rounded-xl bg-slate-50 p-4 dark:bg-slate-800"><h3 className="text-sm font-semibold">Phản hồi riêng của bạn</h3><p className="mt-2 whitespace-pre-wrap break-words text-sm">{item.ownReply}</p></div> : item.allowReply && access.reply && <form onSubmit={e => { e.preventDefault(); reply.mutate(); }} className="grid gap-3"><label className="grid gap-2 text-sm font-medium">Phản hồi riêng cho người gửi<textarea className={`${field} min-h-28 resize-y`} maxLength={2000} required value={body} disabled={busy} onChange={e => setBody(e.target.value)} /></label><p className="text-xs text-slate-500">Một phản hồi theo ngữ cảnh, chỉ bạn và người gửi xem. Không phải trò chuyện lớp.</p><button className={button} type="submit" disabled={busy || !body.trim()}>Gửi phản hồi riêng</button></form>}
    {(ack.isError || hide.isError || reply.isError) && <p role="alert" className="text-sm text-rose-600">{notificationContentError(reply.error) ?? "Chưa thực hiện được. Thông báo có thể đã thu hồi hoặc quyền/phạm vi đã đổi; hãy làm mới. Không có thay đổi điểm hay bài làm."}</p>}
    {access.update && <button className={button} type="button" disabled={busy} onClick={() => hide.mutate()}>Ẩn khỏi hộp thư của tôi</button>}
  </div>;
}

export function NotificationSentActions({ item, onCorrect }: { item: NotificationSent; onCorrect: (item: NotificationSent) => void }) {
  const { user, key } = useNotifications();
  const access = notificationSendAccess(user);
  const client = useQueryClient();
  const [reason, setReason] = useState("");
  const [confirm, setConfirm] = useState(false);
  const actionId = useRef(crypto.randomUUID());
  const retract = useMutation({ mutationFn: () => notificationsApi.retract(item.messageId, actionId.current, reason.trim()), onSuccess: () => { setConfirm(false); void client.invalidateQueries({ queryKey: key }); } });
  return <div className="mt-6 space-y-4 border-t border-slate-100 pt-5 dark:border-slate-800">
    <p className="text-sm font-semibold">Đã xác nhận nhận: {item.acknowledgedCount}/{item.deliveredCount}</p>
    {item.correctionOfMessageId && <p className="text-xs text-slate-500">Đây là bản đính chính, có liên kết tới dấu vết bản gốc.</p>}
    {item.lifecycleStatus !== "Active" ? <p role="status" className="rounded-xl bg-amber-50 p-4 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">{item.lifecycleStatus === "Retracted" ? "Đã thu hồi" : "Đã đính chính"}: {item.lifecycleReason}. Bản gốc được giữ trong lịch sử gửi.</p> : access.retract && <>
      {access.send && <button className={button} type="button" onClick={() => onCorrect(item)}>Soạn bản đính chính</button>}
      <form onSubmit={e => { e.preventDefault(); setConfirm(true); }} className="grid gap-3">
        <label className="grid gap-2 text-sm font-medium">Lý do thu hồi<textarea maxLength={500} required value={reason} disabled={retract.isPending} onChange={e => { setReason(e.target.value); setConfirm(false); }} className={`${field} min-h-24`} /></label>
        <p className="text-xs text-slate-500">Người nhận sẽ thấy dấu vết thu hồi. Không thể lấy lại nội dung họ đã đọc hoặc chụp.</p>
        {!confirm ? <button className={button} type="submit" disabled={!reason.trim() || retract.isPending}>Kiểm tra thu hồi</button> : <div className="rounded-xl bg-rose-50 p-4 dark:bg-rose-500/10"><p className="mb-3 text-sm">Xác nhận thu hồi bản gửi này? Thao tác không xóa lịch sử.</p><button className={button} type="button" disabled={retract.isPending} onClick={() => retract.mutate()}>Xác nhận thu hồi</button><button className={`${button} ml-2`} type="button" onClick={() => setConfirm(false)}>Hủy</button></div>}
        {retract.isError && <p role="alert" className="text-sm text-rose-600">{notificationContentError(retract.error) ?? "Chưa thu hồi được. Hãy làm mới để kiểm tra trạng thái/quyền trước khi thử lại."}</p>}
      </form>
    </>}
    {item.allowReply && <SentReplies key={item.messageId} id={item.messageId} />}
  </div>;
}
function SentReplies({ id }: { id: string }) {
  const { key } = useNotifications();
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: [...key, "replies", id, page], queryFn: ({ signal }) => notificationsApi.replies(id, page, signal), retry: false });
  return <section aria-label="Phản hồi riêng"><h3 className="text-sm font-semibold">Phản hồi riêng</h3>{query.isPending && <p role="status">Đang tải…</p>}{query.isError && <p role="alert">Không tải được phản hồi.</p>}{query.isSuccess && <>
    {!query.data.data.length && <p className="mt-2 text-sm text-slate-500">Chưa có phản hồi.</p>}
    {query.data.data.map(reply => <article key={reply.replyId} className="mt-3 rounded-xl bg-slate-50 p-4 dark:bg-slate-800"><p className="text-xs font-semibold">{reply.senderName} · {new Date(reply.createdAt).toLocaleString("vi-VN")}</p><p className="mt-2 whitespace-pre-wrap break-words text-sm leading-6">{reply.body}</p></article>)}
    {(query.data.meta.totalPages ?? 0) > 1 && <div className="mt-3 flex justify-between"><button className={button} disabled={page <= 1} onClick={() => setPage(page - 1)}>Trước</button><span className="text-sm">Trang {page}</span><button className={button} disabled={page >= (query.data.meta.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Sau</button></div>}
  </>}</section>;
}
