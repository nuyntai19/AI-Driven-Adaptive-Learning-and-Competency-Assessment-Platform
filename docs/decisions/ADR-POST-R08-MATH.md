# ADR-POST-R08-MATH: Visual Math Toolkit, Multimodal Drawing Attachments, Scope Guard and Resilient Storage Fallback

**Status:** APPROVED  
**Date:** 2026-09-12  
**Scope:** Post-R08 Extension — Track 2 (Math Toolkit & Multimodal Evidence)  
**Author:** EduTwin Architecture Team  

---

## 1. Context and Problem Statement

In Vietnamese High School (THPT) STEM education, assessing mathematical competency requires observing authentic problem-solving artifacts:
1. Mathematical notation (equations, calculus, set theory, Greek letters).
2. Graphic scratches, geometric diagrams, coordinate constructions (Oxy).
3. Exact arithmetic verification (fractions, finite decimals, irrational approximations).

In the R00–R08 baseline, students submitted plain text `reasoning_text` and scalar answers. This constrained natural mathematical expression and prevented multimodal reasoning analysis. To bridge this gap, EduTwin introduces a Visual Math Toolkit and Multimodal Drawing Attachment pipeline.

---

## 2. Decision Drivers

1. **Academic Integrity & Anti-Cheating:** Provide calculation and formula tools without embedding automated equation solvers that bypass student reasoning.
2. **Pedagogical Evaluation Accuracy:** Support exact mathematical equivalence (e.g., $1/2 = 0.5 = 2/4$) deterministically without floating-point imprecision.
3. **Data Protection & Multi-Tenant Security:** Secure attachment storage with tenant isolation, cryptographic upload binding, bounded file validation, and strict permission guards.
4. **Operational Failure Resilience:** Differentiate infrastructure storage outages from student misconceptions, preventing free-practice attempts from polluting the teacher review queue.

---

## 3. Architectural Decisions

### 3.1. Visual Math Input Toolbar & KaTeX Preview
- Client-side visual toolbar with 5 specialized mathematical symbol tabs:
  - Basic ($\pm, \times, \div, \sqrt{x}, x^2, x^n, \frac{a}{b}$)
  - Algebra ($\le, \ge, \neq, \approx, \infty, \pi, \alpha, \beta, \theta$)
  - Calculus & Analysis ($\int, \frac{d}{dx}, \sum, \lim$)
  - Sets & Logic ($\in, \notin, \subset, \cup, \cap, \emptyset, \forall, \exists, \implies$)
  - Geometry & Trigonometry ($\sin, \cos, \tan, \cot, \angle, \Delta, \perp, \parallel, ^\circ$)
- Integrated real-time LaTeX rendering via KaTeX configured with `trust: false` to sanitize against script injection.
- Stored as `answer_display_latex` (`VARCHAR(2048) NULL`) in `attempts` table.

### 3.2. Question Answer Evaluation Modes & Rational Normalizer
- Matrix constraint between `QuestionType` and `QuestionAnswerEvaluationMode`:
  - `MultipleChoice`: Strictly `QuestionAnswerEvaluationMode.TextExact`.
  - `Essay`: Strictly `QuestionAnswerEvaluationMode.Manual`.
  - `ShortAnswer`: Supports `TextExact`, `NumericRational`, `Manual`.
- Enforced across `CreateQuestionUseCase`, `UpdateQuestionUseCase`, and `ActivateQuestionUseCase`.
- `MathAnswerNormalizer`:
  - Implemented using .NET `System.Numerics.BigInteger`.
  - Normalizes integers, fractions ($a/b$), mixed numbers, and decimals into a canonical irreducible fraction $P/Q$ ($Q > 0, \gcd(|P|, Q) = 1$).
  - Prevents floating-point rounding errors (e.g., $0.1 + 0.2 \neq 0.3$).

### 3.3. Scientific Calculator Drawer
- Deterministic, client-side calculator drawer (`calculatorEngine.ts`).
- Supports basic arithmetic, trigonometric functions (deg/rad), logarithms ($\ln, \log_{10}$), square roots, and exponents.
- Invariant: Pure calculation only; contains zero symbolic algebra solvers or step-by-step problem-solving engines.

### 3.4. Vector Scratchpad Canvas & Scoped IndexedDB Cache
- Fullscreen modal canvas (`ScratchpadCanvasModal.tsx`).
- Tools: Freehand pen, eraser, 30-step undo/redo, background grid toggle (math grid / ô ly), geometry primitives (ruler, circle, triangle, Oxy coordinate plane).
- Persistence: Stored in browser IndexedDB with strict scoping key:
  `draft:${centerId}:${userId}:${clientSubmissionId}`
- Draft Lifecycle:
  - Retained across accidental page refreshes.
  - Purged automatically upon user logout or TTL expiration on startup.
  - Deleted only after receiving successful server submission acknowledgement (HTTP 202 or HTTP 200 replay).

### 3.5. Bounded Multipart Streaming Upload & Data Protection Token
- Thin controller: `AttemptAttachmentsController.cs` delegates directly to BLL use cases.
- Caller must possess `Student` account type and `learning.attempts.submit` permission.
- `PrepareAttemptAttachmentUploadUseCase`:
  - Enforces server-side size limit $\le 5\text{MB}$ (`5,242,880` bytes).
  - Bounded full PNG validation: 8-byte PNG header (`0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A`), valid `IHDR` chunk ($W, H \le 4096$, allowed color types $\{0,2,4,6\}$, bit depth), streaming chunk decode with memory boundaries, and terminating `IEND` chunk with CRC verification. Rejects truncated or trailing-garbage payloads.
  - Streams temporary file to `tenants/{centerId}/attempt-attachments-temp/{uploadNonce}.png` and computes SHA-256 hash.
  - Generates Data Protection signed token (`drawingUploadToken`) encoding `(centerId, userId, uploadNonce, sha256Hash, expiresAtUtc)`.

### 3.6. Idempotent Replay, Upload Race Semantics & Atomic Promotion
- In `SubmitAttemptUseCase`:
  - Caller must possess `Student` account type and `learning.attempts.submit` permission.
  - If submission already exists: Compares payload and `uploadNonce`. If identical, returns HTTP 200/202 replay success even if the temp blob was already promoted or purged. If mismatched, throws `ConflictException(ErrorCodes.DuplicateSubmission)`.
  - If submission is new: Validates signed token against caller context and hash.
  - Atomic promote semantics: Promotes temp file to permanent destination `tenants/{centerId}/attempt-attachments/{uploadNonce}.png` with `overwrite: false` (or `FileMode.CreateNew`).
  - Race conditions:
    - If permanent file exists with identical SHA-256 hash: Idempotent promotion succeeds.
    - If permanent file exists with different hash: Fail-closed (`InvalidOperationException`).
  - Database unique constraint `(center_id, upload_nonce)` (`ux_attempt_attachments_center_id_upload_nonce`) is the authoritative race arbiter.
  - MySQL Error 1062 on this unique index is mapped directly to `ConflictException(ErrorCodes.UploadTokenAlreadyUsed)` (HTTP 409).

### 3.7. Physical Table 40 (`attempt_attachments`)
- Schema details:
  - Table name: `attempt_attachments`.
  - Primary Key: `id` (BIGINT UNSIGNED AUTO_INCREMENT).
  - Columns: `id`, `center_id`, `attempt_id`, `upload_nonce`, `storage_key`, `file_size_bytes`, `content_type`, `sha256_hash`, `created_at`, `created_by` (satisfies TA audit columns).
  - Composite FK: `(center_id, attempt_id)` references `attempts(center_id, attempt_id)`.
  - Database Constraints:
    - `CONSTRAINT ck_attempt_attachments_file_size_bytes CHECK (file_size_bytes >= 1 AND file_size_bytes <= 5242880)`
    - `CONSTRAINT ck_attempt_attachments_content_type CHECK (content_type = 'image/png')`
    - `CONSTRAINT ux_attempt_attachments_center_id_storage_key UNIQUE (center_id, storage_key)`
    - `CONSTRAINT ux_attempt_attachments_center_id_upload_nonce UNIQUE (center_id, upload_nonce)`
    - `CONSTRAINT ux_attempt_attachments_center_id_attempt_id UNIQUE (center_id, attempt_id)`

### 3.8. Unified Scope Guard (`IAttemptTeacherReviewScopeGuard`)
- Unifies authorization across Attachment Download, Teacher Review Queue, and Teacher Override.
- Verification rules:
  - Same tenant: `Attempt.CenterId == caller.CenterId`.
  - Valid assignment: `Attempt.AssignmentId != null`.
  - Targeted student: `Assignment.AssignmentTargets.Any(t => t.StudentId == attempt.StudentId)`.
  - Responsible teacher: `Assignment.Class.TeacherId == caller.UserId` (or caller is `CenterManager`).
- Invariant: Free-practice attempts (`AssignmentId == null`) fail closed (returns HTTP 404 for attachment download; invisible in review queue; rejects teacher overrides).

### 3.9. Storage Outage Handling & Free-Practice Terminal Failure
- `AttachmentStorageUnavailable` is an operational failure, recorded in `AIAnalysisJob.LastErrorCode`. It is NOT an academic misconception and must never be evaluated by `EvidenceGate`.
- `AIAnalysisJobStateMachine`: Upgraded to support up to 3 persisted retries with exponential backoff before transitioning to `RetryExhausted`.
- Resolution on retry exhaustion:
  - Assignment-scoped attempt: Routes to Teacher Review Queue (`RequiresTeacherReview = true`, weight = 0, fallback feedback).
  - Free-practice attempt: Must NOT enter the Teacher Review Queue (as no teacher has authorization to view free-practice attempts). Transitions to:
    - `AIAnalysisJob.Status = AIJobStatus.FailedTerminal`
    - `Attempt.Status = AttemptStatus.AnalysisFailed`
    - Student UI displays clear infrastructure failure messaging and a "Resubmit" action with a new `ClientSubmissionId`.
  - `ck_attempts_status` is updated to include `'AnalysisFailed'`.

### 3.10. Sweeper Semantics (`AttachmentOrphanCleanupWorker`)
- Background worker executing outside tenant context:
  - Must use intentional `IgnoreQueryFilters().AsNoTracking()`.
  - Queries active storage keys:
    `var referencedKeys = await _dbContext.AttemptAttachments.IgnoreQueryFilters().AsNoTracking().Select(a => a.StorageKey).ToHashSetAsync(ct);`
  - Rejects following symbolic links / reparse points (`FileAttributes.ReparsePoint`).
  - Purges expired temp files and unreferenced permanent files exceeding `AttachmentStorage__GracePeriodHours`.

---

## 4. Consequences and Verification

### Positive:
- High school students can submit genuine geometric, algebraic, and scratchpad proof artifacts.
- Multimodal Gemini AI receives both visual diagrams and written reasoning.
- Zero risk of deadlocks or orphaned review queue items from free-practice storage failures.

### Negative / Complexity:
- Two-phase upload flow requires careful frontend lifecycle coordination.
- Permanent storage cleanup requires a governed, asynchronous background sweeper.

### Verification Plan:
- Unit tests: `MathAnswerNormalizerTests`, `ShortAnswerGraderEvaluationModeTests`, `calculatorEngine.test.ts`, `scratchpadStorage.test.ts`.
- Integration tests: `SubmitAttemptWithAttachmentTests`, `MultimodalStorageResilienceTests`, `AttachmentOrphanCleanupWorkerTests`, `AttemptAttachmentsControllerTests`, `AIAnalysisJobStateMachineTests`.
