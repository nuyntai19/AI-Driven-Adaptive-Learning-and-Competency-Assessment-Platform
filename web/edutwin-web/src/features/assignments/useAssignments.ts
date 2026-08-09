import { useQuery } from '@tanstack/react-query';
import { getAssignments } from '../../api/assignmentsApi';
import type { GetAssignmentsParams } from '../../api/assignmentsApi';

export const useAssignments = (params?: GetAssignmentsParams) => {
  return useQuery({
    queryKey: ['assignments', params],
    queryFn: () => getAssignments(params),
  });
};
