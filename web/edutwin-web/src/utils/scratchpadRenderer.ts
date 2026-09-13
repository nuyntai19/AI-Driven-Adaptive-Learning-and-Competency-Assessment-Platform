import type {
  ScratchpadGrid,
  ScratchpadPoint,
  ScratchpadStroke,
} from "../types/scratchpad";

export const DEFAULT_CANVAS_WIDTH = 1200;
export const DEFAULT_CANVAS_HEIGHT = 800;

export function drawStrokes(context: CanvasRenderingContext2D, strokes: readonly ScratchpadStroke[]) {
  for (const stroke of strokes) drawStroke(context, stroke);
}

export function drawStroke(context: CanvasRenderingContext2D, stroke: ScratchpadStroke) {
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

export function drawAxes(context: CanvasRenderingContext2D, origin: ScratchpadPoint, end: ScratchpadPoint) {
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

export function drawGrid(context: CanvasRenderingContext2D, grid: ScratchpadGrid, width: number, height: number) {
  if (grid === "none") return;
  const minor = grid === "o_ly" ? 20 : 24;
  const major = grid === "o_ly" ? 100 : 0;
  context.save();
  context.strokeStyle = "#e2e8f0";
  context.lineWidth = 1;
  for (let x = 0; x <= width; x += minor) {
    context.beginPath();
    context.moveTo(x, 0);
    context.lineTo(x, height);
    context.stroke();
  }
  for (let y = 0; y <= height; y += minor) {
    context.beginPath();
    context.moveTo(0, y);
    context.lineTo(width, y);
    context.stroke();
  }
  if (major) {
    context.strokeStyle = "#cbd5e1";
    for (let x = 0; x <= width; x += major) {
      context.beginPath();
      context.moveTo(x, 0);
      context.lineTo(x, height);
      context.stroke();
    }
    for (let y = 0; y <= height; y += major) {
      context.beginPath();
      context.moveTo(0, y);
      context.lineTo(width, y);
      context.stroke();
    }
  }
  context.restore();
}

export async function renderPng(
  strokes: readonly ScratchpadStroke[],
  grid: ScratchpadGrid,
  width: number = DEFAULT_CANVAS_WIDTH,
  height: number = DEFAULT_CANVAS_HEIGHT
): Promise<Blob> {
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
