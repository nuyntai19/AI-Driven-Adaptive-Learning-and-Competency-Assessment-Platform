# Class lifecycle and authoritative reports — active implementation

Implementation and localhost migration completed. Backend 3961 pass/0 fail/77 skipped; frontend 596 pass/0 fail; dedicated SQL run 10/10 pass; protected 19-table fingerprint unchanged before manager re-login. Teacher Math and Center Manager Chrome responsive/score/audit/form proof captured; no real class archived. After manager login, only one user's last_login_at/updated_at/row_version changed; other 18 protected tables unchanged. No push. See docs/verification/CLASS-LIFECYCLE-AND-AUTHORITATIVE-REPORTS-2026-10-09.md for exact files, decisions, and two additional UX/metric-definition observations requiring user choice.

User approved on 09/10/2026: manager-driven archive/reopen with reason/audit, correction of automatic History classification, real server grades/risk/report completeness. No push requested. Preserve all assignments/attempts/grades/Twin. Do not archive a real class on behalf of a logged-out manager or pretend migration was a human action.

Plan:

1. Reuse nullable-actor AuthorizationAuditLog. UpdateClass status transition needs LifecycleReason; Status Active/Archived is authoritative and LearningScope Current/History follows it. Audit before/after in the same transaction. Archive pauses new work but retains curriculum configurations and read/grading access. Reopening cannot resurrect ended curriculum applications.
2. New forward migration synchronizes inconsistent scope from existing Status (not missing GradeLevel), writes System/Migration audit with null actor, and adds canonical-state check. Do not rewrite applied migration. Archive/reopen UI + paged class history.
3. Uniform Current scope guard on student assignment list/badge; assignment create/publish guard agrees with lifecycle.
4. Bulk class report server using the same deterministic assignment-score calculator as students; no completion-as-score, no made-up score when data pending, no risk without assessable evidence. Target-specific records, no silent first-page truncation or swallowed progress failures. Pagination for viewing, complete authorized snapshot for exports.
5. Unit/frontend/live MySQL tests (throwaway DB), private backup/fingerprints before deployment, UI proof, exact changed-file report compared with baseline of this turn. No provider calls needed.

Current decisions:

- Keep existing ClassStatus and LearningScope, canonical pair Active+Current or Archived+History.
- Archive class does not end/edit teacher-owned curriculum ledger: class state makes it dormant. A still-Published/unended configuration may resume only upon explicit manager reopen; ended/archived curriculum remains ended. Display paused-by-class clearly.
- Use existing audit entity (ActorUserId is nullable, append-only) instead of adding a new general activity subsystem. Null actor + explicit migration source is not a manager action.
- Legacy unknown-grade classes remain Active until an actual lifecycle action; never infer History from grade-null.
- Grade statistics use final approved scores; provisional scores can appear as provisional in records, not counted as final class/student averages or score-based risk.

Safety: dirty worktree from many prior tasks. functions store key lifecycleTurnBaseline has path/hash baseline. Existing private academic backup and audit reports are historical references, not new migration proof. Current localhost has 10 classes: 6 Active/Current at A and 4 Active/History (2 A legacy, 2 B). 204 attempts/analyses must stay unchanged. Teacher Math currently logged in on Chrome; manager UI testing may require user login.
