import { RichMathEditor } from "../math/RichMathEditor";

/**
 * The student's answer/explanation box. Subjects that use formulas get the math editor (with "Chèn công thức" and
 * the "$" shortcut); language subjects get a plain text box, where "$" is just a character.
 */
export function WorkTextBox({ math, id, label, value, onChange, placeholder, minHeight = "120px", disabled }: {
  math: boolean; id?: string; label: string; value: string; onChange: (value: string) => void;
  placeholder?: string; minHeight?: string; disabled?: boolean;
}) {
  if (math) return <RichMathEditor id={id} variant="student" value={value} onChange={onChange} minHeight={minHeight} placeholder={placeholder} disabled={disabled} />;
  return <textarea id={id} aria-label={label} value={value} disabled={disabled} onChange={e => onChange(e.target.value)} placeholder={placeholder}
    style={{ minHeight }}
    className="block w-full resize-y rounded-xl border border-slate-300 bg-white p-3 text-sm leading-relaxed text-slate-900 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100" />;
}
