import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { httpClient } from "../api/httpClient";
import { useAuthStore } from "../stores/authStore";

interface HistoryItem { actionType: string; actorName: string; source: string; reason: string; createdAt: string }
interface History { data: HistoryItem[]; page: number; totalPages: number; totalItems: number }
const labels: Record<string,string> = { ClassArchived:"Lưu trữ lớp", ClassReopened:"Mở lại lớp", ClassUpdated:"Cập nhật lớp", ClassScopeCorrected:"Sửa phân loại phạm vi (hệ thống)" };
export function ClassHistoryPanel({ classId }: { classId: string }) {
  const [page, setPage] = useState(1);
  const actor = useAuthStore(s => s.user);
  const query = useQuery({ queryKey:["class-history",actor?.centerId,actor?.userId,classId,page], queryFn:async () =>
    (await httpClient.get<History>(`/classes/${classId}/history`, {params:{page}})).data });
  return <details className="rounded-xl border p-4"><summary className="font-semibold cursor-pointer">Nhật ký thay đổi lớp</summary>
    {query.isPending ? <p>Đang tải nhật ký...</p> : query.isError ? <p role="alert">Không tải được nhật ký. <button onClick={() => query.refetch()}>Tải lại</button></p> : <>
      {!query.data.data.length && <p className="mt-3">Chưa có thay đổi được ghi nhận. Dữ liệu cũ không được gán giả người thực hiện.</p>}
      <ul className="space-y-3 mt-3">{query.data.data.map((item,index) => <li key={index}>
        <strong>{labels[item.actionType] ?? item.actionType}</strong> · {item.actorName} · {new Date(item.createdAt).toLocaleString("vi-VN")}
        <p>{item.reason}</p><span className="text-xs">Nguồn: {item.source}</span>
      </li>)}</ul>
      {query.data.totalPages > 1 && <div className="flex gap-3 mt-3"><button disabled={page === 1} onClick={() => setPage(page-1)}>Trước</button>
        <span>Trang {page}/{query.data.totalPages}</span><button disabled={page >= query.data.totalPages} onClick={() => setPage(page+1)}>Sau</button></div>}
    </>}
  </details>;
}
