import assert from "node:assert/strict";
import test from "node:test";
import {
  buildAttemptSessionKey,
  clearAttemptSessionId,
  clearUserAttemptSessionIds,
  getOrCreateAttemptSessionId,
  type SessionStorageLike,
} from "../src/utils/attemptSessionStorage.ts";

class MemorySessionStorage implements SessionStorageLike {
  private readonly values = new Map<string, string>();

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  get length(): number {
    return this.values.size;
  }

  key(index: number): string | null {
    return [...this.values.keys()][index] ?? null;
  }
}

const scope = {
  centerId: "center-a",
  userId: "student-a",
  subjectId: "subject-math",
  questionId: "question-42",
};

test("attempt session identity is scoped and survives a reload-style lookup", () => {
  const storage = new MemorySessionStorage();
  const created = getOrCreateAttemptSessionId(scope, storage, () => "submission-stable");
  const restored = getOrCreateAttemptSessionId(scope, storage, () => "submission-should-not-be-created");

  assert.equal(created, "submission-stable");
  assert.equal(restored, "submission-stable");
  assert.equal(
    buildAttemptSessionKey(scope),
    "edutwin:attempt-session:center-a:student-a:subject-math:question-42"
  );
});

test("clearing a successful attempt session allows only a new submission identity", () => {
  const storage = new MemorySessionStorage();
  getOrCreateAttemptSessionId(scope, storage, () => "submission-old");
  clearAttemptSessionId(scope, storage);

  assert.equal(getOrCreateAttemptSessionId(scope, storage, () => "submission-new"), "submission-new");
});

test("account switch clears only the outgoing student's attempt identities", () => {
  const storage = new MemorySessionStorage();
  const firstQuestion = scope;
  const secondQuestion = { ...scope, questionId: "question-43" };
  const otherStudent = { ...scope, userId: "student-b" };

  getOrCreateAttemptSessionId(firstQuestion, storage, () => "student-a-first");
  getOrCreateAttemptSessionId(secondQuestion, storage, () => "student-a-second");
  getOrCreateAttemptSessionId(otherStudent, storage, () => "student-b-first");

  clearUserAttemptSessionIds(scope.centerId, scope.userId, storage);

  assert.equal(storage.getItem(buildAttemptSessionKey(firstQuestion)), null);
  assert.equal(storage.getItem(buildAttemptSessionKey(secondQuestion)), null);
  assert.equal(storage.getItem(buildAttemptSessionKey(otherStudent)), "student-b-first");
});
