import { useQuery } from '@tanstack/react-query';
import {
  getAssignableQuestions,
  getAssignmentClasses,
  getAssignmentClassStudents,
} from '../../api/assignmentsApi';
import type {
  GetAssignmentClassesParams,
  GetAssignableQuestionsParams,
  GetAssignmentClassStudentsParams,
} from '../../api/assignmentsApi';

export interface WizardQueryOptions {
  enabled?: boolean;
}

export const useAssignmentClasses = (
  params?: GetAssignmentClassesParams,
  options?: WizardQueryOptions
) =>
  useQuery({
    queryKey: ['assignment-options', 'classes', params],
    queryFn: () => getAssignmentClasses(params),
    enabled: options?.enabled ?? true,
    staleTime: 30_000,
  });

export const useAssignableQuestions = (
  paramsOrSubjectId: string | GetAssignableQuestionsParams | undefined,
  options?: WizardQueryOptions
) => {
  const subjectId =
    typeof paramsOrSubjectId === 'string'
      ? paramsOrSubjectId
      : paramsOrSubjectId?.subjectId;

  return useQuery({
    queryKey: ['assignment-options', 'questions', paramsOrSubjectId],
    queryFn: () => getAssignableQuestions(paramsOrSubjectId!),
    enabled: (options?.enabled ?? true) && !!subjectId,
    staleTime: 30_000,
  });
};

export const useAssignmentClassStudents = (
  paramsOrClassId: string | GetAssignmentClassStudentsParams | undefined,
  options?: WizardQueryOptions
) => {
  const classId =
    typeof paramsOrClassId === 'string'
      ? paramsOrClassId
      : paramsOrClassId?.classId;

  return useQuery({
    queryKey: ['assignment-options', 'students', paramsOrClassId],
    queryFn: () => getAssignmentClassStudents(paramsOrClassId!),
    enabled: (options?.enabled ?? true) && !!classId,
    staleTime: 30_000,
  });
};
