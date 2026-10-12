import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import { createRequire } from "node:module";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { scratchpadPoint, scratchpadTransform } from "../src/utils/scratchpadViewport.ts";
import { drawGrid, drawStrokes } from "../src/utils/scratchpadRenderer.ts";

const require = createRequire(import.meta.url);
test("paper aspect ratio and pointer round trips stay exact in narrow/tall or wide viewports", () => {
  for (const [width, height] of [[450, 650], [1200, 300], [320, 210], [800, 800]]) {
    for (const zoom of [0.2, 1, 3]) {
      const pan = { x: 70, y: -45 };
      const t = scratchpadTransform(width, height, zoom, pan);
      for (const point of [{ x: 0, y: 0 }, { x: 600, y: 400 }, { x: 1200, y: 800 }]) {
        const screen = { x: t.x + point.x * t.scale, y: t.y + point.y * t.scale };
        const roundTrip = scratchpadPoint(screen, width, height, zoom, pan);
        assert.ok(Math.abs(roundTrip.x - point.x) < 1e-8);
        assert.ok(Math.abs(roundTrip.y - point.y) < 1e-8);
      }
      assert.ok(Math.abs((1200 * t.scale) / (800 * t.scale) - 1.5) < 1e-12);
    }
  }
});

test("panning uses screen pixels and moves the paper by the same distance at every fit/zoom", () => {
  for (const zoom of [0.2, 1, 3]) {
    const a = scratchpadTransform(450, 650, zoom, { x: 0, y: 0 });
    const b = scratchpadTransform(450, 650, zoom, { x: 37, y: 61 });
    assert.equal(b.x - a.x, 37);
    assert.equal(b.y - a.y, 61);
    assert.equal(a.scale, b.scale);
  }
});

function context() {
  const calls: any[] = [];
  const mock: any = { calls };
  for (const name of ["save", "restore", "clearRect", "translate", "scale", "beginPath", "moveTo", "lineTo", "stroke", "drawImage"]) mock[name] = (...args: any[]) => calls.push([name, ...args]);
  mock.fillRect = (...args: any[]) => calls.push(["fillRect", mock.fillStyle, ...args]);
  return mock;
}

test("actual canvas redraw paints white paper and applies a uniform transform; eraser uses separate ink layer", () => {
  const source = fs.readFileSync(new URL("../src/components/math/ScratchpadInlinePanel.tsx", import.meta.url), "utf8");
  const ast = ts.createSourceFile("Scratchpad.tsx", source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  let callback: ts.Node | undefined;
  const walk = (node: ts.Node) => { if (ts.isVariableDeclaration(node) && node.name.getText(ast) === "redraw") callback = (node.initializer as ts.CallExpression).arguments[0]; ts.forEachChild(node, walk); };
  walk(ast);
  assert.ok(callback);
  const code = ts.transpileModule(`exports.redraw = ${callback.getText(ast)};`, { compilerOptions: { target: ts.ScriptTarget.ES2020 } }).outputText;
  const display = context();
  const ink = context();
  const exports: any = {};
  vm.runInNewContext(code, { exports, canvasRef: { current: { width: 450, height: 650, getContext: () => display } },
    inkCanvasRef: { current: null }, document: { createElement: () => ({ getContext: () => ink }) },
    viewport: { width: 450, height: 650 }, zoom: 1, pan: { x: 0, y: 0 }, CANVAS_WIDTH: 1200, CANVAS_HEIGHT: 800,
    scratchpadTransform, drawGrid, drawStrokes, strokes: [], grid: "math_grid", isDrawingRef: { current: false } });
  exports.redraw();
  assert.deepEqual(display.calls.find((c: any[]) => c[0] === "scale").slice(1), [0.375, 0.375]);
  assert.ok(display.calls.some((c: any[]) => c[0] === "fillRect" && c[1] === "#ffffff" && c[4] === 1200 && c[5] === 800));
  assert.ok(ink.calls.some((c: any[]) => c[0] === "clearRect"));
  assert.ok(display.calls.some((c: any[]) => c[0] === "drawImage"));
});

const sideSource = fs.readFileSync(new URL("../src/components/math/SideAssistantWorkspace.tsx", import.meta.url), "utf8");
const sideCode = ts.transpileModule(sideSource, { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true } }).outputText;
const sideExports: any = {};
vm.runInNewContext(sideCode, { exports: sideExports, require: (name: string) => {
  if (name.startsWith("react")) return require(name);
  if (name.endsWith("AttemptScratchpadAttachment")) return { AttemptScratchpadAttachment: ({ attemptId }: any) => React.createElement("div", { "data-server-attempt": attemptId }, "Submitted image") };
  if (name.endsWith("ScratchpadInlinePanel")) return { ScratchpadInlinePanel: () => React.createElement("div", { "data-local-draft": true }, "Local draft") };
  return {};
}});

function side(extra: object) {
  return renderToStaticMarkup(React.createElement(sideExports.SideAssistantWorkspace, { activeTab: "scratchpad", centerId: "center", userId: "user", clientSubmissionId: "local-key", onClose: () => {}, onChangeTab: () => {}, ...extra }));
}

test("submitted scratchpad mounts server attachment only, not local IndexedDB drawing canvas", () => {
  const html = side({ isReadOnly: true, submittedAttemptId: "7", hasSubmittedScratchpad: true });
  assert.match(html, /data-server-attempt="7"/);
  assert.doesNotMatch(html, /data-local-draft|Local draft/);
});

test("review without submitted attachment has an explicit empty state, never substitutes a local draft", () => {
  const html = side({ isReadOnly: true, isScratchpadAttached: true, submittedAttemptId: "7", hasSubmittedScratchpad: false });
  assert.match(html, /không có ảnh nháp/);
  assert.doesNotMatch(html, /data-local-draft|data-server-attempt/);
  assert.match(side({ isReadOnly: false }), /data-local-draft/);
});
