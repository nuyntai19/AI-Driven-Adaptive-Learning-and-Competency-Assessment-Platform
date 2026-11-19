import { useQuery } from "@tanstack/react-query";
import { getReconciledStudentAcademicContext } from "../api/dashboardsApi";
import { organizationApi } from "../api/organizationApi";
import { useAuthStore } from "../stores/authStore";
import { studentSubjectsInView } from "./studentSubjectScope";

/** Current study uses active subjects; history also includes discontinued subjects of the student's classes. */
export function useStudentSubjects(history = false) {
  const user = useAuthStore((state) => state.user);
  const subjectsQuery = useQuery({
    queryKey: ["subjects", "student-workspace", user?.centerId, user?.userId, history ? "history" : "active"],
    queryFn: () => organizationApi.listSubjects(history ? undefined : true),
    enabled: !!user,
    staleTime: 5 * 60 * 1000,
  });
  // Without a subject filter the context lists the classes of every subject.
  const contextQuery = useQuery({
    queryKey: ["student-academic-context", user?.centerId, user?.userId, "", "", false],
    queryFn: () => getReconciledStudentAcademicContext("", "", false),
    enabled: !!user,
    staleTime: 30_000,
  });

  return {
    subjects: studentSubjectsInView(subjectsQuery.data?.data ?? [], contextQuery.data?.classes ?? [], history),
    isLoading: subjectsQuery.isLoading || contextQuery.isLoading,
    isError: subjectsQuery.isError || contextQuery.isError,
    refetch: () => Promise.all([subjectsQuery.refetch(), contextQuery.refetch()]),
  };
}
