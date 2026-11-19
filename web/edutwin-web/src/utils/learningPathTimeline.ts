import type { AgendaItemDto } from "../types/learningPath";

export function learningPathTimeline(missions: AgendaItemDto[]) {
  const groups = new Map<string, AgendaItemDto[]>();
  for (const mission of missions) {
    const key = mission.milestoneOrder ? `stage-${mission.milestoneOrder}` : `skill-${mission.skillId}`;
    const group = groups.get(key);
    if (group) group.push(mission);
    else groups.set(key, [mission]);
  }
  return Array.from(groups, ([key, missions]) => ({ key, missions }));
}
