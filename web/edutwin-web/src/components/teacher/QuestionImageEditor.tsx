import { useState } from "react";
import { normalizeQuestionImage } from "../../utils/questionImage";
import { QuestionImage } from "../QuestionImage";

export function QuestionImageEditor({ value, existingQuestionId, hasExistingImage, onChange, onBusyChange }: {
  value?: string; existingQuestionId?: string; hasExistingImage: boolean;
  onChange: (value: string | undefined, remove: boolean) => void; onBusyChange: (busy: boolean) => void;
}) {
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  return <section className="space-y-3 rounded-xl border border-indigo-400/40 bg-indigo-500/5 p-4">
    <h3 className="text-sm font-bold">Ảnh đề bài / hình minh họa</h3>
    <p className="text-sm opacity-80">Có thể dùng ảnh chứa toàn bộ đề, không cần gõ lại nội dung. Vẫn nhập đáp án, lời giải và tiêu chí chấm bên dưới. Hãy cắt ảnh rõ chữ và ký hiệu, không đưa thông tin cá nhân vào ảnh.</p>
    <label className="inline-flex cursor-pointer items-center rounded-lg border border-indigo-400 px-4 py-2 text-sm font-semibold">
      {busy ? "Đang xử lý ảnh..." : "Chọn ảnh PNG / JPG / WebP"}
      <input aria-label="Chọn ảnh đề bài" type="file" accept="image/png,image/jpeg,image/webp" disabled={busy} className="sr-only" onChange={async e => {
        const file = e.target.files?.[0]; e.target.value = ""; if (!file) return;
        setError(""); setBusy(true); onBusyChange(true);
        try { onChange(await normalizeQuestionImage(file), false); }
        catch (err) { setError(err instanceof Error ? err.message : "Không xử lý được ảnh."); }
        finally { setBusy(false); onBusyChange(false); }
      }} />
    </label>
    <p className="text-xs opacity-70">Một ảnh/câu · ảnh gốc tối đa 8 MB · ảnh lưu tối đa 2 MB, cạnh dài tối đa 2048 px. Không tự suy đoán dữ kiện từ ảnh mờ.</p>
    {error && <p role="alert" className="text-sm text-rose-600 dark:text-rose-300">{error}</p>}
    {value ? <div className="rounded-xl bg-white p-3"><img src={value} alt="Xem trước ảnh đề bài" className="mx-auto max-h-96 max-w-full object-contain" /></div> :
      hasExistingImage && existingQuestionId ? <QuestionImage questionId={existingQuestionId} hasImage /> : null}
    {(value || hasExistingImage) && <button type="button" disabled={busy} onClick={() => onChange(undefined, true)} className="rounded-lg border border-rose-400 px-3 py-1.5 text-sm font-semibold text-rose-600 dark:text-rose-300">Bỏ ảnh khỏi câu hỏi</button>}
  </section>;
}
