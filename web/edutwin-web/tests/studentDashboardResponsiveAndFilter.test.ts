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
  assert.match(layoutSource, /max-w-\[1800px\] mx-auto px-4 sm:px-6 lg:px-8/);
  // Header row must have min-w-0 and responsive height
  assert.match(layoutSource, /flex items-center justify-between h-16 sm:h-18 lg:h-20 gap-3 sm:gap-4 min-w-0/);
  // Left side must have min-w-0 and not rigid shrink-0
  assert.match(layoutSource, /<div className="flex items-center gap-3 xl:gap-6 min-w-0">/);
  // Browser QA found 1280px too narrow for the larger type and utility controls.
  assert.match(layoutSource, /<nav className="hidden min-\[1440px\]:flex items-center gap-1 xl:gap-2 min-w-0">/);
});

const radarChartSource = readFileSync(new URL("../src/components/student/StudentRadarChart.tsx", import.meta.url), "utf8");
const dashboardAndRadarSource = dashboardSource + "\n" + radarChartSource;

test("Phase 11: StudentDashboardPage layout, stat cards, radar and progress responsiveness", () => {
  assert.match(dashboardSource, /w-full max-w-\[1800px\] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0/);
  // Bento KPI cards must use responsive grid with min-w-0
  assert.match(dashboardSource, /grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5/);
  // Radar + Progress section must stack properly on tablet/mobile and 2 cols on xl
  assert.match(dashboardSource, /grid-cols-1 xl:grid-cols-2 gap-6/);
  // Chart wrappers must have min-w-0
  assert.match(dashboardAndRadarSource, /h-72 w-full min-w-0 mt-4 relative/);
  // ResponsiveContainer must specify minWidth={0}
  assert.match(dashboardAndRadarSource, /<ResponsiveContainer width="100%" height="100%" minWidth=\{0\}>/);
  // RadarChart must have appropriate outerRadius so labels do not overflow
  assert.match(dashboardAndRadarSource, /outerRadius="60%"/);
  assert.match(radarChartSource, /groups.length >= 3 && groups.length <= 8/);
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
  assert.match(dashboardSource, /queryKey:\s*\["studentDashboard",\s*selectedSubjectId\s*\|\|\s*"all", selectedClassId, isHistory\]/);
  assert.match(dashboardSource, /getStudentDashboard\(selectedSubjectId\s*\|\|\s*undefined, selectedClassId \|\| undefined, isHistory\)/);
  // Radar title switches dynamically
  assert.match(dashboardSource, /isAllSubjects \? "Radar Năng Lực Theo Môn Học" : "Radar Năng Lực Theo Chuyên Đề"/);
  // Hero scope pill switches dynamically
  assert.match(dashboardSource, /isAllSubjects \? "Phạm vi: Toàn bộ môn học" : `Môn: \$\{subject\.subjectName\}`/);
});

test("Phase 12: dashboardsApi passes subjectId only when present", () => {
  assert.match(apiSource, /if \(subjectId\) \{\s*params\.subjectId = subjectId;\s*\}/);
});

test("Phase 13: StudentLayout tab navigation preserves subjectId when set and clean when Toàn bộ", () => {
  assert.match(layoutSource, /for \(const key of \["subjectId", "classId", "history"\]\)/);
  assert.match(layoutSource, /return p.size \? `\$\{path\}\?\$\{p\}` : path/);
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

  // The separate context row is visible even on wide displays; no duplicate selectors.
  assert.doesNotMatch(layoutSource, /data-testid="student-subject-selector-desktop"/);
  assert.match(layoutSource, /<div className="border-t border-slate-200\/80 dark:border-slate-800\/80 bg-slate-50\/90 dark:bg-\[#0b1329\]\/90 backdrop-blur-xs py-2">/);
  assert.match(layoutSource, /data-testid="student-subject-selector-sub"/);

  // No breakpoint gap: desktop nav and hamburger switch at the same width.
  assert.match(layoutSource, /className="hidden min-\[1440px\]:flex items-center/);
  assert.match(layoutSource, /className="min-\[1440px\]:hidden p-2 rounded-xl/);
});

