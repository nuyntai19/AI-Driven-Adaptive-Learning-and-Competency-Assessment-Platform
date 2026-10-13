/** Preserve view context only; curriculum assignment is never a student URL option. */
export const studentScopeUrl = (path: string, params: URLSearchParams, extras: { assignmentId?: string } = {}): string => {
  const scope = new URLSearchParams();
  if (extras.assignmentId) scope.set("assignmentId", extras.assignmentId);
  const subject = params.get("subjectId");
  if (subject) {
    scope.set("subjectId", subject);
    const classId = params.get("classId");
    if (classId) scope.set("classId", classId);
  }
  if (params.get("history") === "true") scope.set("history", "true");
  return scope.size ? `${path}?${scope}` : path;
};

/** A detail ID belongs to its original class, not to a newly selected academic scope. */
export const studentScopeChangeUrl = (pathname: string, params: URLSearchParams): string => {
  if (pathname === "/hoc-tap/lo-trinh-hoc-tap" && params.has("pathId")) {
    return studentScopeUrl(pathname, params);
  }
  if (/^\/hoc-tap\/luyen-tap(?:\/[^/]+)?\/?$/.test(pathname) && params.has("sessionId")) {
    return studentScopeUrl("/hoc-tap/luyen-tap", params);
  }
  if (/^\/hoc-tap\/bai-tap\/[^/]+\/?$/.test(pathname)) {
    return studentScopeUrl("/hoc-tap/bai-tap", params);
  }
  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
};

/** Return to the durable plan of the session rather than a newly generated version. */
export const studentLearningPathReturnUrl = (params: URLSearchParams, sessionId?: string | null): string => {
  const url = studentScopeUrl("/hoc-tap/lo-trinh-hoc-tap", params);
  const pathId = sessionId?.slice(0, 36);
  return pathId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(pathId)
    ? `${url}${url.includes("?") ? "&" : "?"}pathId=${encodeURIComponent(pathId)}` : url;
};
