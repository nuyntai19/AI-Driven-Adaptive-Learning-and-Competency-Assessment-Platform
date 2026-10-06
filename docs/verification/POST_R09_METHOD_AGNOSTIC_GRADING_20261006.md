# Mathematical grading and method-agnostic AI feedback — 2026-10-06

## Scope and diagnosis

The real local `Test 1` submission was inspected without resubmitting it or running browser/E2E tests. Attempt 6 (question 10001) submitted `D=R\{2}` against the teacher reference `R \ {2}`. `TextExact` marked this mathematically valid answer incorrect. Gemini independently produced a valid domain solution and reasoning quality 95, but the legacy builder replaced its feedback with a canned message anchored to the incorrect deterministic result.

## Implemented

- Added explicit `ShortAnswer + MathEquivalent` grading, authoring/import support and visual answer input. The bounded grammar supports rational numbers, parenthesized 2D coordinates, finite sets of rational numbers, and real numbers excluding finitely many rational points. Notation, ordering and duplicate set members are normalized; unsupported expressions defer to teacher review rather than receiving an automatic zero. `TextExact` remains literal for linguistic answers. This is not a general-purpose CAS.
- Strengthened the AI prompt: the teacher solution is one valid reference, not a mandatory sequence. Valid concise alternative methods deserve full reasoning quality; trivial omitted arithmetic and different wording/order are not errors. Substantive inferences, domain conditions, divisors and extraneous solutions must still be checked. Digit cancellation in `16/64` is explicitly identified as an invalid derivation despite the correct numerical result.
- Added advisory answer assessment and reasoning verdict to the provider schema, strict parser, persistence and feedback DTO. Original 12-field responses remain readable; new provider output requests both extra fields. Malformed/unknown assessment values are rejected.
- Preserved actual Gemini feedback and reasoning concerns instead of overwriting/erasing them when the final answer is right. Disagreement, explicit invalid reasoning, uncertainty or confidence below 80 routes to teacher review with zero AI mastery evidence weight. A minor defect label alone is not treated as a contradiction. AI observations cannot silently overwrite the deterministic/human grade.
- Separated grade-source labels from AI commentary; legacy canned feedback is explicitly labelled as a system notice. Normalized per-question display scores to the assignment's ten-point total, including questions with different internal maxima.
- Removed image bytes from text JSON serialization; the scratchpad is still supplied as an actual multimodal image part, avoiding duplicated base64 payloads in the prompt.
- Added a tenant/question-scoped repair command. It skips human-overridden or human-reviewed attempts, appends superseding evidence, marks cached overall commentary stale, replays affected-topic mastery and subject confidence calibration, and recomputes aggregate twin/goal data. Original submissions, analyses, evidence, history, attachments and append-only triggers are preserved.

## Verification

| Check | Result |
| --- | --- |
| BLL suite, `Category!=MySql` | 3,674 passed, 0 failed, 62 skipped; 3,736 discovered |
| Frontend suite | 456 passed, 0 failed |
| ESLint | 0 errors, 32 existing warnings; not a zero-warning result |
| TypeScript / Vite production build | Passed |
| Bundle budget | 3/3 passed; entry about 107.65 kB; large deferred MathLive chunk still produces a Vite warning |
| Docker API release build | 0 compile errors / 0 compile warnings |
| `git diff --check` | No whitespace errors; Git still reports existing LF/CRLF conversion notices |
| Runtime | Web HTTP 200; protected API HTTP 401 without a session; existing four containers only |

An unrelated cancellation-aware background-service test timed out during concurrent builds. Its isolated retry passed, and the final full BLL run passed. No test assertion was weakened to hide that timeout. An initial repair execution hit the existing `calculation_version varchar(20)` limit; its transaction rolled back completely. The repair marker was shortened to `math-replay-v1`, tested and rebuilt before retry.

## Local database repair and deployment

- Private backup: `C:\Users\ACER\AppData\Local\Temp\EduTwin-backups\2026-10-05_before_math_repair_08e2ea6c22a3403e84bb327eb0916bed.sql` (450,974 bytes; UTC filename date).
- Backup SHA-256: `b0c09cf6bd4093b2c5e1e701a5ea19b59f95ba6a128759f37d9f24fde989c199`.
- Applied forward migration: `20261005185553_AddMethodAgnosticMathGrading` (34 migrations total). Rollback maps the new mode to `Manual`, never silently back to literal matching.
- Explicit repair scope: center `10000000-0000-0000-0000-000000000001`, question `10001`.
- Attempt 6: `IsCorrect=true`, internal score `20/20`, reason `MATH_EQUIVALENT`, status `NeedsTeacherReview`. Attempt 5 remains correct at `10/10` internally. Equal per-question normalization yields **10/10 provisional** for the two-question assignment, not a final teacher-approved grade.
- Evidence count: 2 → 3. History count: 2 → 3. All three evidence protection triggers remain present. Running the command again corrected zero attempts and added no duplicate evidence/history.
- Existing legacy AI quality/solution remain historical observations; their overwritten original feedback cannot be recovered. They were not regenerated or represented as fresh Gemini feedback. Corrected legacy evidence remains review-only until teacher confirmation.
- Running API image: `sha256:b06acac8c6c7e98b01ded3bb12de2addddde06d378f126581a9f263e1dcfd0d0`.
- Running web image: `sha256:6715744508226360d1700c9ebd2c0aa9292d9845eb2cb3b61ff5f2d906ea4f02`.
- MySQL data, attachment and data-protection volumes retained. No database reset, trigger removal, extra permanent containers or real-student resubmission. No Git commit or push was performed during verification; subsequent delivery was separately authorized by the user.

## Limits and follow-up

These tests verify the contract, prompt requirements, normalization and review safeguards. They do not prove that an LLM will recognize every valid method or fallacy. A representative provider-evaluation set remains necessary to measure those rates. General algebraic equivalence, interval notation, irrational set elements and unrestricted equations are outside this bounded grader and defer for human assessment. Existing recommendations/learning paths generated from old evidence were not deleted or rewritten during this scoped grade repair; they should be re-evaluated following teacher confirmation. Browser visual acceptance is intentionally left to the user.

## Git delivery scope

The user authorized committing and pushing the verified assignment-submission, question-numbering and grading corrections to `student/answer`. Delivery includes source, EF migration, regression tests, verification reports, and the credential-safe database backup helper. Local screenshots, seeded assignment SQL, historical browser diagnostics and one-off data-removal scripts are excluded and preserved locally. This delivery does not claim that PDF/Word bulk import or unrestricted symbolic mathematical equivalence is implemented.
