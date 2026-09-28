import { useQuery } from '@tanstack/react-query';
import { getStudentAssignmentById } from '../../api/assignmentsApi';
import { useAuthStore } from '../../stores/authStore';

export const useStudentAssignment = (id: string | undefined) => {
  const currentUser = useAuthStore((state) => state.user);

  return useQuery({
    queryKey: ['student-assignment', currentUser?.centerId, currentUser?.userId, id],
    queryFn: () => getStudentAssignmentById(id!),
    enabled: !!id && !!currentUser,
    staleTime: 0,
    refetchOnMount: 'always',
  });
};
