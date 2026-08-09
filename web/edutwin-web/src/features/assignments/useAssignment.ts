import { useQuery } from '@tanstack/react-query';
import { getAssignmentById } from '../../api/assignmentsApi';

export const useAssignment = (id: string | undefined) => {
  return useQuery({
    queryKey: ['assignments', id],
    queryFn: () => getAssignmentById(id!),
    enabled: !!id,
  });
};
