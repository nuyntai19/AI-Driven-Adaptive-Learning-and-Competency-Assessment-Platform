export interface EnrollmentClass { classId: string; gradeLevel?: number | null; status: string }

// Initial enrollment is strict. Deliberate grade exceptions use the separate
// add-to-class workflow with an explicit reason, never this account form.
export function initialEnrollmentClasses<T extends EnrollmentClass>(classes: readonly T[], grade: number): T[] {
  return classes.filter(c => c.status === "Active" && c.gradeLevel === grade);
}
