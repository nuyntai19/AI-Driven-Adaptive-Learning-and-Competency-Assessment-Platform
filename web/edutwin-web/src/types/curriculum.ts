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
  graphVersionId?: string | null;
  applicationClasses?: CurriculumClassSummary[];
  classIds: string[];
  nodeIds: string[];
  rowVersion: string;
}

export interface CreateCurriculumRequest {
  graphVersionId?: string | null;
  visibility?: import("./questions").MaterialVisibility;
  teacherId?: string | null;
  subjectId: string;
  gradeLevel?: number | null;
  title: string;
  description?: string;
  nodeIds: string[];
}

export interface UpdateCurriculumRequest {
  graphVersionId?: string | null;
  nodeIds?: string[];
  classIds?: string[];
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
  graphVersionId?: string | null;
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

export interface CurriculumClassSummary {
  classId: string;
  className: string;
  gradeLevel?: number | null;
  academicYear: string;
  applicationRole?: string | null;
  isCurrent: boolean;
  isPlanned?: boolean;
}

