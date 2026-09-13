/** A point in the canvas's intrinsic coordinate space, not CSS pixels. */
export interface ScratchpadPoint {
  x: number;
  y: number;
}

export type ScratchpadTool = "pen" | "eraser" | "line" | "circle" | "triangle" | "oxy";

export type ScratchpadGrid = "none" | "math_grid" | "o_ly";

export interface ScratchpadStroke {
  id: string;
  tool: ScratchpadTool;
  color: string;
  lineWidth: number;
  /** Freehand tools use points; shape tools use start and end. */
  points: ScratchpadPoint[];
  start?: ScratchpadPoint;
  end?: ScratchpadPoint;
}

export interface ScratchpadDraft {
  storageKey: string;
  centerId: string;
  userId: string;
  clientSubmissionId: string;
  strokes: ScratchpadStroke[];
  gridType: ScratchpadGrid;
  canvasWidth: number;
  canvasHeight: number;
  updatedAt: number;
  expiresAt: number;
}
