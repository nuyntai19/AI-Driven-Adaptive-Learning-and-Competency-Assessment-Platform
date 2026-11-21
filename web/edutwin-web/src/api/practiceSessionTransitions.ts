import type { QueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "./adaptivePracticeApi";

/** Keep the cached session and question lifecycle in step with the server. */
export async function transitionPracticeSession(client: QueryClient, sessionId: string, action: "pause" | "resume") {
  const sessionKey = ["adaptive-practice", "session", sessionId];
  const questionKey = ["adaptive-practice", "question", sessionId];
  // A question requested before pause/resume can finish afterwards with an empty
  // response. Cancel it before the transition so it cannot overwrite the current question.
  await Promise.all([
    client.cancelQueries({ queryKey: sessionKey, exact: true }),
    client.cancelQueries({ queryKey: questionKey, exact: true }),
  ]);
  const session = await (action === "pause"
    ? adaptivePracticeApi.pauseSession(sessionId)
    : adaptivePracticeApi.resumeSession(sessionId));
  // Invalidate while fetching is disabled by the caller. Even a cached null must
  // be fetched again when the confirmed InProgress state enables the question.
  await client.invalidateQueries({ queryKey: questionKey, exact: true, refetchType: "none" });
  client.setQueryData(sessionKey, session);
  void client.invalidateQueries({ queryKey: ["adaptive-practice", "active-session"] });
  return session;
}
