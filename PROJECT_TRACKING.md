# EduTwin — Course Tracking, Weekly Requirements and Contribution

> Phiên bản: 1.2-draft
> Trạng thái: ACTIVE TRACKING
> Cập nhật gần nhất: 2026-09-11
> Quy tắc: không xóa lịch sử tuần; sửa sai bằng một entry đính chính mới
> Chủ sở hữu: Team lead; mọi thành viên xác nhận phần việc của mình

## 1. Mục đích

Tài liệu này là hồ sơ quá trình của đồ án:

- yêu cầu giảng viên theo tuần;
- decision và assumption;
- baseline trước môn học;
- kế hoạch năm thành viên;
- tiến độ, blocker và risk;
- commit/PR/test/Figma làm bằng chứng;
- cơ sở xác định tỷ lệ đóng góp.

Đây không phải nơi mô tả chi tiết table hoặc endpoint. Các nội dung đó thuộc DATABASE_SCHEMA.md và API_CONTRACTS.md.

## 2. Course metadata

| Trường | Giá trị |
|---|---|
| Môn học | TBD |
| Lớp/nhóm học phần | TBD |
| Giảng viên | TBD |
| Nhóm | 5 thành viên |
| Tuần bắt đầu | Week 1 — ngày TBD |
| Deadline cuối kỳ | TBD |
| Course repository | TBD |
| Prototype repository | nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform |
| Pre-course source-code snapshot | 2d768f270e0395bcafcbcab2305ac3617fb5f9ca |
| Documentation rebaseline checkpoint | b14f6c4171dc55043a3bb910332061c55ba66a7f |
| Approved import snapshot | TBD — khóa sau docs review/correction |
| Course initial-import commit | TBD — tạo trong repository môn học |

## 3. Baseline provenance

Ba mốc phải được ghi riêng: source-code snapshot, documentation rebaseline/approved import snapshot và initial-import commit trong repository môn học. Repository môn học phải có commit đầu:

~~~text
chore: import transparent pre-course EduTwin baseline

Source repository:
nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform

Source commit:
TBD approved import snapshot (phải truy ngược được source code 2d768f2 và documentation checkpoint b14f6c4)

This commit contains work completed before the course.
Course contribution starts after this baseline commit.
~~~

Không cherry-pick documentation rebaseline sau initial import rồi tính thành đóng góp học kỳ. Khi nhóm khóa snapshot, điền full SHA approved import và SHA initial-import ở metadata; giữ `2d768f2` để truy vết code và `b14f6c4` để truy vết điểm bắt đầu rebaseline tài liệu.

Không:

- squash baseline thành công việc mới;
- chia baseline thành commit giả của năm thành viên;
- thay author để tạo contribution giả;
- xóa repository prototype;
- tuyên bố feature cũ là tiến độ tuần mới.

## 4. Baseline verification ngày 2026-09-08

| Gate | Kết quả |
|---|---|
| Branch | feat/center-organization |
| HEAD | 2d768f2 — feat(twin): add mastery v1 calculator |
| Working tree trước audit | Clean |
| .NET restore | Pass |
| Release build | Pass, 0 warning, 0 error |
| Full .NET tests | 2.853 pass, 3 skip, 0 fail |
| Frontend production build | Pass |
| EF pending model changes | None |
| Migration-generated tables | 31 |
| Indexes | 75 |
| Foreign keys | 66 |
| Check constraints | 59 |
| Live MySQL schema audit | PENDING — Docker CLI/connection chưa sẵn sàng trên máy audit |
| MySQL integration tests | 3 skipped vì thiếu EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING |

Không được dùng kết quả test --no-build từ binary cũ nếu build hiện tại thất bại. Trong lần audit này, dependency đã được restore và build lại thành công trước khi chốt số liệu.

## 5. Trạng thái implementation tại baseline

| Module | Trạng thái | Ghi chú |
|---|---|---|
| Governance/spec v1 | IMPLEMENTED nhưng cần re-baseline | Tài liệu cũ không phản ánh course/RBAC |
| Solution/Docker/data | IMPLEMENTED | Live MySQL audit còn thiếu |
| Auth/tenant | IMPLEMENTED | Role policy vẫn hard-code |
| Organization | IMPLEMENTED đáng kể | Teacher/Student/Class/Subject |
| Knowledge Graph | IMPLEMENTED đáng kể | CRUD/DAG/UI nền |
| Curriculum/Question | IMPLEMENTED đáng kể | Backend và UI nền |
| Assignment | IMPLEMENTED đáng kể | Draft/publish/student query |
| Attempt/AI Job | IMPLEMENTED | Transaction, lease, polling |
| Gemini analysis | IMPLEMENTED | Validation/retry/fallback/observability |
| Mastery Calculator | IMPLEMENTED v1 pure function | Chưa có Evidence Gate |
| Dynamic RBAC | NOT STARTED | Không có table/API/UI |
| Evidence Gate | NOT STARTED | analysis_confidence chưa điều tiết mastery |
| Twin Orchestrator | NOT STARTED/INCOMPLETE | Runtime gần như dừng ở ReasoningAnalysis |
| Recommendation pipeline | INCOMPLETE | Schema có, orchestration chưa xong |
| Final dashboards/release | INCOMPLETE | Cần contract/security/E2E |

### 5.1. Trạng thái kỹ thuật hiện tại của prototype — 2026-09-11

Mục 4–5 phía trên là snapshot lịch sử ngày 2026-09-08 và không được sửa lại để giả rằng tính năng đã tồn tại tại baseline. Bảng dưới đây ghi tiến triển kỹ thuật mới trên repository prototype/personal; các commit này không tự động được tính là đóng góp học kỳ và phải được import/re-verify minh bạch nếu nhóm tạo course repository.

| Roadmap | Trạng thái kỹ thuật | Requirement/decision | Bằng chứng source | Verification cục bộ | Acceptance còn lại |
|---|---|---|---|---|---|
| R05 — Evidence Gate và AI safety | TECHNICALLY VERIFIED / FROZEN | DEC-009, DEC-010, DEC-016, DEC-019, DEC-020; MASTER_PLAN R05 | `9636e5b`, `1d84980`, final freeze `52378b8` (`52378b82ef685d4f67886b7bfe44867c2ac30b5b`) | Release build 0 warning/error; full suite 3.014 pass + 9 MySQL tests chạy trên MySQL thật pass; EF model synchronized | Group/course-repository review và CI evidence |
| R06 — Twin completion orchestrator | TECHNICALLY VERIFIED / FROZEN | DEC-009, DEC-016, DEC-017, DEC-019; MASTER_PLAN R06 | `9636e5b`, `1d84980`, final freeze `52378b8` (`52378b82ef685d4f67886b7bfe44867c2ac30b5b`) | Targeted R06 pass; MySQL R05/R06 9/9; full suite 3.014 pass; failure/concurrency/replay/tri-state coverage pass | Group/course-repository review và CI evidence |
| R07 — Recommendation và quyết định ML | TECHNICALLY VERIFIED / FROZEN | DEC-011; MASTER_PLAN R07/P14 | Base `b6f3edb`, hardening `7d63a73`, remediation verified | Release build 0 warning/error; recommendation/query-filter 45 pass + 10 MySQL pass; full .NET 3.048 pass + 9 skip; web 8 pass + production build; EF no model drift; direct scan clean; ML readiness N=0/NO-GO | Group/course-repository review và CI evidence |

Các invariant được chốt trong closeout:

- Behavior calibration và Teacher Override replay dùng cùng nguồn effective correctness: `reasoning_analysis.override_is_correct ?? attempt.is_correct`.
- Replay history lưu `calculation_version = replay-v1`, replay summary và dữ liệu từng step; không trình bày kết quả replay nhiều attempt như một phép tính mastery đơn.
- Tri-state correctness (`bool?`) được bảo toàn xuyên suốt từ `MasteryCalculationInput`, `MasteryCalculator` đến `ReplayStepBreakdown` / history: positive-weight + null correctness fail closed; zero-weight + null correctness giữ nguyên `null`, không coerce thành `false`, mastery delta = 0.
- Raw AI observation không bị Teacher Override ghi đè; preliminary grading provenance trên Attempt được giữ nguyên.
- Các cột override trong `reasoning_analyses` biểu diễn human override hiện hành và được bảo vệ bằng optimistic versioning.
- `evidence_assessments` và `twin_update_history` append-only giữ policy/replay lineage, nhưng chưa snapshot đầy đủ payload của mọi phiên bản override. Nếu nghiên cứu/audit yêu cầu khôi phục nguyên văn từng override cũ, phải có Change Proposal cho bảng append-only riêng; không được tuyên bố khả năng này ở phiên bản hiện tại.
- Các số test trên (3.014 .NET tests, 9 MySQL tests, 8 web tests) là log local execution đã chạy lại, chưa phải GitHub Actions/CI attestation.

## 6. Lecturer requirement log

### Week 1 — ngày TBD

Nguồn: bản ghi .Net.m4a, Bản ghi mới 17.m4a và Bản ghi mới 18.m4a; transcript do thành viên cung cấp.

| ID | Nội dung ghi nhận | Tác động | Action |
|---|---|---|---|
| W01-01 | Khảo sát khách hàng/người dùng trước khi chốt chức năng | Requirements | Tạo interview/Figma validation |
| W01-02 | Có tài liệu đặc tả actor, chức năng, quy trình và quyền | Docs | PROJECT_REQUIREMENTS + API/UI spec |
| W01-03 | Dùng Figma và lấy xác nhận trước khi code UI | UX | Tạo prototype/review log |
| W01-04 | Phân quyền linh động, có màn hình cấp quyền | Architecture | Dynamic RBAC theo center |
| W01-05 | Dùng .NET, EF Core, LINQ, MVC hoặc Web API | Stack | Giữ ASP.NET Core Web API |
| W01-06 | Database hơn 20 bảng và ràng buộc chặt | Data | Audit 31 bảng, bổ sung RBAC đúng nhu cầu |
| W01-07 | GitHub có đủ thành viên và theo dõi quá trình | Governance | Course repo + transparent baseline |
| W01-08 | Báo cáo tiến độ hằng tuần | Tracking | Dùng template ở mục 11 |
| W01-09 | Phân công xét độ khó và thống nhất thời hạn | Teamwork | Weighted work package |
| W01-10 | Báo cáo cuối có tỷ lệ đóng góp | Assessment | Contribution evidence |
| W01-11 | Tuấn Tài báo lại rằng AI/ML chất lượng có thể đạt điểm cao; chưa có đoạn transcript tương ứng trong ba bản hiện lưu | Product/provenance | Giữ Evidence-first AI, ML decision gate; bổ sung timestamp/transcript hoặc biên bản xác nhận |

Khi giảng viên bổ sung yêu cầu, thêm một dòng mới với tuần/ngày/nguồn. Không sửa entry cũ để làm mất lịch sử.

## 7. Decision log

| ID | Ngày | Decision | Trạng thái |
|---|---|---|---|
| DEC-001 | 2026-09-08 | Giữ EduTwin, không rewrite prototype | APPROVED |
| DEC-002 | 2026-09-08 | Tạo course repository mới và import baseline minh bạch | APPROVED |
| DEC-003 | 2026-09-08 | Giữ ASP.NET Core Web API + React; không bắt buộc Razor Pages | APPROVED |
| DEC-004 | 2026-09-08 | Database audit trước; không thêm table chỉ để tăng số lượng | APPROVED |
| DEC-005 | 2026-09-08 | Dynamic RBAC chỉ trong từng center; không có Platform Admin | APPROVED |
| DEC-006 | 2026-09-08 | Permission catalog do hệ thống định nghĩa; CenterManager tạo role và gán permission | APPROVED |
| DEC-007 | 2026-09-08 | Tenant/ownership guard vẫn bắt buộc sau RBAC | APPROVED |
| DEC-008 | 2026-09-08 | Không dùng legacy-role OR new-permission trong migration | APPROVED |
| DEC-009 | 2026-09-08 | AI là evidence source; deterministic BLL sở hữu Twin/recommendation | APPROVED |
| DEC-010 | 2026-09-08 | Chèn Evidence Gate giữa ReasoningAnalysis và Twin | APPROVED |
| DEC-011 | 2026-09-08 | ML.NET chỉ triển khai sau feasibility/evaluation gate | APPROVED |
| DEC-012 | 2026-09-08 | Phân công theo vertical business module và weighted work package | APPROVED |
| DEC-013 | 2026-09-08 | Role/permission/user-role phải tương thích account type và được khóa bằng quan hệ chuẩn hóa/composite FK | PROPOSED — cần nhóm review trước migration |
| DEC-014 | 2026-09-09 | Center chỉ được provision bằng seed/migration/deployment; course MVP không có Platform Admin hoặc UI/API tạo/xóa Center | USER-APPROVED — group review pending |
| DEC-015 | 2026-09-09 | JSON authorizationVersion và JWT auth_version dùng duy nhất users.auth_version; mutation quyền phải bump/revoke atomically | USER-APPROVED — group review pending |
| DEC-016 | 2026-09-09 | Evidence tách source/trust/decision mode; Replay chỉ là history event | USER-APPROVED — group review pending |
| DEC-017 | 2026-09-09 | AI là enhancement không đáng tin cậy; core learning/management và fallback phải chạy không cần Gemini/Internet | USER-APPROVED — group review pending |
| DEC-018 | 2026-09-09 | Phân công hiện hành dùng effort points, dependency và review chéo trong TEAM_ASSIGNMENT.md; không suy ra 20% từ tên module | PROPOSED — cần năm thành viên xác nhận kỹ năng/giờ |
| DEC-019 | 2026-09-09 | Essay/preliminary isCorrect null là ReviewOnly; Mastery chỉ đổi sau Teacher HumanConfirmed + replay | DOCUMENTED — group review pending |
| DEC-020 | 2026-09-09 | Evidence analysis và supersession phải cùng Attempt bằng BLL invariant + composite FK/MySQL integration test | DOCUMENTED — group review pending |
| DEC-021 | 2026-09-09 | User rowVersion chống lost update; authorizationVersion/auth_version chỉ làm stale token/cache | DOCUMENTED — group review pending |
| DEC-022 | 2026-09-09 | Tách source snapshot 2d768f2, docs checkpoint b14f6c4, approved import snapshot và course initial-import SHA | DOCUMENTED — group review pending |

## 8. Team roster

Tên do Tuấn Tài cung cấp; các trường còn lại không được tự bịa.

| Member | Họ tên | Mã SV | GitHub | Kỹ năng mạnh | Cần hỗ trợ | Giờ/tuần |
|---|---|---|---|---|---|---:|
| A | Tuấn Tài | TBD | TBD | TBD | TBD | TBD |
| B | Thịnh | TBD | TBD | TBD | TBD | TBD |
| C | Khoa | TBD | TBD | TBD | TBD | TBD |
| D | Thành Tài | TBD | TBD | TBD | TBD | TBD |
| E | Sơn | TBD | TBD | TBD | TBD | TBD |

## 9. Ownership map đề xuất ngày 2026-09-08

Bảng A–E dưới đây là đề xuất lịch sử trước khi có tên thành viên. Nó không phải phân công cuối và được TEAM_ASSIGNMENT.md bản 2026-09-09 thay thế cho công việc hiện hành.

Đây là ownership, chưa phải tỷ lệ điểm cuối.

| Owner | Vertical |
|---|---|
| A | Identity, tenant, Dynamic RBAC |
| B | Knowledge Graph, Subject, Curriculum |
| C | Question Bank, Assignment |
| D | Attempt, Submission, AI Reasoning, Evidence |
| E | Digital Twin, Risk, Recommendation, ML feasibility |

Mỗi người phải tham gia ít nhất:

- requirement/use case;
- database hoặc data contract;
- BLL/API;
- UI hoặc integration;
- tests;
- documentation/demo.

Không phân chia cố định A chỉ database, B chỉ frontend, C chỉ tài liệu.

## 10. Initial work packages

Điểm effort là tương đối và phải review sau mỗi hai tuần.

| WP | Owner | Mô tả | Effort | Dependencies | Status |
|---|---|---|---:|---|---|
| WP-GOV-01 | Shared | Course repo, baseline, docs re-baseline | 5 | None | IN PROGRESS |
| WP-A01 | A | RBAC schema/migration/backfill | 7 | WP-GOV-01 | NOT STARTED |
| WP-A02 | A | Permission evaluator và migration strategy | 8 | WP-A01 | NOT STARTED |
| WP-A03 | A | Role/permission API | 6 | WP-A02 | NOT STARTED |
| WP-A04 | A | Role admin UI | 6 | WP-A03 | NOT STARTED |
| WP-A05 | A | Privilege-escalation/integration tests | 7 | WP-A02 | NOT STARTED |
| WP-B01 | B | Audit Knowledge/Curriculum against stakeholder needs | 4 | WP-GOV-01 | NOT STARTED |
| WP-B02 | B | Close content contract/UI gaps | 7 | WP-B01 | NOT STARTED |
| WP-B03 | B | Graph/curriculum relational and query-plan tests | 7 | WP-B02 | NOT STARTED |
| WP-C01 | C | Audit Question/Assignment workflows | 4 | WP-GOV-01 | NOT STARTED |
| WP-C02 | C | Complete Question/Assignment UI and permissions | 8 | WP-A02, WP-C01 | NOT STARTED |
| WP-C03 | C | Publish/idempotency/target integration tests | 7 | WP-C02 | NOT STARTED |
| WP-D01 | D | Evidence Gate contract/schema | 6 | WP-GOV-01 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-D02 | D | Deterministic Evidence Gate | 8 | WP-D01 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-D03 | D | Review queue/override/replay integration | 8 | WP-D02 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-D04 | D | Malformed/low-confidence/fallback tests | 7 | WP-D02 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-E01 | E | Twin Orchestrator and Behavior updater | 9 | WP-D02 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-E02 | E | Risk and Twin history | 7 | WP-E01 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-E03 | E | Opportunity Gap/Recommendation | 9 | WP-E02 | PROTOTYPE VERIFIED; COURSE REPLAY PENDING |
| WP-E04 | E | Twin/recommendation UI | 7 | WP-E03 | NOT STARTED |
| WP-E05 | E/Shared | ML.NET feasibility report | 4 | Dataset available | PROTOTYPE VERIFIED; NO-GO DOCUMENTED |
| WP-REL-01 | Shared | E2E, performance, accessibility and demo | 8 | Core WP | NOT STARTED |

Nếu effort mỗi thành viên lệch lớn, trưởng nhóm phải chuyển work package hoặc thêm reviewer/co-owner. Không dùng bảng này để mặc định 20% × 5.

### 10.1. Nguồn phân công hiện hành

TEAM_ASSIGNMENT.md là nguồn chi tiết về owner, reviewer, dependency, điểm khó, cách hỗ trợ và acceptance. PROJECT_TRACKING.md chỉ ghi kết quả thật theo tuần để tránh hai bảng phân công cùng được sửa song song.

## 11. Weekly report template

### Week N — YYYY-MM-DD đến YYYY-MM-DD

#### Yêu cầu/feedback mới từ giảng viên hoặc stakeholder

| Source | Requirement/feedback | Decision | Requirement IDs |
|---|---|---|---|
| TBD | TBD | TBD | TBD |

#### Kế hoạch

| Member | WP/Task | Deliverable | Deadline | Effort |
|---|---|---|---|---:|
| TBD | TBD | TBD | TBD | TBD |

#### Kết quả

| Member | Hoàn thành | Commit/PR | Test/Figma/Evidence | Status |
|---|---|---|---|---|
| TBD | TBD | TBD | TBD | TBD |

#### Chỉ số

- Tổng work package hoàn thành: TBD.
- Phần trăm theo effort: TBD.
- Build/test state: TBD.
- Requirements verified: TBD.

#### Blocker/risk

| ID | Mô tả | Owner | Next action | Due |
|---|---|---|---|---|
| TBD | TBD | TBD | TBD | TBD |

#### Điều chỉnh tuần sau

- TBD.

## 12. Contribution evidence

Contribution không chỉ là số commit hoặc số dòng.

| Nhóm evidence | Ví dụ | Trọng số gợi ý |
|---|---|---:|
| Requirement/analysis | Interview, use case, acceptance, ADR | 15% |
| Implementation | DB/BLL/API/UI code có chất lượng | 35% |
| Verification | Test, migration audit, security/performance | 25% |
| Review/integration | Review chéo, conflict resolution, integration | 15% |
| Documentation/demo | Docs đúng, Figma, slide, demo ownership | 10% |

Tỷ lệ cuối phải dựa trên work package đã nghiệm thu và bằng chứng, sau đó được cả nhóm review. Không tự động lấy GitHub contribution graph làm điểm.

## 13. AI-assisted work record

Mỗi task dùng AI ghi:

| Field | Nội dung |
|---|---|
| Human owner | Người chịu trách nhiệm |
| AI tool | Codex/Gemini/khác |
| Use | Phân tích/implementation/review/test |
| Prompt/task ID | Link hoặc nội dung tóm tắt |
| Human verification | Diff/test/decision đã kiểm tra |
| Commit/PR | Evidence |

Không:

- để AI quyết định requirement thay stakeholder;
- commit code không hiểu;
- chạy lặp vô hạn;
- giả author hoặc contribution;
- giấu baseline cũ.

## 14. Risk register

| ID | Risk | Probability | Impact | Mitigation | Owner |
|---|---|---|---|---|---|
| R-001 | Dynamic RBAC gây privilege escalation hoặc gán chéo account type | Medium | Critical | Additive migration, normalized applicability, composite FK, no OR, security tests | Tuấn Tài |
| R-002 | Cross-tenant data leak | Low/Medium | Critical | Filter + composite FK + guard + tests | Tuấn Tài/Khoa |
| R-003 | AI hợp lệ cú pháp nhưng phân tích sai | High | High | Evidence Gate + review/override | Sơn |
| R-004 | Gemini/internet unavailable khi demo | High | High | Durable job + fallback + offline demo | Sơn/Thành Tài |
| R-005 | Twin/recommendation quá tải một thành viên | High | High | Split WP, co-owner/rebalance | Lead |
| R-006 | Requirements được viết ngược từ code | Medium | High | Source/status/evidence mandatory | Lead |
| R-007 | Database spec khác migration/MySQL thật | Medium | High | Five-layer audit + INFORMATION_SCHEMA | Khoa/Tuấn Tài |
| R-008 | Tests xanh bằng InMemory nhưng fail MySQL | Medium | High | Mandatory MySQL integration CI | Shared |
| R-009 | Commit nhiều nhưng đóng góp thấp | Medium | Medium | Weighted deliverable evidence | Lead |
| R-010 | AI-generated code không thành viên nào giải thích được | Medium | High | Human owner demo/review gate | All |
| R-011 | Tài liệu drift với source | High | High | Traceability + docs gate | All |
| R-012 | Thêm ML khi thiếu dataset/metric | Medium | Medium | ML decision gate | Sơn/Khoa |

## 15. Immediate gates

1. Hoàn tất và review documentation re-baseline.
2. Điền course metadata, mã SV/GitHub/kỹ năng/giờ khả dụng và để cả năm thành viên xác nhận TEAM_ASSIGNMENT.md.
3. Tạo transparent course repository.
4. Khảo sát stakeholder và Figma.
5. Chạy live MySQL schema/integration audit.
6. Nhóm review DEC-013 đến DEC-018, sau đó freeze RBAC/Evidence schema và API.
7. Chỉ sau đó bắt đầu migration/source tasks.

## 16. Change policy

- Không xóa entry tuần/decision/risk đã được dùng làm bằng chứng.
- Đính chính bằng entry mới có link về entry cũ.
- Mỗi trạng thái Done phải có evidence.
- Mỗi tuần trưởng nhóm và thành viên xác nhận bảng phân công.
- Các thay đổi requirement phải cập nhật PROJECT_REQUIREMENTS.md trước task implementation.

## 17. Documentation change log

Append-only; sửa sai bằng entry mới, không xóa entry đã dùng làm evidence.

| Ngày | Version | Files/phạm vi | Lý do | Human owner | Trạng thái/evidence |
|---|---|---|---|---|---|
| 2026-07-15 | 1.0 | CONSTITUTION, DATABASE_SCHEMA, API_CONTRACTS, MASTER_PLAN, PROMPT_TEMPLATES | Prototype technical baseline | Prototype team | Historical source baseline |
| 2026-09-08 | 2.0-draft | Toàn bộ bộ tài liệu authoritative | Course rebaseline, dynamic RBAC, Evidence Gate, team tracking | Nhóm EduTwin | Working tree; chờ group review/commit |
| 2026-09-08 | archive-v0 | docs/archive/vision-v0/EduTwin-Overview.docx | Bảo tồn vision Next.js/FastAPI/Supabase/PostgreSQL cũ, không còn authority | Nhóm EduTwin | Archived, không xóa |
| 2026-09-09 | 2.1-draft | Requirements/core specs/tracking + TEAM_ASSIGNMENT + CODEBASE_CHANGE_PLAN + DOCX hiện hành | Làm rõ AI không phải operational core, Center provisioning, users.auth_version, last-admin, evidence ba chiều, Week 1 và phân công năm người | Tuấn Tài/nhóm EduTwin | Working tree; chờ group review/commit |

Khi chuyển 2.1-draft sang APPROVED/FROZEN, thêm entry ghi reviewer, commit/PR và ngày nhóm xác nhận.
