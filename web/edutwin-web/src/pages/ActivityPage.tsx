import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { httpClient } from "../api/httpClient";
import { useAuthStore } from "../stores/authStore";
import type { ApiCollectionResponse } from "../types/api";
import { activitySourceLabels as sourceLabels, activityActionLabels as actionLabels, activityTitle, activityDescription, activityTargetLabel, activityStatus } from "../utils/activityPresentation";
import { notificationDateBoundary } from "../utils/notificationHelpers";

interface Activity {
  source: string; sourceId: string; occurredAt: string; actorName: string; action: string; targetType: string; targetId: string;
  beforeStatus: string | null; afterStatus: string | null; version: string | null; reason: string | null; link: string | null;
  targetLabel?: string | null;
}
const button = "rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-slate-700 dark:hover:bg-slate-800";
export function ActivityPage({ embedded = false }: { embedded?: boolean }) {
  const user = useAuthStore(s => s.user);
  return <ActivityView key={`${user?.centerId}:${user?.userId}`} embedded={embedded} />;
}
function ActivityView({ embedded }: { embedded: boolean }) {
  const user = useAuthStore(s => s.user);
  const [page, setPage] = useState(1); const [source, setSource] = useState(""); const [from, setFrom] = useState(""); const [until, setUntil] = useState("");
  const [action, setAction] = useState("");
  const [expanded, setExpanded] = useState<string | null>(null);
  const administrative = user?.accountType === "CenterManager" || user?.accountType === "PlatformAdmin";
  const query = useQuery({ queryKey: ["activity", user?.centerId, user?.userId, page, source, action, from, until],
    queryFn: async ({ signal }) => (await httpClient.get<ApiCollectionResponse<Activity>>("/activity", { signal, params: { page, pageSize: 20,
      source: source || undefined, action: action || undefined, from: notificationDateBoundary(from), until: notificationDateBoundary(until, true) } })).data,
    retry: false, enabled: !!user });
  const totalPages = Math.max(1, query.data?.meta.totalPages ?? 1);
  const Heading = embedded ? "h2" : "h1";
  return <section className={`notification-surface w-full min-w-0 text-base text-slate-900 dark:text-slate-100 ${embedded ? "" : "mx-auto max-w-[1500px] p-4 sm:p-6 lg:p-8"}`}>
    <header className="mb-6 flex flex-wrap items-start justify-between gap-4"><div>{!embedded && <p className="text-xs font-semibold uppercase tracking-widest text-indigo-600 dark:text-indigo-400">Bằng chứng hoạt động</p>}<Heading className={embedded ? "text-xl font-bold" : "mt-2 text-2xl font-bold sm:text-3xl"}>{embedded ? "Tổng quan hoạt động" : "Nhật ký hoạt động"}</Heading><p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500 dark:text-slate-400">{administrative ? "Metadata quản trị trong phạm vi được cấp. Không bao gồm bài làm, ảnh nháp hoặc điểm riêng của học sinh." : "Lịch sử có nguồn thật trong phạm vi của bạn. Đọc nhật ký không thay đổi bài làm, điểm hoặc trạng thái xử lý."}</p></div><button type="button" onClick={() => void query.refetch()} disabled={query.isFetching} className={button}>Làm mới</button></header>
    <div className="mb-5 flex flex-wrap items-end gap-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
      <label className="min-w-[180px] flex-1 text-xs font-medium text-slate-500">Nguồn lịch sử<select value={source} onChange={e => { setSource(e.target.value); setPage(1); }} className="mt-2 block w-full rounded-xl border border-slate-200 bg-transparent px-3 py-2.5 text-sm text-slate-900 dark:border-slate-700 dark:text-white"><option value="">Tất cả nguồn được phép</option>{Object.entries(sourceLabels).filter(([key]) => !administrative || ["Audit", "Curriculum", "Business"].includes(key)).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      <label className="min-w-[180px] flex-1 text-xs font-medium text-slate-500">Hoạt động<select value={action} onChange={e => { setAction(e.target.value); setPage(1); }} className="mt-2 block w-full rounded-xl border border-slate-200 bg-transparent px-3 py-2.5 text-sm dark:border-slate-700"><option value="">Tất cả hoạt động</option>{Object.entries(actionLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      <label className="text-xs font-medium text-slate-500">Từ ngày<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1); }} className="mt-2 block rounded-xl border border-slate-200 bg-transparent px-3 py-2.5 text-sm dark:border-slate-700" /></label>
      <label className="text-xs font-medium text-slate-500">Đến ngày<input type="date" value={until} onChange={e => { setUntil(e.target.value); setPage(1); }} className="mt-2 block rounded-xl border border-slate-200 bg-transparent px-3 py-2.5 text-sm dark:border-slate-700" /></label>
      <button type="button" className={button} onClick={() => { setSource(""); setAction(""); setFrom(""); setUntil(""); setPage(1); }}>Xóa bộ lọc</button>
    </div>
    <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      {query.isPending && <p role="status" className="p-12 text-center text-slate-500">Đang đọc các nguồn lịch sử…</p>}
      {query.isError && <div role="alert" className="p-10 text-center"><h2 className="font-semibold">Chưa tải được nhật ký</h2><p className="mt-2 text-sm text-slate-500">Kiểm tra kết nối/quyền hoặc khoảng ngày rồi thử lại. Lỗi không được coi là lịch sử rỗng.</p></div>}
      {query.isSuccess && query.data.data.length === 0 && <div className="p-12 text-center"><h2 className="font-semibold">Chưa có hoạt động phù hợp</h2><p className="mt-2 text-sm text-slate-500">Thử đổi bộ lọc. Hệ thống không dựng sự kiện giả từ ngày cập nhật và không gửi lại thông báo cũ.</p></div>}
      <ol className="divide-y divide-slate-100 dark:divide-slate-800">{query.data?.data.map(item => {
        const id = `${item.source}:${item.sourceId}`; const open = expanded === id;
        return <li key={id} className="p-4 sm:p-6"><button type="button" aria-expanded={open} onClick={() => setExpanded(open ? null : id)} className="flex w-full items-start gap-3 text-left sm:gap-4"><span className="mt-1 grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-400" aria-hidden="true">◷</span><div className="min-w-0 flex-1"><div className="flex flex-wrap justify-between gap-2"><h2 className="text-lg font-semibold">{activityTitle(item.action)}</h2><time className="text-xs text-slate-500" dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleString("vi-VN")}</time></div><p className="mt-2 break-words font-medium text-slate-700 dark:text-slate-200">{item.targetLabel || activityTargetLabel(item.targetType)}</p><p className="mt-2 text-sm leading-7 text-slate-600 dark:text-slate-300">{activityDescription(item.action)}</p><p className="mt-3 text-xs text-slate-500">Thực hiện bởi {item.actorName} · {sourceLabels[item.source] ?? "Nguồn lịch sử khác"}</p></div><span aria-hidden="true" className="text-slate-400">{open ? "−" : "+"}</span></button>
          {open && <div className="mt-4 space-y-4 rounded-xl bg-slate-50 p-4 text-sm sm:ml-14 dark:bg-slate-950/40">{(item.beforeStatus || item.afterStatus) && <p><span className="font-semibold">Thay đổi: </span>{activityStatus(item.beforeStatus)} → {activityStatus(item.afterStatus)}</p>}{item.version && <p>Lần cập nhật kết quả: {item.version}</p>}{item.reason && <p className="whitespace-pre-wrap break-words leading-7"><span className="font-semibold">Ghi chú: </span>{item.reason}</p>}{item.link && <Link className="inline-flex font-medium text-indigo-600 dark:text-indigo-400" to={item.link}>Xem nội dung liên quan →</Link>}<details className="border-t border-slate-200 pt-3 dark:border-slate-800"><summary className="cursor-pointer text-xs text-slate-500">Thông tin kỹ thuật (khi cần hỗ trợ)</summary><dl className="mt-3 space-y-2 break-all text-xs text-slate-500"><div><dt className="font-semibold">Nguồn / mã bản ghi</dt><dd>{item.source} · {item.sourceId}</dd></div><div><dt className="font-semibold">Loại hoạt động</dt><dd>{item.action}</dd></div><div><dt className="font-semibold">Đối tượng / mã đối tượng</dt><dd>{item.targetType} · {item.targetId}</dd></div></dl></details></div>}
        </li>;
      })}</ol>
      <p className="px-4 pb-3 text-xs text-slate-500">Mỗi cửa sổ xem tối đa 2.000 hoạt động. Dùng bộ lọc ngày để xem lịch sử xa hơn.</p>
      <footer className="flex items-center justify-between gap-3 border-t border-slate-100 p-4 dark:border-slate-800"><button className={button} type="button" disabled={page <= 1 || query.isFetching} onClick={() => setPage(page - 1)}>← Trước</button><span className="text-xs text-slate-500">Trang {page}/{totalPages} · {query.data?.meta.totalItems ?? "—"} hoạt động</span><button className={button} type="button" disabled={page >= totalPages || page >= 100 || query.isFetching} onClick={() => setPage(page + 1)}>Sau →</button></footer>
    </div>
  </section>;
}
