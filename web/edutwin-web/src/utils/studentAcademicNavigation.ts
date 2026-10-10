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
  if (/^\/hoc-tap\/bai-tap\/[^/]+\/?$/.test(pathname)) {
    return studentScopeUrl("/hoc-tap/bai-tap", params);
  }
  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
};
