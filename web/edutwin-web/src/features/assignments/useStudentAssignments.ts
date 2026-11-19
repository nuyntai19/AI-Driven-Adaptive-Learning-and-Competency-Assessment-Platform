import { useQuery } from '@tanstack/react-query';
import { getStudentAssignments } from '../../api/assignmentsApi';
import type { GetStudentAssignmentsParams } from '../../api/assignmentsApi';

export const useStudentAssignments = (params?: GetStudentAssignmentsParams) => {
  return useQuery({
    queryKey: ['student-assignments', params],
    queryFn: async () => {
      const first = await getStudentAssignments({ ...params, page: 1 });
      const assignments = [...first.data];
      const totalPages = first.meta.totalPages ?? 1;
      for (let page = 2; page <= totalPages; page++) {
        const next = await getStudentAssignments({ ...params, page });
        assignments.push(...next.data);
      }
      return assignments;
    },
    staleTime: 0,
    refetchOnMount: 'always',
  });
};
