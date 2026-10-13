import { useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { notificationsApi } from "../../api/notificationsApi";
import type { NotificationDraft, NotificationPreview, NotificationSent } from "../../types/notifications";
import { NotificationIcon } from "./NotificationIcon";
import { notificationContentError } from "../../utils/notificationHelpers";

const field = "mt-2 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-3 text-sm text-slate-900 outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/10 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100";
function errorMessage(error: unknown) {
  const contentError = notificationContentError(error);
  if (contentError) return contentError;
  if (isAxiosError(error)) {
    if (error.response?.status === 409) return "Người nhận hoặc nội dung đã thay đổi. Hãy xem trước lại; thông báo chưa được gửi thêm.";
    if (error.response?.status === 429) return "Bạn thao tác quá nhanh. Vui lòng chờ một phút rồi thử lại.";
    if (error.response?.status === 403) return "Bạn không còn quyền gửi cho phạm vi này. Hãy làm mới danh sách người nhận.";
    if (!error.response) return "Kết nối bị gián đoạn. Thông báo có thể đã được tiếp nhận; thử lại cùng bản soạn này sẽ không gửi trùng.";
  }
  return "Không thể tiếp nhận thông báo. Kiểm tra nội dung, phạm vi người nhận và thử xem trước lại.";
}
export function NotificationComposer({ queryKey, onCancel, onSent, correction }: { queryKey: readonly unknown[]; onCancel: () => void; onSent: () => void; correction?: NotificationSent | null }) {
  const options = useQuery({ queryKey: [...queryKey, "compose-options"], queryFn: ({ signal }) => notificationsApi.options(signal), retry: false, staleTime: 0 });
  const [kind, setKind] = useState("");
  const [title, setTitle] = useState(correction?.title ?? "");
  const [body, setBody] = useState(correction?.body ?? "");
  const [priority, setPriority] = useState<"Normal" | "Important" | "Urgent">(correction?.priority ?? "Normal");
  const [category, setCategory] = useState<"Information" | "Request">(correction?.category ?? "Information");
  const [requiresAcknowledgement, setRequiresAcknowledgement] = useState(correction?.requiresAcknowledgement ?? false);
  const [allowReply, setAllowReply] = useState(correction?.allowReply ?? false);
  const [correctionReason, setCorrectionReason] = useState("");
  const [ids, setIds] = useState<string[]>([]);
  const [search, setSearch] = useState("");
  const [review, setReview] = useState<NotificationPreview | null>(null);
  const clientId = useRef(crypto.randomUUID());
  const option = options.data?.data.find(x => x.kind === kind) ?? options.data?.data[0];
  const draft: NotificationDraft = { title: title.trim(), body: body.trim(), audienceKind: option?.kind ?? "", targetIds: [...ids].sort(), priority, category, requiresAcknowledgement, allowReply, correctionOfMessageId: correction?.messageId ?? null, correctionReason: correction ? correctionReason.trim() : null };
  const preview = useMutation({ mutationFn: notificationsApi.preview, onSuccess: (result) => setReview(result.data) });
  const send = useMutation({ mutationFn: () => notificationsApi.send(draft, review!.token, clientId.current), onSuccess: onSent,
    onError: (error) => { if (isAxiosError(error) && error.response?.status === 409) setReview(null); } });
  const busy = preview.isPending || send.isPending;
  function changed() { setReview(null); preview.reset(); send.reset(); }
  const filtered = option?.choices.filter(x => x.label.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi"))) ?? [];
  const support = option?.kind === "PlatformSupport" || option?.kind === "CenterManagersRelay";
  return <div className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-7 dark:border-slate-800 dark:bg-slate-900">
    <div className="mb-6 flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-lg font-bold">{correction ? "Soạn bản đính chính" : "Soạn thông báo mới"}</h2><p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Xem trước người nhận trước khi xác nhận gửi.</p></div><button type="button" disabled={busy} onClick={onCancel} className="rounded-lg px-3 py-2 text-sm text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800">Quay lại hộp thư</button></div>
    {options.isPending && <p role="status" className="py-8 text-center text-slate-500">Đang tải phạm vi người nhận…</p>}
    {options.isError && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-sm text-rose-700 dark:bg-rose-500/10 dark:text-rose-300">Không tải được phạm vi gửi. <button onClick={() => void options.refetch()} className="font-semibold underline">Thử lại</button></div>}
    {options.isSuccess && <form onSubmit={(event) => { event.preventDefault(); setReview(null); preview.mutate(draft); }}>
      <fieldset disabled={busy} className="grid min-w-0 gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(280px,0.85fr)]">
        <div className="min-w-0 space-y-5">
          {correction && <label className="block text-sm font-semibold">Lý do đính chính<textarea className={field} maxLength={500} required value={correctionReason} onChange={e => { setCorrectionReason(e.target.value); changed(); }} /><span className="mt-2 block text-xs font-normal text-slate-500">Chọn lại người nhận trong phạm vi gốc; server không cho thêm người ngoài bản gốc. Bản gốc được giữ, không sửa âm thầm.</span></label>}
          <div className="grid gap-4 sm:grid-cols-2"><label className="block text-sm font-semibold">Loại thông báo<select className={field} value={category} onChange={e => { setCategory(e.target.value as "Information" | "Request"); changed(); }}><option value="Information">Thông tin</option><option value="Request">Yêu cầu</option></select></label><label className="block text-sm font-semibold">Ưu tiên<select className={field} value={priority} onChange={e => { setPriority(e.target.value as "Normal" | "Important" | "Urgent"); changed(); }}><option value="Normal">Thông thường</option><option value="Important">Quan trọng</option><option value="Urgent">Khẩn</option></select></label></div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={requiresAcknowledgement} onChange={e => { setRequiresAcknowledgement(e.target.checked); changed(); }} />Yêu cầu xác nhận đã nhận</label>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={allowReply} onChange={e => { setAllowReply(e.target.checked); changed(); }} />Cho phép một phản hồi riêng</label>
          <label className="block text-sm font-semibold">Tiêu đề<input className={field} value={title} maxLength={160} required placeholder="Ví dụ: Nhắc chuẩn bị bài học ngày mai" onChange={e => { setTitle(e.target.value); changed(); }} /></label>
          <label className="block text-sm font-semibold">Nội dung<textarea className={`${field} min-h-52 resize-y font-normal leading-6`} value={body} maxLength={2000} required placeholder="Nhập hướng dẫn hoặc nội dung cần thông báo…" onChange={e => { setBody(e.target.value); changed(); }} /><span className="mt-1 block text-right text-xs font-normal text-slate-400">{body.length}/2000 ký tự</span></label>
          <p className="rounded-xl bg-slate-50 p-3 text-xs leading-5 text-slate-500 dark:bg-slate-800 dark:text-slate-400">{support ? "Kênh hỗ trợ vận hành: không gửi bài làm, ảnh nháp, đáp án, điểm hoặc thông tin riêng của học sinh. Server chặn dấu hiệu nhạy cảm phổ biến, nhưng không nhận diện tuyệt đối; hãy tự kiểm tra trước khi gửi." : "Không đưa điểm hoặc nhận xét riêng của một học sinh vào thông báo chung."} Không gửi mật khẩu/token, link hoặc tệp qua thông báo.</p>
        </div>
        <div className="min-w-0 rounded-xl border border-slate-200 bg-slate-50/60 p-4 dark:border-slate-700 dark:bg-slate-950/40">
          <label className="block text-sm font-semibold">Gửi đến<select className={field} value={option?.kind ?? ""} onChange={e => { setKind(e.target.value); setIds([]); setSearch(""); changed(); }}>{options.data.data.map(o => <option key={o.kind} value={o.kind}>{o.label}</option>)}</select></label>
          <label className="mt-4 block text-sm font-medium">Tìm trong danh sách<input className={field} value={search} placeholder="Nhập tên để tìm…" onChange={e => setSearch(e.target.value)} /></label>
          <div className="my-3 flex items-center justify-between gap-2 text-xs"><span className="text-slate-500">Đã chọn {ids.length}</span><button type="button" className="font-semibold text-indigo-600 dark:text-indigo-400" onClick={() => { setIds([...new Set([...ids, ...filtered.map(x => x.id)])]); changed(); }}>Chọn kết quả đang hiển thị</button></div>
          <div className="max-h-64 space-y-1 overflow-y-auto rounded-lg border border-slate-200 bg-white p-2 dark:border-slate-700 dark:bg-slate-900">{filtered.length ? filtered.map(choice => <label key={choice.id} className="flex cursor-pointer items-start gap-3 rounded-lg p-2.5 text-sm hover:bg-slate-50 dark:hover:bg-slate-800"><input type="checkbox" className="mt-1 accent-indigo-600" checked={ids.includes(choice.id)} onChange={e => { setIds(e.target.checked ? [...ids, choice.id] : ids.filter(id => id !== choice.id)); changed(); }} /><span className="min-w-0 break-words">{choice.label}</span></label>) : <p className="p-4 text-center text-sm text-slate-500">Không có người nhận/lớp phù hợp.</p>}</div>
          {option?.kind === "ClassStudents" && <p className="mt-3 text-xs leading-5 text-slate-500">Chỉ lớp đang hoạt động và do bạn phụ trách. Học sinh học nhiều lớp chỉ nhận một lần.</p>}
        </div>
      </fieldset>
      {(preview.isError || send.isError) && <p role="alert" className="mt-5 rounded-xl bg-rose-50 p-4 text-sm text-rose-700 dark:bg-rose-500/10 dark:text-rose-300">{errorMessage(send.error ?? preview.error)}</p>}
      {review && <div className="mt-6 rounded-xl border border-indigo-200 bg-indigo-50 p-5 dark:border-indigo-500/30 dark:bg-indigo-500/10"><p className="font-semibold">Xác nhận gửi đến {review.recipientCount} người nhận</p><p className="mt-2 text-sm leading-6 text-slate-600 dark:text-slate-300">{review.recipientNames.slice(0, 12).join(", ")}{review.recipientCount > 12 ? ` và ${review.recipientCount - 12} người khác` : ""}</p><p className="mt-2 text-xs text-slate-500">Phạm vi sẽ được kiểm tra lại khi gửi. Xem trước có hiệu lực 5 phút.</p></div>}
      <div className="mt-6 flex flex-wrap items-center justify-end gap-3"><button type="submit" disabled={busy || !draft.title || !draft.body || !ids.length} className="rounded-xl border border-slate-300 px-5 py-3 text-sm font-semibold disabled:opacity-40 dark:border-slate-600">{preview.isPending ? "Đang kiểm tra…" : "Xem trước người nhận"}</button>{review && <button type="button" disabled={busy} onClick={() => send.mutate()} className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"><NotificationIcon kind="send" className="h-4 w-4" />{send.isPending ? "Đang tiếp nhận…" : "Xác nhận gửi"}</button>}</div>
    </form>}
  </div>;
}
