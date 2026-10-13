# Academic review and AI completion verification — 2026-10-06

## Delivered scope

- Optional scored essay rubrics in question authoring and teacher grading. The
  server validates every criterion and calculates the total; history retains the
  criterion definitions and awarded points used for that review.
- Teacher and student question grades use a ten-point display scale. Reasoning
  quality remains a separately labelled 0–100 indicator. Assignment contributions
  and totals follow the existing equal-question-weight policy.
- Stable published question numbering, one primary question-review action,
  high-contrast cancellation, and unsaved-change detection tied to actual edits.
- Final assignment approval has server-side eligibility and concurrency checks.
  Further grading requires explicit reopening with a reason and audit entry.
  Voiding a question requires acknowledgement when finalized results are affected.
- Teacher and student feedback share provenance-aware system/AI labels and show
  the saved AI solution. Legacy canned feedback is not presented as new AI prose.
- Submitted scratchpad evidence uses the authenticated server attachment. Canvas
  paper stays white in both themes, with a lighter grid and uniform viewport fit.
- Private/Shared questions and curricula are scoped to the same center. Sharing
  permits authorized reading/reuse, not editing another teacher's material or
  accessing their class targets. Teacher typography uses the existing design system.

## AI waiting defect and recovery

The observed slow completion was not simply model latency: reading pending
student feedback recorded solution exposure and changed the attempt row version.
The worker discarded its completed response and only recovered after the job lease
expired. Pending student feedback now neither exposes the reference solution nor
mutates the attempt. A teacher reading feedback does not mark student exposure.

The worker accepts only an observational solution-exposure-only row change;
answer, reasoning, assignment, grade and retry changes still invalidate its work.
Regression tests cover repeated feedback reads during an in-flight provider call
and require a single successful analysis, while preserving true-drift rejection.

After 45 seconds the assignment UI leaves the full-screen waiting state and shows
the saved submission read-only, with analysis continuing in the background.
This is a foreground UX bound, not a guarantee that the external AI completes in
45 seconds. Polling and timers are cleaned up on unmount; network recovery does not
require submitting the original answer again.

## Verification

| Check | Result |
| --- | --- |
| BLL test suite, rerun before delivery | 3,775 passed, 0 failed, 62 skipped; 3,837 total |
| Frontend test suite, rerun before delivery | 542 passed, 0 failed, 26 suites |
| ESLint | 0 errors, 31 existing warnings |
| TypeScript and Vite production build | Passed during implementation |
| Docker API release build | Passed, 0 warnings and 0 errors |
| Docker web production build | Passed |
| API and proxied readiness checks after deployment | Healthy |
| Git whitespace check | Passed |

The 62 skipped tests require separately configured live MySQL integration
infrastructure. They are not counted as passes. No fresh full actor/browser E2E
run was performed; the user requested to do UI acceptance themselves.

Migration `20261006140310_AddAcademicSharingAndRubricHistory` adds material
visibility and rubric history. It was applied to the local database with a private
backup retained outside the repository. Only API and web were recreated for the
latest AI waiting fix; MySQL/Adminer and existing submissions were preserved.

## Deliberately outside this delivery

- Notifications, actor announcements and a unified activity-log screen are planned
  in `docs/plans/NOTIFICATIONS-AND-ACTIVITY-LOG-PLAN.md`, not implemented here.
- PDF/Word bulk import is a future feature.
- Real-time online/presence monitoring was excluded at the user's request.
- A teacher-requested, versioned student retake workflow is not implemented here.
- The reported Windows Vietnamese-IME/MathLive interaction is not certified fixed
  by this delivery. Switching to ENG for formula input is the verified workaround;
  normal Vietnamese prose input is a separate editor path.
- Symbolic equivalence is intentionally bounded, not a general computer algebra
  system. Unsupported answers or genuinely uncertain reasoning require teacher
  review rather than automatic credit or an unjustified zero.

Only application source, regression tests, migrations and these planning/
verification documents are included in Git delivery. Local screenshots, sample
SQL, E2E fixtures, maintenance helpers, environment files and database backups are
excluded. Passing checks establishes readiness for this scope, not proof that all
possible UI, model, integration or concurrency defects are absent.
