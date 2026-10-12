import type { ClassDto } from "../types/organization";

export function groupCurriculumClasses(classes: ClassDto[], actor: string | undefined, subject: string,
  grade: number | null | undefined, savedIds: string[]) {
  const eligible = classes.filter(c => c.teacher.teacherId === actor && c.subject.subjectId === subject &&
    c.status === "Active" && c.learningScope !== "History" && (c.gradeLevel != null || savedIds.includes(c.classId)));
  const matching = eligible.filter(c => grade != null && c.gradeLevel === grade);
  const exceptions = eligible.filter(c => grade == null || c.gradeLevel !== grade);
  return { eligible, matching, exceptions };
}

export function needsNewGradeException(classes: ClassDto[], selectedIds: string[], savedIds: string[],
  grade: number | null | undefined): boolean {
  return classes.some(c => selectedIds.includes(c.classId) && !savedIds.includes(c.classId) &&
    (grade == null || c.gradeLevel == null || c.gradeLevel !== grade));
}

export function activeApplicationClassCount(applications: {classId: string; endedAt: string | null; pausedByClass?: boolean}[]): number {
  return new Set(applications.filter(a => !a.endedAt && !a.pausedByClass).map(a => a.classId)).size;
}
