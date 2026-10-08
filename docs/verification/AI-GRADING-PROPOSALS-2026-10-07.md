# AI grading proposals and Vietnamese explanations

## Agreed workflow

- AI analyzes **every submitted answer with reasoning**, including multiple choice. It supports error diagnosis and Digital Twin evidence; objective grading is not a reason to skip AI.
- Multiple-choice submissions retain the original option ID in the database. The inference payload resolves it to the option label and text and supplies all active, same-tenant choices.
- Manual/essay evaluation means **AI proposes a score; the teacher approves or adjusts it**, not "AI refuses to grade". A proposal is never a final grade.
- With a scored rubric, AI proposes each criterion's points. The server validates criterion identity, count, precision and bounds and computes the total. Teacher approval records the rubric snapshot in append-only review history.
- Answer/assignment points are presented on a **10-point scale**. Reasoning quality remains a separate **0–100 diagnostic indicator** and is not added to assignment points.
- Ordinary correct, valid reasoning has no error root-cause nodes. Merely listing the tested knowledge topics must not create a false review alert.
- A materially different method is accepted without arbitrary deductions and flagged for teacher validation. Different wording, concise reasoning or English answers are not "different methods". Genuine contradictions, invalid reasoning, uncertainty and provider failure retain their safeguards.
- All new AI feedback and explanatory solutions use Vietnamese. English exercise answers and quotations remain English inside that explanation. Authoring guidance recommends Vietnamese explanations, e.g. "Dùng thì hiện tại đơn, chủ ngữ she nên chọn goes."
- A pending teacher approval is **not an incorrect answer**. Summary analysis counts and finalized/evaluated score counts are separate.

## Persistence and safety

Migration: `20261007091024_AddAIGradingProposals` adds only three columns to `reasoning_analyses`:

- `suggested_score`
- `suggested_rubric_grade_json`
- `uses_alternative_method`

The local rollout takes a complete private backup and verifies unchanged user-data inventory and a hash of submitted answers, reasoning, stored points, correctness, state and timestamps. No volume reset, attempt removal or automatic regrading is performed. Existing English feedback on already-submitted attempts remains historical data; it is not silently rewritten.

Provider-profile provenance changes to `vietnamese-grade-proposal-v3`, preventing reuse of checkpoints from an old prompt/profile.

## Verification

- Full backend suite: 3,861 passed, 69 opt-in MySQL tests skipped in the ordinary run, including the null-score language-guard regression.
- Frontend: 551 tests passed, including six additional verdict-badge regression tests; TypeScript/Vite production build succeeded.
- Real MySQL synthetic 50-question pipeline and 100-question quota/partial-response recovery pipeline passed with the new contract, preserving per-question analysis, attachment association, evidence and Twin history without duplicates.
- Bounded real Gemini experiment: 15 synthetic cases, five microbatch calls. All contracts parsed after handling a quoted English model answer; 14/15 answer/reasoning observations matched. English multiple-choice and rubric essay cases returned Vietnamese explanations and complete proposal points.
- Remaining model limitation: in one deliberately invalid scratchpad case, Gemini recognized invalid reasoning but confused the scratchpad's conclusion with the separately typed final answer. This remains a teacher-reviewed case, not an automatic final grade. Prompting is not a guarantee of mathematical correctness; deterministic scores and explicit teacher approval remain authoritative.

Private raw synthetic reports and the original 50-question UI benchmark are retained under ignored `storage/verification/`; they are not committed with API keys or real student submission data.

## Pending essay verdict badge follow-up

The teacher workspace previously treated nullable `isCorrect` as a boolean condition, showing an ungraded essay as incorrect. The badge now explicitly distinguishes authoritative true/false from an unknown verdict. A non-fallback AI answer assessment is labeled as an AI opinion pending teacher approval, even for a legacy analysis without a suggested score. Fallback, uncertain and missing assessments never imply that the student was wrong; processing attempts remain pending. Tests exercise both the helper and rendered grading workspace, including conflicts where the authoritative verdict must take precedence. This presentation-only fix does not regrade historical attempts or change stored scores.
