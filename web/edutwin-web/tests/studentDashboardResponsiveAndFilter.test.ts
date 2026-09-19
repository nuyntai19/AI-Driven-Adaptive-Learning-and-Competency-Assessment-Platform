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
  // Header container must be responsive max-w-[1600px]
  assert.match(layoutSource, /max-w-\[1600px\] mx-auto px-4 sm:px-6 lg:px-8/);
  // Header row must have min-w-0 and responsive height
  assert.match(layoutSource, /flex items-center justify-between h-16 sm:h-18 lg:h-20 gap-3 sm:gap-4 min-w-0/);
  // Left side must have min-w-0 and not rigid shrink-0
  assert.match(layoutSource, /<div className="flex items-center gap-3 xl:gap-6 min-w-0">/);
  // Nav must be visible on desktop lg (1024px+)
  assert.match(layoutSource, /<nav className="hidden lg:flex items-center gap-1 xl:gap-2 min-w-0">/);
});

test("Phase 11: StudentDashboardPage layout, stat cards, radar and progress responsiveness", () => {
  // Page container must have min-w-0 and max-w-[1600px]
  assert.match(dashboardSource, /w-full max-w-\[1600px\] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0/);
  // Bento KPI cards must use responsive grid with min-w-0
  assert.match(dashboardSource, /grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5/);
  // Radar + Progress section must stack properly on tablet/mobile and 2 cols on xl
  assert.match(dashboardSource, /grid-cols-1 xl:grid-cols-2 gap-6/);
  // Chart wrappers must have min-w-0
  assert.match(dashboardSource, /h-72 w-full min-w-0 mt-4 relative/);
  // ResponsiveContainer must specify minWidth={0}
  assert.match(dashboardSource, /<ResponsiveContainer width="100%" height="100%" minWidth=\{0\}>/);
  // RadarChart must have appropriate outerRadius so labels do not overflow
  assert.match(dashboardSource, /outerRadius="68%"/);
});

test("Phase 12: Subject filter defaults to 'Toàn bộ' and does not auto-select first subject", () => {
  // Does not force subjects[0] in useEffect
  assert.doesNotMatch(layoutSource, /newParams\.set\("subjectId", subjects\[0\]\.subjectId\)/);
  // Desktop and mobile dropdowns must have 'Toàn bộ' as first option with value=""
  assert.match(layoutSource, /<option value="">Toàn bộ<\/option>/);
  // When empty, deletes subjectId query param
  assert.match(layoutSource, /newParams\.delete\("subjectId"\)/);
});

test("Phase 12: StudentDashboardPage supports 'Toàn bộ' and Option A subject-level radar", () => {
  // Does not block page with SubjectRequiredState
  assert.doesNotMatch(dashboardSource, /SubjectRequiredState/);
  // Query executes with selectedSubjectId || undefined
  assert.match(dashboardSource, /queryKey:\s*\["studentDashboard",\s*selectedSubjectId\s*\|\|\s*"all"\]/);
  assert.match(dashboardSource, /getStudentDashboard\(selectedSubjectId\s*\|\|\s*undefined\)/);
  // Radar title switches dynamically
  assert.match(dashboardSource, /isAllSubjects \? "Radar Năng Lực Theo Môn Học" : "Radar Năng Lực Theo Chuyên Đề"/);
  // Hero scope pill switches dynamically
  assert.match(dashboardSource, /isAllSubjects \? "Phạm vi: Toàn bộ môn học" : `Môn: \$\{subject\.subjectName\}`/);
});

test("Phase 12: dashboardsApi passes subjectId only when present", () => {
  assert.match(apiSource, /if \(subjectId\) \{\s*params\.subjectId = subjectId;\s*\}/);
});

test("Phase 13: StudentLayout tab navigation preserves subjectId when set and clean when Toàn bộ", () => {
  assert.match(layoutSource, /const getTabUrl = \(path: string\) => \{\s*return activeSubjectId \? `\$\{path\}\?subjectId=\$\{activeSubjectId\}` : path;\s*\};/);
});

test("Phase 14: Responsive 2-tier header architecture prevents item overlap", () => {
  // Adaptive label for 'Luyện tập thích ứng' (min-[1760px]:inline) / 'Luyện tập' (inline min-[1760px]:hidden)
  assert.match(layoutSource, /<span className="hidden min-\[1760px\]:inline">Luyện tập thích ứng<\/span>/);
  assert.match(layoutSource, /<span className="inline min-\[1760px\]:hidden">Luyện tập<\/span>/);

  // Adaptive label for 'Hồ sơ năng lực (Twin)' / 'Hồ sơ năng lực'
  assert.match(layoutSource, /<span className="hidden xl:inline">Hồ sơ năng lực \(Twin\)<\/span>/);
  assert.match(layoutSource, /<span className="inline xl:hidden">Hồ sơ năng lực<\/span>/);

  // Adaptive label for 'Bài tập của tôi' / 'Bài tập'
  assert.match(layoutSource, /<span className="hidden xl:inline">Bài tập của tôi<\/span>/);
  assert.match(layoutSource, /<span className="inline xl:hidden">Bài tập<\/span>/);

  // Row 1 Subject Selector is only on ultra-wide (>= 1760px)
  assert.match(layoutSource, /<div className="relative hidden min-\[1760px\]:block shrink-0">/);
  assert.match(layoutSource, /data-testid="student-subject-selector-desktop"/);

  // Row 2 Context Sub-bar is active for viewports < 1760px (min-[1760px]:hidden)
  assert.match(layoutSource, /<div className="min-\[1760px\]:hidden border-t border-slate-200\/80 dark:border-slate-800\/80 bg-slate-50\/90 dark:bg-\[#0b1329\]\/90 backdrop-blur-xs py-2">/);
  assert.match(layoutSource, /data-testid="student-subject-selector-sub"/);

  // Zero breakpoint gap: nav visible at lg:flex, hamburger hidden at lg:hidden
  assert.match(layoutSource, /className="hidden lg:flex items-center/);
  assert.match(layoutSource, /className="lg:hidden p-2 rounded-xl/);
});

