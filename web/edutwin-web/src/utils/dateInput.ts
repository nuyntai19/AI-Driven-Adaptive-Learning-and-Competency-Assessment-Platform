/**
 * Text form of date fields: always dd/mm/yyyy (and 24-hour HH:mm), whatever the browser's locale.
 * Values stay in the native input format: "yyyy-mm-dd", or "yyyy-mm-ddTHH:mm" with a time.
 */
const DATE = /^(\d{2})\/(\d{2})\/(\d{4})$/;
const DATE_TIME = /^(\d{2})\/(\d{2})\/(\d{4}) (\d{2}):(\d{2})$/;

/** "yyyy-mm-dd[THH:mm]" → "dd/mm/yyyy[ HH:mm]"; "" for anything else. */
export function formatDateInput(value: string | null | undefined, withTime = false): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(value ?? "");
  if (!m) return "";
  const date = `${m[3]}/${m[2]}/${m[1]}`;
  return withTime ? `${date} ${m[4] ?? "00"}:${m[5] ?? "00"}` : date;
}

/** "dd/mm/yyyy[ HH:mm]" → "yyyy-mm-dd[THH:mm]", or null when incomplete or not a real date. */
export function parseDateInput(text: string, withTime = false): string | null {
  const m = (withTime ? DATE_TIME : DATE).exec(text.trim());
  if (!m) return null;
  const [day, month, year] = [Number(m[1]), Number(m[2]), Number(m[3])];
  const check = new Date(year, month - 1, day);
  if (year < 1900 || check.getFullYear() !== year || check.getMonth() !== month - 1 || check.getDate() !== day) return null;
  if (withTime && (Number(m[4]) > 23 || Number(m[5]) > 59)) return null;
  const iso = `${m[3]}-${m[2]}-${m[1]}`;
  return withTime ? `${iso}T${m[4]}:${m[5]}` : iso;
}

/** True for a complete dd/mm/yyyy without a time (a date-time field then assumes 00:00). */
export function isDateOnlyText(text: string): boolean {
  return DATE.test(text.trim());
}

/** Adds the separators while typing digits: "0912" → "09/12/", "9/" → "09/", "… 7:" → "… 07:". */
export function maskDateInput(raw: string, withTime = false): string {
  const padded = raw.replace(/(^|[/\s])(\d)(?=[/.:-])/g, (_, separator: string, digit: string) => `${separator}0${digit}`);
  const digits = padded.replace(/\D/g, "").slice(0, withTime ? 12 : 8);
  let out = "";
  for (let i = 0; i < digits.length; i++) {
    if (i === 2 || i === 4) out += "/";
    else if (i === 8) out += " ";
    else if (i === 10) out += ":";
    out += digits[i];
  }
  // Show the next separator as soon as a group is complete, so typing feels natural.
  if (digits.length === 2 || digits.length === 4) out += "/";
  return out;
}
