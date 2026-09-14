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
  if (axios.isAxiosError(error) && error.response) {
    const data = error.response.data as ProblemDetails | undefined;
    const status = error.response.status;
    const title = data?.title || null;
    const detail = data?.detail || null;
    const errorCode = data?.errorCode || null;
    const traceId = data?.traceId || (error.response.headers["x-trace-id"] as string) || null;

    let message = detail || title || error.message || "An unexpected error occurred.";
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
    return {
      status: null,
      title: null,
      detail: null,
      errorCode: null,
      traceId: null,
      message: error.message,
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
  if (axios.isAxiosError(error) && error.response) {
    const status = error.response.status;
    const data = error.response.data as ProblemDetails | undefined;
    return status === 409 || data?.errorCode === "OVERRIDE_CONFLICT";
  }
  return false;
}

export function isConcurrencyConflict(error: unknown): boolean {
  if (axios.isAxiosError(error) && error.response) {
    const status = error.response.status;
    const data = error.response.data as ProblemDetails | undefined;
    return status === 409 || data?.errorCode === "CONCURRENCY_CONFLICT" || data?.errorCode === "OVERRIDE_CONFLICT";
  }
  return false;
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

