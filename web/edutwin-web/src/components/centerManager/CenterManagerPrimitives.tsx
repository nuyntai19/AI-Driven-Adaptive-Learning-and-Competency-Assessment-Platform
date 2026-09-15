import type { ReactNode } from "react";
import { extractProblemDetails, mapSafeOperationalError } from "../../utils/problemDetails";

export interface BreadcrumbItem {
  label: string;
  href?: string;
}

interface PageHeaderProps {
  title: string;
  description?: string;
  eyebrow?: string;
  breadcrumbs?: BreadcrumbItem[];
  actions?: ReactNode;
}

export function PageHeader({ title, description, eyebrow, breadcrumbs = [], actions }: PageHeaderProps) {
  return (
    <header className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        {breadcrumbs.length > 0 && (
          <nav aria-label="Breadcrumb" className="mb-3">
            <ol className="flex flex-wrap items-center gap-2 text-xs text-[var(--cm-text-muted)]">
              {breadcrumbs.map((item, index) => (
                <li key={`${item.label}-${index}`} className="flex items-center gap-2">
                  {index > 0 && <span aria-hidden="true">/</span>}
                  {item.href ? (
                    <a className="cm-focus-ring rounded hover:text-[var(--cm-text)]" href={item.href}>{item.label}</a>
                  ) : (
                    <span aria-current={index === breadcrumbs.length - 1 ? "page" : undefined}>{item.label}</span>
                  )}
                </li>
              ))}
            </ol>
          </nav>
        )}
        {eyebrow && <p className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--cm-cyan)]">{eyebrow}</p>}
        <h1 className="text-2xl font-semibold tracking-tight text-[var(--cm-text)] sm:text-3xl">{title}</h1>
        {description && <p className="mt-2 max-w-3xl text-sm text-[var(--cm-text-secondary)]">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </header>
  );
}

interface MetricCardProps {
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  supportingText?: string;
  trend?: { label: string; tone?: "positive" | "negative" | "neutral" };
}

export function MetricCard({ label, value, icon, supportingText, trend }: MetricCardProps) {
  const trendTone = trend?.tone === "positive"
    ? "text-emerald-300"
    : trend?.tone === "negative"
      ? "text-rose-300"
      : "text-[var(--cm-text-secondary)]";

  return (
    <article className="cm-surface relative overflow-hidden p-5">
      <div aria-hidden="true" className="absolute inset-x-0 bottom-0 h-0.5 bg-gradient-to-r from-[var(--cm-cyan)] to-[var(--cm-indigo)]" />
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-sm text-[var(--cm-text-secondary)]">{label}</p>
          <p className="mt-2 text-3xl font-semibold tracking-tight text-[var(--cm-text)]">{value}</p>
        </div>
        {icon && <span className="rounded-xl bg-indigo-500/10 p-2.5 text-indigo-300">{icon}</span>}
      </div>
      {(supportingText || trend) && (
        <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
          {supportingText && <span className="text-[var(--cm-text-muted)]">{supportingText}</span>}
          {trend && <span className={trendTone}>{trend.label}</span>}
        </div>
      )}
    </article>
  );
}

type StatusTone = "success" | "warning" | "danger" | "neutral" | "info";

const statusToneByName: Record<string, StatusTone> = {
  active: "success",
  published: "success",
  completed: "success",
  draft: "warning",
  pending: "warning",
  inactive: "neutral",
  archived: "neutral",
  disabled: "neutral",
  failed: "danger",
  rejected: "danger",
};

interface StatusBadgeProps {
  status: string;
  label?: string;
  tone?: StatusTone;
}

export function StatusBadge({ status, label, tone = statusToneByName[status.toLowerCase()] ?? "info" }: StatusBadgeProps) {
  const toneClasses: Record<StatusTone, string> = {
    success: "border-emerald-400/30 bg-emerald-400/10 text-emerald-300",
    warning: "border-amber-400/30 bg-amber-400/10 text-amber-200",
    danger: "border-rose-400/30 bg-rose-400/10 text-rose-200",
    neutral: "border-slate-500/30 bg-slate-500/10 text-slate-300",
    info: "border-cyan-400/30 bg-cyan-400/10 text-cyan-200",
  };

  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold ${toneClasses[tone]}`}>
      <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-current" />
      {label ?? status}
    </span>
  );
}

interface SkeletonProps {
  className?: string;
  label?: string;
  decorative?: boolean;
}

export function Skeleton({ className = "h-4 w-full", label = "Đang tải dữ liệu", decorative = false }: SkeletonProps) {
  return (
    <span
      className={`cm-skeleton block rounded-md ${className}`}
      role={decorative ? undefined : "status"}
      aria-hidden={decorative || undefined}
      aria-label={decorative ? undefined : label}
    />
  );
}

interface ConcurrencyBannerProps {
  onReload: () => void;
  isReloading?: boolean;
  message?: string;
}

export function ConcurrencyBanner({ onReload, isReloading = false, message }: ConcurrencyBannerProps) {
  return (
    <section role="alert" className="flex flex-col gap-3 rounded-xl border border-amber-400/30 bg-amber-400/10 p-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h2 className="text-sm font-semibold text-amber-100">Dữ liệu đã thay đổi</h2>
        <p className="mt-1 text-sm text-amber-100/80">
          {message ?? "Dữ liệu đã được cập nhật ở phiên khác. Hãy tải bản mới nhất trước khi tiếp tục."}
        </p>
      </div>
      <button type="button" className="cm-secondary-button shrink-0" onClick={onReload} disabled={isReloading}>
        {isReloading ? "Đang tải lại…" : "Tải dữ liệu mới nhất"}
      </button>
    </section>
  );
}

interface SafeErrorPanelProps {
  error: unknown;
  fallback?: string;
  onRetry?: () => void;
}

export function SafeErrorPanel({ error, fallback, onRetry }: SafeErrorPanelProps) {
  const message = mapSafeOperationalError(error, fallback);
  const { traceId } = extractProblemDetails(error);
  const traceSuffix = traceId ? ` (Mã theo dõi: ${traceId})` : "";
  const messageWithoutDuplicateTrace = traceSuffix && message.endsWith(traceSuffix)
    ? message.slice(0, -traceSuffix.length)
    : message;

  return (
    <section role="alert" aria-live="assertive" className="rounded-xl border border-rose-400/30 bg-rose-400/10 p-4">
      <h2 className="text-sm font-semibold text-rose-100">Không thể tải dữ liệu</h2>
      <p className="mt-1 text-sm text-rose-100/80">{messageWithoutDuplicateTrace}</p>
      {traceId && <p className="mt-2 break-all font-mono text-xs text-rose-200/70">Trace ID: {traceId}</p>}
      {onRetry && (
        <button type="button" className="cm-secondary-button mt-3" onClick={onRetry}>Thử lại</button>
      )}
    </section>
  );
}
