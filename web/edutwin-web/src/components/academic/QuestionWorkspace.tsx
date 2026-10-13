import React from "react";
import { useParams } from "react-router-dom";
import { TeacherQuestionBankView } from "../../pages/teacher/TeacherQuestionBankView";
import { TeacherQuestionEditorView } from "../../pages/teacher/TeacherQuestionEditorView";

export interface QuestionWorkspaceProps {
  actor?: "teacher" | "center-manager";
  mode?: "bank" | "editor";
  questionId?: string;
  readOnly?: boolean;
}

/**
 * QuestionWorkspace:
 * Unified Question presentation workspace handling question bank exploration,
 * authoring with RichMathEditor, formula WYSIWYG, and opt-in reasoning toggle.
 */
export const QuestionWorkspace: React.FC<QuestionWorkspaceProps> = ({
  actor = "teacher",
  mode,
}) => {
  const { id } = useParams<{ id: string }>();
  const isEditor = mode === "editor" || Boolean(id) || window.location.pathname.endsWith("/tao-moi");

  return (
    <div className="academic-workspace academic-question-workspace w-full" data-workspace-actor={actor}>
      {isEditor ? <TeacherQuestionEditorView /> : <TeacherQuestionBankView />}
    </div>
  );
};
