import React, { useState, useRef, useEffect, useCallback } from "react";

interface FunctionGrapherProps {
  initialExpression?: string;
  onAttachToScratchpad?: (formula: string) => void;
}

export const FunctionGrapher: React.FC<FunctionGrapherProps> = ({
  initialExpression = "1 / (x - 2)",
  onAttachToScratchpad,
}) => {
  const [expression, setExpression] = useState(initialExpression);
  const [error, setError] = useState<string | null>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);

  // Range and scale settings
  const [xMin, setXMin] = useState(-8);
  const [xMax, setXMax] = useState(8);
  const [yMin, setYMin] = useState(-6);
  const [yMax, setYMax] = useState(6);

  // Safe expression evaluator for f(x)
  const compileFunction = useCallback((expr: string): ((x: number) => number) | null => {
    try {
      // Normalize common math syntax to JS Math
      const sanitized = expr
        .replace(/\^/g, "**")
        .replace(/sin\(/g, "Math.sin(")
        .replace(/cos\(/g, "Math.cos(")
        .replace(/tan\(/g, "Math.tan(")
        .replace(/sqrt\(/g, "Math.sqrt(")
        .replace(/abs\(/g, "Math.abs(")
        .replace(/log\(/g, "Math.log10(")
        .replace(/ln\(/g, "Math.log(")
        .replace(/pi/gi, "Math.PI")
        .replace(/e\b/g, "Math.E");

      // Validate allowed characters (digits, operators, Math functions, x, whitespace, parentheses)
      if (!/^[0-9xX+\-*/%.()Math\s,]+$/.test(sanitized)) {
        throw new Error("Biểu thức chứa ký tự không hợp lệ");
      }

      const fn = new Function("x", `"use strict"; return (${sanitized.replace(/x/g, "x")});`);
      // Test run with x = 1
      fn(1);
      return fn as (x: number) => number;
    } catch {
      return null;
    }
  }, []);

  // Draw Cartesian coordinate system and function curve
  const drawGraph = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;

    // Clear background
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, width, height);

    // Coordinate mapping functions
    const toCanvasX = (x: number) => ((x - xMin) / (xMax - xMin)) * width;
    const toCanvasY = (y: number) => height - ((y - yMin) / (yMax - yMin)) * height;

    // 1. Draw Grid lines
    ctx.lineWidth = 1;
    ctx.strokeStyle = "#e2e8f0";

    // Vertical grid
    for (let x = Math.ceil(xMin); x <= Math.floor(xMax); x++) {
      const cx = toCanvasX(x);
      ctx.beginPath();
      ctx.moveTo(cx, 0);
      ctx.lineTo(cx, height);
      ctx.stroke();
    }

    // Horizontal grid
    for (let y = Math.ceil(yMin); y <= Math.floor(yMax); y++) {
      const cy = toCanvasY(y);
      ctx.beginPath();
      ctx.moveTo(0, cy);
      ctx.lineTo(width, cy);
      ctx.stroke();
    }

    // 2. Draw Axes (Oxy)
    ctx.lineWidth = 2;
    ctx.strokeStyle = "#475569";
    const originX = toCanvasX(0);
    const originY = toCanvasY(0);

    // X axis
    ctx.beginPath();
    ctx.moveTo(0, originY);
    ctx.lineTo(width, originY);
    ctx.stroke();

    // Y axis
    ctx.beginPath();
    ctx.moveTo(originX, 0);
    ctx.lineTo(originX, height);
    ctx.stroke();

    // Arrow heads
    // X arrow
    ctx.beginPath();
    ctx.moveTo(width - 10, originY - 5);
    ctx.lineTo(width, originY);
    ctx.lineTo(width - 10, originY + 5);
    ctx.fillStyle = "#475569";
    ctx.fill();

    // Y arrow
    ctx.beginPath();
    ctx.moveTo(originX - 5, 10);
    ctx.lineTo(originX, 0);
    ctx.lineTo(originX + 5, 10);
    ctx.fill();

    // Axis Labels
    ctx.font = "12px sans-serif";
    ctx.fillStyle = "#334155";
    ctx.fillText("x", width - 15, originY + 18);
    ctx.fillText("y", originX + 10, 15);
    ctx.fillText("O", originX - 14, originY + 14);

    // Number marks
    ctx.font = "10px sans-serif";
    ctx.fillStyle = "#64748b";
    for (let x = Math.ceil(xMin); x <= Math.floor(xMax); x++) {
      if (x === 0) continue;
      const cx = toCanvasX(x);
      ctx.fillText(String(x), cx - 4, originY + 14);
    }
    for (let y = Math.ceil(yMin); y <= Math.floor(yMax); y++) {
      if (y === 0) continue;
      const cy = toCanvasY(y);
      ctx.fillText(String(y), originX + 5, cy + 3);
    }

    // 3. Plot Function Curve
    const fn = compileFunction(expression);
    if (!fn) {
      setError("Cú pháp hàm số chưa hợp lệ (Ví dụ: x^2 - 3, 1/(x - 2), sin(x))");
      return;
    }
    setError(null);

    ctx.lineWidth = 2.5;
    ctx.strokeStyle = "#4f46e5"; // Indigo color
    ctx.beginPath();

    let isDrawing = false;
    const step = (xMax - xMin) / (width * 2);

    for (let x = xMin; x <= xMax; x += step) {
      try {
        const y = fn(x);
        if (isNaN(y) || !isFinite(y) || Math.abs(y) > 100) {
          isDrawing = false;
          continue;
        }

        const cx = toCanvasX(x);
        const cy = toCanvasY(y);

        if (!isDrawing) {
          ctx.moveTo(cx, cy);
          isDrawing = true;
        } else {
          ctx.lineTo(cx, cy);
        }
      } catch {
        isDrawing = false;
      }
    }
    ctx.stroke();
  }, [compileFunction, expression, xMax, xMin, yMax, yMin]);

  useEffect(() => {
    drawGraph();
  }, [drawGraph]);

  const presets = [
    { label: "1 / (x - 2)", expr: "1 / (x - 2)" },
    { label: "x² - 4", expr: "x^2 - 4" },
    { label: "sin(x)", expr: "sin(x)" },
    { label: "√x", expr: "sqrt(x)" },
    { label: "2x + 1", expr: "2 * x + 1" },
  ];

  return (
    <div className="flex flex-col h-full bg-white rounded-xl overflow-hidden border border-slate-200">
      {/* Formula Input */}
      <div className="p-3 bg-slate-50 border-b border-slate-200 flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="font-serif italic font-bold text-slate-700 text-sm">f(x) =</span>
          <input
            type="text"
            value={expression}
            onChange={(e) => setExpression(e.target.value)}
            placeholder="Nhập hàm số: x^2 - 3, 1/(x-2)..."
            className="flex-1 px-3 py-1.5 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 font-mono text-slate-900"
          />
          {onAttachToScratchpad && (
            <button
              type="button"
              onClick={() => onAttachToScratchpad(expression)}
              className="px-2.5 py-1.5 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border border-indigo-200 rounded-lg text-xs font-semibold transition-colors cursor-pointer"
              title="Chép hàm số sang bảng nháp"
            >
              Chép vào nháp
            </button>
          )}
        </div>

        {/* Quick Presets */}
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="text-[11px] text-slate-500">Mẫu:</span>
          {presets.map((p) => (
            <button
              key={p.label}
              type="button"
              onClick={() => setExpression(p.expr)}
              className={`px-2 py-0.5 text-xs rounded border transition-colors cursor-pointer font-mono ${
                expression === p.expr
                  ? "bg-indigo-600 text-white border-indigo-600 font-bold"
                  : "bg-white text-slate-700 border-slate-200 hover:bg-slate-100"
              }`}
            >
              {p.label}
            </button>
          ))}
        </div>

        {error && <p className="text-xs text-rose-600">{error}</p>}
      </div>

      {/* Canvas */}
      <div className="relative flex-1 bg-white min-h-[360px] flex items-center justify-center p-2">
        <canvas
          ref={canvasRef}
          width={520}
          height={380}
          className="w-full h-auto max-h-[420px] rounded-lg border border-slate-100 shadow-xs"
        />
      </div>

      {/* Zoom / View controls */}
      <div className="px-3 py-2 bg-slate-50 border-t border-slate-200 flex items-center justify-between text-xs text-slate-600">
        <span>Đồ thị hàm số hai chiều (Oxy)</span>
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={() => {
              setXMin((prev) => prev * 0.8);
              setXMax((prev) => prev * 0.8);
              setYMin((prev) => prev * 0.8);
              setYMax((prev) => prev * 0.8);
            }}
            className="px-2 py-1 bg-white border border-slate-300 rounded hover:bg-slate-100 font-bold"
            title="Phóng to"
          >
            +
          </button>
          <button
            type="button"
            onClick={() => {
              setXMin((prev) => prev * 1.25);
              setXMax((prev) => prev * 1.25);
              setYMin((prev) => prev * 1.25);
              setYMax((prev) => prev * 1.25);
            }}
            className="px-2 py-1 bg-white border border-slate-300 rounded hover:bg-slate-100 font-bold"
            title="Thu nhỏ"
          >
            -
          </button>
          <button
            type="button"
            onClick={() => {
              setXMin(-8);
              setXMax(8);
              setYMin(-6);
              setYMax(6);
            }}
            className="px-2 py-1 bg-white border border-slate-300 rounded hover:bg-slate-100 text-xs"
            title="Đặt lại góc nhìn"
          >
            Mặc định
          </button>
        </div>
      </div>
    </div>
  );
};
