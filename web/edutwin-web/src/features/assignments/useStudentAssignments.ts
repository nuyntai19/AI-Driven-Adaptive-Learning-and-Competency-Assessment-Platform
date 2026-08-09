import { useQuery } from '@tanstack/react-query';
import { getStudentAssignments } from '../../api/assignmentsApi';
import type { GetStudentAssignmentsParams } from '../../api/assignmentsApi';

export const useStudentAssignments = (params?: GetStudentAssignmentsParams) => {
  return useQuery({
    queryKey: ['student-assignments', params],
    queryFn: () => getStudentAssignments(params),
  });
};
