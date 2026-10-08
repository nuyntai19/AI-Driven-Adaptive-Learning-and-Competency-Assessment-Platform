import type { AttemptFeedbackDataDto } from "../types/learning";

export function isAIProcessingPending(data: AttemptFeedbackDataDto): boolean {
  return data.aiProcessing
    ? ["Pending", "Processing"].includes(data.aiProcessing.status)
    : ["PendingAnalysis", "Processing"].includes(data.status);
}

export function getAIProcessingNotice(data: AttemptFeedbackDataDto, scoreOnly = false) {
  const pending = isAIProcessingPending(data);
  if (!pending && data.analysis && !data.analysis.isFallback) return null;
  const saved = "Bài làm của bạn đã được lưu.";
  const reference = !pending && !scoreOnly ? " Đáp án và lời giải giáo viên vẫn hiển thị bên dưới." : "";
  const reason = data.aiProcessing?.reason;
  if (pending) {
    if (reason === "CapacityWait" || reason === "QuotaWait") return {
      title: reason === "QuotaWait" ? "Đang chờ quota AI được khôi phục" : "Đang chờ lượt xử lý của dịch vụ AI",
      message: `${saved} Hệ thống sẽ tự thử lại khi có khả năng xử lý; bạn không cần nộp lại bài.`,
    };
    if (reason === "ResponseInvalid") return {
      title: "AI đang kiểm tra lại kết quả chưa hợp lệ",
      message: `${saved} Hệ thống đang chấm lại riêng câu này, không chấm lại các câu đã thành công.`,
    };
    return { title: "AI đang phân tích câu trả lời", message: `${saved} Kết quả của riêng câu này đang được xử lý.` };
  }
  const title = reason === "ResponseInvalid" ? "Kết quả AI chưa đạt kiểm tra hợp lệ"
    : reason === "Timeout" ? "Dịch vụ AI phản hồi quá thời gian cho phép"
    : reason === "AttachmentUnavailable" ? "Chưa thể đọc ảnh nháp để phân tích"
    : reason === "ProviderUnavailable" ? "Dịch vụ AI chưa thể hoàn tất phân tích"
    : "Chưa có kết quả phân tích AI hợp lệ";
  return { title, message: `${saved} Kết quả dự phòng không phải kết luận của AI. Bạn có thể thử chấm lại nếu nút chấm lại còn được phép sử dụng.${reference}` };
}

export function areSubmittedAnalysesTerminal(questions: readonly {
  isVoided?: boolean;
  attemptStatus?: string | null;
  latestAttempt?: { status: string } | null;
}[]): boolean {
  return questions.length > 0 && questions.every(q => q.isVoided ||
    ["Completed", "NeedsTeacherReview", "AnalysisFailed"].includes(q.latestAttempt?.status ?? q.attemptStatus ?? ""));
}
