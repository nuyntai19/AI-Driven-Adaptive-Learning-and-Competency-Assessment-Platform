# R07 ML Data-Readiness and Evaluation Protocol

> Status: APPROVED PROTOCOL — DATA GATE NOT MET
> Decision link: docs/decisions/DEC-011-ML-Feasibility-Record.md
> Current eligible dataset size: N = 0

## 1. Purpose

This protocol defines what would count as valid evidence for replacing or augmenting the deterministic opportunity-v1 ranking with ML.NET. It does not authorize model deployment and it does not claim that an ML evaluation has run.

## 2. Evaluation unit and provenance

One row represents one eligible topic candidate at the instant a recommendation is generated. Every row must be reconstructable from versioned, non-superseded records:

- Center, Student, Subject, Topic and recommendation trigger;
- calculation version and candidate rank;
- mastery before recommendation;
- exam importance, curriculum order and estimated learning effort;
- prerequisite readiness;
- governed recent reasoning aggregate and sample count;
- whether the recommendation was accepted or dismissed;
- subsequent governed evidence used to derive the label.

Rows from synthetic seeds, unit tests, unresolved tenant context, deleted entities, invalid curriculum scope, review-only evidence, or users without required consent are excluded.

The current N is zero by construction: the R07 repository contains neither an approved consent record/workflow for ML profiling nor a governed historical export manifest. Until both exist, no database row is eligible even if local demo attempts are present.

## 3. Label definition

Primary label: VerifiedMasteryGain14d.

It is the deterministic KnowledgeTwin mastery after the earliest of 14 UTC days or three subsequent positive-weight governed evidence events for the same Student/Subject/Topic, minus mastery at recommendation time. The label is unavailable when no subsequent governed event exists; unavailable labels are excluded, not coerced to zero.

Secondary outcome: ProductiveRecommendation, true only when the recommendation was accepted and VerifiedMasteryGain14d is greater than zero. Dismissal alone is not proof of poor learning value.

## 4. Split policy and leakage prevention

- Group eligible rows by `(CenterId, StudentId)`. Order groups by their earliest recommendation timestamp, then `CenterId`, then `StudentId` for deterministic ties.
- Assign whole groups in that order to approximate 70% train, 15% validation and 15% test by row count. A group crossing a target boundary stays intact in the earlier partition; record achieved ratios and timestamp ranges.
- All events for one Student therefore remain in exactly one partition. Report any timestamp overlap between partitions rather than silently claiming a strict row-level temporal split.
- No feature may use evidence created after the recommendation timestamp.
- Curriculum version, calculation version and feature-extraction version are retained.
- Center-level performance is reported separately; rows from one Center must not silently dominate the aggregate.

## 5. Metrics and adoption gate

Primary ranking metric: NDCG@5 against VerifiedMasteryGain14d.

Secondary metrics:

- MAE for predicted mastery gain;
- top-1 positive-gain precision;
- candidate coverage;
- prerequisite-violation rate;
- performance by Center, Subject and GradeLevel where sample size permits.

Adoption requires all of the following:

1. At least 10,000 eligible labeled events across multiple Centers/classes/subjects.
2. Prerequisite-violation rate exactly 0 after deterministic gating.
3. Test NDCG@5 improves over opportunity-v1 by at least 5% relative with a predeclared confidence interval.
4. No material regression in any adequately sampled Center/GradeLevel slice.
5. Offline and no-network demo succeeds with model/version provenance and deterministic fallback.
6. Human review approves explainability and privacy evidence.

Every evaluated artifact must carry a model version, feature-extractor version, immutable dataset-manifest hash, training timestamp and metric report. Shadow inference may rank only candidates already admitted by deterministic tenant/curriculum/prerequisite/evidence gates. Missing or invalid model output, timeout, version mismatch or offline failure must select `opportunity-v1`; Teacher Override and subsequent deterministic replay remain authoritative.

## 6. Current R07 result

No governed historical export meeting the rules above exists in the repository. Therefore:

- no train/validation/test files are generated;
- no ML.NET package or model artifact is added;
- no metric is reported;
- opportunity-v1 remains the operational baseline;
- the system must never call the heuristic output a learned probability forecast.

This explicit no-data result satisfies the decision gate honestly; it does not satisfy a future ML adoption gate.
