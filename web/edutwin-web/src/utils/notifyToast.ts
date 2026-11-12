/**
 * Page notifications shown as bottom-right toasts by AlertToastHost.
 *
 * Mark a page-level message with `data-notify="toast"` (and `data-notify-tone="success" | "info"`
 * for non-errors). The element itself is hidden by CSS; its text, buttons (proxied) and live-region
 * role move to the toast. A button with `data-notify-dismiss`, an "✕"/"×", or a label starting with
 * "Đóng" is treated as the close button, so closing the toast also clears the page state.
 */

export const NOTIFY_SELECTOR = '[data-notify="toast"]';

export type NotifyTone = "error" | "success" | "info";

export interface NotifyToastModel {
  text: string;
  tone: NotifyTone;
  actions: { label: string; index: number }[];
  dismissIndex: number | null;
}

const CLOSE_LABELS = new Set(["✕", "×", "x", "close"]);

export function isCloseLabel(label: string): boolean {
  const normalized = label.replace(/\s+/g, " ").trim().toLocaleLowerCase("vi");
  return CLOSE_LABELS.has(normalized) || normalized.startsWith("đóng");
}

/**
 * Visible text of an element without its buttons, whitespace collapsed. Separate elements are not
 * glued together ("Không thể lưu…" + "Bài thi…"): a heading ends with ":" and other elements with a space.
 */
export function notificationText(element: Element): string {
  const copy = element.cloneNode(true) as Element;
  copy.querySelectorAll("button").forEach((button) => button.remove());
  copy.querySelectorAll("*").forEach((child) => {
    const text = child.textContent?.trim() ?? "";
    const heading = /^H[1-6]$/.test(child.tagName) && text && !/[.:!?…]$/.test(text);
    child.append(heading ? ": " : " ");
  });
  return copy.textContent?.replace(/\s+/g, " ").replace(/\s+([:.,!?])/g, "$1").trim() ?? "";
}

/** Reads a page notification: text, tone, and which buttons are actions or "close". */
export function readNotification(element: Element): NotifyToastModel {
  const tone = element.getAttribute("data-notify-tone");
  const actions: { label: string; index: number }[] = [];
  let dismissIndex: number | null = null;
  element.querySelectorAll("button").forEach((button, index) => {
    const label = (button.getAttribute("aria-label") || button.textContent || "").replace(/\s+/g, " ").trim();
    if (button.hasAttribute("data-notify-dismiss") || isCloseLabel(label)) {
      if (dismissIndex === null) dismissIndex = index;
    } else if (label) actions.push({ label, index });
  });
  return { text: notificationText(element), tone: tone === "success" || tone === "info" ? tone : "error", actions, dismissIndex };
}
