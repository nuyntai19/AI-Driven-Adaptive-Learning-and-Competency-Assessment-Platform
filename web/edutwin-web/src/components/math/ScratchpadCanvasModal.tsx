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
  saveScratchpadDraft,
} from "../../utils/scratchpadStorage";

const CANVAS_WIDTH = 1200;
const CANVAS_HEIGHT = 800;
const MAX_HISTORY_STATES = 30;
const MAX_EXPORT_BYTES = 5 * 1024 * 1024;

const tools: ReadonlyArray<{ value: ScratchpadTool; label: string; title: string }> = [
  { value: "pen", label: "Bút", title: "Bút vẽ tự do" },
  { value: "eraser", label: "Tẩy", title: "Tẩy nét vẽ" },
  { value: "line", label: "Thước", title: "Vẽ đoạn thẳng" },
  { value: "circle", label: "Compa", title: "Vẽ hình tròn hoặc elip" },
  { value: "triangle", label: "Tam giác", title: "Vẽ tam giác cân" },
  { value: "oxy", label: "Trục Oxy", title: "Vẽ hệ trục tọa độ" },
];

const colours = ["#0f172a", "#2563eb", "#dc2626", "#16a34a"] as const;
const lineWidths = [1, 2, 4, 6] as const;

export interface ScratchpadCanvasModalProps {
  isOpen: boolean;
  onClose: () => void;
  centerId: string;
  userId: string;
  clientSubmissionId: string;
  /** Gate 5 will upload this transient PNG; Gate 4 never persists it in IndexedDB. */
  onExportPng?: (png: Blob) => void;
}

/**
 * Full-screen vector scratchpad. IndexedDB stores only vector strokes and scope metadata;
 * the PNG is generated on demand and remains in memory for the caller to use.
 */
export const ScratchpadCanvasModal = ({
  isOpen,
  onClose,
  centerId,
  userId,
  clientSubmissionId,
  onExportPng,
}: ScratchpadCanvasModalProps) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const isDrawingRef = useRef(false);
  const currentPointsRef = useRef<ScratchpadPoint[]>([]);
  const startPointRef = useRef<ScratchpadPoint | null>(null);
  const previewPointRef = useRef<ScratchpadPoint | null>(null);

  const [tool, setTool] = useState<ScratchpadTool>("pen");
  const [colour, setColour] = useState<string>(colours[0]);
  const [lineWidth, setLineWidth] = useState<number>(2);
  const [grid, setGrid] = useState<ScratchpadGrid>("math_grid");
  const [strokes, setStrokes] = useState<ScratchpadStroke[]>([]);
  const [undoHistory, setUndoHistory] = useState<ScratchpadStroke[][]>([]);
  const [redoHistory, setRedoHistory] = useState<ScratchpadStroke[][]>([]);
  const [isDraftLoaded, setIsDraftLoaded] = useState(false);
  const [saveStatus, setSaveStatus] = useState("Sẵn sàng");
  const [exportStatus, setExportStatus] = useState<string | null>(null);

  const latestDraftRef = useRef({ strokes, grid });
  useEffect(() => {
    latestDraftRef.current = { strokes, grid };
  }, [grid, strokes]);

  const saveDraft = useCallback(
    async (nextStrokes: ScratchpadStroke[], nextGrid: ScratchpadGrid) => {
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
        await saveScratchpadDraft(draft);
        setSaveStatus("Đã lưu trên thiết bị");
      } catch {
        setSaveStatus("Không thể lưu nháp cục bộ");
      }
    },
    [centerId, clientSubmissionId, userId]
  );

  useEffect(() => {
    if (!isOpen) return;

    let active = true;
    setIsDraftLoaded(false);
    setExportStatus(null);
    setSaveStatus("Đang tải nháp…");
    void getScratchpadDraft(centerId, userId, clientSubmissionId)
      .then((draft) => {
        if (!active) return;
        setStrokes(draft?.strokes ?? []);
        setGrid(draft?.gridType ?? "math_grid");
        setUndoHistory([]);
        setRedoHistory([]);
        setIsDraftLoaded(true);
        setSaveStatus(draft ? "Đã khôi phục nháp" : "Nháp mới");
      })
      .catch(() => {
        if (active) {
          setIsDraftLoaded(true);
          setSaveStatus("Không thể tải nháp cũ");
        }
      });

    return () => {
      active = false;
    };
  }, [centerId, clientSubmissionId, isOpen, userId]);

  const redraw = useCallback(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;

    context.clearRect(0, 0, canvas.width, canvas.height);
    drawStrokes(context, strokes);

    if (!isDrawingRef.current) return;
    const preview = toPreviewStroke(
      tool,
      colour,
      lineWidth,
      currentPointsRef.current,
      startPointRef.current,
      previewPointRef.current
    );
    if (preview) drawStroke(context, preview);
  }, [colour, lineWidth, strokes, tool]);

  useEffect(() => {
    redraw();
  }, [redraw]);

  const toCanvasPoint = (event: React.PointerEvent<HTMLCanvasElement>): ScratchpadPoint => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    return {
      x: Math.max(0, Math.min(canvas.width, (event.clientX - rect.left) * (canvas.width / rect.width))),
      y: Math.max(0, Math.min(canvas.height, (event.clientY - rect.top) * (canvas.height / rect.height))),
    };
  };

  const finishDrawing = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!isDrawingRef.current) return;
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

    const previous = latestDraftRef.current.strokes;
    const next = [...previous, { ...draftStroke, id: createStrokeId() }];
    latestDraftRef.current = { strokes: next, grid };
    setUndoHistory((history) => [previous, ...history].slice(0, MAX_HISTORY_STATES));
    setStrokes(next);
    setRedoHistory([]);
    void saveDraft(next, grid);
  };

  const undo = useCallback(() => {
    if (!undoHistory.length) return;
    const [previous, ...remaining] = undoHistory;
    latestDraftRef.current = { strokes: previous, grid };
    setUndoHistory(remaining);
    setRedoHistory((history) => [strokes, ...history].slice(0, MAX_HISTORY_STATES));
    setStrokes(previous);
    void saveDraft(previous, grid);
  }, [grid, saveDraft, strokes, undoHistory]);

  const redo = useCallback(() => {
    if (!redoHistory.length) return;
    const [next, ...remaining] = redoHistory;
    latestDraftRef.current = { strokes: next, grid };
    setUndoHistory((history) => [strokes, ...history].slice(0, MAX_HISTORY_STATES));
    setStrokes(next);
    setRedoHistory(remaining);
    void saveDraft(next, grid);
  }, [grid, redoHistory, saveDraft, strokes]);

  const clearAll = () => {
    if (!strokes.length || !window.confirm("Xóa toàn bộ nét vẽ trong bảng nháp?")) return;
    setUndoHistory((history) => [strokes, ...history].slice(0, MAX_HISTORY_STATES));
    setRedoHistory([]);
    latestDraftRef.current = { strokes: [], grid };
    setStrokes([]);
    void saveDraft([], grid);
  };

  const changeGrid = (nextGrid: ScratchpadGrid) => {
    latestDraftRef.current = { strokes, grid: nextGrid };
    setGrid(nextGrid);
    void saveDraft(strokes, nextGrid);
  };

  const exportPng = async () => {
    const canvas = canvasRef.current;
    if (!canvas || !isDraftLoaded) return;
    setExportStatus("Đang tạo ảnh PNG…");

    try {
      const png = await renderPng(strokes, grid, canvas.width, canvas.height);
      if (png.size > MAX_EXPORT_BYTES) {
        setExportStatus("Ảnh vượt quá giới hạn 5 MB; hãy xóa bớt nét vẽ rồi thử lại.");
        return;
      }

      onExportPng?.(png);
      setExportStatus(`Đã tạo ảnh PNG (${formatBytes(png.size)}).`);
    } catch {
      setExportStatus("Không thể tạo ảnh PNG từ bảng nháp.");
    }
  };

  const close = useCallback(() => {
    if (isDraftLoaded) {
      const current = latestDraftRef.current;
      void saveDraft(current.strokes, current.grid);
    }
    onClose();
  }, [isDraftLoaded, onClose, saveDraft]);

  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        close();
      } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
        event.preventDefault();
        if (event.shiftKey) redo();
        else undo();
      } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "y") {
        event.preventDefault();
        redo();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [close, isOpen, redo, undo]);

  if (!isOpen) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="scratchpad-title"
      className="fixed inset-0 z-50 flex bg-slate-950/70 p-2 backdrop-blur-sm sm:p-4"
    >
      <section className="flex h-full w-full flex-col overflow-hidden rounded-xl bg-white shadow-2xl">
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 bg-slate-50 px-4 py-3">
          <div>
            <h2 id="scratchpad-title" className="text-base font-bold text-slate-900">Bảng vẽ nháp</h2>
            <p className="text-xs text-slate-500">{saveStatus} · Tự xóa sau 24 giờ hoặc khi đăng xuất</p>
          </div>
          <button type="button" onClick={close} className="rounded-md px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-200">
            Đóng
          </button>
        </header>

        <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 px-3 py-2 text-xs">
          <div className="flex flex-wrap gap-1" aria-label="Công cụ vẽ">
            {tools.map((item) => (
              <button
                key={item.value}
                type="button"
                title={item.title}
                onClick={() => setTool(item.value)}
                className={`rounded px-2 py-1.5 font-medium ${tool === item.value ? "bg-indigo-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200"}`}
              >
                {item.label}
              </button>
            ))}
          </div>
          <span className="hidden h-5 w-px bg-slate-200 sm:block" />
          <div className="flex gap-1" aria-label="Màu nét vẽ">
            {colours.map((item) => (
              <button
                key={item}
                type="button"
                title={`Màu ${item}`}
                aria-label={`Chọn màu ${item}`}
                onClick={() => { setColour(item); setTool("pen"); }}
                className={`h-6 w-6 rounded-full border-2 ${colour === item && tool !== "eraser" ? "border-indigo-500 ring-2 ring-indigo-200" : "border-slate-300"}`}
                style={{ backgroundColor: item }}
              />
            ))}
          </div>
          <div className="flex gap-1" aria-label="Độ dày nét vẽ">
            {lineWidths.map((item) => (
              <button key={item} type="button" onClick={() => setLineWidth(item)} className={`rounded px-2 py-1 ${lineWidth === item ? "bg-indigo-100 text-indigo-700" : "bg-slate-100 text-slate-700"}`}>
                {item}px
              </button>
            ))}
          </div>
          <div className="ml-auto flex flex-wrap gap-1">
            {(["none", "math_grid", "o_ly"] as const).map((item) => (
              <button key={item} type="button" onClick={() => changeGrid(item)} className={`rounded px-2 py-1 ${grid === item ? "bg-indigo-100 text-indigo-700" : "bg-slate-100 text-slate-700"}`}>
                {item === "none" ? "Nền trơn" : item === "math_grid" ? "Lưới toán" : "Ô ly"}
              </button>
            ))}
            <button type="button" disabled={!undoHistory.length} onClick={undo} className="rounded bg-slate-100 px-2 py-1 text-slate-700 disabled:opacity-40">Hoàn tác</button>
            <button type="button" disabled={!redoHistory.length} onClick={redo} className="rounded bg-slate-100 px-2 py-1 text-slate-700 disabled:opacity-40">Làm lại</button>
            <button type="button" disabled={!strokes.length} onClick={clearAll} className="rounded bg-rose-50 px-2 py-1 text-rose-700 disabled:opacity-40">Xóa hết</button>
            <button type="button" disabled={!isDraftLoaded} onClick={() => void exportPng()} className="rounded bg-indigo-600 px-2 py-1 font-semibold text-white disabled:opacity-40">Tạo PNG</button>
          </div>
        </div>

        {exportStatus && <p role="status" className="border-b border-slate-200 px-4 py-2 text-xs text-slate-600">{exportStatus}</p>}

        <div className={`relative min-h-0 flex-1 overflow-hidden bg-white ${gridClassName(grid)}`} aria-busy={!isDraftLoaded}>
          <canvas
            ref={canvasRef}
            width={CANVAS_WIDTH}
            height={CANVAS_HEIGHT}
            className={`h-full w-full touch-none cursor-crosshair ${isDraftLoaded ? "" : "pointer-events-none opacity-50"}`}
            onPointerDown={(event) => {
              event.currentTarget.setPointerCapture(event.pointerId);
              isDrawingRef.current = true;
              const point = toCanvasPoint(event);
              if (tool === "pen" || tool === "eraser") currentPointsRef.current = [point];
              else startPointRef.current = point;
              previewPointRef.current = point;
              redraw();
            }}
            onPointerMove={(event) => {
              if (!isDrawingRef.current) return;
              const point = toCanvasPoint(event);
              if (tool === "pen" || tool === "eraser") currentPointsRef.current.push(point);
              previewPointRef.current = point;
              redraw();
            }}
            onPointerUp={finishDrawing}
            onPointerCancel={finishDrawing}
            aria-label="Bảng vẽ nháp toán học"
          />
        </div>

        <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 bg-slate-50 px-4 py-2 text-xs text-slate-600">
          <span>{strokes.length} thao tác · Ctrl/Cmd+Z để hoàn tác · tối đa 30 bước</span>
          <span>PNG chỉ được tạo trong bộ nhớ; chưa tải lên máy chủ ở Gate 4.</span>
        </footer>
      </section>
    </div>
  );
};

function createStrokeId(): string {
  return typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
    ? crypto.randomUUID()
    : `stroke-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function toPreviewStroke(
  tool: ScratchpadTool,
  color: string,
  lineWidth: number,
  points: ScratchpadPoint[],
  start: ScratchpadPoint | null,
  end: ScratchpadPoint | null
): ScratchpadStroke | null {
  if (tool === "pen" || tool === "eraser") {
    const committedPoints = end ? [...points, end] : points;
    return committedPoints.length ? { id: "preview", tool, color, lineWidth, points: committedPoints } : null;
  }
  return start && end ? { id: "preview", tool, color, lineWidth, points: [], start, end } : null;
}

function drawStrokes(context: CanvasRenderingContext2D, strokes: readonly ScratchpadStroke[]) {
  for (const stroke of strokes) drawStroke(context, stroke);
}

function drawStroke(context: CanvasRenderingContext2D, stroke: ScratchpadStroke) {
  context.save();
  context.lineCap = "round";
  context.lineJoin = "round";
  context.strokeStyle = stroke.color;
  context.fillStyle = stroke.color;
  context.lineWidth = stroke.tool === "eraser" ? stroke.lineWidth * 4 : stroke.lineWidth;
  context.globalCompositeOperation = stroke.tool === "eraser" ? "destination-out" : "source-over";

  if (stroke.tool === "pen" || stroke.tool === "eraser") {
    if (stroke.points.length === 1) {
      context.beginPath();
      context.arc(stroke.points[0].x, stroke.points[0].y, context.lineWidth / 2, 0, Math.PI * 2);
      context.fill();
    } else if (stroke.points.length > 1) {
      context.beginPath();
      context.moveTo(stroke.points[0].x, stroke.points[0].y);
      for (const point of stroke.points.slice(1)) context.lineTo(point.x, point.y);
      context.stroke();
    }
  } else if (stroke.start && stroke.end) {
    const { start, end } = stroke;
    context.beginPath();
    if (stroke.tool === "line") {
      context.moveTo(start.x, start.y);
      context.lineTo(end.x, end.y);
    } else if (stroke.tool === "circle") {
      context.arc(start.x, start.y, Math.max(1, Math.hypot(end.x - start.x, end.y - start.y)), 0, Math.PI * 2);
    } else if (stroke.tool === "triangle") {
      context.moveTo(start.x, end.y);
      context.lineTo((start.x + end.x) / 2, start.y);
      context.lineTo(end.x, end.y);
      context.closePath();
    } else if (stroke.tool === "oxy") {
      drawAxes(context, start, end);
      context.restore();
      return;
    }
    context.stroke();
  }
  context.restore();
}

function drawAxes(context: CanvasRenderingContext2D, origin: ScratchpadPoint, end: ScratchpadPoint) {
  const horizontal = Math.max(80, Math.abs(end.x - origin.x));
  const vertical = Math.max(80, Math.abs(end.y - origin.y));
  context.beginPath();
  context.moveTo(origin.x - horizontal, origin.y);
  context.lineTo(origin.x + horizontal, origin.y);
  context.moveTo(origin.x, origin.y - vertical);
  context.lineTo(origin.x, origin.y + vertical);
  context.stroke();
  context.beginPath();
  context.moveTo(origin.x + horizontal, origin.y);
  context.lineTo(origin.x + horizontal - 10, origin.y - 5);
  context.lineTo(origin.x + horizontal - 10, origin.y + 5);
  context.closePath();
  context.fill();
  context.beginPath();
  context.moveTo(origin.x, origin.y - vertical);
  context.lineTo(origin.x - 5, origin.y - vertical + 10);
  context.lineTo(origin.x + 5, origin.y - vertical + 10);
  context.closePath();
  context.fill();
  context.font = "bold 14px sans-serif";
  context.fillText("x", origin.x + horizontal + 5, origin.y + 4);
  context.fillText("y", origin.x - 5, origin.y - vertical - 6);
  context.fillText("O", origin.x - 15, origin.y + 15);
}

async function renderPng(strokes: readonly ScratchpadStroke[], grid: ScratchpadGrid, width: number, height: number): Promise<Blob> {
  const background = document.createElement("canvas");
  background.width = width;
  background.height = height;
  const backgroundContext = background.getContext("2d");
  const overlay = document.createElement("canvas");
  overlay.width = width;
  overlay.height = height;
  const overlayContext = overlay.getContext("2d");
  if (!backgroundContext || !overlayContext) throw new Error("Canvas rendering is unavailable.");

  backgroundContext.fillStyle = "#ffffff";
  backgroundContext.fillRect(0, 0, width, height);
  drawGrid(backgroundContext, grid, width, height);
  drawStrokes(overlayContext, strokes);
  backgroundContext.drawImage(overlay, 0, 0);

  return new Promise<Blob>((resolve, reject) => {
    background.toBlob((blob) => {
      if (blob && blob.type === "image/png") resolve(blob);
      else reject(new Error("PNG encoding failed."));
    }, "image/png");
  });
}

function drawGrid(context: CanvasRenderingContext2D, grid: ScratchpadGrid, width: number, height: number) {
  if (grid === "none") return;
  const minor = grid === "o_ly" ? 20 : 24;
  const major = grid === "o_ly" ? 100 : 0;
  context.save();
  context.strokeStyle = "#e2e8f0";
  context.lineWidth = 1;
  for (let x = 0; x <= width; x += minor) { context.beginPath(); context.moveTo(x, 0); context.lineTo(x, height); context.stroke(); }
  for (let y = 0; y <= height; y += minor) { context.beginPath(); context.moveTo(0, y); context.lineTo(width, y); context.stroke(); }
  if (major) {
    context.strokeStyle = "#cbd5e1";
    for (let x = 0; x <= width; x += major) { context.beginPath(); context.moveTo(x, 0); context.lineTo(x, height); context.stroke(); }
    for (let y = 0; y <= height; y += major) { context.beginPath(); context.moveTo(0, y); context.lineTo(width, y); context.stroke(); }
  }
  context.restore();
}

function gridClassName(grid: ScratchpadGrid): string {
  if (grid === "math_grid") return "bg-[linear-gradient(to_right,#e2e8f0_1px,transparent_1px),linear-gradient(to_bottom,#e2e8f0_1px,transparent_1px)] bg-[size:24px_24px]";
  if (grid === "o_ly") return "bg-[linear-gradient(to_right,#e2e8f0_1px,transparent_1px),linear-gradient(to_bottom,#e2e8f0_1px,transparent_1px)] bg-[size:20px_20px]";
  return "";
}

function formatBytes(bytes: number): string {
  return `${Math.max(1, Math.ceil(bytes / 1024))} KB`;
}
