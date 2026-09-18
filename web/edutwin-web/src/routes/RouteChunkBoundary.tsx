import { Component, type ReactNode } from "react";

interface RouteChunkBoundaryProps {
  children: ReactNode;
}

interface RouteChunkBoundaryState {
  failed: boolean;
  error?: Error | null;
}

export const RouteLoadingFallback = () => (
  <main
    className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-slate-100"
    role="status"
    aria-live="polite"
    aria-busy="true"
  >
    <div className="flex items-center gap-3 rounded-xl border border-slate-700 bg-slate-900 px-5 py-4 shadow-xl">
      <span className="h-5 w-5 animate-spin rounded-full border-2 border-indigo-300 border-t-transparent" aria-hidden="true" />
      <span>Đang tải không gian làm việc…</span>
    </div>
  </main>
);

export class RouteChunkBoundary extends Component<RouteChunkBoundaryProps, RouteChunkBoundaryState> {
  state: RouteChunkBoundaryState = { failed: false, error: null };

  static getDerivedStateFromError(error: Error): RouteChunkBoundaryState {
    return { failed: true, error };
  }

  componentDidCatch(error: Error, info: any) {
    console.error("RouteChunkBoundary caught runtime error:", error, info);
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-slate-100">
        <section className="w-full max-w-lg rounded-2xl border border-slate-700 bg-slate-900 p-8 text-center shadow-xl" role="alert">
          <h1 className="text-xl font-bold">Không thể tải màn hình này</h1>
          <p className="mt-3 text-sm text-slate-300">
            Phiên bản giao diện có thể vừa được cập nhật hoặc kết nối đang gián đoạn. Hãy tải lại để tiếp tục.
          </p>
          {this.state.error && (
            <div className="mt-4 p-3 rounded-xl bg-rose-950/60 border border-rose-800 text-left text-xs font-mono text-rose-300 max-h-40 overflow-auto whitespace-pre-wrap">
              {this.state.error.message || String(this.state.error)}
            </div>
          )}
          <button
            type="button"
            className="mt-6 rounded-lg bg-indigo-500 px-5 py-2.5 font-semibold text-white hover:bg-indigo-400 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-300"
            onClick={() => window.location.reload()}
          >
            Tải lại trang
          </button>
        </section>
      </main>
    );
  }
}
