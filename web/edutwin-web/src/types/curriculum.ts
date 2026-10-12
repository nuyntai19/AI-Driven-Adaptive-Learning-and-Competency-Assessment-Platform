export type ReviewStatus = "Draft" | "Published" | "Archived";

export interface Curriculum {
  visibility?: import("./questions").MaterialVisibility;
  curriculumId: string;
  subjectId: string;
  teacherId: string | null;
  title: string;
  description: string | null;
  reviewStatus: ReviewStatus;
  gradeLevel?: number | null;
  classIds: string[];
  nodeIds: string[];
  rowVersion: string;
}

export interface CreateCurriculumRequest {
  visibility?: import("./questions").MaterialVisibility;
  teacherId?: string | null;
  subjectId: string;
  gradeLevel?: number | null;
  title: string;
  description?: string;
  nodeIds: string[];
}

export interface UpdateCurriculumRequest {
  visibility?: import("./questions").MaterialVisibility;
  title: string;
  description?: string;
  gradeLevel?: number | null;
  rowVersion: string;
}

export interface PublishCurriculumRequest {
  rowVersion: string;
}

export interface UpdateCurriculumClassesRequest {
  classIds: string[];
  rowVersion: string;
}

export interface UpdateCurriculumNodesRequest {
  nodeIds: string[];
  rowVersion: string;
}

export interface ArchiveCurriculumRequest {
  rowVersion: string;
  reason: string;
}

export interface CloneCurriculumRequest {
  title?: string;
}

