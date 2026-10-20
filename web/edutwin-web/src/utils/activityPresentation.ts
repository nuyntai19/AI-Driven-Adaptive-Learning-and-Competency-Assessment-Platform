export const activitySourceLabels: Record<string, string> = { Audit: "Nhật ký thay đổi", Assessment: "Nộp bài và phân tích", TeacherReview: "Giáo viên đánh giá", ReviewRequest: "Yêu cầu xem xét", Curriculum: "Giáo trình của lớp", Business: "Cập nhật hệ thống" };
export const activityActionLabels: Record<string, string> = {
  AssignmentResultApproved: "Chốt kết quả bài tập", AssignmentResultReopened: "Mở lại kết quả để xem xét", AssignmentQuestionVoided: "Loại câu lỗi khỏi bài tập",
  ClassArchived: "Lưu trữ lớp học", ClassReopened: "Mở lại lớp học", ClassUpdated: "Cập nhật lớp học", ClassTeacherChanged: "Đổi giáo viên phụ trách", ClassWorkHandover: "Tiếp nhận công việc của lớp",
  ClassMemberJoined: "Thêm học sinh vào lớp", ClassMemberRemoved: "Học sinh rời lớp", Approved: "Duyệt điểm một câu", Adjusted: "Điều chỉnh điểm một câu",
  ReviewRequestCreated: "Gửi yêu cầu xem xét bài làm", ReviewRequestResolved: "Giải quyết yêu cầu xem xét", ReviewRequested: "Yêu cầu xem xét bài làm", QuestionReported: "Báo câu hỏi có vấn đề",
  CurriculumApplied: "Áp dụng giáo trình cho lớp", CurriculumApplicationEnded: "Ngừng áp dụng giáo trình", CurriculumArchived: "Lưu trữ giáo trình", AssignmentCreated: "Tạo bài tập", CurriculumCreated: "Tạo giáo trình",
  AssignmentPublished: "Giao bài tập", AssignmentClosed: "Đóng bài tập", AssignmentUpdated: "Cập nhật bài tập", AssignmentSubmitted: "Nhận bài nộp", AttemptSubmitted: "Nộp câu trả lời", AIAnalysisCompleted: "Hoàn tất phân tích câu trả lời", AssignmentAiReady: "AI đã xử lý toàn bài", AIAnalysisSummary: "Cập nhật kết quả phân tích",
  AccountAccessChanged: "Cập nhật quyền truy cập tài khoản", AdministrativeActivity: "Ghi nhận thay đổi quản trị", NotificationReplyReceived: "Nhận phản hồi riêng cho thông báo", ManualNotice: "Gửi thông báo", SystemNotice: "Thông báo hệ thống", NotificationDispatchRedriven: "Thử gửi lại thông báo lỗi",
  TeacherDeleted: "Ngừng sử dụng tài khoản giáo viên", StudentDeleted: "Ngừng sử dụng tài khoản học sinh", TeacherPasswordReset: "Đặt lại mật khẩu giáo viên", StudentPasswordReset: "Đặt lại mật khẩu học sinh", UserStatusChanged: "Đổi trạng thái tài khoản",
  CenterCreated: "Tạo trung tâm", CenterStatusUpdated: "Đổi trạng thái trung tâm", CenterMetadataUpdated: "Cập nhật thông tin trung tâm", CenterManagerPasswordReset: "Đặt lại mật khẩu quản lý", CenterManagerCreated: "Tạo tài khoản quản lý", CenterPrimaryManagerChanged: "Đổi quản lý chính", PlatformAdminPasswordChanged: "Đổi mật khẩu quản trị", PlatformAdminSessionsRevoked: "Thu hồi phiên quản trị",
};
const descriptions: Record<string, string> = {
  AssignmentSubmitted: "Học sinh đã nộp toàn bài. Phân tích và kết quả giáo viên duyệt được ghi nhận riêng.",
  AttemptSubmitted: "Câu trả lời đã được lưu vào hệ thống, gồm cả câu chủ động bỏ qua nếu có.",
  AIAnalysisCompleted: "Đã lưu kết quả phân tích cho câu trả lời. Đây chưa phải xác nhận điểm cuối của giáo viên.",
  AssignmentAiReady: "Các câu trong bài đã được xử lý. Xem bài làm để biết kết quả phân tích và trạng thái giáo viên duyệt.",
  AssignmentResultApproved: "Giáo viên đã xác nhận kết quả cuối của bài tập.", AssignmentResultReopened: "Kết quả được mở lại để kiểm tra; chưa được coi là điểm đã chốt.",
  Approved: "Giáo viên đã duyệt kết quả đánh giá câu hỏi.", Adjusted: "Giáo viên đã điều chỉnh kết quả đánh giá câu hỏi.",
  NotificationReplyReceived: "Người nhận đã gửi một phản hồi riêng. Nội dung chỉ có trong thông báo đã gửi, không công khai trong nhật ký.",
  ClassArchived: "Lớp chuyển sang chỉ xem lịch sử; không tiếp tục học hoặc giao bài mới.", ClassReopened: "Lớp được mở lại. Các thao tác vẫn tuân theo quyền và phạm vi hiện tại.",
  CurriculumApplied: "Giáo trình được gán cho lớp; học sinh không tự thay đổi giáo trình.", CurriculumApplicationEnded: "Giáo trình không còn được áp dụng; lịch sử học tập được giữ nguyên.",
  AssignmentQuestionVoided: "Câu lỗi được loại khỏi phạm vi tính điểm của bài tập này, không xóa bài làm cũ.",
  ReviewRequestCreated: "Một yêu cầu xem xét đã được ghi nhận, chưa đồng nghĩa đã được giải quyết.", ReviewRequestResolved: "Giáo viên đã xử lý yêu cầu xem xét.",
  NotificationDispatchRedriven: "Một lượt phân phối lỗi được đưa vào hàng chờ thử lại; không xóa lịch sử gửi.",
};
export function activityTitle(action: string): string { return activityActionLabels[action] ?? "Cập nhật được ghi nhận"; }
export function activityDescription(action: string): string { return descriptions[action] ?? "Thay đổi này được ghi nhận từ nguồn lịch sử thật. Xem chi tiết trong phạm vi được cấp quyền."; }
export function activityTargetLabel(type: string): string {
  return ({ StudentAssignmentResult: "Kết quả bài tập", Attempt: "Bài làm của học sinh", Class: "Lớp học", Curriculum: "Giáo trình", KnowledgeNode: "Chủ đề kiến thức", KnowledgeEdge: "Liên kết kiến thức", AdministrativeMetadata: "Thông báo hoặc cập nhật hệ thống", NotificationDispatch: "Lượt phân phối thông báo", User: "Tài khoản", Teacher: "Giáo viên", Student: "Học sinh", CenterManager: "Quản lý trung tâm", Center: "Trung tâm", AuthorizationRole: "Vai trò", UserRoleAssignment: "Phân quyền tài khoản" } as Record<string, string>)[type] ?? "Đối tượng trong phạm vi của bạn";
}
export function activityStatus(value: string | null): string {
  if (!value) return "Chưa ghi nhận";
  return ({ Pending: "Chờ xác nhận", Approved: "Đã chốt", Applied: "Đang áp dụng", Ended: "Đã ngừng áp dụng", Resolved: "Đã giải quyết", Dismissed: "Không chấp nhận yêu cầu", DeadLetter: "Gửi lỗi, cần kiểm tra", Completed: "Hoàn tất", Active: "Đang hoạt động", Archived: "Đã lưu trữ" } as Record<string, string>)[value] ?? (/^\d+(?:\.\d+)? \/ 10 \(điểm câu\)$/.test(value) ? value : "Đã cập nhật");
}
