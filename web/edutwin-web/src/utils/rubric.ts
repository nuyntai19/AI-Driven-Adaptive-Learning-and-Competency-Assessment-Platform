import type { GradingCriteria, RubricCriterion } from "../types/questions.ts";
import type { RubricGrade, RubricScoreInput } from "../types/reviews.ts";

export type RubricForm = Record<string, { score: string; comment: string }>;
export function hydrateRubricForm(criteria: RubricCriterion[], grade?: RubricGrade | null): RubricForm {
  return Object.fromEntries(criteria.map(c => {
    const saved = grade?.criteria.find(r => r.criterionId === c.criterionId);
    return [c.criterionId, { score: saved ? String(Math.round(saved.awardedScore / grade!.maxScore * 1000) / 100) : '', comment: saved?.comment || '' }];
  }));
}

export function buildRubricScores(criteria: RubricCriterion[], form: RubricForm, maxScore: number): { scores: RubricScoreInput[]; error: string | null; total: number } {
  const scores: RubricScoreInput[] = [];
  for (const c of criteria) {
    const entry = form[c.criterionId];
    const numeric = Number(entry?.score);
    const raw = Math.round(numeric * maxScore / 10 * 100) / 100;
    if (!entry?.score.trim() || !Number.isFinite(numeric) || numeric < 0 || numeric > Number((c.maxScore / maxScore * 10).toFixed(2)) || raw > c.maxScore)
      return { scores: [], total: 0, error: `Hãy nhập điểm hợp lệ cho tiêu chí “${c.title}”.` };
    scores.push({ criterionId: c.criterionId, awardedScore: raw, comment: entry.comment.trim() || null });
  }
  return { scores, total: Math.round(scores.reduce((sum, c) => sum + c.awardedScore, 0) * 100) / 100, error: null };
}

export function hydrateGradingCriteria(value?: GradingCriteria | string | null): GradingCriteria {
  if (typeof value === "string") return { schemaVersion: "1.0", requiredIdeas: [], commonErrors: [], scoringNotes: value, criteria: [] };
  return { schemaVersion: value?.schemaVersion || "1.0", requiredIdeas: [...(value?.requiredIdeas || [])],
    commonErrors: [...(value?.commonErrors || [])], scoringNotes: value?.scoringNotes || "",
    criteria: (value?.criteria || []).map(c => ({ ...c, ...(c.visualRequirements ? { visualRequirements: [...c.visualRequirements] } : {}) })) };
}

export function rubricDefinitionError(criteria: RubricCriterion[], maxScore: number): string | null {
  if (!criteria.length) return null;
  if (criteria.some(c => (c.visualRequirements || []).length > 12 ||
      (c.visualRequirements || []).some(r => !r.trim() || r.length > 500) ||
      new Set(c.visualRequirements || []).size !== (c.visualRequirements || []).length))
    return "Yêu cầu ảnh nháp: tối đa 12 mục mỗi tiêu chí, không để dòng trống/trùng, mỗi mục tối đa 500 ký tự.";
  if (criteria.length > 20 || new Set(criteria.map(c => c.criterionId)).size !== criteria.length ||
      criteria.some(c => !c.criterionId.trim() || !c.title.trim() || c.title.length > 200 || !Number.isFinite(c.maxScore) ||
        c.maxScore <= 0 || Math.abs(c.maxScore * 100 - Math.round(c.maxScore * 100)) > 1e-7))
    return "Mỗi tiêu chí cần tên, mã riêng và điểm tối đa dương (tối đa 2 chữ số thập phân).";
  return Math.abs(criteria.reduce((sum, c) => sum + c.maxScore, 0) - maxScore) > 1e-7
    ? "Tổng điểm tối đa của các tiêu chí phải bằng điểm tối đa câu hỏi." : null;
}
