import assert from "node:assert/strict";
import test from "node:test";
import { releaseMathLivePageStyles } from "../src/utils/visualMathFieldLifecycle.ts";

test("clears a leftover keyboard padding and a leftover MathLive scroll lock", () => {
  const body = { style: { paddingBottom: "241px", marginRight: "15px", overflow: "hidden" } } as unknown as HTMLElement;
  releaseMathLivePageStyles(body);
  assert.deepEqual(body.style, { paddingBottom: "", marginRight: "", overflow: "" });
});

test("keeps the scroll lock of an app modal, which has no margin-right", () => {
  const body = { style: { paddingBottom: "", marginRight: "", overflow: "hidden" } } as unknown as HTMLElement;
  releaseMathLivePageStyles(body);
  assert.equal(body.style.overflow, "hidden");
});
