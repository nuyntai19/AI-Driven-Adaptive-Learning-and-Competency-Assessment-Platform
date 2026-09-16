import React, { useState, useEffect, useRef, useCallback } from "react";
import { httpClient } from "../api/httpClient";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { extractProblemDetails } from "../utils/problemDetails";

interface ScratchpadAttachmentDrawerProps {
  attemptId: string | null;
  isOpen: boolean;
  onClose: () => void;
  studentName?: string;
  questionText?: string;
}

type ErrorState = {
  status?: number;
  message: string;
} | null;

export const ScratchpadAttachmentDrawer: React.FC<ScratchpadAttachmentDrawerProps> = ({
  attemptId,
  isOpen,
  onClose,
  studentName,
  questionText,
}) => {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorState, setErrorState] = useState<ErrorState>(null);
  const [zoomLevel, setZoomLevel] = useState<number>(100);

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canReadAttachment = hasPermission(permissions.learningAttemptsReadScoped);
  const blobUrlRef = useRef<string | null>(null);

  // Synchronize ref with state for clean revocation
  useEffect(() => {
    blobUrlRef.current = blobUrl;
  }, [blobUrl]);

  const cleanupBlob = useCallback(() => {
    if (blobUrlRef.current) {
      URL.revokeObjectURL(blobUrlRef.current);
      blobUrlRef.current = null;
      setBlobUrl(null);
    }
  }, []);

  const fetchAttachment = useCallback(async (targetAttemptId: string) => {
    if (!canReadAttachment) {
      setErrorState({
        status: 403,
        message: "Bạn không có quyền truy cập tệp nháp của học sinh (Cần quyền: learning.attempts.read_scoped).",
      });
      return;
    }

    cleanupBlob();
    setIsLoading(true);
    setErrorState(null);
    setZoomLevel(100);

    try {
      const response = await httpClient.get<Blob>(
        `/learning/attempts/${targetAttemptId}/attachment`,
        { responseType: "blob" }
      );
      const url = URL.createObjectURL(response.data);
      blobUrlRef.current = url;
      setBlobUrl(url);
    } catch (err: unknown) {
      const details = extractProblemDetails(err);
      if (details.status === 404) {
        setErrorState({
          status: 404,
          message: "Lượt làm bài này không có tệp nháp vẽ kèm theo, hoặc nằm ngoài phạm vi phụ trách của bạn.",
        });
      } else if (details.status === 503) {
        setErrorState({
          status: 503,
          message: "Tệp đính kèm tạm thời không khả dụng. Hệ thống đang đồng bộ lưu trữ, vui lòng thử lại sau.",
        });
      } else if (details.status === 403) {
        setErrorState({
          status: 403,
          message: "Từ chối truy cập: Bạn không có quyền xem tệp nháp của lượt giải này.",
        });
      } else {
        setErrorState({
          status: details.status || 500,
          message: details.message || "Không thể tải tệp nháp bài làm.",
        });
      }
    } finally {
      setIsLoading(false);
    }
  }, [canReadAttachment, cleanupBlob]);

  useEffect(() => {
    if (isOpen && attemptId) {
      fetchAttachment(attemptId);
    } else {
      cleanupBlob();
      setErrorState(null);
    }

    return () => {
      cleanupBlob();
    };
  }, [isOpen, attemptId, fetchAttachment, cleanupBlob]);

  // Handle ESC key
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="scratchpad-drawer-title"
      className="fixed inset-0 z-50 overflow-hidden bg-slate-900/60 backdrop-blur-sm transition-opacity"
    >
      <div className="fixed inset-y-0 right-0 flex max-w-full pl-10">
        <div className="w-screen max-w-2xl bg-white shadow-2xl flex flex-col">
          {/* Drawer Header */}
          <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4 bg-slate-50">
            <div>
              <span className="text-xs font-bold uppercase tracking-wider text-indigo-600">
                Lớp vẽ tương tác (Scratchpad)
              </span>
              <h2 id="scratchpad-drawer-title" className="text-lg font-bold text-slate-900">
                Bản Vẽ Nháp Của Học Sinh
              </h2>
              {studentName && (
                <p className="text-xs text-slate-500 mt-0.5">
                  Học sinh: <strong className="text-slate-800">{studentName}</strong> · Lượt làm #{attemptId}
                </p>
              )}
            </div>
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg p-2 text-slate-400 hover:bg-slate-200 hover:text-slate-700 transition-colors"
              aria-label="Đóng ngăn xem bản nháp"
            >
              ✕
            </button>
          </div>

          {/* Context Snippet */}
          {questionText && (
            <div className="border-b border-slate-100 bg-amber-50/50 px-6 py-2.5 text-xs text-amber-900">
              <span className="font-semibold">Câu hỏi: </span>
              <span className="line-clamp-2">{questionText}</span>
            </div>
          )}

          {/* Controls Bar */}
          <div className="flex items-center justify-between border-b border-slate-200 px-6 py-2 bg-white text-xs">
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setZoomLevel((z) => Math.max(50, z - 25))}
                disabled={!blobUrl || isLoading}
                className="rounded border border-slate-200 px-2 py-1 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"
              >
                - Thu nhỏ
              </button>
              <span className="font-mono font-medium text-slate-600 min-w-[3rem] text-center">
                {zoomLevel}%
              </span>
              <button
                type="button"
                onClick={() => setZoomLevel((z) => Math.min(200, z + 25))}
                disabled={!blobUrl || isLoading}
                className="rounded border border-slate-200 px-2 py-1 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"
              >
                + Phóng to
              </button>
              <button
                type="button"
                onClick={() => setZoomLevel(100)}
                disabled={!blobUrl || isLoading || zoomLevel === 100}
                className="rounded border border-slate-200 px-2 py-1 text-slate-600 hover:bg-slate-50 disabled:opacity-40"
              >
                Gốc (100%)
              </button>
            </div>

            {blobUrl && (
              <a
                href={blobUrl}
                download={`scratchpad-attempt-${attemptId}.png`}
                className="inline-flex items-center gap-1.5 font-semibold text-indigo-600 hover:text-indigo-700"
              >
                <span>Tải ảnh PNG</span>
                <span>↓</span>
              </a>
            )}
          </div>

          {/* Drawer Body */}
          <div className="flex-1 overflow-auto p-6 bg-slate-100 flex items-center justify-center">
            {isLoading && (
              <div className="flex flex-col items-center gap-3 text-slate-500">
                <div className="h-8 w-8 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent" />
                <span className="text-sm font-medium animate-pulse">Đang tải bản vẽ nháp của học sinh...</span>
              </div>
            )}

            {!isLoading && errorState && (
              <div className="rounded-xl bg-white p-8 text-center max-w-md shadow-sm ring-1 ring-slate-200">
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-50 text-2xl text-amber-600">
                  {errorState.status === 404 ? "ℹ️" : "⚠️"}
                </div>
                <h3 className="mt-3 text-base font-bold text-slate-900">
                  {errorState.status === 404
                    ? "Không có bản vẽ nháp"
                    : errorState.status === 503
                    ? "Lưu trữ tạm gián đoạn"
                    : errorState.status === 403
                    ? "Không có quyền truy cập"
                    : "Lỗi tải tệp"}
                </h3>
                <p className="mt-2 text-xs text-slate-600 leading-relaxed">
                  {errorState.message}
                </p>
                {attemptId && errorState.status !== 404 && errorState.status !== 403 && (
                  <button
                    type="button"
                    onClick={() => fetchAttachment(attemptId)}
                    className="mt-4 rounded-lg bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white shadow hover:bg-indigo-500"
                  >
                    Thử lại
                  </button>
                )}
              </div>
            )}

            {!isLoading && !errorState && blobUrl && (
              <div className="flex items-center justify-center overflow-auto max-w-full max-h-full">
                <img
                  src={blobUrl}
                  alt={`Bản vẽ nháp lượt làm #${attemptId}`}
                  style={{ width: `${zoomLevel}%` }}
                  className="rounded-lg shadow-md border border-slate-300 bg-white transition-all duration-150"
                />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
