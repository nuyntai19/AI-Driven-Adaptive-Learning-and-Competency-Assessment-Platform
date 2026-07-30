import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { questionsApi } from "../../api/questionsApi";
import type { CreateQuestionRequest, UpdateQuestionRequest, ActivateQuestionRequest, ArchiveQuestionRequest, QuestionFilter } from "../../types/questions";

export function useQuestions(filter?: QuestionFilter) {
  return useQuery({
    queryKey: ["questions", filter],
    queryFn: () => questionsApi.getAll(filter),
  });
}

export function useQuestion(id: string) {
  return useQuery({
    queryKey: ["questions", id],
    queryFn: () => questionsApi.getById(id),
    enabled: !!id,
  });
}

export function useCreateQuestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateQuestionRequest) => questionsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
    },
  });
}

export function useUpdateQuestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateQuestionRequest }) => questionsApi.update(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
      queryClient.invalidateQueries({ queryKey: ["questions", variables.id] });
    },
  });
}

export function useActivateQuestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: ActivateQuestionRequest }) => questionsApi.activate(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
      queryClient.invalidateQueries({ queryKey: ["questions", variables.id] });
    },
  });
}

export function useArchiveQuestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: ArchiveQuestionRequest }) => questionsApi.archive(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
      queryClient.invalidateQueries({ queryKey: ["questions", variables.id] });
    },
  });
}

export function useDeleteQuestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => questionsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
    },
  });
}
