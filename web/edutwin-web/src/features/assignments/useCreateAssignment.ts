import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createAssignment } from '../../api/assignmentsApi';
import type { CreateAssignmentRequest } from '../../types/assignments';

export const useCreateAssignment = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateAssignmentRequest) => createAssignment(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['assignments'] });
    },
  });
};
