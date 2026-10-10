# AI grading optimization — historical pause and resumed handoff

Saved on 2026-10-07 (Asia/Bangkok) at the user's explicit request to pause while switching Codex accounts. The user explicitly resumed with “oke tiếp tục đi” and “tiếp tục phần còn dang dở đi”. The paused snapshot below is historical; do not repeat completed steps from it. Current implementation/configuration and verification are recorded in [AI-GRADING-THROUGHPUT.md](AI-GRADING-THROUGHPUT.md) and PROJECT_TRACKING section 25.

## Resumed implementation state

Implementation and local rollout are complete. Checkpoint writes are transactionally fenced, database errors are separated from grading failures, chronological evidence commits reuse saved responses, successful commits remove recovery copies, and teacher changes enqueue durable downstream updates. Provider pools coordinate across service instances. Worker configuration is four jobs, with two provider calls for unverified project metadata. Full backend suite: 3,824 passed, 0 failed, 67 integration tests skipped in that run; seven selected real-MySQL cases passed in separate isolated runs. Synthetic scope/throughput tests passed, including 5,000 jobs. EF reports no pending model changes. The sub-microsecond lease comparison was corrected and verified on MySQL.

Docker API was rebuilt and recreated with current source. Readiness passed, startup error count was zero, and SQL is at `20261006173556_AddAIProcessingCoordination`. Verified seven keys without exposing their values; model remains `gemini-2.5-flash`. Before rollout there were no active analysis jobs. The local backup is in `%TEMP%/EduTwin-backups/2026-10-06_before_ai_processing_2ec7cafa0f724a699d404c2391802a31.sql` (SHA256 `8e8a93bef84e22a306fe513a776e40dee887b50acfe3e45dc5b97169a6266b86`). Inventory of users, students, assignments, attempts, evidence and history was unchanged across migration/startup. No real provider stress test, model switch, commit or push was performed. All source changes remain uncommitted; no push is authorized by this resumed implementation request.

## Historical snapshot at the pause — not current status

## Workspace and Git

- Workspace: `D:\AI-Driven Adaptive Learning and Competency Assessment Platform`.
- Branch: `student/answer`.
- HEAD: `1ed89808a1eb85b2ac41a6b167a3e18b7805225f` (`feat(academic): add scoped rubrics and harden review completion`).
- All new work described below is uncommitted. No commit/push, runtime restart, Docker rebuild, database migration, or real provider load test was performed in the optimization implementation turn.
- Preserve the dirty worktree. Do not reset files or delete unrelated scripts/evidence.
- No active shell/build session was outstanding when the pause was saved.

## User-approved objective and non-negotiable semantics

Optimize grading throughput for 40–50/100 questions and many simultaneous student submissions, without sacrificing evidence used for individual learning paths.

1. Multiple-choice and other answered question types still receive AI reasoning analysis. Deterministic answer grading is a separate authority, not an excuse to bypass AI.
2. Preserve answer, reasoning, attached scratchpad, teacher solution/reference, diagnostic root-cause knowledge nodes, method, misconception, confidence and evidence history.
3. Keep method-agnostic evaluation and real mathematical-fallacy detection. Correct answers do not automatically prove valid reasoning/mastery.
4. Preserve rubric/manual-grading policy, teacher override/final-review precedence, scoped question voiding, tenant isolation and zero-weight evidence where applicable.
5. Preserve per-question evidence and the current Twin mastery calculation. Do not replace 50 evidence records with a single average or fabricate reasoning quality.
6. AI calls may run concurrently; shared student aggregate updates must be short and safely coordinated across instances. Never hold DB locks while calling Gemini.
7. Recommendations/summary can be delayed off the grading critical path, but must be durably queued and regenerated after relevant changes. They are necessary downstream work, not optional work to silently discard.
8. Do not change model, introduce micro-batching, remove/defer AI Solution, expand learning-path algorithms or change the scoring scale in this first phase without a further user decision.
9. Tests/benchmarks use synthetic data. Do not create student assignments/evidence in the user's real dataset or consume real API quota without an explicit scoped benchmark request.
10. No push was authorized in this implementation turn.

## Existing changes that predate this implementation

The preceding key-list change is already in the worktree and must be preserved:

- `.env.example`, `README.md`, `docker-compose.yml`.
- `src/EduTwin.API/AssessmentAndReasoning/AI/DependencyInjection.cs`.
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiOptions.cs`.
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiDependencyInjectionTests.cs`.
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiOptionsTests.cs`.

Local ignored `.env` holds seven keys in `GEMINI_LIST_KEY` as a JSON array; do not print/copy secrets into this document, tests, logs, Git, or reports. Model remained `gemini-2.5-flash`. Legacy key configuration remains supported in code. Prior key-list verification: 3791 backend tests passed, 62 skipped; focused Gemini tests 60 passed; prior Docker API was healthy. Those are **prior results**, not verification of the new optimization patches.

The user first answered “same project”, then clarified seven keys originate from seven different Gemini Pro accounts. This does not establish their project IDs, API tiers or RPM/TPM/RPD. Do not infer seven independent quotas. Unverified keys must share one conservative pool until metadata is supplied.

Unrelated local/untracked files to preserve and exclude from publication:

- `docs/verification/evidence/assignment_submission_acceptance/`.
- `scripts/e2e/`.
- `scripts/ops/remove_demo_assignments.cjs`, `repair_known_e2e_orphans.cjs`, `runtime_inventory.cjs`.
- `scripts/seed_student_assignments.sql`, `scripts/start_api.ps1`.

## Code findings behind the implementation

- `AIAnalysisJobBackgroundService` originally awaited jobs sequentially. BatchSize is discovery size, not concurrency.
- `AIAnalysisJobProcessor` originally held the provider response only in memory, then committed analysis/evidence/Twin/job status together. A concurrency loser could lose the response and wait for lease recovery/reinvoke AI.
- `KnowledgeTwinUpdater` updates mastery incrementally per evidence. Completion order and replay equivalence need explicit attention before enabling parallel processing.
- `TwinCompletionOrchestrator` updates analysis, evidence, behavior/knowledge/history/goals/student Twin and assignment progress. Do not casually separate these atomic semantics.
- `StudentLockHelper` already provides a tenant-safe MySQL student-row `FOR UPDATE` lock and is used by `RecommendationEngine`.
- `OverallAssignmentCommentWorkflow` synthesizes comments in code; it is not another Gemini call. It returns null while analyses are still processing.
- Existing MySQL concurrency test for two different jobs/same student deliberately produces Completed + LostRace and tests eventual recovery. Existing manual-constructor unit tests exercise the legacy path and will not alone verify new DI-injected services.
- Gemini .NET SDK `Google.GenAI` 1.18.0 defaults to five transport attempts unless configured otherwise. Its `ClientError`/`ServerError` derive from HttpRequestException and expose integer StatusCode. Avoid raw exception logging.

## Draft patches already applied (incomplete, NOT production-ready)

### DAL foundations

Added:

- `src/EduTwin.DAL/AssessmentAndReasoning/AIAnalysisCheckpoint.cs`.
- `src/EduTwin.DAL/AssessmentAndReasoning/AIStudentPostProcessingJob.cs`.
- `src/EduTwin.DAL/AssessmentAndReasoning/AIProviderQuotaState.cs`.
- `src/EduTwin.DAL/Persistence/Configurations/AssessmentAndReasoning/AIProcessingConfiguration.cs`.

Modified `EduTwinDbContext.cs` with the three DbSets. Checkpoints/post-processing jobs use tenant filters; provider quota state is cross-tenant capacity metadata, containing no API credentials/submission data.

**No EF migration/designer/model snapshot update has been generated yet. Do not run this code against the existing schema without migration.**

### Checkpoint/processor

Added `AIAnalysisCheckpointStore.cs` and `AIProcessingMetrics.cs` under BLL Processing, plus `AIAnalysisDeferredException.cs` under BLL AI.

- Request fingerprint hashes serialized request, provider profile, question RowVersion and individual image SHA256s (ImageParts are JsonIgnore, so explicitly hashing image bytes matters).
- Draft checkpoint store reads/deserializes an internal provider result and saves it after a lease/version eligibility query.
- IAIService has a default AnalysisProfileVersion; GeminiAIService supplies model/prompt-policy/contract identity.
- Processor reads checkpoint, validates its response, avoids provider invocation on a hit, builds analysis, persists checkpoint, then performs authoritative transaction.
- Production DI supplies checkpoint/post-processing dependencies; optional defaults retain manual-constructor compatibility for older tests.
- Production success/fallback transactions acquire StudentLockHelper when checkpoint support is present.
- Capacity deferral reschedules without consuming the existing AI-fallback retry budget.
- Concurrency failure after a saved checkpoint reschedules quickly rather than intentionally waiting five minutes.
- Queue requests are inserted inside the authoritative grading transaction; direct post-commit tasks remain only as a constructor-compatibility fallback when no queue dependency is injected.

### Post-processing queue

Added BLL `AIStudentPostProcessingQueue.cs` (enqueue + processor) and API `AIStudentPostProcessingBackgroundService.cs`.

- Coalesced key: Center/Student/Subject/AssignmentScope (Guid.Empty for practice).
- Revision/ProcessedRevision prevents an older worker from acknowledging newer enqueues.
- Student DB lock, lease, durable retry/backoff, two-second initial delay, no endless moving of due time.
- Worker uses fresh tenant-scoped DI contexts and processes five jobs per center per scan.
- Registered through AddAssessmentAndReasoning/AddAIAnalysisJobBackgroundWorker.
- Pending assignment analyses keep summary work pending; no fake completed summary.

### Worker

- Added MaxConcurrentJobs (1..32, current default remains 1).
- Program binds `AIAnalysisWorker` configuration.
- Replaced sequential loop with bounded Parallel.ForEachAsync; counters use Interlocked; each item retains separate scopes.
- Round-robin centers within the already-discovered batch.
- Queue-wait and job/provider duration metrics added.
- **No effective local/docker concurrency increase has been configured yet.**

### Gemini quota/client

- GeminiOptions now supports optional QuotaPools: project ID, zero-based indexes into deduplicated key list, maximum concurrent calls, RPM/estimated input TPM/RPD.
- Without verified metadata, all keys share `unverified-shared-project`, maximum two provider calls, unknown RPM/TPM/RPD rather than invented quotas.
- New `GeminiQuotaCoordinator.cs` uses SQL quota-row locks and a serialized rolling ledger shared across instances. Holds no transaction while inference executes. Lease expiry protects crash recovery. Daily window uses America/Los_Angeles.
- Ledger tracks rolling request count/input-token estimates, active reservations, daily count and shared exponential-backoff cooldown.
- Admission estimate is conservative UTF8 bytes/2 plus image allowance, reconciled with provider PromptTokenCount. It is not an exact tokenizer/quota guarantee; authoritative 429 is handled.
- GoogleGenAIGenerateContentClient was rewritten to share text/image handling, acquire/release quota reservations, avoid retrying another key in a blocked pool, sanitize logs and set SDK Attempts=1 (durable retry owns transient errors).
- GeminiAIService propagates AIAnalysisDeferredException through both inner/outer catches.

## Exact interruption point / known unfinished work

The last tool call successfully replaced `GoogleGenAIGenerateContentClient.cs`, then a second apply_patch failed verification because an accidental empty hunk targeted `GeminiLearningPathPlanEnricher.cs`.

**Consequences:**

- Client rewrite is present.
- No change to GeminiLearningPathPlanEnricher.cs was applied.
- Intended addition of `AIProcessingMetrics.Tokens` was NOT applied. The rewritten client references this missing member, so current code likely fails compilation until repaired.
- Last successful build happened BEFORE that client rewrite, using:
  `dotnet build src/EduTwin.API/EduTwin.API.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v quiet`
  It passed with zero warnings/errors. Do not represent it as a build of the current paused worktree.
- Normal parallel/shared-compiler builds in the sandbox failed strangely or hit named-pipe UnauthorizedAccessException. Use single MSBuild node and disable shared compilation as above.

## Critical review items when explicitly resumed

1. Repair missing Tokens metric; build current source first.
2. Audit checkpoint I/O exception boundaries: ReadAsync is currently inside the general provider-failure catch, so a checkpoint DB outage could incorrectly consume an AI failure retry. Separate storage/provider failures properly.
3. Make checkpoint save fencing atomic, not just an eligibility query followed by an independent update. Revalidate lease/owner/request under a short transaction; do not let an expired worker overwrite a newer checkpoint.
4. Validate checkpoint identity against all relevant submission/attachment changes and teacher/final-review mutations. Preserve the earlier observational SolutionExposedAt-only version-drift fix.
5. Ensure serialized commits preserve deterministic/equivalent mastery evidence ordering (or use existing chronological replay) before enabling concurrency. Locks prevent lost writes but alone do not establish order.
6. Review success/fallback/storage-failure paths consistently. All evidence-changing paths must retain isolation/idempotency/teacher precedence; invalid checkpoint JSON must not become a student grading failure.
7. Audit post-processing retries/cancellation/revision fencing/stale triggers and workload fairness. Pending summary is not an exception-worthy failure; consider representing it separately from operational FailureCount.
8. Existing teacher approve/override still directly generate recommendations/comments. Decide whether to enqueue safely in their transactions; ensure teacher changes supersede old pending AI tasks. Do not broaden grading policy.
9. Existing worker logging labels every RetryScheduled as AI_ANALYSIS_ATTEMPT_FAILED, including capacity deferral. Correct status/metrics distinction and student-visible messaging.
10. Verify SDK status classification, cancellation, release TTL, nonretryable auth/config failures and safe fallback across real independent pools. No raw key fragments/messages in logs. No broad account rotation to circumvent restrictions.
11. Review null/malformed configuration validation, JSON quota-state corruption, fixed IDs/day resets, in-memory vs real MySQL behavior, reservation cleanup and quota token estimation limits. Add MySQL tests for cross-instance row-lock coordination.
12. Generate EF migration + model snapshot with only intended three new tables/FKs/indexes; inspect Up/Down SQL and ensure no unrelated model drift/destructive operations.
13. Add substantial tests: checkpoint reuse after DB conflict/restart, invalidation for answer/image/prompt/model/question version changes, expired worker fencing, teacher-reviewed result preservation, no duplicate evidence/Twin history, MCQ still uses AI, bounded parallel scopes/counters/fairness, quota blocks/cooldown/day/expiry, durable postqueue coalescing/new revision/cancel/failure.
14. Adjust DI hosted-service test that currently asserts a single total IHostedService (a second post-processing service is now registered). Existing manual-constructor processor tests are not sufficient feature coverage.
15. Run focused tests then full backend suite. Existing MySQL tests are skipped unless their isolated test database environment is configured; do not claim production race safety from InMemory alone. Use a disposable synthetic database, not the user's student dataset.
16. UI progress already has background processing semantics from earlier work, but new capacity-wait/error codes and downstream-update state have not been reviewed/changed. Keep submitted work read-only and do not reintroduce retry-submit/upload bugs.
17. Finish configuration examples, Docker passthrough and operational/benchmark documentation. Benchmark fake provider latency and synthetic batches 50/100/100x50; distinguish simulated throughput from real Gemini performance. Actual RPM/TPM/model access still needs user metadata.
18. Only after code/test/schema review: plan a safe local rollout without active jobs, preserving DB/volumes. Current Docker is still the earlier version. No automatic paid billing/quota upgrade, model change, real API stress test, commit or push.

## Provider/legal references already verified

- https://ai.google.dev/gemini-api/docs/rate-limits — quota is per project, not key; RPM/input TPM/RPD.
- https://ai.google.dev/gemini-api/docs/google-ai-plans — Pro subscription vs direct API billing/tiers; Cloud-credit benefits require activation.
- https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash — existing users continue; Google recommends newer models for new projects. No automatic migration.
- https://ai.google.dev/gemini-api/docs/troubleshooting — specific transient retries/backoff/jitter; no infinite retries on client errors.
- https://ai.google.dev/gemini-api/docs/generate-content/thinking — current 2.5 thinking budget differs from 3.x levels; do not disable reasoning indiscriminately.
- https://ai.google.dev/gemini-api/docs/generate-content/caching — cache doesn't replace student-specific analysis or bypass TPM.
- https://ai.google.dev/gemini-api/docs/batch-api — cheaper async batch is not the interactive grading path.
- https://ai.google.dev/gemini-api/terms — under-18-directed API client restriction; unpaid-service data handling restrictions.
- https://cloud.google.com/terms/service-terms — section 20(d) age restriction also present; switching to Vertex is not automatically a workaround.

Before production for real THPT students, seek provider/contract confirmation about permitted under-18 educational use. This is a production decision, not permission to invent a workaround or change provider now.

## Next-turn entry point

After explicit user resume: read this file, inspect git diff/current code, repair the interrupted patch and execute the critical review/test list. Do not restart finished key-list work or claim this paused implementation is complete. Keep the user informed in Vietnamese, with updates during ongoing work.
