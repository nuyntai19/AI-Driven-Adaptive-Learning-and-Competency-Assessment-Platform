import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { curriculumApi } from "../../api/curriculumApi";
import type { CreateCurriculumRequest, UpdateCurriculumRequest, PublishCurriculumRequest, UpdateCurriculumClassesRequest, UpdateCurriculumNodesRequest, ReviewStatus } from "../../types/curriculum";

export function useCurriculums(subjectId?: string, status?: ReviewStatus) {
  return useQuery({
    queryKey: ["curriculums", subjectId, status],
    queryFn: () => curriculumApi.getAll({ subjectId, status }),
  });
}

export function useCurriculum(id: string) {
  return useQuery({
    queryKey: ["curriculums", id],
    queryFn: () => curriculumApi.getById(id),
    enabled: !!id,
  });
}

export function useCreateCurriculum() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateCurriculumRequest) => curriculumApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["curriculums"] });
    },
  });
}

export function useUpdateCurriculum() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateCurriculumRequest }) => curriculumApi.update(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["curriculums"] });
      queryClient.invalidateQueries({ queryKey: ["curriculums", variables.id] });
    },
  });
}

export function usePublishCurriculum() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: PublishCurriculumRequest }) => curriculumApi.publish(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["curriculums"] });
      queryClient.invalidateQueries({ queryKey: ["curriculums", variables.id] });
    },
  });
}

export function useUpdateCurriculumClasses() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateCurriculumClassesRequest }) => curriculumApi.updateClasses(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["curriculums"] });
      queryClient.invalidateQueries({ queryKey: ["curriculums", variables.id] });
    },
  });
}

export function useUpdateCurriculumNodes() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateCurriculumNodesRequest }) => curriculumApi.updateNodes(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["curriculums"] });
      queryClient.invalidateQueries({ queryKey: ["curriculums", variables.id] });
    },
  });
}
