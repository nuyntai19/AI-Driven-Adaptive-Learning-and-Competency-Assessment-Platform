import React from "react";
import { useQuery } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../../api/adaptivePracticeApi";

interface Props {
  classId?: string;
  subjectId?: string;
}

export const TeacherAdaptivePracticeSection: React.FC<Props> = ({ classId, subjectId }) => {
  const sessionsQuery = useQuery({
    queryKey: ["teacherAdaptivePracticeSessions", classId, subjectId],
    queryFn: () => adaptivePracticeApi.getTeacherSessions(classId, subjectId, 15),
  });
  const sessions = sessionsQuery.data || [];

  const alertsQuery = useQuery({
    queryKey: ["teacherAdaptivePracticeAlerts", classId],
    queryFn: () => adaptivePracticeApi.getTeacherAlerts(classId),
  });
  const alerts = alertsQuery.data || [];

  return (
    <div className="space-y-6">
      {/* Intervention Alerts Card */}
      {alerts.length > 0 && (
        <div className="p-5 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 space-y-3">
          <div className="flex items-center gap-2">
            <span className="text-amber-600 dark:text-amber-400 font-bold text-sm">⚠</span>
            <h4 className="text-sm font-bold text-amber-900 dark:text-amber-200">
              Cảnh báo học sinh cần giáo viên can thiệp ({alerts.length})
            </h4>
          </div>
          <p className="text-xs text-amber-800/90 dark:text-amber-300">
            Các học sinh dưới đây gặp khó khăn lặp lại ở kiến thức nền trong các phiên luyện tập thích ứng gần đây.
          </p>
          <div className="space-y-2 pt-1">
            {alerts.map((al) => (
              <div
                key={al.alertId}
                className="p-3 rounded-xl bg-white dark:bg-slate-900 border border-amber-200 dark:border-amber-800/80 flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs"
              >
                <div>
                  <strong className="text-slate-900 dark:text-white">{al.studentName}</strong>: {al.description}
                  <div className="text-[11px] text-slate-400 mt-0.5">
                    Chủ đề: {al.topicName} ({al.subjectName}) · {new Date(al.createdAt).toLocaleDateString("vi-VN")}
                  </div>
                </div>
                <span className="px-2.5 py-1 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 shrink-0">
                  Cần giao bài củng cố
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Practice Sessions Log */}
      <div className="th-card p-5 space-y-4">
        <div className="flex items-center justify-between pb-2 border-b border-slate-100 dark:border-slate-800">
          <div>
            <h4 className="text-sm font-bold text-slate-900 dark:text-white">
              Nhật ký phiên luyện tập thích ứng của học sinh
            </h4>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Theo dõi kết quả, số câu thực hiện và mức độ đạt mục tiêu của từng học sinh
            </p>
          </div>
        </div>

        {sessionsQuery.isLoading ? (
          <div className="py-8 text-center text-xs text-slate-400">Đang tải dữ liệu phiên...</div>
        ) : sessions.length === 0 ? (
          <div className="py-8 text-center text-xs text-slate-400">Chưa có phiên luyện tập nào được ghi nhận.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-100 dark:border-slate-800 text-slate-400 font-semibold uppercase text-[10px]">
                <tr>
                  <th className="py-2.5 px-3">Học sinh</th>
                  <th className="py-2.5 px-3">Chủ đề</th>
                  <th className="py-2.5 px-3">Số câu làm</th>
                  <th className="py-2.5 px-3">Đạt mục tiêu</th>
                  <th className="py-2.5 px-3">Trạng thái kết thúc</th>
                  <th className="py-2.5 px-3">Thời điểm</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {sessions.map((s) => (
                  <tr key={s.sessionId} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/40">
                    <td className="py-2.5 px-3 font-semibold text-slate-900 dark:text-slate-100">
                      {s.studentName}
                    </td>
                    <td className="py-2.5 px-3 text-slate-700 dark:text-slate-300">
                      <div>{s.topicName}</div>
                      <div className="text-[10px] text-slate-400">{s.subjectName}</div>
                    </td>
                    <td className="py-2.5 px-3 font-medium">
                      {s.correctCount}/{s.totalQuestions} đúng
                    </td>
                    <td className="py-2.5 px-3">
                      <span
                        className={`inline-flex px-2 py-0.5 rounded text-[10px] font-bold ${
                          s.goalAchievementStatus === "Achieved"
                            ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300"
                            : s.requiresIntervention
                            ? "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300"
                            : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {s.goalAchievementStatus === "Achieved"
                          ? "Đạt mục tiêu"
                          : s.requiresIntervention
                          ? "Cần bổ trợ"
                          : "Hoàn thành"}
                      </span>
                    </td>
                    <td className="py-2.5 px-3 text-slate-500 text-[11px]">
                      {s.closeReason === "GoalAchievedWithEvidence"
                        ? "Đủ bằng chứng"
                        : s.closeReason === "RemediationNeeded"
                        ? "Khó khăn lặp lại"
                        : s.closeReason === "QuestionLimitReached"
                        ? "Hết số câu"
                        : "Học sinh dừng"}
                    </td>
                    <td className="py-2.5 px-3 text-slate-400 text-[11px]">
                      {s.completedAt ? new Date(s.completedAt).toLocaleDateString("vi-VN") : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
