import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

const read = (file: string) => fs.readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");

test("the mission player offers the same tools as the assignment player", () => {
  const source = read("components/learningPathV4/MissionPlayerModal.tsx");
  assert.match(source, /<WorkToolbar /);
  assert.match(source, /<SideAssistantWorkspace /);
  assert.match(source, /<RichMathEditor /);
  assert.match(source, /<ScratchpadInlinePanel /);
  assert.match(source, /<BookmarkButton /);
});

test("hard-question marks live on the server, not in the browser", () => {
  const player = read("pages/LearningPlayerPage.tsx");
  assert.doesNotMatch(player, /edutwin-bookmarks/);
  assert.match(player, /useQuestionBookmarks\(\)/);
  assert.match(read("pages/AdaptivePracticeSessionPage.tsx"), /<BookmarkButton /);
});

test("the learning path asks the server for the goal's minutes on first load", () => {
  const page = read("pages/StudentLearningPathPage.tsx");
  assert.match(page, /const \[minutes, setMinutes\] = useState\(0\)/);
  assert.match(page, /extraOptions/);
});

test("a standing plan cannot be set below ten minutes a day", () => {
  assert.match(read("utils/studyTime.ts"), /MIN_GOAL_STUDY_MINUTES = 10/);
  for (const form of ["GoalAndProposalModal", "ProposalCustomizationForm"])
    assert.match(read(`components/learningPathV4/${form}.tsx`), /minValue=\{MIN_GOAL_STUDY_MINUTES\}/);
});

test("goal sliders keep the same 5p–5h scale as today's slider, with the ten-minute floor enforced", () => {
  const slider = read("components/learningPathV4/TimeBudgetSlider.tsx");
  assert.match(slider, /const marks = \[5, 60, 120, 180, 240, 300\]/);
  assert.match(slider, /const floor = Math\.max\(min, minValue \?\? min\)/);
  assert.match(slider, /onClick=\{\(\) => pick\(m\)\}/);
  for (const form of ["GoalAndProposalModal", "ProposalCustomizationForm"])
    assert.doesNotMatch(read(`components/learningPathV4/${form}.tsx`), /<TimeBudgetSlider[^>]* min=/);
});

test("date fields read and type as dd/mm/yyyy in every role's screens", () => {
  for (const file of ["pages/ActivityPage.tsx", "pages/AssignmentEditorPage.tsx", "pages/AuthorizationManagementPage.tsx", "pages/PlatformAuditLogsPage.tsx",
    "pages/teacher/TeacherAssignmentEditorView.tsx", "components/reviews/AssignmentGradingWorkspace.tsx", "components/notifications/NotificationInteractions.tsx",
    "components/learningPathV4/GoalAndProposalModal.tsx", "components/learningPathV4/ProposalCustomizationForm.tsx"]) {
    const source = read(file);
    assert.doesNotMatch(source, /type="date"|type="datetime-local"/, file);
    assert.match(source, /<Date(Time)?Input\s/, file);
  }
});

test("the goal form fills in the suggested deadline and warns about a date that is too close before the preview", () => {
  const goal = read("components/learningPathV4/GoalAndProposalModal.tsx");
  assert.match(goal, /estimateLearningGoal\(/);
  assert.match(goal, /if \(suggestedFill\) setDeadline\(suggestedFill\)/);
  assert.match(goal, /role="alertdialog"/);
  assert.match(goal, /blockedByDeadline \?/);
  assert.match(read("api/learningPathApi.ts"), /\/students\/me\/learning-goals\/estimate/);
});

test("every work box shows the answer that will be graded", () => {
  for (const file of ["pages/LearningPlayerPage.tsx", "pages/AdaptivePracticeSessionPage.tsx", "components/learningPathV4/MissionPlayerModal.tsx"])
    assert.match(read(file), /<GradedAnswerPreview\s/);
});
