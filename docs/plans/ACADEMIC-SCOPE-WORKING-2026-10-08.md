# Academic scope implementation — technical implementation, business revision required

Final status (08–09 October 2026): implemented, tested and deployed to localhost; not committed/pushed. Full backend 3953 passed/76 skipped/0 failed, frontend 590 passed/0 failed, two separate opt-in live MySQL cases passed. The new migration is applied. All 18 protected history table fingerprints are unchanged (204 attempts and 204 analyses preserved). Chrome student01 current/history and grouped charts verified. Three real Math grade curricula remain Draft/unapplied; teacher publication/application is a next user workflow, not an unfinished implementation step.

Post-implementation audit 09 October: do NOT treat the above technical result as business completion. See `docs/verification/CLASS-LIFECYCLE-AUDIT-2026-10-09.md` and `CHANGE-INVENTORY-2026-10-09.md`. Broad GradeLevel-NULL => History backfill affected 4 classes (including 2 at B); actor-driven lifecycle/audit is absent and Active+History guards disagree. User requested review, not another silent data mutation. Current turn fixes only TeacherPageHeader responsive (two frontend source files, one new test file); 592 frontend tests pass. Await user direction for lifecycle correction/actor workflow and reported existing teacher score/risk bugs. No new DB writes or push. Latest 17 protected tables unchanged; users differs only Teacher Math login timestamps/row version after user login.

Authoritative completion report: `docs/verification/STUDENT-ACADEMIC-SCOPE-AND-CURRICULUM-HISTORY-2026-10-08.md`.
The notes below are the earlier chronological work log, not pending work; do not repeat their completed actions or use their old migration/test status.

User authorized implementation of: student subject/class view scope (not curriculum assignment rights), current/history class field,
published applied curriculum topic filtering, bounded grouped Atlas/Radar, one active primary curriculum per class with supplemental
curricula and retained application history, tighter grade exception and actor/tenant/subject guards. Do not push; do not publish the
three real Math draft curricula automatically; preserve previous tests/attempts/scores.

Current status (still in progress, NOT a completion report):

- New model `Class.LearningScope` Current/History and `ClassCurriculumApplication` ledger (primary/supplemental, timestamps, actors,
  grade snapshots, grade exception stamps, separate ChangeReason/EndReason). Generated unique keys enforce one active primary
  and one active class/curriculum binding. FK pairs preserve center; same subject/status/teacher/grade enforced on insert by SQL trigger.
- Migration is `20261008162602_AddAcademicClassScopeAndCurriculumApplications` (not applied to real localhost yet).
  Backfills grade-null old classes to History, imports published/archived existing curriculum links into ledger without deleting any
  original rows. Adds checks on membership grade/exception metadata, triggers for immutable ended history/no delete and same-subject
  curriculum nodes. Keep manual SQL tail when re-scaffolding! Earlier two versions of this un-applied migration were removed;
  no real database was reverted. Designer/Snapshot is current.
- Removed attempted 3-column principal keys (center/class/subject and center/curriculum/subject) because they caused EF change-tracker
  regressions in existing fixtures. Subject equality is now validated by the application insert trigger (`c.subject_id=NEW.subject_id`)
  plus server and by separate curriculum-node trigger. Existing center-safe FK pairs remain.
- `CurriculumApplicationUseCase` GET/PUT endpoints `/curriculums/{id}/applications` manage published curricula in own current same-subject
  classes; replacement/removal requires reason; grade mismatch requires reason+actor/time snapshots. Shared published curricula may
  be applied to own classes without editing source. `AssignCurriculumClassesUseCase` now checks class teacher+scope. Publishing draft
  planned classes creates primary ledger rows in transaction; rejects occupied primary. Archive ends active rows, retains history.
- SeedFactory/SeedDataContainer/runtime seeder create primary ledger rows for fresh six-class sample. Manifest ledger matching is still
  to be reviewed. No existing tenant reseed.
- `StudentAcademicScopeReader`: current class membership versus history; applied published primary+supplemental topic union with
  deduplication. No curriculum => explicit empty state, never whole-subject fallback. Class/subject not belonging to student => 404.
- Student dashboard overload uses scope; preserves canonical Twin data. Topic items now have EvidenceCount/ExamImportance/chapter group.
  Recommendations displayed on dashboard are filtered to allowed topic IDs. All-subject aggregates still need EvidenceCount populated
  (currently zero default, which makes Radar mark unseen even for observed subject scores).
- Read-only `/students/me/academic-context` endpoint. Layout component `StudentAcademicContext` shows Current/History + member classes,
  read-only applied title(s), and synchronizes selected class to URL. Tabs preserve subjectId/classId/history. Dashboard and Twin dashboard
  requests include scope. Assignment list API/query filters optional class/history; legacy callers without flags retain old visibility.
- Atlas wrapper groups by chapter and pages max 6 nodes; long labels wrap max3 lines; full name tooltip/aria; synthetic connections are
  explicitly labelled display order rather than prerequisites. Radar weighted observed group summary, 3–8 fully observed groups only;
  otherwise bars + 'Chưa đánh giá'. New `competencyGroups.ts` and four tests.
- Teacher `CurriculumApplicationPanel` for published curriculum with roles, own classes, replacement/exception reason and history.
  Existing frozen-content text updated; only assignment context is mutable. Review selection behavior when switching roles: current
  usecase treats ClassIds as desired all bindings for that curriculum, so changing one role can inadvertently end another role in other
  own classes; MUST fix to operate on selected role only and role conversion of selected class intentionally.

Tests so far:

- Latest focused backend: 1229 passed,19 skipped,0 failed (1248 total).
- Latest MySQL temporary migration test:1 passed (StudentGradeGuards...), with new triggers and final migration. No production migration.
- Frontend Vite build passed twice (MathLive chunk warning unchanged). Full frontend before test updates:581 passed/3 stale static
  assertions failed; assertions have since been updated and four new tests added; must rerun.
- New backend `AcademicScopeTests`: three tests pass after UTC date fixes (scope excludes old class, no application fallback, replacing
  primary retains old record and actor ownership). DashboardBoundaryUnitTests had 3 old tests expecting global graph; now explicit
  applied-curriculum helper fixtures preserve their actual weighting/order assertions.
- Full backend baseline before this feature:3947 passed/75 skipped. Need run full suite after final changes.

Pending before completion:

1. Finish class-scoped workspace assignment badge to match filtered assignment list; daily streak remains global genuine study days.
   `IGetStudentWorkspaceSummaryUseCase` old signature used by many mocks: add overload/default compatibility; controller query extras
   should call old when omitted. Frontend API/layout query key needs class/history.
2. Fix ApplyAsync role-select semantics as noted, idempotent no-op bumps should be avoided, updated class OCC must include all classes
   whose application ended (including archived classes), preserve actor checks and raw exception handling specificity. Read history
   includes EndReason; UI field names compile.
3. Review parent Class/Curriculum mutation with ledger: DeleteClass should reject any application history, UpdateClass grade already
   blocks any active applications; archiving/deleting curricula cannot silently lose ledger visibility.
4. Recommendation candidate builder still uses old `CurriculumClasses` join and ambiguous subject-wide assignment rule. At minimum
   use current active applications and exclude History classes; avoid resurrecting ended bindings from old plan links. It may be prudent
   to retain subject-wide personal practice only for students with no current class (existing behavior); current class but no curriculum
   should block recommendations. Multiple classes/supplemental curricula may be valid; primary uniqueness is per-class, not per-subject.
   Preserve prerequisite mastery outside curriculum; existing tests `RecommendationCurriculumScopeTests`.
5. Add real MySQL integration test exercising application lifecycle, actor denial, cross-subject, missing exception metadata, unique
   primary and trigger history delete/update refusal, student scope distinction/dedup. A good host file is DashboardMySqlIntegrationTests
   (private helper SeedHierarchyAsync, private throwaway MySqlTestDatabase). Avoid real AI provider calls.
6. Fresh backup + fingerprints for 12 protected tables before applying real migration. `scripts/ops/mysql_admin.cjs` reads .env silently;
   never log secrets. `scripts/ops/split_seed_grade_classes_local.cjs` default read-only fingerprint is available but --apply assumes old
   grade-null classes; do NOT run apply again after feature. New backup under ignored storage/backups, no overwrite of old backup.
7. Build Docker api/web; startup migration then health and verify original columns/hashes of protected history untouched. New ledger and
   classes learning_scope expected changes; original curriculum_classes planning rows should stay intact. Old tests4assignments,
   204attempts/204analyses/2attachments, no new AI requests. Previous grade-split verification doc has hashes.
8. Browser verification with logged student01 (user just confirmed login). Current browserChrome3, tab30999451 at
   `/hoc-tap/tong-quan?subjectId=30000000-0000-0000-0000-000000000003`. Persistent handles `eduBrowser`,`eduTab`,`uiProofFs`.
   Old bundle currently shows81 topics. Skill computer-use fully read this turn (SKILL+guidance+confirmations); browser Cua is preferred.
   After compaction use `cua.rewriteDocumentation()` before further UI actions. On deploy reload; inspect Current empty class (new real
   curricula still draft) and History old3-topic map and old assignments3/3+scores. Save/inspect screenshots and embed final proof.
9. Verification report and concise Vietnamese handoff, clearly explain new class awaits teacher applying published curriculum and no
   student curriculum assignment rights; no push.

Safety: repo is very dirty from earlier authorized fixes; preserve unrelated edits. No active goal. No subagents requested this turn.
All new code edits use apply_patch; .NET/Docker/MySQL needs require_escalated. EF scaffolding via DAL factory uses fake design-only
ConnectionStrings__Default (127.0.0.1 port1) for offline generation. Manual migration tail is also stored in functions session key
academicMigrationTail and path key academicMigrationPath, but actual file is authoritative. No secrets in these artifacts.
