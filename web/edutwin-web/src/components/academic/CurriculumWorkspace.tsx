import React from "react";
import { useParams } from "react-router-dom";
import { TeacherCurriculumListView } from "../../pages/teacher/TeacherCurriculumListView";
import { TeacherCurriculumEditorView } from "../../pages/teacher/TeacherCurriculumEditorView";

export interface CurriculumWorkspaceProps {
  actor?: "teacher" | "center-manager";
  mode?: "list" | "editor";
  curriculumId?: string;
  readOnly?: boolean;
}

/**
 * CurriculumWorkspace:
 * Unified Curriculum presentation workspace handling curriculum listing,
 * structuring, syllabus authoring, and topic node bindings.
 */
export const CurriculumWorkspace: React.FC<CurriculumWorkspaceProps> = ({
  actor = "teacher",
  mode,
}) => {
  const { id } = useParams<{ id: string }>();
  const isEditor = mode === "editor" || Boolean(id) || window.location.pathname.endsWith("/tao-moi");

  return (
    <div className="academic-workspace academic-curriculum-workspace w-full" data-workspace-actor={actor}>
      {isEditor ? <TeacherCurriculumEditorView /> : <TeacherCurriculumListView />}
    </div>
  );
};
