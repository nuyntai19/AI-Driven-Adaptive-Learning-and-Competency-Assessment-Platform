import { useQuery } from '@tanstack/react-query';
import { getAssignmentProgress } from '../../api/assignmentsApi';

export const useAssignmentProgress = (id: string | undefined) => {
  return useQuery({
    queryKey: ['assignments', id, 'progress'],
    queryFn: () => getAssignmentProgress(id!),
    enabled: !!id,
  });
};
