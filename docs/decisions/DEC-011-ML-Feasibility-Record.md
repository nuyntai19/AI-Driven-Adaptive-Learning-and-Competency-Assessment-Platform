# DEC-011: ML.NET Recommendation Feasibility & Adoption Decision

**Status:** REJECTED / NO-GO (Baseline Heuristic `opportunity-v1` Preserved)  
**Date:** 2026-09-11  
**Scope:** R07 (Opportunity Gap, Recommendation Pipeline, Adaptive Learning Path)  
**Author:** EduTwin Architecture Team  

---

## 1. Context and Problem Statement

During the planning of Release R07 (Opportunity Gap, Recommendation Pipeline, and Adaptive Learning Path), an evaluation was conducted on whether to integrate an automated Machine Learning pipeline (specifically Microsoft ML.NET recommendation or regression algorithms) to predict mastery gain and rank candidate topics for students.

## 2. Evaluation Criteria & Findings

### 2.1. Data Availability & Provenance
* **Current State:** The platform is in early deployment; historical datasets containing verified student learning trajectories, longitudinal competency gains, and explicit user feedback are currently insufficient.
* **Risk:** Training ML models on synthetic or sparse data would introduce unpredictable bias, catastrophic forgetting, or ungrounded recommendations ("garbage in, garbage out").

### 2.2. Deterministic Governance and Traceability
* **Educational Compliance:** In regulated educational contexts, recommendations directly determine student instructional pathways.
* **Requirement:** Every recommended topic must be 100% explainable, deterministic, and auditable. Teachers and center managers must be able to inspect the exact arithmetic breakdown (Current Mastery, Exam Importance, Prerequisite Readiness, Recent Reasoning Average) without black-box opacity.
* **Finding:** ML models do not provide immediate arithmetic guarantees or strict tie-breaking hierarchies without complex explainability frameworks (e.g., TreeSHAP).

### 2.3. Prerequisite Graph Gating & Invariants
* The curriculum knowledge graph enforces strict prerequisite invariants (e.g., candidate locked if prerequisites $< 60\%$, frontier expansion, cycle detection). Pure statistical ML ranking models risk proposing logically blocked concepts unless constrained by heavy heuristic guardrails, which defeats the purpose of end-to-end ML.

### 2.4. Consent and Regulatory Privacy
* Collecting telemetry for automated profiling requires explicit parental/guardian consent and institutional governance under applicable educational privacy regulations. These consent workflows have not yet been formalized.

---

## 3. Decision

1. **NO-GO on ML.NET in R07:** Do not introduce ML.NET model training, weights, or ML-based inference in Release R07.
2. **Preserve Deterministic Heuristic Baseline (`opportunity-v1`):**
   * Formula:
     $$\text{ExpectedScoreGain} = (1 - \text{Mastery01}) \times \text{ExamImportance}$$
     $$\text{ProbabilityOfMastery} = \text{Clamp}(0.20 + 0.60 \times \text{RecentReasoningAverage01} + 0.20 \times \text{PrerequisiteReadiness}, 0, 1)$$
     $$\text{RawOpportunity} = \frac{\text{ExpectedScoreGain} \times \text{ProbabilityOfMastery}}{\max(\text{EstimatedLearningHours}, 0.5)}$$
   * Strict 5-tier tie-breaking: `NormalizedScore DESC` $\to$ `Mastery ASC` $\to$ `ExamImportance DESC` $\to$ `OrderIndex ASC` $\to$ `TopicId ASC`.
3. **Linear Fallback with Ready Prerequisite Frontier:** Used when governed evidence count $< 3$ or when reasoning telemetry is temporarily unavailable.
4. **MaintenanceReview:** Deterministic consolidation mode triggered when all topics achieve $\ge 80\%$ mastery.

---

## 4. Criteria for Future Re-evaluation

A future release may re-evaluate ML.NET adoption only when all of the following preconditions are met:
1. **Sample Size:** At least 10,000 verified, non-superseded student completion events across multiple classes and subjects.
2. **Consent & Governance:** Explicit user/institutional consent collected for algorithmic personalization.
3. **Shadow Mode Validation:** ML model runs in shadow mode (offline) alongside `opportunity-v1` and demonstrates statistically significant improvement in predicting score gains without violating prerequisite constraints.
4. **Explainability Parity:** A proven attribution mechanism that maps model outputs to transparent, deterministic explanations matching the system's template requirements.
