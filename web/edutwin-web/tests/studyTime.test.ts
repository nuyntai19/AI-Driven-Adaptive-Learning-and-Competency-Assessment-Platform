import test from "node:test";
import assert from "node:assert/strict";
import { formatStudyMinutes, splitReason } from "../src/utils/studyTime.ts";

test("formats study minutes as hours and minutes", () => {
  assert.equal(formatStudyMinutes(45), "45 phút");
  assert.equal(formatStudyMinutes(120), "2 giờ");
  assert.equal(formatStudyMinutes(160), "2 giờ 40 phút");
});

test("keeps the first sentence of a reason and hides the rest", () => {
  const { lead, rest } = splitReason("Phần yếu: luyện theo vòng, mỗi bộ 6 câu. Sau mỗi bộ hệ thống chọn bộ tiếp theo.");
  assert.equal(lead, "Phần yếu: luyện theo vòng, mỗi bộ 6 câu.");
  assert.equal(rest, "Sau mỗi bộ hệ thống chọn bộ tiếp theo.");
  assert.deepEqual(splitReason("Một câu duy nhất"), { lead: "Một câu duy nhất", rest: "" });
});
