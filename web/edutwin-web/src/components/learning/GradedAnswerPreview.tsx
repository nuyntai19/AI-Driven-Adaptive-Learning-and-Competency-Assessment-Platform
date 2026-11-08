import { RichMathText } from "../math/RichMathText";
import { isAnswerReadable } from "../../utils/finalAnswer";

const MATH_MODES = new Set(["NumericRational", "MathEquivalent", "Coordinate2D"]);

interface GradedAnswerPreviewProps {
  /** What will actually be submitted as the final answer. */
  answer: string;
  questionType?: string | null;
  evaluationMode?: string | null;
  /** Language subjects grade every line, not only the last one. */
  wholeWork?: boolean;
}

/** Shows the answer picked out of the student's work, so a wrong pick is caught before submitting. */
export function GradedAnswerPreview({ answer, questionType, evaluationMode, wholeWork = false }: GradedAnswerPreviewProps) {
  if (questionType === "Essay" || questionType === "MultipleChoice") return null;
  const value = answer.trim();

  if (!value || evaluationMode === "Manual") {
    return (
      <p className="text-xs text-slate-500 dark:text-slate-400">
        {wholeWork ? "Toàn bộ bài làm được tính là đáp án." : "Dòng cuối cùng của bài làm được tính là đáp án cuối."}
      </p>
    );
  }
  if (!isAnswerReadable(value, evaluationMode)) {
    return (
      <p role="status" className="text-xs font-medium text-amber-700 dark:text-amber-300">
        Chưa nhận ra đáp án. Hãy viết dòng cuối theo dạng “Đáp án: …” và chèn đáp án bằng công thức.
      </p>
    );
  }
  return (
    <p className="text-xs text-slate-600 dark:text-slate-300">
      Đáp án được chấm:{" "}
      <strong className="text-sm font-semibold text-slate-900 tabular-nums dark:text-white">
        <RichMathText className="inline" text={MATH_MODES.has(evaluationMode ?? "") ? `$${value}$` : value} />
      </strong>
    </p>
  );
}
