import { useEffect, useState } from "react";
import { questionsApi } from "../../api/questionsApi";

interface ImportPreviewImageProps {
  assetId: string;
  previewToken: string;
  revision: number;
  questionKey: string;
  width?: number | null;
  height?: number | null;
  bytes?: number | null;
}

/** Object URLs are scoped to the authenticated preview and revoked on any change. */
export function ImportPreviewImage({ assetId, previewToken, revision, questionKey, width, height, bytes }: ImportPreviewImageProps) {
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState(false);
  const [retry, setRetry] = useState(0);
  const [zoomed, setZoomed] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let objectUrl: string | null = null;
    setUrl(null); setError(false); setZoomed(false);
    void questionsApi.getImportV2Asset(assetId, previewToken, revision, controller.signal).then((blob) => {
      if (!active) return;
      if (!["image/png", "image/jpeg", "image/webp"].includes(blob.type.toLowerCase())) throw new Error("Invalid image response");
      objectUrl = URL.createObjectURL(blob); setUrl(objectUrl);
    }).catch(() => { if (active && !controller.signal.aborted) setError(true); });
    return () => { active = false; controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [assetId, previewToken, revision, retry]);
  return <figure className="space-y-2 rounded-lg border border-slate-200 p-3 dark:border-slate-700">
    <figcaption className="flex flex-wrap items-center justify-between gap-2 text-xs">
      <strong>Ảnh đề bài • {questionKey}</strong>
      {url && <button type="button" aria-expanded={zoomed} onClick={() => setZoomed((value) => !value)} className="rounded border border-teal-500/30 px-2 py-1 text-teal-700 dark:text-teal-300">{zoomed ? "Thu nhỏ ảnh" : "Phóng to ảnh"}</button>}
    </figcaption>
    {error ? <p role="alert" className="text-xs text-rose-600 dark:text-rose-400">Không tải được ảnh của phiên xem trước. <button type="button" onClick={() => setRetry((value) => value + 1)} className="underline">Thử lại</button></p>
      : url ? <div className={`overflow-auto rounded bg-slate-100 dark:bg-slate-800 ${zoomed ? "max-h-[32rem]" : ""}`}><img src={url} alt={`Ảnh đề bài câu ${questionKey}`} className={zoomed ? "h-auto max-w-none" : "mx-auto max-h-64 max-w-full object-contain"} style={zoomed && width ? { width } : undefined} /></div>
        : <p role="status" className="text-xs text-slate-500">Đang tải ảnh đã kiểm tra...</p>}
    {(width || height || bytes) && <p className="text-xs text-slate-500 dark:text-slate-400">{width && height ? `${width} × ${height} px` : ""}{bytes ? ` • ${(bytes / 1024).toFixed(1)} KiB` : ""}</p>}
  </figure>;
}
