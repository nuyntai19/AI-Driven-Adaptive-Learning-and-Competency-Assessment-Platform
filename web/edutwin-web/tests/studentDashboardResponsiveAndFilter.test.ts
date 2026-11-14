import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const layoutSource = readFileSync(new URL("../src/layouts/StudentLayout.tsx", import.meta.url), "utf8");
const dashboardSource = readFileSync(new URL("../src/pages/StudentDashboardPage.tsx", import.meta.url), "utf8");
const apiSource = readFileSync(new URL("../src/api/dashboardsApi.ts", import.meta.url), "utf8");
const cssSource = readFileSync(new URL("../src/index.css", import.meta.url), "utf8");

test("Phase 11: No overflow-x: hidden hack on body or html", () => {
  assert.doesNotMatch(cssSource, /body\s*\{[^}]*overflow-x:\s*hidden/i);
  assert.doesNotMatch(cssSource, /html\s*\{[^}]*overflow-x:\s*hidden/i);
});

test("Phase 11: StudentLayout header and main container responsive constraints", () => {
  // Main must have min-w-0 to prevent flex blowout
  assert.match(layoutSource, /<main className="flex-1 w-full min-w-0 pb-12">/);
  // Larger student workspace still has a bounded responsive container.
  assert.match(layoutSource, /max-w-\[1440px\] mx-auto px-4 sm:px-6 lg:px-8/);
  // Header row must have min-w-0; only this slim row stays sticky.
  assert.match(layoutSource, /flex items-center justify-between h-16 gap-3 sm:gap-4 min-w-0/);
  // Left side must have min-w-0 and not rigid shrink-0
  assert.match(layoutSource, /<div className="flex items-center gap-3 xl:gap-5 min-w-0">/);
  // Compact pill navigation (fixed 15px labels) fits from 1024px; below that the phone tab bar takes over.
  assert.match(layoutSource, /<nav className="hidden lg:flex items-center gap-1 min-w-0"/);
});

const radarChartSource = readFileSync(new URL("../src/components/student/StudentRadarChart.tsx", import.meta.url), "utf8");
const dashboardAndRadarSource = dashboardSource + "\n" + radarChartSource;

test("Phase 11: StudentDashboardPage layout, stat cards, radar and progress responsiveness", () => {
  assert.match(dashboardSource, /w-full max-w-\[1440px\] mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-4 min-w-0/);
  // Bento rows stack to one column below lg and use a 12-column grid above it.
  assert.match(dashboardSource, /grid grid-cols-1 lg:grid-cols-12 gap-4/);
  // Chart wrappers must have min-w-0
  assert.match(dashboardAndRadarSource, /h-72 w-full min-w-0 relative/);
  // ResponsiveContainer must specify minWidth={0}
  assert.match(dashboardAndRadarSource, /<ResponsiveContainer width="100%" height="100%" minWidth=\{0\}>/);
  // RadarChart must have appropriate outerRadius so labels do not overflow
  assert.match(dashboardAndRadarSource, /outerRadius="60%"/);
  assert.match(radarChartSource, /assessedGroups\.length >= 3 && assessedGroups\.length <= 8/);
});

test("Phase 12: Subject filter only offers the student's subjects and has no 'Toàn bộ'", () => {
  assert.doesNotMatch(layoutSource, /Toàn bộ<\/option>/);
  assert.match(layoutSource, /useStudentSubjects\(isHistory\)/);
  assert.match(layoutSource, /resolveStudentSubjectId\(subjects, activeSubjectId/);
});

test("Phase 12: StudentDashboardPage supports 'Toàn bộ' and Option A subject-level radar", () => {
  // Does not block page with SubjectRequiredState
  assert.doesNotMatch(dashboardSource, /SubjectRequiredState/);
  // Query executes with selectedSubjectId || undefined
  assert.match(dashboardSource, /queryKey:\s*\["studentDashboard",\s*selectedSubjectId\s*\|\|\s*"all", selectedClassId, isHistory\]/);
  assert.match(dashboardSource, /getStudentDashboard\(selectedSubjectId\s*\|\|\s*undefined, selectedClassId \|\| undefined, isHistory\)/);
  // The progress card title switches between subjects (all-subject view) and topics.
  assert.match(dashboardSource, /isAllSubjects \? "Năng lực theo môn học" : "Hành trình chuyên đề"/);
});

test("Phase 12: dashboardsApi passes subjectId only when present", () => {
  assert.match(apiSource, /if \(subjectId\) \{\s*params\.subjectId = subjectId;\s*\}/);
});

test("Phase 13: StudentLayout tab navigation preserves subjectId when set", () => {
  assert.match(layoutSource, /for \(const key of \["subjectId", "classId", "history"\]\)/);
  assert.match(layoutSource, /return p.size \? `\$\{path\}\?\$\{p\}` : path/);
});

test("Phase 14: Responsive 2-tier header architecture prevents item overlap", () => {
  // Every tab keeps a short label and a full title; labels never wrap.
  for (const [short, full] of [["Luyện tập", "Luyện tập thích ứng"], ["Hồ sơ năng lực", "Hồ sơ năng lực (Twin)"], ["Bài tập", "Bài tập của tôi"]]) {
    assert.ok(layoutSource.includes(`label: "${full}", short: "${short}"`), `${full} keeps the short label ${short}`);
  }
  assert.match(layoutSource, /whitespace-nowrap/);
  // Inactive labels collapse to icons below xl; the active tab always shows its label.
  assert.match(layoutSource, /<span className=\{item\.active \? "" : "hidden xl:inline"\}>\{item\.short\}<\/span>/);

  // The separate context row is visible on every width; no duplicate selectors.
  assert.doesNotMatch(layoutSource, /data-testid="student-subject-selector-desktop"/);
  assert.match(layoutSource, /\{!isAccountPage && <div className="pt-2">/);
  assert.match(layoutSource, /data-testid="student-subject-selector-sub"/);

  // No breakpoint gap: desktop nav, hamburger and phone tab bar switch at the same width.
  assert.match(layoutSource, /className="hidden lg:flex items-center/);
  assert.match(layoutSource, /className="lg:hidden p-2 rounded-full/);
  assert.match(layoutSource, /className="lg:hidden fixed inset-x-0 bottom-0/);
});

