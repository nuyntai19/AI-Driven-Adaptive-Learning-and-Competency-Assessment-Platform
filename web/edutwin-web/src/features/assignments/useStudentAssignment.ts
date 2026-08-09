import { useQuery } from '@tanstack/react-query';
import { getStudentAssignmentById } from '../../api/assignmentsApi';

export const useStudentAssignment = (id: string | undefined) => {
  return useQuery({
    queryKey: ['student-assignments', id],
    queryFn: () => getStudentAssignmentById(id!),
    enabled: !!id,
  });
};
