import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateAssignment } from '../../api/assignmentsApi';
import type { UpdateAssignmentRequest } from '../../types/assignments';

export const useUpdateAssignment = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateAssignmentRequest }) => updateAssignment(id, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['assignments'] });
      queryClient.invalidateQueries({ queryKey: ['assignments', variables.id] });
    },
  });
};
