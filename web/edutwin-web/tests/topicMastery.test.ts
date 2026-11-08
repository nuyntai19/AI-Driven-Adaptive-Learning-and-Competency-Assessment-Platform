import assert from "node:assert/strict";
import test from "node:test";
import { topicMasteryStatus } from "../src/utils/topicMastery.ts";

test("a topic without evidence is unmeasured even at 0%", () => {
  assert.equal(topicMasteryStatus(0, 0), "noData");
});

test("measured topics are banded by mastery", () => {
  assert.equal(topicMasteryStatus(0, 1), "needsWork");
  assert.equal(topicMasteryStatus(49.9, 3), "needsWork");
  assert.equal(topicMasteryStatus(50, 3), "developing");
  assert.equal(topicMasteryStatus(74.9, 3), "developing");
  assert.equal(topicMasteryStatus(75, 3), "mastered");
});
