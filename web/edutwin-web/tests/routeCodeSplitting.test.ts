import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appSource = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
const boundarySource = readFileSync(new URL("../src/routes/RouteChunkBoundary.tsx", import.meta.url), "utf8");
const homeSource = readFileSync(new URL("../src/pages/AuthenticatedHomePage.tsx", import.meta.url), "utf8");
const centerDashboardSource = readFileSync(new URL("../src/pages/CenterDashboardPage.tsx", import.meta.url), "utf8");

test("heavy management routes use lazy imports and an accessible suspense fallback", () => {
  for (const page of [
    "AuthorizationManagementPage",
    "KnowledgeGraphPage",
    "CurriculumListPage",
    "CurriculumEditorPage",
    "QuestionBankPage",
    "QuestionEditorPage",
    "AssignmentEditorPage",
    "AssignmentProgressPage",
    "CenterDashboardPage",
  ]) {
    assert.match(appSource, new RegExp(`const ${page} = lazy\\(`));
  }

  assert.match(appSource, /<Suspense fallback={<RouteLoadingFallback \/>}>/);
  assert.match(boundarySource, /role="status"/);
  assert.match(boundarySource, /aria-live="polite"/);
});

test("route chunk failures use a safe retry UI without raw error details", () => {
  assert.match(appSource, /<RouteChunkBoundary>/);
  assert.match(boundarySource, /window\.location\.reload\(\)/);
  assert.doesNotMatch(boundarySource, /this\.state\.error|error\.message|error\.stack/);
});

test("home navigation combines capabilities with account-type boundaries", () => {
  assert.match(homeSource, /user\.accountType === "PlatformAdmin"/);
  assert.match(homeSource, /user\.accountType === "CenterManager" && hasAnyPermission/);
  assert.match(homeSource, /permissions\.centerRead/);
  assert.match(homeSource, /permissions\.platformAuditRead/);
  assert.match(centerDashboardSource, /hasPermission\(permissions\.teachersRead\)/);
  assert.match(centerDashboardSource, /hasPermission\(permissions\.classesRead\)/);
  assert.match(centerDashboardSource, /hasPermission\(permissions\.studentsRead\)/);
});
