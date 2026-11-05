import type { AgendaItemDto, ExamMilestone } from "../types/learningPath";

type RouteEntry = { key: string; kind: "stage"; missions: AgendaItemDto[] } | { key: string; kind: "exam"; exam: ExamMilestone };

export function learningPathTimeline(missions: AgendaItemDto[], exams: ExamMilestone[], timeZone: string): RouteEntry[] {
  const groups = new Map<string, AgendaItemDto[]>();
  for (const mission of missions) {
    const key = mission.milestoneOrder ? `stage-${mission.milestoneOrder}` : `skill-${mission.skillId}`;
    groups.set(key, [...(groups.get(key) ?? []), mission]);
  }
  // Preserve prerequisite order among stages; insert fixed class exams at their local calendar date.
  const remaining = [...exams].sort((a, b) => Date.parse(a.examAt) - Date.parse(b.examAt));
  const result: RouteEntry[] = [];
  for (const [key, group] of groups) {
    const date = group.map(m => m.plannedDate).filter((d): d is string => !!d).sort()[0];
    while (date && remaining.length && new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date(remaining[0].examAt)) <= date) {
      const exam = remaining.shift()!;
      result.push({ key: `exam-${exam.assignmentId}`, kind: "exam", exam });
    }
    result.push({ key, kind: "stage", missions: group });
  }
  for (const exam of remaining) result.push({ key: `exam-${exam.assignmentId}`, kind: "exam", exam });
  return result;
}
