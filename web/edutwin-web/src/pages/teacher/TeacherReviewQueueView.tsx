import React from "react";
import { AssignmentGradingWorkspace } from "../../components/reviews/AssignmentGradingWorkspace";

/**
 * TeacherReviewQueueView:
 * Giao diện chấm bài của Giáo viên (route: /giao-vien/cham-bai).
 * Lọc bài tập theo ngày và theo lớp, bấm vào bài tập để xem học sinh đã làm,
 * bấm vào học sinh để xem từng câu và tiến hành chấm bài (chấp thuận AI hoặc can thiệp điểm số).
 */
export const TeacherReviewQueueView: React.FC = () => {
  return <AssignmentGradingWorkspace actor="Teacher" />;
};
