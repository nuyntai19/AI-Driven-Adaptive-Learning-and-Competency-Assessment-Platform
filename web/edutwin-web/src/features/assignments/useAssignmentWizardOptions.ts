import { useQuery } from '@tanstack/react-query';
import {
  getAssignableQuestions,
  getAssignmentClasses,
  getAssignmentClassStudents,
} from '../../api/assignmentsApi';

export const useAssignmentClasses = () =>
  useQuery({
    queryKey: ['assignment-options', 'classes'],
    queryFn: getAssignmentClasses,
    staleTime: 30_000,
  });

export const useAssignableQuestions = (subjectId: string | undefined) =>
  useQuery({
    queryKey: ['assignment-options', 'questions', subjectId],
    queryFn: () => getAssignableQuestions(subjectId!),
    enabled: !!subjectId,
    staleTime: 30_000,
  });

export const useAssignmentClassStudents = (classId: string | undefined) =>
  useQuery({
    queryKey: ['assignment-options', 'students', classId],
    queryFn: () => getAssignmentClassStudents(classId!),
    enabled: !!classId,
    staleTime: 30_000,
  });
