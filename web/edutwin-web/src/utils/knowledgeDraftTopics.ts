import type { KnowledgeGraphNodeDto, KnowledgeGraphVersion } from "../types/knowledgeGraph";
import type { Curriculum } from "../types/curriculum";

/**
 * Topic list of the framework editor: which topics to show and which may be deleted.
 *   grade       topics fitting the draft's grade (or with no grade yet) plus everything selected — the default
 *   all         every topic of the subject library
 *   selected    topics ticked in this draft;  unselected: the rest of the grade list
 *   draftOnly   topics not used by any approved/archived framework or published/archived curriculum
 *   version:ID  topics of another framework version;  curriculum:ID: topics of a curriculum
 */
export type DraftTopicFilter = "grade" | "all" | "selected" | "unselected" | "draftOnly" | `version:${string}` | `curriculum:${string}`;

type VersionLike = Pick<KnowledgeGraphVersion, "graphVersionId" | "status" | "nodeMappings">;
type CurriculumLike = Pick<Curriculum, "curriculumId" | "reviewStatus" | "nodeIds">;

/** Topics whose content is frozen by an approved/archived framework or a published/archived curriculum. */
export function lockedTopicIds(versions: VersionLike[] = [], curricula: CurriculumLike[] = []): Set<string> {
  const locked = new Set<string>();
  for (const version of versions) if (version.status !== "Draft") version.nodeMappings?.forEach(m => locked.add(m.nodeId));
  for (const curriculum of curricula) if (curriculum.reviewStatus !== "Draft") curriculum.nodeIds.forEach(id => locked.add(id));
  return locked;
}

export function filterDraftTopics(input: {
  topics: KnowledgeGraphNodeDto[];
  gradeLevel?: number;
  selectedIds: ReadonlySet<string>;
  lockedIds: ReadonlySet<string>;
  versions?: VersionLike[];
  curricula?: CurriculumLike[];
  filter: DraftTopicFilter;
  search?: string;
}): KnowledgeGraphNodeDto[] {
  const { topics, gradeLevel, selectedIds, lockedIds, filter } = input;
  const fitsGrade = (n: KnowledgeGraphNodeDto) => !gradeLevel || !n.gradeLevels?.length || n.gradeLevels.includes(gradeLevel) || selectedIds.has(n.nodeId);
  let list: KnowledgeGraphNodeDto[];
  if (filter === "all") list = topics;
  else if (filter === "selected") list = topics.filter(n => selectedIds.has(n.nodeId));
  else if (filter === "unselected") list = topics.filter(n => fitsGrade(n) && !selectedIds.has(n.nodeId));
  else if (filter === "draftOnly") list = topics.filter(n => !lockedIds.has(n.nodeId));
  else if (filter.startsWith("version:")) {
    const ids = new Set(input.versions?.find(v => v.graphVersionId === filter.slice(8))?.nodeMappings?.map(m => m.nodeId) ?? []);
    list = topics.filter(n => ids.has(n.nodeId));
  } else if (filter.startsWith("curriculum:")) {
    const ids = new Set(input.curricula?.find(c => c.curriculumId === filter.slice(11))?.nodeIds ?? []);
    list = topics.filter(n => ids.has(n.nodeId));
  } else list = topics.filter(fitsGrade);
  const query = input.search?.trim().toLocaleLowerCase("vi");
  return query ? list.filter(n => `${n.nodeName} ${n.nodeCode}`.toLocaleLowerCase("vi").includes(query)) : list;
}
