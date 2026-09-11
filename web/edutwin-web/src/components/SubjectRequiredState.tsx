import { useQuery } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";

interface SubjectRequiredStateProps {
  onSelect: (subjectId: string) => void;
}

export const SubjectRequiredState = ({ onSelect }: SubjectRequiredStateProps) => {
  const subjects = useQuery({
    queryKey: ["subjects", "student-workspace", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-xl rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
        <h1 className="text-xl font-bold text-slate-900">Chọn môn học</h1>
        <p className="mt-2 text-sm text-slate-600">
          Dashboard, Hồ sơ Năng lực và phiên luyện tập đều cần một môn học cụ thể.
        </p>
        {subjects.isLoading ? (
          <p className="mt-5 text-sm text-slate-500">Đang tải danh sách môn học...</p>
        ) : subjects.isError ? (
          <div className="mt-5 rounded-lg bg-red-50 p-4 text-sm text-red-700">
            Không thể tải danh sách môn học. Vui lòng kiểm tra quyền truy cập hoặc thử lại.
          </div>
        ) : (subjects.data?.data.length ?? 0) === 0 ? (
          <div className="mt-5 rounded-lg bg-amber-50 p-4 text-sm text-amber-800">
            Trung tâm chưa có môn học đang hoạt động.
          </div>
        ) : (
          <label className="mt-5 block text-sm font-semibold text-slate-800">
            Môn học
            <select
              defaultValue=""
              onChange={(event) => event.target.value && onSelect(event.target.value)}
              className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-slate-900"
            >
              <option value="" disabled>Chọn một môn học</option>
              {subjects.data?.data.map((subject) => (
                <option key={subject.subjectId} value={subject.subjectId}>
                  {subject.subjectName}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>
    </div>
  );
};
