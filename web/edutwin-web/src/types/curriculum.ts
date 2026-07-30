export type ReviewStatus = "Draft" | "Published" | "Archived";

export interface Curriculum {
  curriculumId: string;
  subjectId: string;
  teacherId: string | null;
  title: string;
  description: string | null;
  reviewStatus: ReviewStatus;
  classIds: string[];
  nodeIds: string[];
  rowVersion: string;
}

export interface CreateCurriculumRequest {
  teacherId?: string | null;
  subjectId: string;
  title: string;
  description?: string;
  nodeIds: string[];
}

export interface UpdateCurriculumRequest {
  title: string;
  description?: string;
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
