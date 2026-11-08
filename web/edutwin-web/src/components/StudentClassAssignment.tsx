import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { initialEnrollmentClasses } from "../utils/studentClassGrades";
import { mapSafeOperationalError } from "../utils/problemDetails";

/**
 * Lets a center manager enroll an existing student in more classes from the
 * edit form. Each add applies at once, like enrolling from the class page.
 * Only same-grade classes are offered: an other-grade exception needs a
 * written reason, which the class page collects.
 */
export function StudentClassAssignment({ studentId, gradeLevel }: { studentId: string; gradeLevel: number }) {
  const queryClient = useQueryClient();
  const detail = useQuery({
    queryKey: ["studentDetail", studentId],
    queryFn: () => organizationApi.getStudent(studentId),
  });
  const activeClasses = useQuery({
    queryKey: ["classes", "active-for-student-create"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 100, status: "Active" }),
  });
  const addToClass = useMutation({
    mutationFn: (classId: string) => organizationApi.addStudentsToClass(classId, { studentIds: [studentId] }),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ["studentDetail", studentId] });
      void queryClient.invalidateQueries({ queryKey: ["students"] });
      void queryClient.invalidateQueries({ queryKey: ["classes"] });
      void queryClient.invalidateQueries({ queryKey: ["classStudents", result.classId] });
      void queryClient.invalidateQueries({ queryKey: ["candidateStudents"] });
    },
  });

  const joined = detail.data?.classes ?? [];
  const joinedIds = new Set(joined.map((item) => item.classId));
  const available = initialEnrollmentClasses(activeClasses.data?.data ?? [], gradeLevel)
    .filter((item) => !joinedIds.has(item.classId));
  const isLoading = detail.isLoading || activeClasses.isLoading;

  return (
    <div className="space-y-2">
      <p className="block text-xs font-medium text-[var(--cm-text-secondary)]">
        Lớp học đang tham gia ({joined.length})
      </p>
      {joined.length === 0 ? (
        <p className="text-xs text-[var(--cm-text-secondary)]">{isLoading ? "Đang tải..." : "Chưa tham gia lớp nào."}</p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {joined.map((item) => (
            <span key={item.classId} className="rounded-lg border border-[var(--cm-border-subtle)] px-2 py-1 text-xs text-[var(--cm-text)]">
              {item.className}
            </span>
          ))}
        </div>
      )}

      <p className="block pt-1 text-xs font-medium text-[var(--cm-text-secondary)]">
        Thêm vào lớp khác (khối {gradeLevel})
      </p>
      {available.length === 0 ? (
        <p className="text-xs text-[var(--cm-text-secondary)]">
          {isLoading ? "Đang tải..." : `Không còn lớp khối ${gradeLevel} nào để thêm. Muốn thêm vào lớp khác khối, hãy vào trang Lớp học.`}
        </p>
      ) : (
        <ul className="max-h-36 divide-y divide-[var(--cm-border-subtle)] overflow-y-auto rounded-xl border border-[var(--cm-border-subtle)]">
          {available.map((item) => (
            <li key={item.classId} className="flex items-center justify-between gap-2 p-2 text-xs text-[var(--cm-text)]">
              <span className="min-w-0 truncate">{item.className} · {item.subject.subjectName}</span>
              <button
                type="button"
                disabled={addToClass.isPending}
                onClick={() => addToClass.mutate(item.classId)}
                className="cm-secondary-button h-7 shrink-0 px-2 text-xs disabled:opacity-50"
              >
                + Thêm
              </button>
            </li>
          ))}
        </ul>
      )}
      {addToClass.isError && (
        <p role="alert" className="text-xs text-rose-400">
          {mapSafeOperationalError(addToClass.error, "Không thể thêm học sinh vào lớp.")}
        </p>
      )}
    </div>
  );
}
