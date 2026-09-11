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
