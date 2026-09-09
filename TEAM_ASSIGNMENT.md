# EduTwin — Team Assignment and Collaboration Plan

> Phiên bản: 1.0-draft
> Ngày lập: 2026-09-09
> Trạng thái: PROPOSED — phải được cả năm thành viên xác nhận kỹ năng, giờ khả dụng và deadline
> Nhóm: Tuấn Tài, Thịnh, Khoa, Thành Tài, Sơn
> Nguyên tắc: công bằng theo độ khó/evidence, không mặc định 20% vì có năm người

## 1. Chức năng của tài liệu

Đây là nguồn phân công hiện hành từ A đến Z: ai chịu trách nhiệm, ai review, ai hỗ trợ, dependency nào phải xong trước, acceptance nào chứng minh hoàn thành và cách tái cân bằng khối lượng. PROJECT_TRACKING.md chỉ ghi kế hoạch/kết quả thật theo tuần; MASTER_PLAN.md vẫn sở hữu roadmap kỹ thuật.

Tên thành viên đã được Tuấn Tài cung cấp. Mã sinh viên, GitHub, kỹ năng và giờ/tuần chưa có nên bảng này là phương án cân bằng ban đầu, không phải tỷ lệ đóng góp cuối kỳ.

## 2. Nguyên tắc phân công

1. Mỗi người có 30 effort point dự kiến cho course rebaseline; point đo độ khó/rủi ro, không phải số giờ tuyệt đối.
2. Mỗi người phải có requirement/analysis, code, test, review và demo; không có người chỉ viết tài liệu hoặc chỉ frontend.
3. Không ai tự approve phần mình. Security/data/AI mutation cần ít nhất hai người hiểu.
4. Owner chịu trách nhiệm giải thích thiết kế và kết quả, kể cả khi dùng Codex/Gemini.
5. Commit count/line count không tự động là contribution.
6. Sau mỗi tuần, point còn lại được điều chỉnh theo effort thật và blocker; không sửa ngược evidence tuần cũ.
7. Nếu chênh lệch planned load vượt 20% hoặc một người giữ hai critical path cùng lúc, team lead phải tách/co-own package.

## 3. Roster cần hoàn thiện trong Week 1

| Thành viên | Vai trò chính đề xuất | Mã SV | GitHub | Kỹ năng mạnh | Cần hỗ trợ | Giờ/tuần |
|---|---|---|---|---|---|---:|
| Tuấn Tài | Team lead, integration, security/RBAC | TBD | TBD | TBD | TBD | TBD |
| Thịnh | UX, organization và permission UI | TBD | TBD | TBD | TBD | TBD |
| Khoa | Data integrity, Knowledge/Curriculum | TBD | TBD | TBD | TBD | TBD |
| Thành Tài | Question/Assignment/Learning experience | TBD | TBD | TBD | TBD | TBD |
| Sơn | AI Evidence, Twin và Recommendation | TBD | TBD | TBD | TBD | TBD |

Deadline điền bảng: trước khi nhận implementation task đầu tiên trong course repository.

## 4. Phân bổ effort cân bằng

| Owner | Primary delivery | Primary points | Review/support points | Tổng |
|---|---|---:|---:|---:|
| Tuấn Tài | Governance, RBAC backend, integration/CI | 25 | 5 | 30 |
| Thịnh | Stakeholder/Figma, Organization và RBAC frontend | 25 | 5 | 30 |
| Khoa | Live MySQL audit, Knowledge/Curriculum và query integrity | 25 | 5 | 30 |
| Thành Tài | Question/Assignment, Learning Player và E2E | 25 | 5 | 30 |
| Sơn | Evidence Gate, Twin/Recommendation và AI safety | 25 | 5 | 30 |

Điểm chỉ là baseline. Tỷ lệ cuối dựa trên package đã nghiệm thu và evidence con người có thể bảo vệ.

## 5. Work package chi tiết

### 5.1. Tuấn Tài — Integration và Dynamic RBAC backend

| ID | Deliverable | Point | Dependency | Reviewer |
|---|---|---:|---|---|
| TT-01 | Course repo, transparent baseline, branch protection, issue/PR convention | 4 | Docs approved | Khoa |
| TT-02 | RBAC entities/configurations/migration/backfill/validation/rollback | 7 | KH-01; DEC-013–018 approved | Khoa |
| TT-03 | Permission evaluator, stale auth_version enforcement và token invalidation | 7 | TT-02 | Sơn |
| TT-04 | Role/permission/user-role/audit API và module cutover strategy | 7 | TT-03 | Thịnh |
| TT-05 | Review AI completion transaction/failure injection | 2 | SO-02 | Sơn |
| TT-06 | CI, integration branch, release rehearsal coordination | 3 | All critical packages | Thành Tài |

Tuấn Tài cần Khoa kiểm migration/tenant FK; cần Thịnh xác nhận API phục vụ UI; cần Sơn review transaction giữa Evidence và Twin. Không tự merge security-sensitive PR của mình.

### 5.2. Thịnh — UX, Organization và Permission administration

| ID | Deliverable | Point | Dependency | Reviewer |
|---|---|---:|---|---|
| TH-01 | Phỏng vấn/walkthrough CenterManager, Teacher, Student; Figma + feedback log | 6 | Course repo | Thành Tài |
| TH-02 | Audit/hoàn thiện Organization screens theo capability và resource scope | 5 | TH-01; TT-03 cho cutover | Tuấn Tài |
| TH-03 | Role list/editor, permission matrix, user-role assignment, audit UI | 8 | TT-04 | Tuấn Tài |
| TH-04 | Capability-first navigation, unauthorized/session-stale UX | 6 | TT-03 | Thành Tài |
| TH-05 | Review Student Learning Player/accessibility | 3 | TA-03 | Thành Tài |
| TH-06 | Figma comparison và stakeholder acceptance evidence | 2 | TH-02–04 | Khoa |

Thịnh không hard-code permission hoặc tự suy diễn account-type mapping; backend catalog là nguồn. Cần Tuấn Tài hỗ trợ contract/security và Thành Tài hỗ trợ E2E/browser tests.

### 5.3. Khoa — Database integrity, Knowledge Graph và Curriculum

| ID | Deliverable | Point | Dependency | Reviewer |
|---|---|---:|---|---|
| KH-01 | Live MySQL INFORMATION_SCHEMA audit, 3 skipped tests, EXPLAIN baseline | 6 | Test database | Tuấn Tài |
| KH-02 | Review schema 38 bảng, RBAC/evidence composite FK và migration plan | 5 | DEC-013–018 review | Tuấn Tài |
| KH-03 | Audit/hoàn thiện Subject, Knowledge Graph và Curriculum use cases/UI | 7 | TH-01 insights | Sơn |
| KH-04 | DAG/relational/query-plan MySQL integration tests | 5 | KH-03 | Tuấn Tài |
| KH-05 | Review TT-02 migration/backfill/rollback | 4 | TT-02 | Tuấn Tài |
| KH-06 | Review recommendation prerequisites/tie-break/data provenance | 3 | SO-03 | Sơn |

Khoa cần Sơn review thuật toán graph/recommendation và Tuấn Tài review tenant/security. Không thêm table để đạt số lượng; mọi table phải có use case và constraint.

### 5.4. Thành Tài — Question, Assignment và Student Learning

| ID | Deliverable | Point | Dependency | Reviewer |
|---|---|---:|---|---|
| TA-01 | Audit Question/Assignment workflow theo stakeholder và contract | 5 | TH-01 | Thịnh |
| TA-02 | Hoàn thiện Question Bank/Assignment backend và capability cutover | 7 | TT-03; KH-03 | Khoa |
| TA-03 | Learning Player: idempotent submit, poll, terminal/fallback/review states | 7 | TA-02; SO-01 | Thịnh |
| TA-04 | Assignment/Attempt E2E, direct URL và handcrafted-request security | 6 | TA-03; TT-04 | Tuấn Tài |
| TA-05 | Review RBAC UI/accessibility/responsive | 3 | TH-03–04 | Thịnh |
| TA-06 | Review CI/release demo script | 2 | TT-06 | Tuấn Tài |

Thành Tài cần Khoa xác nhận question/node relations, Sơn cung cấp evidence status contract và Thịnh kiểm UX. Student UI không lộ correct answer hoặc raw AI payload.

### 5.5. Sơn — AI Evidence, Twin và Recommendation

| ID | Deliverable | Point | Dependency | Reviewer |
|---|---|---:|---|---|
| SO-01 | EvidenceAssessment schema contract + pure deterministic Evidence Gate/tests | 7 | KH-02; migration approved | Khoa |
| SO-02 | Completion orchestrator, Behavior updater, Mastery remediation, history/idempotency | 7 | SO-01 | Tuấn Tài |
| SO-03 | Risk/Opportunity/Recommendation deterministic baseline + explanation templates | 6 | SO-02; KH-03 | Khoa |
| SO-04 | Review queue, override/replay và forced-AI-outage tests | 5 | SO-01–03 | Thành Tài |
| SO-05 | ML.NET feasibility/evaluation record; triển khai chỉ khi gate đạt | 2 | Dataset/label approved | Khoa |
| SO-06 | Review auth/evidence transaction boundaries | 3 | TT-03; SO-02 | Tuấn Tài |

Sơn không để Gemini trực tiếp chấm điểm cuối, thay đổi Mastery hoặc chọn recommendation. Cần Tuấn Tài review transaction/concurrency, Khoa review dữ liệu/thuật toán và Thành Tài kiểm consumer UI.

Để tránh một người trở thành critical path duy nhất, SO-01/02 không được bắt đầu nếu Khoa/Tuấn Tài chưa cùng walkthrough schema, same-attempt constraint và Essay-null policy; SO-03/04 phải có Khoa hoặc Thành Tài làm co-owner test/demo trong tuần. Nếu Sơn bị block quá hai ngày làm việc hoặc vượt giờ khả dụng đã khai báo, Team Lead tách package theo Evidence, Orchestrator và UI thay vì dồn thêm việc cho cùng owner.

## 6. Dependency map

~~~text
All → R00 course repo/docs
All + Thịnh lead → R01 interview/Figma
Khoa KH-01/KH-02 + Tuấn Tài → R02 schema audit
Tuấn Tài TT-02/03/04 → R03 RBAC backend
Thịnh TH-03/04 → R04 RBAC UI
Sơn SO-01 → R05 Evidence Gate
Sơn SO-02 + Tuấn Tài review → R06 Twin orchestrator
Sơn SO-03 + Khoa review → R07 Recommendation/ML decision
Thịnh + Thành Tài → R08 dashboards/end-to-end UX
All + Tuấn Tài integration → R09 hardening/report/demo
~~~

Không bắt đầu package downstream nếu upstream contract chưa approved hoặc migration chưa apply/test.

## 7. Review chéo bắt buộc

| Loại thay đổi | Primary reviewer | Second reviewer khi rủi ro cao |
|---|---|---|
| Tenant/RBAC/token | Khoa | Sơn hoặc Thịnh cho consumer impact |
| Migration/FK/index | Tuấn Tài | Owner module liên quan |
| UI/capability/accessibility | Thành Tài | Tuấn Tài cho security |
| Knowledge/Curriculum/Question relations | Khoa | Sơn cho algorithm impact |
| Attempt/Evidence/Twin transaction | Tuấn Tài | Khoa |
| AI parser/fallback | Sơn | Thành Tài |
| Recommendation/ML evaluation | Khoa | Tuấn Tài |
| Documentation/course evidence | Thịnh | Một thành viên không phải tác giả |

Reviewer phải nêu finding có file/line/invariant/tác động. “LGTM” không có bằng chứng không đủ cho critical package.

## 8. Handoff giữa các thành viên

Mỗi handoff phải ghi:

- requirement IDs và trạng thái xác nhận;
- branch/full HEAD và exact diff;
- schema/API version;
- migration apply/rollback/validation;
- test command + result;
- known issue/blocker;
- consumer cần làm gì tiếp;
- ai là reviewer và deadline.

Ví dụ: Sơn bàn giao Evidence Gate cho Thành Tài phải cung cấp enum/DTO terminal state, fallback wording, sample payload và test; không chỉ nói “API xong”.

## 9. Nhịp làm việc tuần

1. Đầu tuần: giảng viên/stakeholder input → requirement IDs.
2. Planning 30–45 phút: chọn package Ready, point, owner, reviewer, deadline.
3. Giữa tuần: demo nhỏ; blocker quá 24 giờ phải báo.
4. Trước PR: owner tự chạy gate và giải thích diff.
5. Review chéo; remediation là commit mới, không che lịch sử.
6. Cuối tuần: demo, cập nhật PROJECT_TRACKING, effort thực tế, evidence và kế hoạch tái cân bằng.

## 10. Definition of Ready/Done cho cá nhân

Ready khi có requirement, dependency, contract, allow-list, acceptance, test, owner và reviewer.

Done khi:

- acceptance quan sát được đã đạt;
- build/test/MySQL/frontend gate phù hợp đã chạy;
- không cross-tenant/permission drift;
- docs/traceability cập nhật;
- reviewer độc lập approve;
- owner demo và trả lời được “vì sao thiết kế như vậy”;
- commit/PR được liên kết trong weekly tracking.

## 11. Tính contribution cuối kỳ

Áp dụng trọng số gợi ý trong PROJECT_TRACKING.md: analysis 15%, implementation 35%, verification 25%, review/integration 15%, docs/demo 10%. Mỗi evidence ghi owner và reviewer; AI tool chỉ ghi ở AI-assisted work record.

Không chốt phần trăm trước khi có:

- tổng accepted point thực tế;
- chất lượng/độ khó sau review;
- contribution ở review/integration;
- mức hoàn thành đúng deadline;
- xác nhận của cả năm thành viên.

## 12. Cơ chế tái cân bằng và xung đột

- Package lớn hơn 8 point phải tách.
- Blocker do dependency không trừ contribution của người bị chặn nếu đã báo đúng hạn.
- Người rảnh hỗ trợ bằng pair-review/test có evidence, không chiếm author của owner.
- Nếu không thống nhất, liệt kê package có point gần bằng nhau và bốc thăm theo hướng dẫn giảng viên.
- Tranh chấp contribution dùng requirement/PR/test/review/demo evidence, không dùng cảm nhận hoặc số commit đơn thuần.

## 13. Việc con người phải điền trước khi freeze

- Mã SV, GitHub, kỹ năng, giờ/tuần của cả năm người.
- Lịch học, deadline và tuần sprint.
- Thành viên chấp nhận/điều chỉnh từng primary area.
- Reviewer cho package tuần đầu.
- Course repository và baseline import SHA.
- Cách quy đổi effort point sang bảng tỷ lệ cuối kỳ được cả nhóm đồng ý.
