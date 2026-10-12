import React from "react";
import { useParams, useLocation } from "react-router-dom";
import { TeacherAssignmentListView } from "../../pages/teacher/TeacherAssignmentListView";
import { TeacherAssignmentEditorView } from "../../pages/teacher/TeacherAssignmentEditorView";
import { TeacherAssignmentProgressView } from "../../pages/teacher/TeacherAssignmentProgressView";

export interface AssignmentWorkspaceProps {
  actor?: "teacher" | "center-manager";
  mode?: "list" | "editor" | "progress";
  assignmentId?: string;
  readOnly?: boolean;
}

/**
 * AssignmentWorkspace:
 * Unified Assignment presentation workspace handling assignment distribution,
 * question sequencing, progress tracking, and student attempt monitoring.
 */
export const AssignmentWorkspace: React.FC<AssignmentWorkspaceProps> = ({
  actor = "teacher",
  mode,
}) => {
  const { id } = useParams<{ id: string }>();
  const location = useLocation();

  const isProgress = mode === "progress" || location.pathname.endsWith("/tien-do");
  const isEditor = mode === "editor" || location.pathname.endsWith("/tao-moi") || (Boolean(id) && !isProgress);

  return (
    <div className="academic-workspace academic-assignment-workspace w-full" data-workspace-actor={actor}>
      {isProgress ? (
        <TeacherAssignmentProgressView />
      ) : isEditor ? (
        <TeacherAssignmentEditorView />
      ) : (
        <TeacherAssignmentListView />
      )}
    </div>
  );
};
