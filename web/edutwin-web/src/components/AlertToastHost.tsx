import { useCallback, useEffect, useRef, useState } from "react";
import { NOTIFY_SELECTOR, notificationText as textOf, readNotification, type NotifyToastModel, type NotifyTone } from "../utils/notifyToast";

/**
 * One place for notifications: the bottom-right corner.
 *
 * 1. Page notifications (`data-notify="toast"`, e.g. "Không thể lưu cấu hình bài tập") are shown
 *    only here. The page element is hidden by CSS; the toast carries its text, its buttons (retry,
 *    close… proxied to the hidden originals) and the live-region role. Errors stay until closed or
 *    until the page clears them; successes (`data-notify-tone="success"`) leave after 5 s.
 * 2. Any other `role="alert"` (load failures, field errors, dialog errors) stays where the page
 *    shows it and is repeated here briefly, aria-hidden, so it is seen without scrolling.
 */


const ALERT_SELECTOR = '[role="alert"]';
/** Alerts already shown right where the user is looking (e.g. a sign-in form) opt out of the corner copy. */
const NO_MIRROR_SELECTOR = '[data-toast-mirror="off"]';
const HOST_ATTRIBUTE = "data-alert-toast-host";
const MIRROR_DURATION_MS = 6000;
const SUCCESS_DURATION_MS = 5000;
const MAX_VISIBLE_TOASTS = 4;


interface Toast extends NotifyToastModel {
  id: number;
  /** Page notification it mirrors; undefined for the brief copies of other alerts. */
  source?: Element;
}

const toneClass: Record<NotifyTone, string> = {
  error: "border-rose-300 bg-rose-50 text-rose-900 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100",
  success: "border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-100",
  info: "border-sky-300 bg-sky-50 text-sky-900 dark:border-sky-800 dark:bg-sky-950 dark:text-sky-100",
};

export function AlertToastHost() {
  const [toasts, setToastState] = useState<Toast[]>([]);
  const toastsRef = useRef<Toast[]>([]);
  const setToasts = useCallback((update: (current: Toast[]) => Toast[]) => {
    const next = update(toastsRef.current);
    if (next === toastsRef.current) return;
    toastsRef.current = next;
    setToastState(next);
  }, []);
  const nextId = useRef(0);
  const mirrored = useRef(new WeakMap<Element, string>());
  /** Text the user closed for a page notification; it reappears only if the text changes. */
  const dismissed = useRef(new WeakMap<Element, string>());

  useEffect(() => {
    const insideHost = (node: Node) => (node instanceof Element ? node : node.parentElement)?.closest(`[${HOST_ATTRIBUTE}]`);

    const reconcileNotifications = () => {
      const sources = [...document.querySelectorAll(NOTIFY_SELECTOR)];
      const current = toastsRef.current;
      let changed = false;
      const next = current.filter((toast) => {
        if (!toast.source) return true;
        const keep = sources.includes(toast.source);
        if (!keep) changed = true;
        return keep;
      });
      const added: Toast[] = [];
      for (const source of sources) {
        const model = readNotification(source);
        if (!model.text || dismissed.current.get(source) === model.text) continue;
        const index = next.findIndex((toast) => toast.source === source);
        if (index >= 0) {
          const old = next[index];
          if (old.text !== model.text || old.tone !== model.tone || JSON.stringify(old.actions) !== JSON.stringify(model.actions) || old.dismissIndex !== model.dismissIndex) {
            next[index] = { ...old, ...model };
            changed = true;
          }
          continue;
        }
        const toast = { id: ++nextId.current, source, ...model };
        next.push(toast);
        added.push(toast);
        changed = true;
      }
      if (!changed) return;
      setToasts(() => next.slice(-MAX_VISIBLE_TOASTS));
      for (const toast of added) {
        if (toast.tone === "error") continue; // errors stay until closed or cleared by the page
        setTimeout(() => {
          dismissed.current.set(toast.source!, toast.text);
          setToasts((list) => list.filter((item) => item.id !== toast.id));
        }, SUCCESS_DURATION_MS);
      }
    };

    const mirror = (element: Element) => {
      if (element.closest(NOTIFY_SELECTOR) || element.closest(`[${HOST_ATTRIBUTE}]`) || element.closest(NO_MIRROR_SELECTOR)) return;
      const text = textOf(element);
      if (!text || mirrored.current.get(element) === text) return;
      mirrored.current.set(element, text);
      if (toastsRef.current.some((toast) => !toast.source && toast.text === text)) return;
      const id = ++nextId.current;
      setToasts((current) => [...current, { id, text, tone: "error" as const, actions: [], dismissIndex: null }].slice(-MAX_VISIBLE_TOASTS));
      setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), MIRROR_DURATION_MS);
    };
    const scan = (node: Node) => {
      const element = node instanceof Element ? node : node.parentElement;
      if (!element) return;
      const owner = element.closest(ALERT_SELECTOR);
      if (owner) mirror(owner);
      if (node instanceof Element) node.querySelectorAll(ALERT_SELECTOR).forEach(mirror);
    };

    document.querySelectorAll(ALERT_SELECTOR).forEach(mirror);
    reconcileNotifications();
    const observer = new MutationObserver((records) => {
      let relevant = false;
      for (const record of records) {
        if (insideHost(record.target)) continue;
        relevant = true;
        if (record.type === "characterData") scan(record.target);
        else record.addedNodes.forEach(scan);
      }
      if (relevant) reconcileNotifications();
    });
    observer.observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ["data-notify", "data-notify-tone"] });
    return () => observer.disconnect();
  }, [setToasts]);

  const close = (toast: Toast) => {
    if (toast.source) {
      dismissed.current.set(toast.source, toast.text);
      // Let the page clear its own state (e.g. setError(null)) when it offers a close button.
      if (toast.dismissIndex !== null) (toast.source.querySelectorAll("button")[toast.dismissIndex] as HTMLButtonElement | undefined)?.click();
    }
    setToasts((current) => current.filter((item) => item.id !== toast.id));
  };

  return (
    <div {...{ [HOST_ATTRIBUTE]: "" }} className="pointer-events-none fixed bottom-4 right-4 z-[9999] flex w-[min(26rem,calc(100vw-2rem))] flex-col gap-2">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          // Page notifications are the live region now (their page element is hidden);
          // brief copies of other alerts stay aria-hidden because the original is announced.
          role={toast.source ? (toast.tone === "error" ? "alert" : "status") : undefined}
          aria-hidden={toast.source ? undefined : true}
          data-toast-tone={toast.tone}
          className={`pointer-events-auto flex items-start gap-2 rounded-xl border p-3 text-sm font-medium shadow-lg ${toneClass[toast.tone]}`}
        >
          <div className="min-w-0 flex-1">
            <p className="m-0 break-words line-clamp-6">{toast.text}</p>
            {toast.actions.length > 0 && (
              <div className="mt-2 flex flex-wrap gap-2">
                {toast.actions.map((action) => (
                  <button
                    key={action.index}
                    type="button"
                    className="rounded-lg border border-current px-2 py-1 text-xs font-semibold"
                    onClick={() => (toast.source?.querySelectorAll("button")[action.index] as HTMLButtonElement | undefined)?.click()}
                  >
                    {action.label}
                  </button>
                ))}
              </div>
            )}
          </div>
          <button
            type="button"
            aria-label="Đóng thông báo"
            tabIndex={toast.source ? 0 : -1}
            onClick={() => close(toast)}
            className="shrink-0 cursor-pointer opacity-70 hover:opacity-100"
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  );
}
