import { useEffect, useRef } from "react";
import { useAuthStore } from "../stores/authStore";
import { bootstrapSession } from "./authApi";

export const AuthBootstrap = ({ children }: { children: React.ReactNode }) => {
  const sessionStatus = useAuthStore((state) => state.sessionStatus);
  const isBootstrapping = useRef(false);

  useEffect(() => {
    if (sessionStatus === "unknown" && !isBootstrapping.current) {
      isBootstrapping.current = true;
      bootstrapSession().finally(() => {
        // Prevent strictly re-running if status updates but we are unmounting
      });
    }
  }, [sessionStatus]);

  if (sessionStatus === "unknown") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <div role="status" aria-live="polite" className="text-center">
          <p className="text-lg font-semibold text-indigo-600">
            Đang khởi tạo phiên làm việc...
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
};
