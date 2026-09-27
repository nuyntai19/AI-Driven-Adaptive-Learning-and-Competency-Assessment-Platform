import assert from "node:assert/strict";
import test from "node:test";
import {
  buildAssignmentDraftKey,
  clearAllLegacyAssignmentDrafts,
  clearLegacyAssignmentDraftsForAssignment,
  readAssignmentDraft,
  writeAssignmentDraft,
  type LocalStorageLike,
} from "../src/utils/assignmentDraftStorage.ts";

class MemoryLocalStorage implements LocalStorageLike {
  private readonly values = new Map<string, string>();

  get length(): number {
    return this.values.size;
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  key(index: number): string | null {
    return Array.from(this.values.keys())[index] ?? null;
  }
}

test("assignment draft key is scoped by center, student, and assignment", () => {
  assert.equal(
    buildAssignmentDraftKey({
      centerId: "center-1",
      userId: "student-a",
      assignmentId: "assignment-9",
    }),
    "edutwin:assignment-answers:center-1:student-a:assignment-9"
  );
});

test("logging out student A then logging in student B never restores A's draft", () => {
  const storage = new MemoryLocalStorage();
  const studentA = {
    centerId: "center-1",
    userId: "student-a",
    assignmentId: "assignment-9",
  };
  const studentB = {
    centerId: "center-1",
    userId: "student-b",
    assignmentId: "assignment-9",
  };
  const studentADraft = JSON.stringify({ question1: { finalAnswer: "A" } });

  writeAssignmentDraft(studentA, studentADraft, storage);
  storage.setItem("edutwin_assignment_answers_assignment-legacy", "unsafe-shared-draft");

  // Logout removes unsafe historical keys, while a user's scoped draft may remain resumable.
  clearAllLegacyAssignmentDrafts(storage);

  assert.equal(storage.getItem("edutwin_assignment_answers_assignment-legacy"), null);
  assert.equal(readAssignmentDraft(studentB, storage), null);
  assert.equal(readAssignmentDraft(studentA, storage), studentADraft);
});

test("legacy assignment-only answer and snapshot keys are deleted instead of migrated", () => {
  const storage = new MemoryLocalStorage();
  const assignmentId = "assignment-legacy";
  storage.setItem(`edutwin_assignment_answers_${assignmentId}`, "student-a-answer");
  storage.setItem(`edutwin_assignment_snapshot_${assignmentId}`, "student-a-image");
  storage.setItem(`edutwin_assignment_snapshot_time_${assignmentId}`, "10:30");
  storage.setItem("edutwin-theme", "dark");

  clearLegacyAssignmentDraftsForAssignment(assignmentId, storage);

  assert.equal(storage.getItem(`edutwin_assignment_answers_${assignmentId}`), null);
  assert.equal(storage.getItem(`edutwin_assignment_snapshot_${assignmentId}`), null);
  assert.equal(storage.getItem(`edutwin_assignment_snapshot_time_${assignmentId}`), null);
  assert.equal(storage.getItem("edutwin-theme"), "dark");
});

test("timer remaining seconds is scoped and persisted properly", async () => {
  const {
    readAssignmentRemainingSeconds,
    writeAssignmentRemainingSeconds,
    removeAssignmentRemainingSeconds,
    buildAssignmentTimerKey,
  } = await import("../src/utils/assignmentDraftStorage.ts");

  const storage = new MemoryLocalStorage();
  const scope = {
    centerId: "center-1",
    userId: "student-a",
    assignmentId: "assignment-9",
  };

  assert.equal(buildAssignmentTimerKey(scope), "edutwin:assignment-timer:center-1:student-a:assignment-9");
  assert.equal(readAssignmentRemainingSeconds(scope, storage), null);

  // Write remaining seconds
  writeAssignmentRemainingSeconds(scope, 99, storage);
  assert.equal(readAssignmentRemainingSeconds(scope, storage), 99);

  // Overwrite when countdown ticks
  writeAssignmentRemainingSeconds(scope, 98, storage);
  assert.equal(readAssignmentRemainingSeconds(scope, storage), 98);

  // Remove on submission or reset
  removeAssignmentRemainingSeconds(scope, storage);
  assert.equal(readAssignmentRemainingSeconds(scope, storage), null);
});

