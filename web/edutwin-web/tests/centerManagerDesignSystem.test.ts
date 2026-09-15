import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const readSource = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");
const styles = readSource("../src/components/centerManager/centerManagerDesignSystem.css");
const primitives = readSource("../src/components/centerManager/CenterManagerPrimitives.tsx");
const table = readSource("../src/components/centerManager/CenterManagerDataTable.tsx");
const overlays = readSource("../src/components/centerManager/CenterManagerOverlays.tsx");
const scope = readSource("../src/components/centerManager/CenterManagerThemeScope.tsx");

test("CenterManager design tokens are actor-scoped and respect reduced motion", () => {
  assert.match(styles, /\[data-actor="center-manager"\]/);
  assert.match(styles, /--cm-bg:\s*#0b0f19/);
  assert.match(styles, /--cm-cyan:\s*#06b6d4/);
  assert.match(styles, /@media \(prefers-reduced-motion: reduce\)/);
  assert.doesNotMatch(styles, /(^|\n)\s*:root\s*\{/);
  assert.match(scope, /data-actor="center-manager"/);
});

test("data primitives expose accessible loading, error, conflict and table semantics", () => {
  assert.match(primitives, /aria-label="Breadcrumb"/);
  assert.match(primitives, /role=\{decorative \? undefined : "status"\}/);
  assert.match(primitives, /role="alert"/);
  assert.match(primitives, /mapSafeOperationalError/);
  assert.match(table, /<caption className="sr-only">/);
  assert.match(table, /scope="col"/);
  assert.match(table, /aria-busy=/);
  assert.match(table, /aria-label={`Phân trang/);
});

test("drawer and confirm dialog enforce modal semantics and shared focus management", () => {
  assert.match(overlays, /useModalAccessibility/);
  assert.match(overlays, /role="dialog"/);
  assert.match(overlays, /role="alertdialog"/);
  assert.match(overlays, /aria-modal="true"/);
  assert.match(overlays, /initialFocusRef:/);
  assert.match(overlays, /aria-label="Đóng bảng điều khiển"/);
});
