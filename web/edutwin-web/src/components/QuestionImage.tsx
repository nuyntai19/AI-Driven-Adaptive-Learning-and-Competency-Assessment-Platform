import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { httpClient } from "../api/httpClient";
import { useAuthStore } from "../stores/authStore";
import { useModalAccessibility } from "../utils/useModalAccessibility";

export function QuestionImage({ questionId, hasImage, revision }: { questionId: string | number; hasImage?: boolean; revision?: string }) {
  const user = useAuthStore(s => s.user);
  const [url, setUrl] = useState(""); const [expanded, setExpanded] = useState(false);
  const modalRef = useRef<HTMLDivElement>(null); const closeRef = useRef<HTMLButtonElement>(null);
  useModalAccessibility({ isOpen: expanded, onClose: () => setExpanded(false), containerRef: modalRef, initialFocusRef: closeRef });
  const query = useQuery({
    queryKey: ["question-image", user?.centerId, user?.userId, String(questionId), revision || ""],
    queryFn: async ({ signal }) => (await httpClient.get<Blob>(`/questions/${encodeURIComponent(String(questionId))}/image`, { responseType: "blob", signal })).data,
    enabled: Boolean(hasImage && user && questionId), retry: 1, gcTime: 0, staleTime: 60_000,
  });
  useEffect(() => { setUrl(""); setExpanded(false); if (!query.data) return; const next = URL.createObjectURL(query.data); setUrl(next); return () => URL.revokeObjectURL(next); }, [query.data, questionId]);
  if (!hasImage) return null;
  if (query.isError) return <div role="alert" className="rounded-xl border border-amber-400 p-3 text-sm">Không tải được ảnh đề bài. <button type="button" onClick={() => query.refetch()} className="font-semibold underline">Thử lại</button></div>;
  if (!url) return <div role="status" className="text-sm">Đang tải ảnh đề bài...</div>;
  return <div className="space-y-2">
    <div className="rounded-xl border border-slate-200 bg-white p-3"><img src={url} alt="Ảnh đề bài" className="mx-auto max-h-[520px] max-w-full object-contain" /></div>
    <button type="button" onClick={() => setExpanded(true)} className="rounded-lg border border-indigo-400 px-3 py-1.5 text-sm font-semibold text-indigo-700 dark:text-indigo-300">Xem ảnh đề lớn</button>
    {expanded && <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/75 p-3" onClick={() => setExpanded(false)}>
      <div ref={modalRef} role="dialog" aria-modal="true" aria-label="Ảnh đề bài phóng lớn" className="max-h-[95vh] w-full max-w-6xl overflow-auto rounded-2xl bg-white p-4 text-slate-900" onClick={e => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between"><h2 className="font-bold">Ảnh đề bài</h2><button ref={closeRef} type="button" onClick={() => setExpanded(false)} className="rounded-lg border border-slate-400 px-4 py-2 font-semibold">Đóng</button></div>
        <img src={url} alt="Ảnh đề bài phóng lớn" className="mx-auto max-w-full" />
      </div>
    </div>}
  </div>;
}
