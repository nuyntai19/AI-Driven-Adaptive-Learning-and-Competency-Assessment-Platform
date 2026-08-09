import { useMutation, useQueryClient } from '@tanstack/react-query';
import { publishAssignment } from '../../api/assignmentsApi';
import type { PublishAssignmentRequest } from '../../types/assignments';

export const usePublishAssignment = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: PublishAssignmentRequest }) => publishAssignment(id, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['assignments'] });
      queryClient.invalidateQueries({ queryKey: ['assignments', variables.id] });
    },
  });
};
