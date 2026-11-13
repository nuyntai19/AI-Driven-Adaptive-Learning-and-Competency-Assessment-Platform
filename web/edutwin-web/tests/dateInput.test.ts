import assert from "node:assert/strict";
import test from "node:test";
import { formatDateInput, isDateOnlyText, maskDateInput, parseDateInput } from "../src/utils/dateInput.ts";

test("dates are shown as dd/mm/yyyy (and 24-hour time) from the native value", () => {
  assert.equal(formatDateInput("2026-12-09"), "09/12/2026");
  assert.equal(formatDateInput("2026-12-09T07:05:30.000Z"), "09/12/2026");
  assert.equal(formatDateInput("2026-12-09T07:05", true), "09/12/2026 07:05");
  assert.equal(formatDateInput("2026-12-09", true), "09/12/2026 00:00");
  assert.equal(formatDateInput(""), "");
  assert.equal(formatDateInput(null), "");
  assert.equal(formatDateInput("12/09/2026"), "");
});

test("typed dd/mm/yyyy becomes the native value only when it is a real, complete date", () => {
  assert.equal(parseDateInput("09/12/2026"), "2026-12-09");
  assert.equal(parseDateInput(" 29/02/2028 "), "2028-02-29");
  assert.equal(parseDateInput("29/02/2027"), null);
  assert.equal(parseDateInput("31/04/2026"), null);
  assert.equal(parseDateInput("12/13/2026"), null, "month first is not accepted");
  assert.equal(parseDateInput("09/12/20"), null);
  assert.equal(parseDateInput("9/12/2026"), null);
  assert.equal(parseDateInput("09/12/2026 23:59", true), "2026-12-09T23:59");
  assert.equal(parseDateInput("09/12/2026 24:00", true), null);
  assert.equal(parseDateInput("09/12/2026", true), null);
  assert.ok(isDateOnlyText("09/12/2026"));
  assert.ok(!isDateOnlyText("09/12/2026 08:00"));
});

test("typing digits adds the separators and pads single-digit parts", () => {
  assert.equal(maskDateInput("0"), "0");
  assert.equal(maskDateInput("09"), "09/");
  assert.equal(maskDateInput("09/1"), "09/1");
  assert.equal(maskDateInput("0912"), "09/12/");
  assert.equal(maskDateInput("09122026"), "09/12/2026");
  assert.equal(maskDateInput("091220261"), "09/12/2026", "a date field stops at the year");
  assert.equal(maskDateInput("9/"), "09/");
  assert.equal(maskDateInput("1/1/"), "01/01/");
  assert.equal(maskDateInput("091220260730", true), "09/12/2026 07:30");
  assert.equal(maskDateInput("09/12/2026 7:", true), "09/12/2026 07");
  assert.equal(maskDateInput("09/12/2026 073", true), "09/12/2026 07:3");
});
