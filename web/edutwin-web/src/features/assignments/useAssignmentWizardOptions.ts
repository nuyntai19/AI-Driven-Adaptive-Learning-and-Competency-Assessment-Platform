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

export const useAssignmentClasses = (params?: GetAssignmentClassesParams) =>
  useQuery({
    queryKey: ['assignment-options', 'classes', params],
    queryFn: () => getAssignmentClasses(params),
    staleTime: 30_000,
  });

export const useAssignableQuestions = (
  paramsOrSubjectId: string | GetAssignableQuestionsParams | undefined
) => {
  const subjectId =
    typeof paramsOrSubjectId === 'string'
      ? paramsOrSubjectId
      : paramsOrSubjectId?.subjectId;

  return useQuery({
    queryKey: ['assignment-options', 'questions', paramsOrSubjectId],
    queryFn: () => getAssignableQuestions(paramsOrSubjectId!),
    enabled: !!subjectId,
    staleTime: 30_000,
  });
};

export const useAssignmentClassStudents = (
  paramsOrClassId: string | GetAssignmentClassStudentsParams | undefined
) => {
  const classId =
    typeof paramsOrClassId === 'string'
      ? paramsOrClassId
      : paramsOrClassId?.classId;

  return useQuery({
    queryKey: ['assignment-options', 'students', paramsOrClassId],
    queryFn: () => getAssignmentClassStudents(paramsOrClassId!),
    enabled: !!classId,
    staleTime: 30_000,
  });
};
