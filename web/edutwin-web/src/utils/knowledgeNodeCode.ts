import type { KnowledgeNodeType } from "../types/knowledgeGraph";

/**
 * Standard knowledge-node code: [Môn][Khối]_[Loại]_[Tên viết tắt]_[STT]
 *   e.g. "Phương trình bậc hai một ẩn", Topic, Toán (MATH) khối 10 → MATH10_T_PTBH_01
 * The client builds the prefix; the server returns the sequence (max existing + 1 over the whole
 * subject, deleted nodes included) via GET /knowledge/nodes/code-suggestion.
 */

export const nodeCodeSyntax = "[Môn][Khối]_[Loại]_[Tên viết tắt]_[STT]";
export const maxNodeCodeLength = 50;

/** One letter per level; Concept uses K (Khái niệm) so it never clashes with Chapter. */
export const nodeTypeCodeLetter: Record<KnowledgeNodeType, string> = {
  Subject: "M",
  Chapter: "C",
  Topic: "T",
  Skill: "S",
  Concept: "K",
};

export const standardNodeCodePattern = /^[A-Z0-9]+_[CTSKM]_[A-Z0-9]+_\d{2,}$/;

/**
 * Function words dropped from abbreviations. Matched on the accented lowercase word, because
 * unaccented forms collide ("đề" in "Mệnh đề" vs "để", "vẽ" vs "về", "tự" vs "từ").
 */
const stopWords = new Set([
  "và", "của", "các", "những", "với", "trong", "cho", "là", "về", "theo", "từ", "hoặc", "để", "ở", "tại",
  "and", "of", "the", "a", "an", "to", "in", "on", "for", "with", "or",
]);
/** Leading task verbs ("Tìm cực trị…") describe the exercise, not the topic. */
const leadingVerbs = new Set(["tìm", "find"]);
const maxInitials = 4;

const toCodeChars = (word: string) =>
  word.replace(/đ/g, "d").replace(/Đ/g, "D").normalize("NFD").replace(/[̀-ͯ]/g, "").toUpperCase().replace(/[^A-Z0-9]/g, "");

/** Words split on any non-letter/digit run, keeping accents for stop-word matching. */
function nameWords(text: string): string[] {
  return text.normalize("NFC").toLocaleLowerCase("vi").split(/[^\p{L}\p{N}]+/u).filter(Boolean);
}

/** Uppercase ASCII words: diacritics removed, đ → D. */
export function codeWords(text: string): string[] {
  return nameWords(text).map(toCodeChars).filter(Boolean);
}

/**
 * Initials of the first meaningful words (max 4); numbers are kept whole ("Bài 3" → B3).
 * A single-word name keeps up to its first 4 letters ("Logarit" → LOGA).
 */
export function abbreviateNodeName(name: string): string {
  let words = nameWords(name);
  while (words.length > 1 && leadingVerbs.has(words[0])) words = words.slice(1);
  const meaningful = words.filter(w => !stopWords.has(w));
  const used = (meaningful.length ? meaningful : words).map(toCodeChars).filter(Boolean).slice(0, maxInitials);
  if (!used.length) return "";
  if (used.length === 1) return /^\d+$/.test(used[0]) ? used[0] : used[0].slice(0, maxInitials);
  return used.map(w => (/^\d+$/.test(w) ? w : w[0])).join("");
}

/**
 * Standard codes of the programme's four subjects (mirrors SubjectCodeGenerator on the server), so
 * node codes stay uniform even for subjects stored with an older code (ENGLISH, VATLY, HOAHOC, TOAN12).
 */
const standardSubjects: [code: string, aliases: string[]][] = [
  ["MATH", ["TOAN", "TOANHOC", "MATH", "MATHS", "MATHEMATICS"]],
  ["PHYS", ["VATLY", "VATLI", "LY", "LI", "PHYS", "PHYSICS"]],
  ["CHEM", ["HOAHOC", "HOA", "CHEM", "CHEMISTRY"]],
  ["ENG", ["TIENGANH", "ANH", "ANHVAN", "ENG", "ENGLISH"]],
];

function standardCodeOf(text: string): string | null {
  const ascii = codeWords(text).join("");
  let letters = ascii.replace(/\d/g, "");
  const digits = ascii.replace(/\D/g, "");
  if (letters.startsWith("MON") && letters.length > 3) letters = letters.slice(3);
  const match = standardSubjects.find(([, aliases]) => aliases.includes(letters));
  return match ? match[0] + digits : null;
}

/** Subject part of node codes: the standard code (by name, then by stored code) or the stored code itself. */
export function standardSubjectCode(subjectCode: string, subjectName?: string | null): string {
  return (subjectName && standardCodeOf(subjectName)) || standardCodeOf(subjectCode) || codeWords(subjectCode).join("");
}

/** Subject code + grade ("MATH" + 10 → MATH10); a code that already ends with that grade is kept (TOAN12). */
export function subjectGradeSegment(subjectCode: string, gradeLevel?: number | null, subjectName?: string | null): string {
  const code = standardSubjectCode(subjectCode, subjectName);
  if (!code || !gradeLevel) return code;
  const grade = String(gradeLevel);
  const before = code.slice(0, code.length - grade.length);
  return code.endsWith(grade) && before && !/\d$/.test(before) ? code : `${code}${grade}`;
}

/** Prefix without the sequence, or null while the name/subject does not yet give one. */
export function buildNodeCodePrefix(input: { subjectCode: string; subjectName?: string | null; gradeLevel?: number | null; nodeType: KnowledgeNodeType; nodeName: string }): string | null {
  const head = subjectGradeSegment(input.subjectCode, input.gradeLevel, input.subjectName);
  const abbreviation = abbreviateNodeName(input.nodeName);
  if (!head || !abbreviation) return null;
  // Room for "_NN": the server rejects prefixes longer than 47 characters.
  const prefix = `${head}_${nodeTypeCodeLetter[input.nodeType]}_${abbreviation}`;
  return prefix.length <= maxNodeCodeLength - 3 ? prefix : null;
}

export function formatNodeCode(prefix: string, sequence: number): string {
  return `${prefix}_${String(sequence).padStart(2, "0")}`;
}

export function isStandardNodeCode(code: string): boolean {
  return code.length <= maxNodeCodeLength && standardNodeCodePattern.test(code);
}
