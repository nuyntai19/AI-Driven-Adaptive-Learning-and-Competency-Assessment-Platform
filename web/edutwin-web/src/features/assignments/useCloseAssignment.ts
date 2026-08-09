import { useMutation, useQueryClient } from '@tanstack/react-query';
import { closeAssignment } from '../../api/assignmentsApi';
import type { CloseAssignmentRequest } from '../../types/assignments';

export const useCloseAssignment = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: CloseAssignmentRequest }) => closeAssignment(id, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['assignments'] });
      queryClient.invalidateQueries({ queryKey: ['assignments', variables.id] });
    },
  });
};
