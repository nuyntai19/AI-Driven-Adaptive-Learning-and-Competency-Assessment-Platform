import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getAttemptAttachment } from "../../api/learningFeedbackApi";
import { useAuthStore } from "../../stores/authStore";

/** Shared, authenticated, read-only view of an immutable submitted scratchpad. */
export function AttemptScratchpadAttachment({ attemptId }: { attemptId: string | number }) {
  const user = useAuthStore((state) => state.user);
  const [image, setImage] = useState<{ blob: Blob; url: string } | null>(null);
  const [imageFailed, setImageFailed] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const query = useQuery({
    queryKey: ["attempt-attachment", user?.centerId, user?.userId, String(attemptId)],
    queryFn: ({ signal }) => getAttemptAttachment(attemptId, signal),
    enabled: Boolean(user),
    retry: 1,
    staleTime: 60_000,
    gcTime: 60_000,
  });

  useEffect(() => {
    if (!query.data) return;
    const url = URL.createObjectURL(query.data);
    setImage({ blob: query.data, url });
    setImageFailed(false);
    return () => URL.revokeObjectURL(url);
  }, [query.data]);

  useEffect(() => {
    setExpanded(false);
  }, [attemptId]);

  useEffect(() => {
    if (!expanded) return;
    const close = (event: KeyboardEvent) => {
      if (event.key === "Escape") setExpanded(false);
    };
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, [expanded]);

  // Never display an object URL from the previously selected question/account.
  const url = image?.blob === query.data ? image?.url : null;
  const failed = query.isError || imageFailed;

  return (
    <section aria-label="Bản vẽ nháp đã nộp" className="mt-4 rounded-2xl border border-emerald-200 dark:border-emerald-800 bg-emerald-50/80 dark:bg-emerald-950/40 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-xs font-bold text-emerald-800 dark:text-emerald-300">✏️ Bản vẽ nháp đã nộp</p>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">Ảnh gốc được lưu cùng bài làm và chỉ có thể xem.</p>
        </div>
        {url && !failed && (
          <button type="button" onClick={() => setExpanded(true)} className="rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-emerald-500">
            Xem to
          </button>
        )}
      </div>
      {failed ? (
        <div role="alert" className="mt-3 text-sm text-amber-800 dark:text-amber-300">
          Không thể tải ảnh nháp lúc này. Bài nộp của bạn không bị thay đổi.
          <button type="button" disabled={query.isFetching} onClick={() => void query.refetch()} className="ml-2 underline disabled:opacity-50">Tải lại ảnh</button>
        </div>
      ) : url ? (
        <button type="button" onClick={() => setExpanded(true)} aria-label="Phóng to bản vẽ nháp đã nộp" className="mt-3 block">
          <img src={url} alt="Bản nháp của học sinh" onError={() => setImageFailed(true)} className="max-h-40 max-w-full rounded-lg border border-emerald-200 bg-white object-contain" />
        </button>
      ) : <p role="status" className="mt-3 text-xs text-slate-500">Đang tải ảnh nháp…</p>}
      {expanded && url && !failed && (
        <div role="dialog" aria-modal="true" aria-label="Ảnh nháp đã nộp" className="fixed inset-0 z-[1000] flex items-center justify-center bg-black/80 p-4" onClick={() => setExpanded(false)}>
          <div className="relative max-h-[90vh] max-w-[95vw] overflow-auto rounded-xl bg-white p-4" onClick={(event) => event.stopPropagation()}>
            <button autoFocus type="button" onClick={() => setExpanded(false)} className="mb-3 rounded-lg bg-slate-100 px-3 py-1.5 text-sm font-bold text-slate-900">Đóng (Esc)</button>
            <img src={url} alt="Bản vẽ nháp gốc của học sinh" className="max-h-[75vh] max-w-full object-contain" />
          </div>
        </div>
      )}
    </section>
  );
}
