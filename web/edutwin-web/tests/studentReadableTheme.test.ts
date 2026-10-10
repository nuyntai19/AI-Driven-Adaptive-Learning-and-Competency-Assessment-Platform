import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const css = readFileSync(new URL("../src/styles/student-theme.css", import.meta.url), "utf8");
test("student theme uses a cool neutral canvas rather than the old cream palette", () => {
  assert.match(css, /--student-canvas:\s*#F6F8FC/);
  assert.match(css, /--student-surface-subtle:\s*#EDF2F7/);
  assert.doesNotMatch(css, /#FBF8F1|#F5F1E8/);
  assert.match(css, /--student-canvas:\s*#101623/); // dark mode preserved
});
test("larger typography is scoped, responsive and does not simulate browser zoom", () => {
  assert.match(css, /@media \(min-width: 768px\)\s*\{\s*\.student-shell\s*\{/);
  assert.match(css, /--text-2xl:\s*1\.875rem/);
  assert.match(css, /--text-xs:\s*0\.8125rem/); // mobile readable minimum
  assert.doesNotMatch(css, /\bzoom\s*:|transform:\s*scale\(|\bhtml\s*\{|\bbody\s*\{/);
});
