import { useId, useState } from "react";
import { QUESTION_IMPORT_LATEX_EXAMPLES } from "../../utils/questionImportLatexGuide";
import { RichMathText } from "../math/RichMathText";

/** Inline disclosure keeps the import form and its current preview mounted. */
export function ImportLatexGuide({ allowDisplay = false }: { allowDisplay?: boolean } = {}) {
  const panelId = useId();
  const [expanded, setExpanded] = useState(false);
  const [message, setMessage] = useState("");

  async function copyExample(label: string, latex: string) {
    try {
      await navigator.clipboard.writeText(latex);
      setMessage(`Đã sao chép mã: ${label}. Dán vào nội dung câu hỏi trong file.`);
    } catch {
      setMessage("Chưa thể sao chép tự động. Chọn mã công thức rồi nhấn Ctrl+C (hoặc ⌘C trên macOS).");
    }
  }

  return <div className="rounded-xl border border-teal-500/30 bg-teal-50/60 dark:bg-teal-950/20">
    <button
      type="button"
      aria-expanded={expanded}
      aria-controls={panelId}
      onClick={() => setExpanded((value) => !value)}
      className="flex w-full items-center justify-between gap-3 rounded-xl px-4 py-3 text-left text-sm font-semibold text-teal-800 dark:text-teal-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500"
    >
      <span><span aria-hidden="true" className="mr-2">∑</span>Bảng cú pháp LaTeX</span>
      <span aria-hidden="true" className="text-xs">{expanded ? "Thu gọn ▲" : "Xem hướng dẫn ▼"}</span>
    </button>
    <section id={panelId} hidden={!expanded} aria-label="Hướng dẫn công thức cho file import" className="space-y-4 border-t border-teal-500/20 px-4 pb-4 pt-3">
      {expanded && <>
      <div className="space-y-2 text-xs leading-relaxed text-slate-700 dark:text-slate-300">
        <p>Nếu điền trong Excel, đặt ô ở định dạng <strong>Text (Văn bản)</strong>. Chọn <strong>Sao chép mã</strong> rồi dán công thức vào nội dung đề, phương án hoặc lời giải; có thể gõ thêm chữ ở ngoài hai dấu <code>$</code>.</p>
        <p>Ví dụ một ô: <code className="select-all rounded bg-white/70 px-1 dark:bg-slate-900/60">{`Tính ${QUESTION_IMPORT_LATEX_EXAMPLES[1].latex}.`}</code></p>
        <p>Dùng một dấu gạch chéo ngược <code>\</code> cho mỗi lệnh. {allowDisplay ? <>Nguồn Word và gói ZIP V2 cho phép công thức riêng dòng bằng <code>$$...$$</code>; Word Equation được chuyển đổi trong phạm vi hỗ trợ. Kiểm tra lại mọi công thức ở bước xem trước.</> : <>Nguồn Excel/CSV V1 chỉ dùng công thức inline trên một dòng; chưa hỗ trợ <code>$$...$$</code> hoặc công cụ Insert Equation.</>} Không dùng công thức tính của Excel.</p>
        <p>Mã mẫu dùng cho đề, phương án và lời giải. Riêng đáp án chấm số <code>NumericRational</code>, nhập <code>1/2</code> thay vì <code>$1/2$</code>; đáp án tọa độ <code>Coordinate2D</code> nhập dạng <code>(1; 2)</code>.</p>
      </div>
      <div className="grid max-h-[28rem] grid-cols-1 gap-3 overflow-y-auto pr-1 lg:grid-cols-2">
        {QUESTION_IMPORT_LATEX_EXAMPLES.map(({ label, latex }) => <article key={label} className="min-w-0 space-y-3 rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900">
          <div className="flex items-center justify-between gap-3">
            <h3 className="text-xs font-semibold text-slate-800 dark:text-slate-100">{label}</h3>
            <button type="button" aria-label={`Sao chép mã ${label.toLowerCase()}`} onClick={() => copyExample(label, latex)} className="shrink-0 rounded-md border border-teal-500/30 px-2 py-1 text-xs font-medium text-teal-700 hover:bg-teal-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500 dark:text-teal-300 dark:hover:bg-teal-950/50">Sao chép mã</button>
          </div>
          <code className="block select-all overflow-x-auto rounded bg-slate-100 px-2 py-2 text-xs text-slate-700 dark:bg-slate-800 dark:text-slate-200">{latex}</code>
          <div className="min-h-10 overflow-x-auto py-1 text-slate-900 dark:text-slate-100" aria-label={`Xem trước ${label.toLowerCase()}`}><RichMathText text={latex} /></div>
        </article>)}
      </div>
      <p role="status" aria-live="polite" className="min-h-4 text-xs text-teal-800 dark:text-teal-200">{message || "Mỗi mã mẫu có đủ hai dấu $ để dán trực tiếp vào nội dung câu hỏi."}</p>
      </>}
    </section>
  </div>;
}
