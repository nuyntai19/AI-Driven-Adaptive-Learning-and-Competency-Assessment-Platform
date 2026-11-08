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
    assert.match(read(`components/learningPathV4/${form}.tsx`), /min=\{MIN_GOAL_STUDY_MINUTES\}/);
});

test("every work box shows the answer that will be graded", () => {
  for (const file of ["pages/LearningPlayerPage.tsx", "pages/AdaptivePracticeSessionPage.tsx", "components/learningPathV4/MissionPlayerModal.tsx"])
    assert.match(read(file), /<GradedAnswerPreview\s/);
});
