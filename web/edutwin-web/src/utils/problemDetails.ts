import axios from "axios";
import type { ProblemDetails } from "../types/auth";

export interface FormattedError {
  status: number | null;
  title: string | null;
  detail: string | null;
  errorCode: string | null;
  traceId: string | null;
  message: string;
}

export function extractProblemDetails(error: unknown): FormattedError {
  const isAxios = axios.isAxiosError(error);
  const anyError = error as any;
  const response = isAxios ? error.response : anyError?.response;

  if (response) {
    const data = response.data as ProblemDetails | undefined;
    const status = typeof response.status === "number" ? response.status : (typeof anyError?.status === "number" ? anyError.status : null);
    const title = typeof data === "object" ? data?.title || null : null;
    const detail = typeof data === "object" ? data?.detail || null : (typeof data === "string" ? data : null);
    const errorCode = typeof data === "object" ? data?.errorCode || null : null;
    const traceId = (typeof data === "object" && data?.traceId) || (response.headers && (response.headers["x-trace-id"] || response.headers?.get?.("x-trace-id"))) || null;

    let message = detail || title || (anyError?.message && !anyError.message.startsWith("Request failed with status code") ? anyError.message : null);
    if (!message) {
      if (status === 409) {
        message = "Xung đột dữ liệu hoặc trạng thái không hợp lệ.";
      } else if (status === 403) {
        message = "Bạn không có quyền thực hiện thao tác này.";
      } else if (status === 404) {
        message = "Không tìm thấy dữ liệu yêu cầu.";
      } else {
        message = "Đã xảy ra lỗi khi xử lý yêu cầu.";
      }
    }
    if (traceId) {
      message = `${message} (Mã theo dõi: ${traceId})`;
    }

    return {
      status,
      title,
      detail,
      errorCode,
      traceId,
      message,
    };
  }

  if (error instanceof Error) {
    let status = typeof anyError?.status === "number" ? anyError.status : null;
    if (status === null) {
      const match = error.message.match(/status code (\d{3})/i);
      if (match) {
        status = Number(match[1]);
      }
    }
    const detail = typeof anyError?.detail === "string" ? anyError.detail : null;
    const title = typeof anyError?.title === "string" ? anyError.title : null;
    const errorCode = typeof anyError?.errorCode === "string" ? anyError.errorCode : null;
    let message = detail || title || error.message;
    if (message.startsWith("Request failed with status code")) {
      message = status === 409 ? "Xung đột dữ liệu hoặc trạng thái không hợp lệ." : "Đã xảy ra lỗi khi gửi yêu cầu.";
    }
    return {
      status,
      title,
      detail,
      errorCode,
      traceId: null,
      message,
    };
  }

  if (anyError && typeof anyError === "object") {
    const status = typeof anyError.status === "number" ? anyError.status : null;
    const detail = typeof anyError.detail === "string" ? anyError.detail : null;
    const title = typeof anyError.title === "string" ? anyError.title : null;
    const errorCode = typeof anyError.errorCode === "string" ? anyError.errorCode : null;
    const message = detail || title || (typeof anyError.message === "string" ? anyError.message : "Unknown error occurred.");
    return {
      status,
      title,
      detail,
      errorCode,
      traceId: null,
      message,
    };
  }

  return {
    status: null,
    title: null,
    detail: null,
    errorCode: null,
    traceId: null,
    message: "Unknown error occurred.",
  };
}

export function isOverrideConflict(error: unknown): boolean {
  const details = extractProblemDetails(error);
  return details.status === 409 || details.errorCode === "OVERRIDE_CONFLICT";
}

export function isConcurrencyConflict(error: unknown): boolean {
  const details = extractProblemDetails(error);
  return details.status === 409 || details.errorCode === "CONCURRENCY_CONFLICT" || details.errorCode === "OVERRIDE_CONFLICT";
}

export function isConcurrencyConflictError(error: any): boolean {
  return error?.response?.status === 409 || error?.status === 409 || isConcurrencyConflict(error);
}

export function isRateLimit(error: unknown): boolean {
  if (axios.isAxiosError(error) && error.response) {
    const status = error.response.status;
    const data = error.response.data as ProblemDetails | undefined;
    return status === 429 || data?.errorCode === "TOO_MANY_REQUESTS";
  }
  return false;
}

export function isForbidden(error: unknown): boolean {
  if (axios.isAxiosError(error) && error.response) {
    const status = error.response.status;
    const data = error.response.data as ProblemDetails | undefined;
    return status === 403 || data?.errorCode === "FORBIDDEN_RESOURCE" || data?.errorCode === "AUTH_FORBIDDEN";
  }
  return false;
}

export function mapSafeOperationalError(
  error: unknown,
  fallback = "Không thể hoàn tất thao tác. Vui lòng thử lại.",
): string {
  const details = extractProblemDetails(error);
  const messages: Record<string, string> = {
    VALIDATION_FAILED: "Dữ liệu nhập vào chưa hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc.",
    RESOURCE_NOT_FOUND: "Không tìm thấy dữ liệu trong phạm vi được phép truy cập.",
    FORBIDDEN_RESOURCE: "Bạn không có quyền thực hiện thao tác này.",
    AUTH_PERMISSION_REQUIRED: "Bạn không có quyền thực hiện thao tác này.",
    CONCURRENCY_CONFLICT: "Dữ liệu đã được thay đổi ở phiên khác. Vui lòng tải lại dữ liệu mới nhất.",
    DUPLICATE_RESOURCE: "Dữ liệu này đã tồn tại. Vui lòng kiểm tra và chọn giá trị khác.",
    INVALID_STATE_TRANSITION: "Không thể thực hiện thao tác ở trạng thái hiện tại.",
    DAG_CYCLE_DETECTED: "Liên kết này sẽ tạo chu trình phụ thuộc nên không thể lưu.",
  };

  let message = (details.errorCode && messages[details.errorCode]) || fallback;
  if (details.status === 401) message = "Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.";
  if (details.status === 403) message = messages.FORBIDDEN_RESOURCE;
  if (details.status === 404) message = messages.RESOURCE_NOT_FOUND;
  if (details.status === 409 && !details.errorCode) message = messages.CONCURRENCY_CONFLICT;
  if (details.status === 429) message = "Bạn thao tác quá nhanh. Vui lòng đợi một lúc rồi thử lại.";
  if (details.traceId) message = `${message} (Mã theo dõi: ${details.traceId})`;
  return message;
}

export function mapSafeLoginError(error: unknown): string {
  if (axios.isAxiosError(error) && error.response) {
    const status = error.response.status;
    const data = error.response.data as ProblemDetails | undefined;
    const code = data?.errorCode;

    if (status === 429 || code === "TOO_MANY_REQUESTS") {
      return "Hệ thống ghi nhận quá nhiều lượt thử. Vui lòng đợi và thử lại sau ít phút.";
    }
    if (code === "AUTH_INVALID_CREDENTIALS" || status === 401) {
      return "Mã trung tâm, tên đăng nhập hoặc mật khẩu không chính xác.";
    }
    if (code === "AUTH_USER_DISABLED") {
      return "Tài khoản hoặc trung tâm hiện không khả dụng.";
    }
    if (status === 403 || code === "FORBIDDEN_RESOURCE") {
      return "Bạn không có quyền truy cập không gian này.";
    }

    const traceId = data?.traceId || (error.response.headers["x-trace-id"] as string) || null;
    if (traceId) {
      return `Không thể hoàn tất đăng nhập. Vui lòng thử lại sau. (Mã hỗ trợ: ${traceId})`;
    }
  }

  return "Không thể hoàn tất đăng nhập. Vui lòng thử lại sau.";
}
