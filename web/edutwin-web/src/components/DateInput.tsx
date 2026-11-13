import { useEffect, useRef, useState, type InputHTMLAttributes } from "react";
import { formatDateInput, isDateOnlyText, maskDateInput, parseDateInput } from "../utils/dateInput";

/**
 * Date fields that always read and type as dd/mm/yyyy (and 24-hour HH:mm), whatever the browser's locale.
 * A native <input type="date"> follows the browser language, so an English browser shows mm/dd/yyyy.
 * The value stays in the native format ("yyyy-mm-dd", or "yyyy-mm-ddTHH:mm" with time), so these are
 * drop-in replacements; the calendar button opens the browser's own picker.
 */
type BaseProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "value" | "defaultValue" | "onChange" | "min" | "max"> & {
  /** "yyyy-mm-dd" (date) or "yyyy-mm-ddTHH:mm" (date and time); "" when empty or not yet complete. */
  value: string;
  onChange: (value: string) => void;
  min?: string;
  max?: string;
};

const MARGIN = /^!?-?m[trblxy]?-/;

function DateField({ withTime, value, onChange, min, max, className = "", style, disabled, required, onBlur, placeholder, title, ...rest }: BaseProps & { withTime: boolean }) {
  const [text, setText] = useState(() => formatDateInput(value, withTime));
  const textRef = useRef<HTMLInputElement>(null);
  const pickerRef = useRef<HTMLInputElement>(null);

  // Follow changes made outside (a reset, a suggested date) without wiping a date that is still being typed.
  useEffect(() => {
    setText(current => (parseDateInput(current, withTime) ?? "") === value ? current : formatDateInput(value, withTime));
  }, [value, withTime]);

  const parsed = parseDateInput(text, withTime);
  const problem = text && !parsed ? `Nhập theo dạng ${withTime ? "dd/mm/yyyy hh:mm" : "dd/mm/yyyy"}.`
    : parsed && min && parsed < min ? `Chọn từ ${formatDateInput(min, withTime)} trở đi.`
    : parsed && max && parsed > max ? `Chọn đến ${formatDateInput(max, withTime)} là muộn nhất.` : "";
  useEffect(() => { textRef.current?.setCustomValidity(problem); }, [problem]);

  const openPicker = () => {
    const picker = pickerRef.current;
    if (!picker || disabled) return;
    try { picker.showPicker(); } catch { picker.focus(); picker.click(); }
  };

  // Margins belong to the outer box so the calendar button stays centred on the field itself.
  const classes = className.split(/\s+/).filter(Boolean);
  const outer = classes.filter(c => MARGIN.test(c)).join(" ");
  const inner = classes.filter(c => !MARGIN.test(c)).join(" ");

  // A span (not a div) so the field can sit inside a <label>, like the native input it replaces.
  return <span className={`relative block ${outer}`}>
    <input {...rest} ref={textRef} type="text" inputMode="numeric" autoComplete="off" lang="vi"
      placeholder={placeholder ?? (withTime ? "dd/mm/yyyy hh:mm" : "dd/mm/yyyy")}
      maxLength={withTime ? 16 : 10} disabled={disabled} required={required}
      title={problem || title} aria-invalid={rest["aria-invalid"] ?? (problem ? true : undefined)}
      className={`${inner} w-full`} style={{ ...style, paddingRight: "2.5rem" }} value={text}
      onChange={e => {
        const raw = e.target.value;
        const next = raw.length > text.length ? maskDateInput(raw, withTime) : raw;
        setText(next);
        onChange(parseDateInput(next, withTime) ?? "");
      }}
      onBlur={e => {
        // A time left out of a date-time field means the start of the day.
        if (withTime && isDateOnlyText(text)) {
          const next = `${text.trim()} 00:00`; setText(next); onChange(parseDateInput(next, true) ?? "");
        }
        onBlur?.(e);
      }} />
    <button type="button" tabIndex={-1} disabled={disabled} onClick={openPicker} aria-label={withTime ? "Mở lịch chọn ngày giờ" : "Mở lịch chọn ngày"}
      className="absolute inset-y-0 right-1 my-auto flex h-8 w-8 items-center justify-center rounded-md opacity-60 hover:opacity-100 disabled:cursor-not-allowed disabled:opacity-30">
      <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
        <rect x="3" y="4" width="18" height="18" rx="2" /><path d="M16 2v4M8 2v4M3 10h18" />
      </svg>
    </button>
    {/* The browser's own calendar, opened by the button above; it is never shown as a field. It points at no form,
        so only the visible field takes part in form validation. */}
    <input ref={pickerRef} type={withTime ? "datetime-local" : "date"} tabIndex={-1} aria-hidden="true" disabled={disabled} form="date-input-picker-detached"
      className="pointer-events-none absolute bottom-0 left-0 h-px w-full opacity-0" value={value} min={min} max={max}
      onChange={e => { setText(formatDateInput(e.target.value, withTime)); onChange(e.target.value); }} />
  </span>;
}

/** Date field shown and typed as dd/mm/yyyy; value is "yyyy-mm-dd". */
export function DateInput(props: BaseProps) {
  return <DateField {...props} withTime={false} />;
}

/** Date-time field shown and typed as dd/mm/yyyy hh:mm (24-hour); value is "yyyy-mm-ddTHH:mm". */
export function DateTimeInput(props: BaseProps) {
  return <DateField {...props} withTime />;
}
