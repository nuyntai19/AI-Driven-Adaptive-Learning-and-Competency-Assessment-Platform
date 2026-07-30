export interface Meta {
  traceId: string;
  timestamp: string;
  page?: number;
  pageSize?: number;
  totalItems?: number;
  totalPages?: number;
}

export interface ApiResponse<T> {
  data: T;
  meta: Meta;
}

export interface ApiCollectionResponse<T> {
  data: T[];
  meta: Meta;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}
