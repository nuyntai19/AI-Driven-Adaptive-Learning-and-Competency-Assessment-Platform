import React from "react";
import { AssignmentGradingWorkspace } from "../reviews/AssignmentGradingWorkspace";

export interface ReviewQueueWorkspaceProps {
  actor?: "teacher" | "center-manager";
  overrideAllowed?: boolean;
}

/**
 * ReviewQueueWorkspace:
 * Unified Review Queue presentation workspace allowing teachers to review student
 * submissions, inspect AI reasoning evaluations, and apply score/concept overrides.
 */
export const ReviewQueueWorkspace: React.FC<ReviewQueueWorkspaceProps> = ({
  actor = "teacher",
}) => {
  const gradingActor = actor === "center-manager" ? "CenterManager" : "Teacher";

  return (
    <div className="academic-workspace academic-review-workspace w-full" data-workspace-actor={actor}>
      <AssignmentGradingWorkspace actor={gradingActor} />
    </div>
  );
};
