import { createPortal } from "react-dom";
import { useId, useRef, type ReactNode } from "react";
import { useModalAccessibility } from "../../utils/useModalAccessibility";
import { useThemeMode } from "../../utils/themeMode";

interface TeacherDrawerProps {
  isOpen: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}

export function TeacherDrawer({ isOpen, title, description, onClose, children, footer }: TeacherDrawerProps) {
  const containerRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  const { theme } = useThemeMode();

  useModalAccessibility({ isOpen, onClose, containerRef, initialFocusRef: closeButtonRef });

  if (!isOpen || typeof document === "undefined") return null;

  return createPortal(
    <div data-actor="teacher" data-theme={theme} className="fixed inset-0 z-50 flex justify-end bg-black/60 backdrop-blur-sm" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <aside
        ref={containerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        tabIndex={-1}
        className="flex h-full w-full max-w-xl flex-col border-l-[1.5px] border-[var(--th-border)] bg-[var(--th-surface)] shadow-2xl"
      >
        <header className="flex items-start justify-between gap-4 border-b-[1.5px] border-[var(--th-border)] bg-[var(--th-surface-muted)] p-4 sm:p-5">
          <div>
            <h2 id={titleId} className="text-base sm:text-lg font-black text-[var(--th-text)]">{title}</h2>
            {description && <p id={descriptionId} className="mt-1 text-xs font-medium text-[var(--th-text-secondary)]">{description}</p>}
          </div>
          <button ref={closeButtonRef} type="button" className="th-icon-button h-8 w-8 text-sm" aria-label="Đóng bảng điều khiển" onClick={onClose}>×</button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto p-5">{children}</div>
        {footer && <footer className="border-t-[1.5px] border-[var(--th-border)] bg-[var(--th-surface-muted)] p-4">{footer}</footer>}
      </aside>
    </div>,
    document.body,
  );
}

interface TeacherModalProps {
  isOpen: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  maxWidth?: string;
  size?: "sm" | "md" | "lg" | "xl";
}

export function TeacherModal({
  isOpen,
  title,
  description,
  onClose,
  children,
  footer,
  maxWidth,
  size = "md",
}: TeacherModalProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const descriptionId = useId();
  const { theme } = useThemeMode();

  useModalAccessibility({ isOpen, onClose, containerRef, initialFocusRef: closeButtonRef });

  if (!isOpen || typeof document === "undefined") return null;

  // Responsive width percentages based on size
  const responsiveWidthClass =
    maxWidth ||
    (size === "sm"
      ? "w-[92%] sm:w-[75%] md:w-[55%] lg:w-[40%] max-w-md"
      : size === "lg"
      ? "w-[96%] sm:w-[92%] md:w-[85%] lg:w-[76%] xl:w-[68%] max-w-5xl"
      : size === "xl"
      ? "w-[98%] sm:w-[94%] md:w-[90%] lg:w-[84%] xl:w-[78%] max-w-6xl"
      : "w-[94%] sm:w-[88%] md:w-[78%] lg:w-[65%] xl:w-[55%] max-w-3xl");

  return createPortal(
    <div
      data-actor="teacher"
      data-theme={theme}
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/60 p-3 sm:p-5 backdrop-blur-sm animate-in fade-in duration-150"
      onMouseDown={(event) => event.target === event.currentTarget && onClose()}
    >
      <div
        ref={containerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        tabIndex={-1}
        className={`th-surface flex max-h-[88vh] ${responsiveWidthClass} flex-col overflow-hidden rounded-2xl border-[1.5px] border-[var(--th-border)] shadow-2xl`}
      >
        <header className="flex items-start justify-between gap-4 border-b-[1.5px] border-[var(--th-border)] bg-[var(--th-surface-muted)] p-4 sm:p-5">
          <div className="min-w-0 flex-1">
            <h2 id={titleId} className="text-base sm:text-lg font-black text-[var(--th-text)] truncate">{title}</h2>
            {description && <p id={descriptionId} className="mt-1 text-xs font-medium text-[var(--th-text-secondary)]">{description}</p>}
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            className="th-icon-button h-8 w-8 text-base shrink-0"
            aria-label="Đóng cửa sổ"
            onClick={onClose}
          >
            ×
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6">{children}</div>
        {footer && <footer className="border-t-[1.5px] border-[var(--th-border)] bg-[var(--th-surface-muted)] p-3.5 sm:p-4">{footer}</footer>}
      </div>
    </div>,
    document.body,
  );
}

interface TeacherConfirmDialogProps {
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

export function TeacherConfirmDialog({
  isOpen,
  title,
  description,
  confirmLabel = "Xác nhận",
  cancelLabel = "Hủy bỏ",
  tone = "default",
  isConfirming = false,
  onConfirm,
  onClose,
}: TeacherConfirmDialogProps) {
  return (
    <TeacherModal
      isOpen={isOpen}
      title={title}
      description={description}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            disabled={isConfirming}
            className="th-secondary-button text-xs"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isConfirming}
            className={tone === "danger" ? "th-danger-button text-xs" : "th-primary-button text-xs"}
          >
            {isConfirming ? "Đang xử lý..." : confirmLabel}
          </button>
        </div>
      }
    >
      <p className="text-sm text-[var(--th-text-secondary)]">
        Hành động này sẽ được ghi nhận vào hệ thống học thuật của lớp.
      </p>
    </TeacherModal>
  );
}
