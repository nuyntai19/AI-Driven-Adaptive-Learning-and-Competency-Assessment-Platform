# Actor permissions and knowledge-topic navigation — 2026-10-08

## Scope

Audit the four actors' explicit default permissions, restore appropriate missing system-role grants, and fix knowledge-graph question/assignment quick actions. No curriculum or question content is created by this change; no Git commit or push is requested.

## Authorization outcome

| Actor | Default permission count | Boundary |
| --- | ---: | --- |
| PlatformAdmin | 5 | Platform governance, not tenant academic operations |
| CenterManager | 31 | Center organization, authorization and aggregate oversight; not teacher grading/authoring |
| Teacher | 32 | Scoped teaching and academic authoring, including the six node/edge create/update/delete grants |
| Student | 12 | Own learning records and submissions, no teacher or organization administration |

The missing grants found in the existing EDUTWIN_A and EDUTWIN_B system teacher roles were `knowledge.nodes.create/update/delete` and `knowledge.edges.create/update/delete`. Other appropriate defaults were already present. These are existing catalog permissions, not six new API endpoints.

An additive startup backfill runs even with demo seeding disabled. It only updates existing active system roles in active centers, uses active/actor-compatible default permissions, records authorization audit rows, advances role versions and invalidates affected active role holders' sessions. It does not create/reactivate user-role assignments or edit custom roles. Tenant, actor, ownership and lifecycle guards remain enforced. The management layout now rejects non-CenterManager actors; legacy academic redirects remain outside that boundary.

Real MySQL testing caught this driver's inability to bind a primitive Guid collection in a `Contains` query. The startup backfill now uses scalar role-ID predicates with the user filtering kept in SQL and deduplicates users before changing AuthVersion.

## Topic quick actions

- Graph links carry both `subjectId` and `topicNodeId` and are offered only for an active Topic with the relevant permission.
- Question bank reads URL context, validates the API-visible subject/topic, and applies both filters. Invalid/pending/failed context never displays an unfiltered cached result.
- Assignment creation retains the topic when selecting a class of the same subject, clears it with an explanation for a different subject, and ignores quick-action context in edit mode.
- Quick actions do not select questions or recipients and do not create/publish an assignment. Existing class gap-group links retain their separate recipient-selection workflow.
- Touched list/query caches are scoped by center and user; node IDs remain exact UInt64 strings.

## Verification

- Final backend suite: 3,938 passed, 74 opt-in integration tests skipped, 0 failed.
- Focused authorization/query-filter tests after MySQL compatibility fix: 27 passed.
- Opt-in real-MySQL `WorkspaceSummaryAndNewAccountRoles_RealSql_NoProviderCalls`: 1 passed. Uses a disposable database, verifies new-account grants plus repeated backfill, and makes no provider calls.
- Full frontend suite: 582 passed, 0 failed.
- Frontend production build: passed (existing large MathLive chunk warning).
- Database backup saved under ignored `storage/backups/2026-10-08-before-default-permission-backfill.sql` before the runtime update.
- API/web rebuilt and updated on localhost. Readiness reports Healthy / MySQL Healthy.
- Runtime DB verified: both EDUTWIN_A and EDUTWIN_B have Teacher 32, CenterManager 31, Student 12; PLATFORM has PlatformAdmin 5. Exactly two startup backfill audit records each contain six new teacher grants.
- Chrome Teacher Math verification: node/edge creation buttons and selected-node edit/delete controls are visible after session refresh; question-bank quick action for Math/Mũ–Logarit selects the correct filters and shows only question IDs 10005–10009. Assignment quick action keeps that topic when selecting the Math class and shows the same five questions with zero selected. No assignment was saved or published.
- Chrome actor-boundary verification: Teacher Math navigating to `/quan-ly/hoc-sinh` is redirected to `/khong-co-quyen`. Returned the tab to the Math graph afterward.
- The teacher's available classes do not include a different-subject class, so cross-subject clearing is covered by automated tests rather than a live Chrome class-switch test.

## Separate observation (not changed in this scope)

The existing assignment question-selector cards still show raw maximum scores (10/20/30/40/50) and label ShortAnswer as essay in that list. This is separate from the requested authorization/topic-navigation repair and needs its own display consistency follow-up; it was not changed during this task.

## Curriculum vs knowledge graph

KnowledgeNode is scoped by CenterId/SubjectId and has no GradeLevel field. Curriculum has GradeLevel, and CurriculumNode selects/orders knowledge nodes. The intended organization is one Math graph per center, with separate Math 10, Math 11 and Math 12 curricula selecting from that graph; prerequisite links can span grade boundaries. A curriculum is not automatically a complete textbook.

Existing EDUTWIN_A Math data contains only three sample topics and one ungraded curriculum. This permission/navigation repair does not make the Math 10–12 curriculum complete.
