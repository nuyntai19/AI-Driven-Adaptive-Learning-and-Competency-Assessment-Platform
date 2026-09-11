export interface StudentBasicInfoDto {
  studentId: string;
  fullName: string;
}

export interface SubjectBasicInfoDto {
  subjectId: string;
  subjectName: string;
}

export interface StudentSubjectGoalDto {
  targetScore: number;
  remainingDays: number;
  currentPredictedScore: number;
  riskScore: number;
}

export interface StudentMasteryRadarItemDto {
  topicNodeId: string;
  topicName: string;
  mastery: number;
}

export interface StudentProgressLinePointDto {
  recordedAt: string;
  overallSubjectMastery: number;
}

export interface StudentRecommendedActionDto {
  recommendationId?: string;
  strategy: string;
  topicNodeId: string;
  topicName: string;
  questionId?: string;
  opportunityScore: number;
  explanation: string;
}

export interface StudentDashboardDataDto {
  student: StudentBasicInfoDto;
  subject: SubjectBasicInfoDto;
  goal: StudentSubjectGoalDto;
  masteryRadar: StudentMasteryRadarItemDto[];
  progressLine: StudentProgressLinePointDto[];
  action?: StudentRecommendedActionDto | null;
}

export interface ClassBasicInfoDto {
  classId: string;
  className: string;
  subjectId: string;
  subjectName: string;
}

export interface ClassOverviewDto {
  studentCount: number;
  averageMastery: number;
  averagePredictedScore: number;
  assignmentCompletionRate: number;
  highRiskStudentCount: number;
  weakTopicCount: number;
}

export interface ClassHighRiskStudentDto {
  studentId: string;
  fullName: string;
  targetScore: number;
  predictedScore: number;
  remainingDays: number;
  riskScore: number;
}

export interface ClassWeakTopicDto {
  topicNodeId: string;
  topicName: string;
  averageMastery: number;
  affectedStudentCount: number;
}

export interface ClassGapGroupDto {
  groupKey: string;
  topicNodeId: string;
  topicName: string;
  threshold: number;
  studentCount: number;
  studentIds: string[];
  suggestedAction: string;
}

export interface ClassDashboardDataDto {
  class: ClassBasicInfoDto;
  overview: ClassOverviewDto;
  highRiskStudents: ClassHighRiskStudentDto[];
  weakTopics: ClassWeakTopicDto[];
  gapGroups: ClassGapGroupDto[];
}

export interface CenterSummaryDto {
  studentCount: number;
  classCount: number;
  teacherCount: number;
}

export interface SubjectMasterySummaryDto {
  subjectId: string;
  subjectName: string;
  averageMastery: number;
}

export interface ClassHighRiskSummaryDto {
  classId: string;
  className: string;
  highRiskStudentCount: number;
  totalStudentCount: number;
}

export interface ClassRankingItemDto {
  rank: number;
  classId: string;
  className: string;
  subjectName: string;
  averageMastery: number;
  assignmentCompletionRate: number;
  highRiskStudentCount: number;
}

export interface CenterDashboardDataDto {
  summary: CenterSummaryDto;
  masteryBySubject: SubjectMasterySummaryDto[];
  highRiskByClass: ClassHighRiskSummaryDto[];
  classRanking: ClassRankingItemDto[];
}
