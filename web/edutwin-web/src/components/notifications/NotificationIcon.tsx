export function NotificationIcon({ kind = "bell", className = "h-5 w-5" }: { kind?: "bell" | "send" | "check" | "refresh"; className?: string }) {
  return <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
    {kind === "bell" && <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.4-1.4A2 2 0 0118 14.2V11a6 6 0 00-4-5.7V5a2 2 0 10-4 0v.3A6 6 0 006 11v3.2c0 .5-.2 1-.6 1.4L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />}
    {kind === "send" && <path strokeLinecap="round" strokeLinejoin="round" d="M21 3L9 15m12-12l-7 18-5-6-6-5 18-7z" />}
    {kind === "check" && <path strokeLinecap="round" strokeLinejoin="round" d="M5 12l4 4L19 6" />}
    {kind === "refresh" && <path strokeLinecap="round" strokeLinejoin="round" d="M20 7v5h-5M4 17v-5h5m-5 0a8 8 0 0113.5-5.5L20 9m0 3a8 8 0 01-13.5 5.5L4 15" />}
  </svg>;
}
