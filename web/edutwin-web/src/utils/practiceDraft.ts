export interface PracticeDraft { selectedOption: string; reasoningText: string; }
export function practiceDraftKey(center: string, user: string, session: string, item: string): string {
  return `edutwin:practice-draft:${center}:${user}:${session}:${item}`;
}
export function readPracticeDraft(key: string): PracticeDraft {
  try {
    const value = JSON.parse(sessionStorage.getItem(key) ?? "null");
    if (typeof value?.selectedOption === "string" && typeof value?.reasoningText === "string") return { selectedOption: value.selectedOption, reasoningText: value.reasoningText };
  } catch { /* A corrupt or unavailable draft must not prevent opening the question. */ }
  return { selectedOption: "", reasoningText: "" };
}
export function savePracticeDraft(key: string, draft: PracticeDraft): boolean {
  try { sessionStorage.setItem(key, JSON.stringify(draft)); return true; }
  catch { return false; }
}
