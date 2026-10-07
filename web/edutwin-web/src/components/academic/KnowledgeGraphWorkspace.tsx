import React from "react";
import { TeacherKnowledgeGraphView } from "../../pages/teacher/TeacherKnowledgeGraphView";

export interface KnowledgeGraphWorkspaceProps {
  actor?: "teacher" | "center-manager";
  readOnly?: boolean;
  subjectId?: string;
}

/**
 * KnowledgeGraphWorkspace:
 * Unified Knowledge Graph presentation workspace displaying SVG DAG visualization,
 * topic/concept inspection, node/edge creation, and cycle detection.
 */
export const KnowledgeGraphWorkspace: React.FC<KnowledgeGraphWorkspaceProps> = ({
  actor = "teacher",
}) => {
  return (
    <div className="academic-workspace academic-kg-workspace w-full" data-workspace-actor={actor}>
      <TeacherKnowledgeGraphView />
    </div>
  );
};
