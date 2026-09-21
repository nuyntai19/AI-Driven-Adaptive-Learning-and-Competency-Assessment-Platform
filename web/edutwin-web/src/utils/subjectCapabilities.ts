/**
 * Determines whether a subject has math/STEM tool support (Casio, graph, scratchpad, formula toolbar).
 * Only Toán, Vật lý, Hóa học (and English equivalents: Math, Mathematics, Physics, Chemistry) support these tools.
 * Subjects like English, Literature, History, Geography, etc. do NOT have these tools.
 */
export function supportsMathTools(subjectNameOrCode?: string | null): boolean {
  if (!subjectNameOrCode) return false;
  const normalized = subjectNameOrCode.trim().toLowerCase();
  
  // Vietnamese subject names / normalized terms
  const stemKeywords = [
    'toán',
    'toan',
    'vật lý',
    'vat ly',
    'vật lí',
    'vat li',
    'hóa học',
    'hoa hoc',
    'hóa',
    'hoa',
    'math',
    'physics',
    'chemistry'
  ];

  return stemKeywords.some(keyword => normalized.includes(keyword));
}

/**
 * Determines whether a free-response question is strictly formula-only or numeric (where MathLive is appropriate),
 * as opposed to full solution derivation/explanation (where multiline text + newlines is required).
 */
export function isFormulaOnlyQuestion(answerEvaluationMode?: string | null, _questionText?: string | null): boolean {
  if (!answerEvaluationMode) return false;
  const mode = answerEvaluationMode.trim().toLowerCase();
  if (mode === 'formulaonly' || mode === 'formula_only' || mode === 'numericexact' || mode === 'numericrational' || mode === 'numeric' || mode === 'numeric_only') {
    return true;
  }
  return false;
}
