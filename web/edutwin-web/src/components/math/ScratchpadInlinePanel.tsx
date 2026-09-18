import { useCallback, useEffect, useRef, useState } from "react";
import type {
  ScratchpadDraft,
  ScratchpadGrid,
  ScratchpadPoint,
  ScratchpadStroke,
  ScratchpadTool,
} from "../../types/scratchpad";
import {
  DEFAULT_TTL_MS,
  getScratchpadDraft,
  MAX_POINTS_PER_STROKE,
  MAX_STROKES,
  saveScratchpadDraft,
} from "../../utils/scratchpadStorage";
import {
  DEFAULT_CANVAS_HEIGHT as CANVAS_HEIGHT,
  DEFAULT_CANVAS_WIDTH as CANVAS_WIDTH,
  drawGrid,
  drawStroke,
  drawStrokes,
  renderPng,
} from "../../utils/scratchpadRenderer";

const MAX_HISTORY_STATES = 30;

const tools: ReadonlyArray<{ value: ScratchpadTool; label: string; title: string }> = [
  { value: "pen", label: "Bút", title: "Bút vẽ tự do" },
  { value: "eraser", label: "Tẩy", title: "Tẩy nét vẽ" },
  { value: "line", label: "Thước", title: "Vẽ đoạn thẳng" },
  { value: "circle", label: "Compa", title: "Vẽ hình tròn" },
  { value: "triangle", label: "Tam giác", title: "Vẽ tam giác" },
  { value: "oxy", label: "Trục Oxy", title: "Vẽ hệ trục tọa độ" },
];

const colours = ["#0f172a", "#2563eb", "#dc2626", "#16a34a"] as const;
const lineWidths = [1, 2, 4, 6] as const;

export interface ScratchpadInlinePanelProps {
  centerId: string;
  userId: string;
  clientSubmissionId: string;
  isAttached?: boolean;
  isReadOnly?: boolean;
  onExportPng?: (blob: Blob) => void;
  onAttachSnapshot?: (blob: Blob, dataUrl: string) => void;
}

export const ScratchpadInlinePanel = ({
  centerId,
  userId,
  clientSubmissionId,
  isAttached = false,
  isReadOnly = false,
  onExportPng,
  onAttachSnapshot,
}: ScratchpadInlinePanelProps) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const isDrawingRef = useRef(false);
  const currentPointsRef = useRef<ScratchpadPoint[]>([]);
  const startPointRef = useRef<ScratchpadPoint | null>(null);
  const previewPointRef = useRef<ScratchpadPoint | null>(null);

  // Zoom & Pan state
  const [zoom, setZoom] = useState<number>(1.0);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isSpaceHeld, setIsSpaceHeld] = useState<boolean>(false);
  const isPanningRef = useRef<boolean>(false);
  const panStartRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  const [tool, setTool] = useState<ScratchpadTool>("pen");
  const [colour, setColour] = useState<string>(colours[0]);
  const [lineWidth, setLineWidth] = useState<number>(2);
  const [grid, setGrid] = useState<ScratchpadGrid>("math_grid");
  const [strokes, setStrokes] = useState<ScratchpadStroke[]>([]);
  const [undoHistory, setUndoHistory] = useState<ScratchpadStroke[][]>([]);
  const [redoHistory, setRedoHistory] = useState<ScratchpadStroke[][]>([]);
  const [saveStatus, setSaveStatus] = useState("Sẵn sàng");
  const [exportStatus, setExportStatus] = useState<string | null>(null);

  // Spacebar tracking for panning
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (
        e.code === "Space" &&
        !e.repeat &&
        document.activeElement?.tagName !== "INPUT" &&
        document.activeElement?.tagName !== "TEXTAREA"
      ) {
        setIsSpaceHeld(true);
      }
    };
    const onKeyUp = (e: KeyboardEvent) => {
      if (e.code === "Space") {
        setIsSpaceHeld(false);
        isPanningRef.current = false;
      }
    };
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
    };
  }, []);

  const saveDraft = useCallback(
    async (nextStrokes: ScratchpadStroke[], nextGrid: ScratchpadGrid) => {
      if (isReadOnly) return;
      setSaveStatus("Đang lưu nháp…");
      const now = Date.now();
      const draft: ScratchpadDraft = {
        storageKey: "",
        centerId,
        userId,
        clientSubmissionId,
        strokes: nextStrokes,
        gridType: nextGrid,
        canvasWidth: CANVAS_WIDTH,
        canvasHeight: CANVAS_HEIGHT,
        updatedAt: now,
        expiresAt: now + DEFAULT_TTL_MS,
      };

      try {
        const persistence = await saveScratchpadDraft(draft);
        setSaveStatus(persistence === "durable" ? "Đã lưu nháp" : "Lưu tạm trong phiên");
      } catch (error) {
        setSaveStatus(error instanceof Error ? error.message : "Không thể lưu nháp");
      }
    },
    [centerId, clientSubmissionId, userId]
  );

  useEffect(() => {
    let active = true;
    void getScratchpadDraft(centerId, userId, clientSubmissionId)
      .then((draft) => {
        if (!active) return;
        setStrokes(draft?.strokes ?? []);
        setGrid(draft?.gridType ?? "math_grid");
        setUndoHistory([]);
        setRedoHistory([]);
        setSaveStatus(draft ? "Đã khôi phục nháp" : "Nháp mới");
      })
      .catch(() => {
        if (active) {
          setSaveStatus("Không thể tải nháp cũ");
        }
      });

    return () => {
      active = false;
    };
  }, [centerId, clientSubmissionId, userId]);

  // Redraw canvas with current pan and zoom
  const redraw = useCallback(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;

    context.save();
    context.clearRect(0, 0, canvas.width, canvas.height);

    // Apply zoom & pan transform
    context.translate(pan.x, pan.y);
    context.scale(zoom, zoom);

    // Render grid background on canvas
    drawGrid(context, grid, CANVAS_WIDTH, CANVAS_HEIGHT);

    // Render committed strokes
    drawStrokes(context, strokes);

    // Render active stroke in-flight
    if (isDrawingRef.current) {
      const preview = toPreviewStroke(
        tool,
        colour,
        lineWidth,
        currentPointsRef.current,
        startPointRef.current,
        previewPointRef.current
      );
      if (preview) drawStroke(context, preview);
    }

    context.restore();
  }, [colour, grid, lineWidth, pan.x, pan.y, strokes, tool, zoom]);

  useEffect(() => {
    redraw();
  }, [redraw]);

  // Transform browser mouse coordinate to virtual canvas coordinate considering Pan & Zoom
  const toCanvasPoint = (event: React.PointerEvent<HTMLCanvasElement>): ScratchpadPoint => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const clientX = (event.clientX - rect.left) * (canvas.width / rect.width);
    const clientY = (event.clientY - rect.top) * (canvas.height / rect.height);
    return {
      x: (clientX - pan.x) / zoom,
      y: (clientY - pan.y) / zoom,
    };
  };

  const handleWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    const delta = event.deltaY < 0 ? 0.08 : -0.08;
    setZoom((prev) => Math.min(3.0, Math.max(0.2, Number((prev + delta).toFixed(2)))));
  };

  const finishDrawing = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (isPanningRef.current) {
      isPanningRef.current = false;
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
      return;
    }

    if (isReadOnly || !isDrawingRef.current) return;
    isDrawingRef.current = false;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }

    const end = toCanvasPoint(event);
    const draftStroke = toPreviewStroke(
      tool,
      colour,
      lineWidth,
      currentPointsRef.current,
      startPointRef.current,
      end
    );
    currentPointsRef.current = [];
    startPointRef.current = null;
    previewPointRef.current = null;

    if (!draftStroke) {
      redraw();
      return;
    }

    setStrokes((previous) => {
      if (previous.length >= MAX_STROKES) {
        setSaveStatus(`Giới hạn tối đa ${MAX_STROKES} nét vẽ.`);
        return previous;
      }
      setUndoHistory((history) => [...history.slice(-(MAX_HISTORY_STATES - 1)), previous]);
      setRedoHistory([]);
      const next = [...previous, draftStroke];
      void saveDraft(next, grid);
      return next;
    });
  };

  const handlePointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    // Pan mode if space held or middle mouse click
    if (isSpaceHeld || event.button === 1) {
      isPanningRef.current = true;
      panStartRef.current = { x: event.clientX - pan.x, y: event.clientY - pan.y };
      event.currentTarget.setPointerCapture(event.pointerId);
      return;
    }

    if (isReadOnly) return;
    if (event.button !== 0) return;
    isDrawingRef.current = true;
    event.currentTarget.setPointerCapture(event.pointerId);

    const point = toCanvasPoint(event);
    startPointRef.current = point;
    previewPointRef.current = point;

    if (tool === "pen" || tool === "eraser") {
      currentPointsRef.current = [point];
    } else {
      currentPointsRef.current = [];
    }
    redraw();
  };

  const handlePointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    // Handle pan dragging
    if (isPanningRef.current) {
      setPan({
        x: event.clientX - panStartRef.current.x,
        y: event.clientY - panStartRef.current.y,
      });
      return;
    }

    if (isReadOnly || !isDrawingRef.current) return;
    const point = toCanvasPoint(event);
    previewPointRef.current = point;

    if (tool === "pen" || tool === "eraser") {
      appendFreehandPoint(currentPointsRef.current, point);
    }
    redraw();
  };

  const undo = () => {
    if (isReadOnly) return;
    if (undoHistory.length === 0) return;
    const previous = undoHistory[undoHistory.length - 1];
    setRedoHistory((r) => [strokes, ...r.slice(0, MAX_HISTORY_STATES - 1)]);
    setUndoHistory((u) => u.slice(0, -1));
    setStrokes(previous);
    void saveDraft(previous, grid);
  };

  const redo = () => {
    if (isReadOnly) return;
    if (redoHistory.length === 0) return;
    const next = redoHistory[0];
    setUndoHistory((u) => [...u.slice(-(MAX_HISTORY_STATES - 1)), strokes]);
    setRedoHistory((r) => r.slice(1));
    setStrokes(next);
    void saveDraft(next, grid);
  };

  const clear = () => {
    if (isReadOnly) return;
    if (strokes.length === 0) return;
    setUndoHistory((u) => [...u.slice(-(MAX_HISTORY_STATES - 1)), strokes]);
    setRedoHistory([]);
    setStrokes([]);
    void saveDraft([], grid);
  };

  const handleAttachSnapshot = async () => {
    if (isReadOnly) return;
    if (strokes.length === 0) {
      setExportStatus("Vui lòng vẽ nét nháp trước khi đính kèm.");
      setTimeout(() => setExportStatus(null), 2500);
      return;
    }

    setExportStatus("Đang lưu ảnh nháp...");
    try {
      const blob = await renderPng(strokes, grid);
      const reader = new FileReader();
      reader.onloadend = () => {
        const dataUrl = reader.result as string;
        if (onAttachSnapshot) {
          onAttachSnapshot(blob, dataUrl);
        }
        if (onExportPng) {
          onExportPng(blob);
        }
        setExportStatus("Đã cập nhật ảnh nháp đính kèm vào bài làm!");
        setTimeout(() => setExportStatus(null), 3000);
      };
      reader.readAsDataURL(blob);
    } catch {
      setExportStatus("Lỗi xuất ảnh nháp");
      setTimeout(() => setExportStatus(null), 2500);
    }
  };

  return (
    <div className="flex flex-col h-full bg-white dark:bg-slate-900 rounded-2xl overflow-hidden border border-slate-700/80 shadow-2xl">
      {/* Top Controls Bar */}
      {isReadOnly ? (
        <div className="p-3 bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs shrink-0">
          <div className="flex items-center gap-2 text-slate-700 dark:text-slate-300">
            <span className="text-base leading-none">🔒</span>
            <span className="font-extrabold text-emerald-800 dark:text-emerald-400">
              Bản nháp đã nộp · Chế độ chỉ đọc
            </span>
            <span className="text-[11px] text-slate-500 dark:text-slate-400 hidden sm:inline">
              (Bạn vẫn có thể cuộn, phóng to / thu nhỏ để xem lại nét vẽ)
            </span>
          </div>
          <span className="text-[11px] font-bold px-2.5 py-0.5 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
            Chỉ xem
          </span>
        </div>
      ) : (
        <div className="p-3 bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 space-y-2 shrink-0">
          <div className="flex flex-wrap items-center justify-between gap-2">
            {/* Tool selector */}
            <div className="flex flex-wrap items-center gap-1">
              {tools.map((item) => (
                <button
                  key={item.value}
                  type="button"
                  onClick={() => setTool(item.value)}
                  className={`px-2.5 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                    tool === item.value
                      ? "bg-emerald-600 text-white shadow-xs"
                      : "bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 border border-slate-200 dark:border-slate-700"
                  }`}
                  title={item.title}
                >
                  {item.label}
                </button>
              ))}
            </div>

            {/* Undo, Redo, Clear */}
            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={undo}
                disabled={undoHistory.length === 0}
                className="px-2 py-1 text-xs font-bold rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 disabled:opacity-40 cursor-pointer"
                title="Hoàn tác (Ctrl+Z)"
              >
                ↩
              </button>
              <button
                type="button"
                onClick={redo}
                disabled={redoHistory.length === 0}
                className="px-2 py-1 text-xs font-bold rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 disabled:opacity-40 cursor-pointer"
                title="Làm lại (Ctrl+Y)"
              >
                ↪
              </button>
              <button
                type="button"
                onClick={clear}
                className="px-2.5 py-1 text-xs font-bold rounded-lg border border-rose-200 dark:border-rose-900 bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-400 hover:bg-rose-100 cursor-pointer"
                title="Xóa hết nét vẽ"
              >
                Xóa hết
              </button>
            </div>
          </div>

          {/* Colors & Line widths & Grid */}
          <div className="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-slate-200/80 dark:border-slate-800">
            <div className="flex items-center gap-2">
              <span className="text-[11px] text-slate-400">Màu:</span>
              <div className="flex items-center gap-1.5">
                {colours.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => {
                      setColour(c);
                      if (tool === "eraser") setTool("pen");
                    }}
                    className={`w-5 h-5 rounded-full border-2 transition-transform cursor-pointer ${
                      colour === c && tool !== "eraser"
                        ? "border-emerald-500 scale-110 ring-2 ring-emerald-300"
                        : "border-slate-300 dark:border-slate-600"
                    }`}
                    style={{ backgroundColor: c }}
                    title="Chọn màu"
                  />
                ))}
              </div>

              <span className="text-slate-300 dark:text-slate-700">|</span>

              <span className="text-[11px] text-slate-400">Nét:</span>
              <div className="flex items-center gap-1">
                {lineWidths.map((w) => (
                  <button
                    key={w}
                    type="button"
                    onClick={() => setLineWidth(w)}
                    className={`px-1.5 py-0.5 text-[11px] rounded font-bold transition-colors cursor-pointer ${
                      lineWidth === w
                        ? "bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300"
                        : "text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-800"
                    }`}
                  >
                    {w}px
                  </button>
                ))}
              </div>
            </div>

            {/* Grid Type */}
            <div className="flex items-center gap-1 text-[11px]">
              <span className="text-slate-400">Lưới:</span>
              {(["none", "math_grid", "o_ly"] as const).map((g) => (
                <button
                  key={g}
                  type="button"
                  onClick={() => {
                    setGrid(g);
                    void saveDraft(strokes, g);
                  }}
                  className={`px-2 py-0.5 rounded font-bold transition-colors cursor-pointer ${
                    grid === g
                      ? "bg-slate-900 dark:bg-slate-700 text-white"
                      : "bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-300"
                  }`}
                >
                  {g === "none" ? "Trơn" : g === "math_grid" ? "Toán" : "Ô ly"}
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Canvas Area with Zoom/Pan Floating Status Bar */}
      <div
        onWheel={handleWheel}
        className={`relative flex-1 bg-white dark:bg-slate-950 overflow-hidden select-none touch-none ${
          isSpaceHeld ? "cursor-grab" : isReadOnly ? "cursor-default" : tool === "eraser" ? "cursor-cell" : "cursor-crosshair"
        }`}
      >
        {/* Floating Zoom and Pan Control Bar (Chuẩn theo hình ảnh mockup) */}
        <div className="absolute top-2 left-2 z-20 flex items-center gap-2 px-3 py-1 rounded-full bg-slate-900/85 backdrop-blur-md text-white text-xs shadow-lg border border-slate-700/70 select-none">
          <span className="text-slate-300 text-[11px] font-medium hidden sm:inline">
            Cuộn để thu phóng · Giữ Space hoặc kéo để di chuyển...
          </span>
          <div className="flex items-center gap-1 pl-1 border-l border-slate-700">
            <button
              type="button"
              onClick={() => setZoom((z) => Math.max(0.2, Number((z - 0.1).toFixed(2))))}
              className="w-5 h-5 flex items-center justify-center rounded hover:bg-slate-700 font-bold text-slate-200 cursor-pointer"
              title="Thu nhỏ"
            >
              −
            </button>
            <button
              type="button"
              onClick={() => {
                setZoom(1.0);
                setPan({ x: 0, y: 0 });
              }}
              className="px-2 py-0.5 rounded hover:bg-slate-700 font-black text-amber-400 font-mono text-xs cursor-pointer"
              title="Đặt lại về 100%"
            >
              {Math.round(zoom * 100)}%
            </button>
            <button
              type="button"
              onClick={() => setZoom((z) => Math.min(3.0, Number((z + 0.1).toFixed(2))))}
              className="w-5 h-5 flex items-center justify-center rounded hover:bg-slate-700 font-bold text-slate-200 cursor-pointer"
              title="Phóng to"
            >
              +
            </button>
          </div>
        </div>

        <canvas
          ref={canvasRef}
          width={CANVAS_WIDTH}
          height={CANVAS_HEIGHT}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={finishDrawing}
          onPointerCancel={finishDrawing}
          className="w-full h-full block"
        />
      </div>

      {/* Footer / Action bar */}
      <div className="p-3 bg-slate-100 dark:bg-slate-950 border-t border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs shrink-0">
        <span className="text-slate-500 text-[11px] font-medium">
          {isReadOnly ? "🔒 Bản vẽ nháp đã được lưu cố định cùng bài nộp" : saveStatus}
        </span>

        <div className="flex items-center gap-2">
          {exportStatus && (
            <span className="text-[11px] text-emerald-600 dark:text-emerald-400 font-bold animate-in fade-in">
              {exportStatus}
            </span>
          )}

          {isReadOnly ? (
            <span className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl bg-slate-200 dark:bg-slate-800 text-slate-500 dark:text-slate-400 font-bold text-xs select-none">
              🔒 Đã khóa chỉnh sửa
            </span>
          ) : (
            <button
              type="button"
              onClick={handleAttachSnapshot}
              className={`inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-extrabold shadow-sm transition-all cursor-pointer bg-emerald-600 hover:bg-emerald-500 text-white shadow-emerald-600/30`}
            >
              <span>{isAttached ? "✓ Cập nhật ảnh đính kèm" : "📎 Đính kèm vào bài làm"}</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

function appendFreehandPoint(points: ScratchpadPoint[], next: ScratchpadPoint): void {
  const last = points[points.length - 1];
  if (!last) {
    points.push(next);
    return;
  }
  const dx = next.x - last.x;
  const dy = next.y - last.y;
  if (dx * dx + dy * dy >= 4) {
    if (points.length < MAX_POINTS_PER_STROKE) {
      points.push(next);
    }
  }
}

function toPreviewStroke(
  tool: ScratchpadTool,
  color: string,
  lineWidth: number,
  points: ScratchpadPoint[],
  start: ScratchpadPoint | null,
  end: ScratchpadPoint | null
): ScratchpadStroke | null {
  const id = `stroke-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
  if (tool === "pen" || tool === "eraser") {
    const committedPoints = [...points];
    if (end) appendFreehandPoint(committedPoints, end);
    return committedPoints.length ? { id, tool, color, lineWidth, points: committedPoints } : null;
  }
  return start && end ? { id, tool, color, lineWidth, points: [], start, end } : null;
}
