import assert from "node:assert/strict";
import test from "node:test";
import { hydrateGradingCriteria, rubricDefinitionError, hydrateRubricForm, buildRubricScores } from "../src/utils/rubric.ts";

const criteria = [{ criterionId: "method", title: "Phương pháp", description: "Cách giải hợp lệ bất kỳ", maxScore: 0.5 },
  { criterionId: "result", title: "Kết quả", description: "", maxScore: 1.5 }];
test("hydration preserves all legacy criteria and creates an independent copy", () => {
  const existing = { schemaVersion: '1.0', requiredIdeas: ['a', 'b'], commonErrors: ['wrong division'], scoringNotes: '', criteria };
  const hydrated = hydrateGradingCriteria(existing);
  assert.deepEqual(hydrated, existing);
  hydrated.requiredIdeas.push('c'); hydrated.criteria![0].title = 'changed';
  assert.deepEqual(existing.requiredIdeas, ['a', 'b']); assert.equal(existing.criteria[0].title, 'Phương pháp');
});
test("numeric rubric validates bounds, duplicate IDs and an exact point budget", () => {
  assert.equal(rubricDefinitionError(criteria, 2), null);
  assert.ok(rubricDefinitionError(criteria, 3));
  assert.ok(rubricDefinitionError([criteria[0], criteria[0]], 1));
  assert.ok(rubricDefinitionError([{ ...criteria[0], maxScore: 0 }], 0));
});
test("rubric grading requires explicit scores, accepting zero but not blank or excessive scores", () => {
  const blank = hydrateRubricForm(criteria);
  assert.ok(buildRubricScores(criteria, blank, 2).error);
  const result = buildRubricScores(criteria, { method: { score: '2.5', comment: 'valid alternative' }, result: { score: '0', comment: '' } }, 2);
  assert.equal(result.error, null); assert.equal(result.total, 0.5);
  assert.equal(result.scores[0].awardedScore, 0.5);
  assert.ok(buildRubricScores(criteria, { method: { score: '3', comment: '' }, result: { score: '7.5', comment: '' } }, 2).error);
});
test("saved native rubric scores hydrate into a consistent /10 form", () => {
  const form = hydrateRubricForm(criteria, { maxScore: 2, awardedScore: 2, criteria: criteria.map(c => ({ ...c, awardedScore: c.maxScore })) });
  assert.equal(form.method.score, '2.5'); assert.equal(form.result.score, '7.5');
  assert.equal(buildRubricScores(criteria, form, 2).total, 2);
});
