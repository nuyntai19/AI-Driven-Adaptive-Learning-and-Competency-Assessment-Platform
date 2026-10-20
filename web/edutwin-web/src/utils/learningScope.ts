import type { SkillCapabilityStatusDto } from "../types/learningPath";

export function groupLearningSkills(skills: SkillCapabilityStatusDto[]) {
  const groups = new Map<string, { id: string; name: string; skills: SkillCapabilityStatusDto[] }>();
  for (const skill of skills) {
    const id = skill.topicId ?? skill.topicName ?? skill.skillId;
    const group = groups.get(id) ?? { id, name: skill.topicName || skill.skillName, skills: [] };
    group.skills.push(skill);
    groups.set(id, group);
  }
  return [...groups.values()];
}

/** Only actual graph edges imply prerequisites; display order never does. */
export function missingLearningPrerequisites(skills: SkillCapabilityStatusDto[], selected: string[]) {
  const byId = new Map(skills.map(s => [s.skillId, s]));
  const included = new Set(selected);
  const pending = [...selected];
  const missing: SkillCapabilityStatusDto[] = [];
  while (pending.length) {
    for (const id of byId.get(pending.shift()!)?.prerequisiteSkillIds ?? []) {
      if (included.has(id)) continue;
      included.add(id);
      const skill = byId.get(id);
      if (skill) {
        if (skill.state !== "Confirmed") missing.push(skill);
        pending.push(id);
      }
    }
  }
  return missing;
}
