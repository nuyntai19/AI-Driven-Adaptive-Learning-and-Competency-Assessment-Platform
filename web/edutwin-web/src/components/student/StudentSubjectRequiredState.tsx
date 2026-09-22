import React, { useEffect } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { organizationApi } from "../../api/organizationApi";
import { getSubjectTheme } from "./subjectTheme";

interface StudentSubjectRequiredStateProps {
  onSelect: (subjectId: string) => void;
}

export const StudentSubjectRequiredState: React.FC<StudentSubjectRequiredStateProps> = ({ onSelect }) => {
  const subjectsQuery = useQuery({
    queryKey: ["subjects", "student-workspace", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const subjects = subjectsQuery.data?.data || [];

  useEffect(() => {
    if (subjects.length === 0) return;

    try {
      const savedSubjectId = localStorage.getItem("edutwin_last_subject_id");
      if (savedSubjectId && subjects.some((s) => s.subjectId === savedSubjectId)) {
        onSelect(savedSubjectId);
        return;
      }
    } catch {
      // Ignore storage errors
    }

    if (subjects.length === 1) {
      onSelect(subjects[0].subjectId);
    }
  }, [subjects, onSelect]);

  const handleChooseSubject = (subjectId: string) => {
    try {
      localStorage.setItem("edutwin_last_subject_id", subjectId);
    } catch {
      // Ignore storage errors
    }
    onSelect(subjectId);
  };

  return (
    <div className="min-h-[70vh] flex flex-col items-center justify-center p-4 sm:p-6 lg:p-8 student-shell">
      <div className="max-w-xl w-full mx-auto space-y-6">
        <div className="text-center space-y-2">
          <span className="text-xs font-mono uppercase tracking-wider text-stone-500 dark:text-stone-400">
            Không gian học tập
          </span>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-stone-900 dark:text-stone-100">
            Chọn môn học để tiếp tục
          </h1>
          <p className="text-xs sm:text-sm text-stone-600 dark:text-stone-400 max-w-md mx-auto leading-relaxed">
            Chọn môn học bạn muốn theo dõi để hệ thống hiển thị hồ sơ năng lực và bài luyện tập tương ứng.
          </p>
        </div>

        {subjectsQuery.isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {[1, 2, 3, 4].map((i) => (
              <div
                key={i}
                className="h-24 animate-pulse rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70"
              />
            ))}
          </div>
        ) : subjects.length === 0 ? (
          <div className="rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/80 dark:border-stone-800/80 p-8 text-center shadow-xs">
            <p className="text-sm font-semibold text-stone-800 dark:text-stone-200">
              Chưa có môn học nào khả dụng
            </p>
            <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
              Vui lòng liên hệ giáo viên hoặc quản trị viên trung tâm để được phân công lớp học.
            </p>
            <Link
              to="/hoc-tap/tong-quan"
              className="mt-4 inline-flex items-center text-xs font-semibold text-sky-700 dark:text-sky-400 hover:underline"
            >
              ← Về Tổng quan
            </Link>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {subjects.map((subj) => {
              const theme = getSubjectTheme(subj.subjectName);
              const initials = subj.subjectName.slice(0, 2).toUpperCase();

              return (
                <button
                  key={subj.subjectId}
                  type="button"
                  onClick={() => handleChooseSubject(subj.subjectId)}
                  className="group relative flex items-center gap-3.5 p-4 rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/90 dark:border-stone-800/90 hover:border-stone-400 dark:hover:border-stone-600 shadow-xs hover:shadow-sm text-left transition-all cursor-pointer overflow-hidden"
                >
                  <div
                    className="absolute left-0 top-0 bottom-0 w-1 transition-all group-hover:w-1.5"
                    style={{ backgroundColor: theme.color }}
                  />

                  <div
                    className="w-10 h-10 rounded-lg flex items-center justify-center font-bold text-xs font-mono shrink-0 ml-1"
                    style={{
                      backgroundColor: theme.bg,
                      color: theme.color,
                      border: `1px solid ${theme.border}`,
                    }}
                  >
                    {initials}
                  </div>

                  <div className="min-w-0 flex-1">
                    <h3 className="text-sm font-bold text-stone-900 dark:text-stone-100 group-hover:text-stone-700 dark:group-hover:text-white truncate">
                      {subj.subjectName}
                    </h3>
                    <p className="text-[11px] font-mono text-stone-500 dark:text-stone-400 truncate">
                      Mã: {subj.subjectCode || "N/A"}
                    </p>
                  </div>

                  <span className="text-stone-400 group-hover:text-stone-800 dark:group-hover:text-stone-200 transition-colors text-sm">
                    →
                  </span>
                </button>
              );
            })}
          </div>
        )}

        <div className="text-center pt-2">
          <Link
            to="/hoc-tap/tong-quan"
            className="text-xs text-stone-500 dark:text-stone-400 hover:text-stone-900 dark:hover:text-stone-100 transition-colors"
          >
            ← Về trang tổng quan
          </Link>
        </div>
      </div>
    </div>
  );
};
