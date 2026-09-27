import React from "react";
import { Navigate } from "react-router-dom";
import { AssignmentGradingWorkspace } from "../components/reviews/AssignmentGradingWorkspace";
import { CenterManagerThemeScope } from "../components/centerManager/CenterManagerThemeScope";
import { useAuthStore } from "../stores/authStore";
import { resolveReviewQueueViewMode } from "../utils/reviewQueueHelpers";

/**
 * CenterManagerReviewQueueView:
 * Giao diện hàng đợi duyệt bài dành riêng cho Quản lý Trung tâm (/quan-ly/duyet-bai).
 * Lọc bài tập theo ngày và theo lớp, xem học sinh đã làm bài và từng câu hỏi.
 * QUYỀN CHẤM BÀI ĐÃ BỊ LOẠI BỎ CHO MANAGER (chỉ được xem và giám sát, không có nút/form chấm điểm).
 */
export const CenterManagerReviewQueueView: React.FC = () => {
  return (
    <CenterManagerThemeScope>
      <AssignmentGradingWorkspace actor="CenterManager" />
    </CenterManagerThemeScope>
  );
};

/**
 * TeacherReviewQueueLegacyView:
 * Fallback view cho Teacher nếu truy cập đường dẫn /quan-ly/duyet-bai.
 */
export const TeacherReviewQueueLegacyView: React.FC = () => {
  return <AssignmentGradingWorkspace actor="Teacher" />;
};

/**
 * ReviewQueuePage:
 * Canonical export với Actor Isolation nghiêm ngặt:
 * - Giáo viên (Teacher) tự động redirect sang /giao-vien/cham-bai.
 * - Quản lý trung tâm (CenterManager) hiển thị CenterManagerReviewQueueView với chế độ giám sát.
 */
export const ReviewQueuePage: React.FC = () => {
  const user = useAuthStore((state) => state.user);

  if (user?.accountType === "Teacher") {
    return <Navigate to="/giao-vien/cham-bai" replace />;
  }

  const mode = resolveReviewQueueViewMode(user?.accountType);
  return mode === "CenterManager" ? (
    <CenterManagerReviewQueueView />
  ) : (
    <TeacherReviewQueueLegacyView />
  );
};

export default ReviewQueuePage;
