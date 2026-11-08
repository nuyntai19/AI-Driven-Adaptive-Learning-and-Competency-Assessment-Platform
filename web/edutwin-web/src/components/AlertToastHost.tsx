import { useEffect, useRef, useState } from "react";

const ALERT_SELECTOR = '[role="alert"]';
const TOAST_DURATION_MS = 4000;
const MAX_VISIBLE_TOASTS = 3;

function alertText(element: Element): string {
  const copy = element.cloneNode(true) as Element;
  copy.querySelectorAll("button").forEach((button) => button.remove());
  return copy.textContent?.replace(/\s+/g, " ").trim() ?? "";
}

/**
 * Repeats every error the app shows (any role="alert") in a corner toast, so
 * it is seen without scrolling to wherever the page rendered it. Inline
 * messages stay in place because some carry a retry action. The toasts are
 * aria-hidden: screen readers already announce the role="alert" element.
 */
export function AlertToastHost() {
  const [toasts, setToasts] = useState<{ id: number; text: string }[]>([]);
  const nextId = useRef(0);
  const shownText = useRef(new WeakMap<Element, string>());

  useEffect(() => {
    const report = (element: Element) => {
      const text = alertText(element);
      if (!text || shownText.current.get(element) === text) return;
      shownText.current.set(element, text);
      const id = ++nextId.current;
      setToasts((current) => current.some((toast) => toast.text === text)
        ? current
        : [...current, { id, text }].slice(-MAX_VISIBLE_TOASTS));
      setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), TOAST_DURATION_MS);
    };
    const scan = (node: Node) => {
      const element = node instanceof Element ? node : node.parentElement;
      if (!element) return;
      const owner = element.closest(ALERT_SELECTOR);
      if (owner) report(owner);
      if (node instanceof Element) node.querySelectorAll(ALERT_SELECTOR).forEach(report);
    };

    document.querySelectorAll(ALERT_SELECTOR).forEach(report);
    const observer = new MutationObserver((records) => {
      for (const record of records) {
        if (record.type === "characterData") scan(record.target);
        else record.addedNodes.forEach(scan);
      }
    });
    observer.observe(document.body, { subtree: true, childList: true, characterData: true });
    return () => observer.disconnect();
  }, []);

  if (toasts.length === 0) return null;
  return (
    <div aria-hidden="true" className="fixed bottom-4 right-4 z-[9999] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2">
      {toasts.map((toast) => (
        <div key={toast.id} className="flex items-start gap-2 rounded-xl border border-rose-300 bg-rose-50 p-3 text-sm font-medium text-rose-900 shadow-lg dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100">
          <span className="min-w-0 flex-1 break-words line-clamp-4">{toast.text}</span>
          <button
            type="button"
            tabIndex={-1}
            onClick={() => setToasts((current) => current.filter((item) => item.id !== toast.id))}
            className="shrink-0 cursor-pointer text-rose-500 hover:text-rose-800 dark:hover:text-rose-200"
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  );
}
