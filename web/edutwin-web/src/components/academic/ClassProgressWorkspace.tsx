import React from "react";
import { TeacherClassDashboardView } from "../../pages/teacher/TeacherClassDashboardView";

export interface ClassProgressWorkspaceProps {
  actor?: "teacher" | "center-manager";
  classId?: string;
  readOnly?: boolean;
}

/**
 * ClassProgressWorkspace:
 * Unified Class Progress presentation workspace showing real-time class metrics,
 * student competency distributions, mastery progress, and risk factor monitoring.
 */
export const ClassProgressWorkspace: React.FC<ClassProgressWorkspaceProps> = ({
  actor = "teacher",
}) => {
  return (
    <div className="academic-workspace academic-class-progress-workspace w-full" data-workspace-actor={actor}>
      <TeacherClassDashboardView />
    </div>
  );
};
