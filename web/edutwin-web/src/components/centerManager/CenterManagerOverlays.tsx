import { createPortal } from "react-dom";
import { useId, useRef, type ReactNode } from "react";
import { useModalAccessibility } from "../../utils/useModalAccessibility";

interface DrawerProps {
  isOpen: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}

export function Drawer({ isOpen, title, description, onClose, children, footer }: DrawerProps) {
  const containerRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useModalAccessibility({ isOpen, onClose, containerRef, initialFocusRef: closeButtonRef });

  if (!isOpen || typeof document === "undefined") return null;

  return createPortal(
    <div data-actor="center-manager" className="fixed inset-0 z-50 flex justify-end bg-slate-950/70" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <aside
        ref={containerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        tabIndex={-1}
        className="flex h-full w-full max-w-xl flex-col border-l border-[var(--cm-border)] bg-[var(--cm-surface)] shadow-2xl"
      >
        <header className="flex items-start justify-between gap-4 border-b border-[var(--cm-border-subtle)] p-5">
          <div>
            <h2 id={titleId} className="text-lg font-semibold text-[var(--cm-text)]">{title}</h2>
            {description && <p id={descriptionId} className="mt-1 text-sm text-[var(--cm-text-secondary)]">{description}</p>}
          </div>
          <button ref={closeButtonRef} type="button" className="cm-icon-button h-10 w-10 border border-[var(--cm-border)]" aria-label="Đóng bảng điều khiển" onClick={onClose}>×</button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto p-5">{children}</div>
        {footer && <footer className="border-t border-[var(--cm-border-subtle)] p-5">{footer}</footer>}
      </aside>
    </div>,
    document.body,
  );
}

interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: "default" | "danger";
  isConfirming?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

export function ConfirmDialog({
  isOpen,
  title,
  description,
  confirmLabel = "Xác nhận",
  cancelLabel = "Hủy",
  tone = "default",
  isConfirming = false,
  onConfirm,
  onClose,
}: ConfirmDialogProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const cancelButtonRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useModalAccessibility({ isOpen, onClose, containerRef, initialFocusRef: cancelButtonRef });

  if (!isOpen || typeof document === "undefined") return null;

  return createPortal(
    <div data-actor="center-manager" className="fixed inset-0 z-50 grid place-items-center bg-slate-950/70 p-4" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <div ref={containerRef} role="alertdialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={descriptionId} tabIndex={-1} className="cm-surface w-full max-w-md p-6">
        <h2 id={titleId} className="text-lg font-semibold text-[var(--cm-text)]">{title}</h2>
        <p id={descriptionId} className="mt-2 text-sm leading-6 text-[var(--cm-text-secondary)]">{description}</p>
        <div className="mt-6 flex justify-end gap-3">
          <button ref={cancelButtonRef} type="button" className="cm-secondary-button" onClick={onClose} disabled={isConfirming}>{cancelLabel}</button>
          <button
            type="button"
            className={tone === "danger" ? "cm-primary-button !bg-rose-600 hover:!bg-rose-500" : "cm-primary-button"}
            onClick={onConfirm}
            disabled={isConfirming}
          >
            {isConfirming ? "Đang xử lý…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  );
}

