import React, { useEffect } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { ThemeToggle } from "./ThemeToggle";

interface SubjectRequiredStateProps {
  onSelect: (subjectId: string) => void;
}

function getSubjectIcon(name: string): { icon: string; bg: string; text: string; border: string } {
  const lower = name.toLowerCase();
  if (lower.includes("toán")) {
    return { icon: "📐", bg: "bg-indigo-50 dark:bg-indigo-950/60", text: "text-indigo-600 dark:text-indigo-400", border: "border-indigo-200 dark:border-indigo-800" };
  }
  if (lower.includes("lý") || lower.includes("vật lí")) {
    return { icon: "⚡", bg: "bg-amber-50 dark:bg-amber-950/60", text: "text-amber-600 dark:text-amber-400", border: "border-amber-200 dark:border-amber-800" };
  }
  if (lower.includes("hóa")) {
    return { icon: "🧪", bg: "bg-emerald-50 dark:bg-emerald-950/60", text: "text-emerald-600 dark:text-emerald-400", border: "border-emerald-200 dark:border-emerald-800" };
  }
  if (lower.includes("sinh")) {
    return { icon: "🧬", bg: "bg-teal-50 dark:bg-teal-950/60", text: "text-teal-600 dark:text-teal-400", border: "border-teal-200 dark:border-teal-800" };
  }
  if (lower.includes("văn") || lower.includes("ngữ văn")) {
    return { icon: "📖", bg: "bg-rose-50 dark:bg-rose-950/60", text: "text-rose-600 dark:text-rose-400", border: "border-rose-200 dark:border-rose-800" };
  }
  if (lower.includes("anh") || lower.includes("tiếng anh")) {
    return { icon: "🌐", bg: "bg-sky-50 dark:bg-sky-950/60", text: "text-sky-600 dark:text-sky-400", border: "border-sky-200 dark:border-sky-800" };
  }
  return { icon: "📚", bg: "bg-violet-50 dark:bg-violet-950/60", text: "text-violet-600 dark:text-violet-400", border: "border-violet-200 dark:border-violet-800" };
}

export const SubjectRequiredState: React.FC<SubjectRequiredStateProps> = ({ onSelect }) => {
  const subjectsQuery = useQuery({
    queryKey: ["subjects", "student-workspace", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const subjects = subjectsQuery.data?.data || [];

  // Smart Auto-selection:
  // If only 1 subject exists or a previously selected subject is found in localStorage, auto-select!
  useEffect(() => {
    if (subjects.length === 0) return;

    try {
      const savedSubjectId = localStorage.getItem("edutwin_last_subject_id");
      if (savedSubjectId && subjects.some((s) => s.subjectId === savedSubjectId)) {
        onSelect(savedSubjectId);
        return;
      }
    } catch {
      // Ignore localStorage errors
    }

    if (subjects.length === 1) {
      onSelect(subjects[0].subjectId);
    }
  }, [subjects, onSelect]);

  const handleChooseSubject = (subjectId: string) => {
    try {
      localStorage.setItem("edutwin_last_subject_id", subjectId);
    } catch {
      // Ignore localStorage errors
    }
    onSelect(subjectId);
  };

  return (
    <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] text-slate-900 dark:text-slate-100 flex flex-col antialiased">
      {/* Top Simple Header */}
      <header className="h-16 px-6 sm:px-10 border-b border-slate-200/80 dark:border-slate-800 bg-white/80 dark:bg-[#0f172a]/80 backdrop-blur-md flex items-center justify-between">
        <Link to="/hoc-tap/tong-quan" className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-600 flex items-center justify-center text-white font-black text-sm shadow-md shadow-indigo-600/30">
            ET
          </div>
          <div>
            <span className="text-base font-black tracking-tight text-slate-900 dark:text-white">EduTwin</span>
            <span className="hidden sm:inline-block ml-2 text-xs font-semibold px-2 py-0.5 rounded-md bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-400 border border-indigo-200/60 dark:border-indigo-800">
              Học Sinh
            </span>
          </div>
        </Link>

        <div className="flex items-center gap-3">
          <ThemeToggle />
          <Link
            to="/hoc-tap/tong-quan"
            className="text-xs sm:text-sm font-bold text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white transition-colors"
          >
            ‹ Về Dashboard
          </Link>
        </div>
      </header>

      {/* Main Selection Body */}
      <main className="flex-1 flex items-center justify-center p-4 sm:p-6 lg:p-8">
        <div className="max-w-2xl w-full mx-auto space-y-6">
          {/* Header Card */}
          <div className="text-center space-y-3">
            <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 border border-indigo-200/80 dark:border-indigo-800">
              <span>✨</span>
              <span>Phiên Luyện Tập Thích Ứng (AI Adaptive)</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-black text-slate-900 dark:text-white tracking-tight">
              Chọn Môn Học Để Tiếp Tục
            </h1>
            <p className="text-sm text-slate-600 dark:text-slate-400 max-w-lg mx-auto font-normal leading-relaxed">
              Vui lòng chọn môn học bạn muốn học tập để hệ thống AI tải đúng ngân hàng câu hỏi và cập nhật Hồ sơ năng lực Digital Twin của bạn.
            </p>
          </div>

          {/* Subjects Grid */}
          {subjectsQuery.isLoading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {[1, 2].map((i) => (
                <div
                  key={i}
                  className="h-28 rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-5 animate-pulse"
                />
              ))}
            </div>
          ) : subjectsQuery.isError ? (
            <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/40 p-6 text-center border border-rose-200 dark:border-rose-800">
              <p className="text-sm font-bold text-rose-700 dark:text-rose-300">
                Không thể tải danh sách môn học. Vui lòng kiểm tra lại kết nối mạng.
              </p>
              <button
                type="button"
                onClick={() => subjectsQuery.refetch()}
                className="mt-3 px-4 py-2 rounded-xl bg-rose-600 hover:bg-rose-500 text-white text-xs font-bold transition-all cursor-pointer"
              >
                Thử lại
              </button>
            </div>
          ) : subjects.length === 0 ? (
            <div className="rounded-2xl bg-amber-50 dark:bg-amber-950/40 p-6 text-center border border-amber-200 dark:border-amber-800">
              <p className="text-sm font-bold text-amber-800 dark:text-amber-300">
                Trung tâm chưa có môn học nào đang mở.
              </p>
              <Link
                to="/hoc-tap/tong-quan"
                className="inline-block mt-3 px-4 py-2 rounded-xl bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold transition-all"
              >
                Quay lại trang chủ
              </Link>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {subjects.map((subject) => {
                const style = getSubjectIcon(subject.subjectName);
                return (
                  <button
                    key={subject.subjectId}
                    type="button"
                    onClick={() => handleChooseSubject(subject.subjectId)}
                    className="group text-left p-5 rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 hover:border-indigo-500/80 dark:hover:border-indigo-500/80 shadow-xs hover:shadow-md hover:shadow-indigo-500/5 transition-all cursor-pointer flex flex-col justify-between gap-4"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <div
                          className={`w-12 h-12 rounded-2xl ${style.bg} ${style.border} border flex items-center justify-center text-2xl shrink-0 group-hover:scale-110 transition-transform`}
                        >
                          {style.icon}
                        </div>
                        <div>
                          <h2 className="text-base font-black text-slate-900 dark:text-white group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors">
                            {subject.subjectName}
                          </h2>
                          <p className="text-xs text-slate-400 font-medium">
                            Mã môn: {subject.subjectCode || "THPT"}
                          </p>
                        </div>
                      </div>
                    </div>

                    <div className="pt-3 border-t border-slate-100 dark:border-slate-800/80 flex items-center justify-between text-xs font-bold text-indigo-600 dark:text-indigo-400">
                      <span>Bắt đầu luyện tập</span>
                      <span className="group-hover:translate-x-1 transition-transform">→</span>
                    </div>
                  </button>
                );
              })}
            </div>
          )}

          {/* Bottom Back Action */}
          <div className="text-center pt-2">
            <Link
              to="/hoc-tap/tong-quan"
              className="inline-flex items-center gap-1.5 text-xs font-bold text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors cursor-pointer"
            >
              <span>← Quay lại Tổng quan học tập</span>
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
};
