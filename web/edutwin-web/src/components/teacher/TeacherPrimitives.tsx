import type { ReactNode } from "react";
import { extractProblemDetails, mapSafeOperationalError } from "../../utils/problemDetails";

export interface TeacherBreadcrumbItem {
  label: string;
  href?: string;
}

interface TeacherPageHeaderProps {
  title: string;
  description?: string;
  subtitle?: string;
  eyebrow?: string;
  breadcrumbs?: TeacherBreadcrumbItem[];
  actions?: ReactNode;
}

export function TeacherPageHeader({ title, description, subtitle, eyebrow, breadcrumbs = [], actions }: TeacherPageHeaderProps) {
  const desc = subtitle ?? description;
  return (
    <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between pb-2">
      <div className="min-w-0">
        {breadcrumbs.length > 0 && (
          <nav aria-label="Breadcrumb" className="mb-2">
            <ol className="flex flex-wrap items-center gap-1.5 text-xs text-[var(--th-text-muted)]">
              {breadcrumbs.map((item, index) => (
                <li key={`${item.label}-${index}`} className="flex items-center gap-1.5">
                  {index > 0 && <span aria-hidden="true" className="text-stone-400">/</span>}
                  {item.href ? (
                    <a className="th-focus-ring rounded font-medium text-[var(--th-text-secondary)] hover:text-[var(--th-text)]" href={item.href}>
                      {item.label}
                    </a>
                  ) : (
                    <span aria-current={index === breadcrumbs.length - 1 ? "page" : undefined} className="font-bold text-[var(--th-text)]">
                      {item.label}
                    </span>
                  )}
                </li>
              ))}
            </ol>
          </nav>
        )}
        {eyebrow && (
          <span className="mb-2 inline-block rounded-md border border-[var(--th-border)] bg-[var(--th-surface-muted)] px-2 py-0.5 text-[10px] font-black uppercase tracking-[0.14em] text-[var(--th-text-secondary)] shadow-sm">
            {eyebrow}
          </span>
        )}
        <h1 className="text-2xl font-black tracking-tight text-[var(--th-text)] sm:text-3xl">{title}</h1>
        {desc && <p className="mt-1.5 max-w-3xl text-sm font-medium text-[var(--th-text-secondary)]">{desc}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2.5">{actions}</div>}
    </header>
  );
}

interface TeacherMetricCardProps {
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  unit?: string;
  color?: string;
  supportingText?: string;
  trend?: { label: string; tone?: "positive" | "negative" | "neutral" };
}

export function TeacherMetricCard({ label, value, icon, unit, supportingText, trend }: TeacherMetricCardProps) {
  const trendTone = trend?.tone === "positive"
    ? "text-emerald-700 dark:text-emerald-300 font-bold"
    : trend?.tone === "negative"
      ? "text-rose-700 dark:text-rose-300 font-bold"
      : "text-[var(--th-text-secondary)] font-medium";

  return (
    <article className="th-surface relative overflow-hidden p-5 transition-transform hover:-translate-y-0.5">
      {/* Neo-brutalist top accent line */}
      <div aria-hidden="true" className="absolute inset-x-0 top-0 h-1 bg-[var(--th-terracotta)]" />
      <div className="flex items-start justify-between gap-4 pt-1">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-[var(--th-text-muted)]">{label}</p>
          <div className="mt-2 flex items-baseline gap-1.5">
            <span className="text-3xl font-black tracking-tight text-[var(--th-text)]">{value}</span>
            {unit && <span className="text-xs font-semibold text-[var(--th-text-muted)]">{unit}</span>}
          </div>
        </div>
        {icon && (
          <span className="grid h-10 w-10 place-items-center rounded-lg border border-[var(--th-border)] bg-[var(--th-surface-muted)] text-base shadow-sm">
            {icon}
          </span>
        )}
      </div>
      {(supportingText || trend) && (
        <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-[var(--th-border-subtle)] pt-2.5 text-xs">
          {supportingText && <span className="font-medium text-[var(--th-text-muted)]">{supportingText}</span>}
          {trend && <span className={trendTone}>{trend.label}</span>}
        </div>
      )}
    </article>
  );
}

type TeacherStatusTone = "success" | "warning" | "danger" | "neutral" | "info";

const statusToneByName: Record<string, TeacherStatusTone> = {
  active: "success",
  published: "success",
  completed: "success",
  draft: "warning",
  pending: "warning",
  inactive: "neutral",
  archived: "neutral",
  disabled: "neutral",
  closed: "neutral",
  failed: "danger",
  rejected: "danger",
};

interface TeacherStatusBadgeProps {
  status: string;
  label?: string;
  tone?: TeacherStatusTone;
}

export function TeacherStatusBadge({ status, label, tone = statusToneByName[status.toLowerCase()] ?? "info" }: TeacherStatusBadgeProps) {
  const toneClasses: Record<TeacherStatusTone, string> = {
    success: "th-badge-success",
    warning: "th-badge-warning",
    danger: "th-badge-danger",
    neutral: "th-badge-neutral",
    info: "th-badge-info",
  };

  return (
    <span className={`th-badge ${toneClasses[tone]}`}>
      <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-current" />
      {label ?? status}
    </span>
  );
}

interface TeacherSkeletonProps {
  className?: string;
  label?: string;
  decorative?: boolean;
  height?: number | string;
  width?: number | string;
}

export function TeacherSkeleton({ className = "h-4 w-full", label = "Đang tải dữ liệu", decorative = false, height, width }: TeacherSkeletonProps) {
  const style: React.CSSProperties = {};
  if (height !== undefined) {
    style.height = typeof height === "number" ? `${height}px` : height;
  }
  if (width !== undefined) {
    style.width = typeof width === "number" ? `${width}px` : width;
  }

  return (
    <span
      style={style}
      className={`block animate-pulse rounded-md border border-[var(--th-border-subtle)] bg-[var(--th-surface-muted)] ${className}`}
      role={decorative ? undefined : "status"}
      aria-hidden={decorative || undefined}
      aria-label={decorative ? undefined : label}
    />
  );
}

interface TeacherConcurrencyBannerProps {
  onReload: () => void;
  isReloading?: boolean;
  message?: string;
}

export function TeacherConcurrencyBanner({ onReload, isReloading = false, message }: TeacherConcurrencyBannerProps) {
  return (
    <section role="alert" className="flex flex-col gap-3 rounded-xl border-2 border-amber-600 bg-amber-50 p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between text-amber-950">
      <div>
        <h2 className="text-sm font-bold text-amber-900">Dữ liệu đã thay đổi (Phiên bản mới)</h2>
        <p className="mt-1 text-xs font-medium text-amber-800">
          {message ?? "Dữ liệu đã được cập nhật ở phiên khác. Hãy tải bản mới nhất trước khi tiếp tục."}
        </p>
      </div>
      <button
        type="button"
        onClick={onReload}
        disabled={isReloading}
        className="th-secondary-button text-xs py-1.5 px-3"
      >
        {isReloading ? "Đang tải lại..." : "Tải lại dữ liệu"}
      </button>
    </section>
  );
}

interface TeacherSafeErrorPanelProps {
  error: unknown;
  fallback?: string;
  onRetry?: () => void;
  title?: string;
}

export function TeacherSafeErrorPanel({
  error,
  fallback = "Không thể tải dữ liệu.",
  onRetry,
  title = "Có lỗi xảy ra",
}: TeacherSafeErrorPanelProps) {
  const problem = typeof error === "string" ? { traceId: null } : extractProblemDetails(error);
  const message = typeof error === "string" ? error : mapSafeOperationalError(error, fallback);

  return (
    <section role="alert" className="rounded-xl border-2 border-rose-600 bg-rose-50 p-6 space-y-3 text-rose-950 shadow-sm">
      <h2 className="text-base font-bold text-rose-900">{title}</h2>
      <p className="text-xs font-medium text-rose-800">{message}</p>
      {problem.traceId && (
        <p className="text-[11px] font-mono text-rose-700">
          Trace ID: {problem.traceId}
        </p>
      )}
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="th-secondary-button mt-2 text-xs py-1.5 px-3"
        >
          Thử lại
        </button>
      )}
    </section>
  );
}
