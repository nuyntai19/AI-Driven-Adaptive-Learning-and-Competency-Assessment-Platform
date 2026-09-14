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
| R07 — Recommendation và quyết định ML | TECHNICALLY VERIFIED / FROZEN | DEC-011; MASTER_PLAN R07/P14 | Base `b6f3edb`, hardening `7d63a73`, deadlock-fix `5416d92`, distinct-concurrency verified | Release build 0 warning/error; recommendation/query-filter 45 pass + 10 MySQL pass; full .NET 3.058 pass + 0 fail + 0 skip (20/20 live-MySQL tests pass); web 8 pass + production build; EF no model drift; direct scan clean; ML readiness N=0/NO-GO | Group/course-repository review và CI evidence |
| R08 — Dashboard và end-to-end UX | TECHNICALLY VERIFIED / UX ACCEPTANCE PENDING | MASTER_PLAN R08/P15–P17; UI_UX_SPEC mục 14–18 | `1749e78`, `13bf809`, `ccd017f`, `0f78937`, `c3827f5`, `8d9eb5b`, `dee01b3`, `16c4604`, `6d0694a`, và deterministic timestamp hardening | Release build 0 warning/error (.NET 10); full suite 3.058 unit + 25/25 live MySQL tests pass (tổng 3.083 backend tests); web 17/17 pass, lint sạch, production build sạch; EF no model drift; Learning Player submit → URL persistence → polling → FallbackCompleted → feedback replayed và verified trên browser thật; UInt64 string boundary verified | Chưa đủ UX Done: toàn bộ Figma URL/reviewer/date/status còn TBD/PENDING; chưa có stakeholder feedback/approval bền vững trong repo; các số test là local execution trên MySQL Docker thật, chưa phải GitHub Actions/CI attestation |

Các invariant được chốt trong closeout:

- Behavior calibration và Teacher Override replay dùng cùng nguồn effective correctness: `reasoning_analysis.override_is_correct ?? attempt.is_correct`.
- Replay history lưu `calculation_version = replay-v1`, replay summary và dữ liệu từng step; không trình bày kết quả replay nhiều attempt như một phép tính mastery đơn.
- Tri-state correctness (`bool?`) được bảo toàn xuyên suốt từ `MasteryCalculationInput`, `MasteryCalculator` đến `ReplayStepBreakdown` / history: positive-weight + null correctness fail closed; zero-weight + null correctness giữ nguyên `null`, không coerce thành `false`, mastery delta = 0.
- Raw AI observation không bị Teacher Override ghi đè; preliminary grading provenance trên Attempt được giữ nguyên.
- Các cột override trong `reasoning_analyses` biểu diễn human override hiện hành: `OverrideVersion` đóng vai trò là business optimistic-version counter cho override, trong khi concurrency protection của EF Core được đảm bảo bởi `ReasoningAnalysis.RowVersion` (cột `row_version` được đánh dấu `IsConcurrencyToken()`).
- `recommendation_generation_states.last_outcome` là generation watermark lưu các outcome sinh khuyến nghị (`Generated`, `NoCandidate`, `Blocked`); hành động `Accepted` / `Dismissed` là lifecycle action riêng của Recommendation và không ghi đè giá trị `last_outcome` của generation watermark.
- Transaction A trong production (`AIAnalysisJobProcessor` và `TeacherOverrideUseCase`) vận hành theo optimistic concurrency model thuần túy (không lấy pessimistic `Student` row lock); bảo vệ chống lost update hoàn toàn dựa vào EF Core concurrency tokens (`RowVersion`) trên `KnowledgeTwin`, `BehaviorTwin`, `StudentTwin`, `Attempt` và `AIAnalysisJob`. Khi có race giữa hai job/attempt cùng học sinh, transaction thua nhận `DbUpdateConcurrencyException`, rollback an toàn thành `LostRace`, và quy trình background recovery sau khi hết hạn lease (`CandidateDiscovery` -> `RecoverExpiredLease` -> `Claim` -> `AIAnalysisJobProcessor`) thành công đưa toàn bộ evidence vào Digital Twin mà không gây duplicate hay treo job `Processing`.
- `evidence_assessments` và `twin_update_history` append-only giữ policy/replay lineage, nhưng chưa snapshot đầy đủ payload của mọi phiên bản override. Nếu nghiên cứu/audit yêu cầu khôi phục nguyên văn từng override cũ, phải có Change Proposal cho bảng append-only riêng; không được tuyên bố khả năng này ở phiên bản hiện tại.
- Các số test trên (3.058 .NET tests, 20 MySQL tests, 8 web tests) là log local execution đã chạy lại và verify trên MySQL Docker thật, chưa phải GitHub Actions/CI attestation.

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
| 2026-09-12 | 3.0-post-r08 | ADR-POST-R08-PLATFORM, ADR-POST-R08-MATH, MASTER_PLAN (Mục 132), CONSTITUTION (Mục 3.3), PROJECT_REQUIREMENTS, API_CONTRACTS, UI_UX_SPEC, DATABASE_SCHEMA (Bảng 40), PROJECT_TRACKING | Post-R08 Scope Amendments: Quản trị nền tảng (Track 1) và Bộ công cụ Toán học & Minh chứng đa phương thức (Track 2) theo phê duyệt Plan v3.1.3-ADDENDUM-03 | Nhóm EduTwin | Gate 1 Initial Baseline; zero code change |
| 2026-09-12 | 3.0.1-gate1-corrective | ADR-POST-R08-PLATFORM, ADR-POST-R08-MATH, MASTER_PLAN, CONSTITUTION, PROJECT_REQUIREMENTS, API_CONTRACTS, UI_UX_SPEC, DATABASE_SCHEMA, PROJECT_TRACKING | Hiệu chỉnh đồng bộ Gate 1 theo Plan v3.1.3-ADDENDUM-03: chuẩn hóa create-center fields khớp domain model, endpoint đặt lại mật khẩu /centers/{centerId}/managers/{managerUserId}/reset-password với quyền platform.managers.manage và expectedUserRowVersion, yêu cầu Student + learning.attempts.submit, loại bỏ trạng thái Attempt tự phát sinh (khóa 5 trạng thái chuẩn: PendingAnalysis, Processing, Completed, NeedsTeacherReview, AnalysisFailed), PlatformAdminProvisioner skip existing admin không reset password, attempt_attachments bổ sung created_by và FK attempts(center_id, attempt_id), answer_display_latex VARCHAR(2048), ghi nhận 39 bảng hiện hành và Bảng 40 là mục tiêu sau Gate 5, UC-04 cập nhật 3 persisted retries và hợp đồng polling terminal FailedTerminal/AnalysisFailed kèm luồng resubmit | Nhóm EduTwin | Gate 1 COMPLETED sau cross-document diff verification; zero source code change; bảo tồn 2 file DOCX |
| 2026-09-12 | 3.0.3-gate2-completed | Gate 2: Track 1 — Platform Administration (Backend, Migrations, Frontend, Tests) | Hoàn thành toàn diện Gate 2 theo Plan v3.1.3-ADDENDUM-03: Thêm vai trò PlatformAdmin, cập nhật 5 MySQL CHECK constraints, sinh migration AddPlatformAdminSupportAndCheckConstraints, triển khai PlatformBootstrapOptions, PlatformAdminProvisioner, IPlatformCenterService và PlatformCenterService, PlatformCentersController, Frontend PlatformCentersPage.tsx. | Nhóm EduTwin | Gate 2 initial commit |
| 2026-09-12 | 3.0.4-gate2-frozen | Gate 2: Technical Freeze Corrective Pass (Concurrency, Live MySQL, Fail-closed Invariants) | Hoàn thành khắc phục toàn diện theo kết quả audit kỹ thuật: (1) Xử lý triệt để OCC race mapping DbUpdateConcurrencyException -> 409 ConcurrencyConflict; (2) Xử lý race trùng CenterCode bắt lỗi MySQL unique 1062 -> 409 DuplicateResource; (3) Enforce mutual invariant PlatformAdmin <=> ReservedPlatformCenterId trong ClaimsResolver và GetCurrentUserUseCase; (4) Fail-closed malformed tenant state & bad seed trong PlatformAdminProvisioner; (5) Thống nhất chính sách mật khẩu >= 12 ký tự cho bootstrap, tạo trung tâm và đặt lại mật khẩu quản lý; (6) Tức thì vô hiệu hóa phiên làm việc khi đình chỉ trung tâm (bulk bump auth_version + revoke refresh tokens trong cùng transaction); (7) Migration Down() dọn dẹp an toàn tránh lỗi FK Restrict; (8) Chuẩn hóa deterministic FixedUtcNow và mock TimeProvider (zero DateTime.UtcNow / TimeProvider.System trong platform tests); (9) Thiết lập bộ kiểm thử live MySQL PlatformMySqlIntegrationTests (8/8 tests pass). Toàn bộ 3,115/3,115 .NET tests pass (0 failed, 0 skipped!), 21/21 web tests pass, eslint 0 error, build production pass, EF pending model changes: None. Đạt chuẩn kỹ thuật đóng băng. | Nhóm EduTwin | GATE 2 FROZEN; 0 skipped; 2 file DOCX bảo tồn nguyên vẹn |
| 2026-09-12 | 3.0.5-gate2-frozen-final | Gate 2: Final Freeze & Audit Correction (Relational Interceptor Concurrency Race, Consecutive Reset OCC, Manager Search Filter, Web 12-char Policy) | Khắc phục triệt để các phát hiện audit vòng 2: (1) Khắc phục lỗi OCC UI: bổ sung InitialManagerUserRowVersion vào contract PlatformCenterListItemDto và UI cache, giải quyết dứt điểm lỗi reset mật khẩu liên tiếp bị 409 do stale/hardcoded row version; (2) Triển khai test concurrency quan hệ thực thụ: sử dụng DbCommandInterceptor trên live MySQL container mô phỏng race condition vật lý ném DbUpdateConcurrencyException và MySQL 1062 duplicate key thay vì test logic; (3) Bổ sung integration test tuần tự 1 -> 2 -> 3 -> stale 409; (4) Bổ sung tìm kiếm trung tâm theo username và display_name của manager trước phân trang; (5) Enforce minLength 12 ký tự cho mật khẩu trên UI và cập nhật placeholder; (6) Loại bỏ hoàn toàn fallback password tĩnh trong mọi tài liệu. Kết quả kiểm thử local có kiểm chứng: 3,120/3,120 .NET tests pass (0 failed, 0 skipped!), 13/13 live MySQL integration tests pass, 21/21 Node.js test runner tests pass, eslint 0 error, web build pass, EF model up-to-date (0 pending changes). | Nhóm EduTwin | GATE 2 FROZEN; 0 skipped; 2 file DOCX bảo tồn nguyên vẹn |
| 2026-09-13 | 3.0.6-gate2-final-corrective | Gate 2: Deployment Invariant, Strict 1062 Index Matching & Fresh Volume Rehearsal | Khắc phục triệt để các phát hiện audit vòng 3: (1) Bổ sung truyền biến môi trường PlatformBootstrap__* vào container api trong docker-compose.yml và cấu hình --log-bin-trust-function-creators=1 cho mysql service, kiểm chứng khởi động sạch thành công từ zero (fresh volume), API healthy, PLATFORM center & platform.admin khởi tạo tự động, login thành công; (2) Đồng bộ API_CONTRACTS.md Contract 75 & 76 với trường initialManagerUserRowVersion (string OCC token); (3) Khóa chặt logic bắt duplicate 1062 bằng phép AND bắt buộc khớp index ux_centers_center_code, bổ sung negative test kiểm chứng 1062 trên index khác bị rethrow; (4) Loại bỏ hoàn toàn fallback "1" trên Web UI (fail-closed refetch nếu thiếu version); (5) Rerun toàn bộ backend test suite tại final HEAD với live MySQL (đạt 3,121/3,121 pass, 0 failed, 0 skipped), 14/14 MySQL integration tests pass, 21/21 web tests pass, EF model up-to-date (0 drift). | Nhóm EduTwin | GATE 2 FROZEN; 0 skipped; 2 file DOCX bảo tồn nguyên vẹn |
| 2026-09-13 | 3.0.7-gate3-frozen-final | Gate 3: Track 2A/B — Math Toolbar, Evaluation Mode Matrix, BigInteger Rational Normalizer & Scientific Calculator Drawer | Hoàn thành toàn diện Gate 3 theo Plan v3.1.3-ADDENDUM-03: (1) Schema & Migration answer_evaluation_mode (VARCHAR(32), CHECK ck_questions_answer_evaluation_mode), answer_display_latex (VARCHAR(2048) NULL); (2) BLL IMathAnswerNormalizer & MathAnswerNormalizer sử dụng BigInteger phân số tối giản P/Q (Q > 0, gcd(|P|, Q) = 1) bảo toàn độ chính xác tuyệt đối, hỗ trợ chuẩn hóa phân số \frac{a}{b}, số thập phân dấu chấm/phẩy, hỗn số và số nguyên; (3) Graders & Attempt flow hỗ trợ 3 chế độ chấm (MultipleChoice -> TextExact, Essay -> Manual, ShortAnswer -> TextExact/NumericRational/Manual) kèm validation khóa cứng khi kích hoạt câu hỏi; (4) Frontend KaTeX preview ({ throwOnError: false, trust: false } chống XSS), MathInputToolbar 5 tabs, ScientificCalculatorDrawer (lịch sử, bộ nhớ M+/M-/MR/MC, deg/rad, phím số & hàm, zero symbolic algebra), chèn ký hiệu tại vị trí con trỏ (cursor insertion), tách bạch đáp án chấm điểm số học và biểu diễn công thức hiển thị; (5) Toàn bộ 3,221/3,221 .NET tests pass (0 failed, 0 skipped!), 26/26 web tests pass, eslint 0 error, build production clean trong 14.28s, EF model 0 drift. | Nhóm EduTwin | GATE 3 FROZEN; 0 skipped; 2 file DOCX bảo tồn nguyên vẹn |
| 2026-09-13 | 3.0.8-gate3-corrective-final | Gate 3: Migration Corrective Pass, Live MySQL Backfill Verification & Formal Technical Freeze | Hoàn tất khắc phục audit kỹ thuật Gate 3: (1) Tách bạch migration 20260912173142_AddEvaluationModeAndDisplayLatex thành schema thuần túy; tạo migration độc lập 20260913044542_BackfillQuestionEvaluationModeMatrix thực thi backfill dữ liệu lịch sử (Essay -> Manual, MultipleChoice -> TextExact) trên live database nâng cấp; (2) Thiết lập bộ kiểm thử QuestionEvaluationModeMySqlIntegrationTests (2/2 pass trên live MySQL container port 3307) kiểm chứng cả hai kịch bản: nâng cấp từ baseline Gate 3 cũ và khởi tạo fresh database kèm seeder thật + CHECK constraint thật; (3) Khóa chặt semantics chấm điểm NumericRational: cú pháp toán học chưa hỗ trợ giữ IsCorrect=null, Score=0 và chuyển Teacher Review; câu bỏ qua (SKIPPED) tính sai/0 điểm; (4) Chuẩn hóa frontend answerEvaluationMode required với canonical fallback TextExact thay vì tự suy đoán NumericRational; (5) Toàn bộ 3,223/3,223 .NET tests pass (0 failed, 0 skipped!), 41/41 live MySQL integration tests pass, 26/26 web tests pass, eslint 0 error, build production clean trong 15.08s, EF model 0 drift. Đóng băng kỹ thuật chính thức Gate 3 (Formal Freeze). | Nhóm EduTwin | GATE 3 FORMAL FREEZE; 0 skipped; 2 file DOCX bảo tồn nguyên vẹn |
| 2026-09-13 | 3.0.9-gate4-corrective-final | Gate 4: Scoped Vector Scratchpad Corrective Pass | Khắc phục audit Gate 4: giữ `clientSubmissionId` ổn định theo `centerId + userId + subjectId + questionId` trong `sessionStorage` để nháp IndexedDB tự khôi phục sau reload/remount và chỉ xoá identity khi attempt được server chấp nhận; chuyển test storage sang `fake-indexeddb` để kiểm tra transaction/object store bền vững qua đóng-mở kết nối; áp dụng các giới hạn 400 strokes, 1.000 points/stroke, 20.000 total points và 1 MB serialized draft; phân biệt rõ lưu IndexedDB bền vững với fallback memory chỉ tồn tại trong phiên. Không thay đổi backend/schema; PNG vẫn chỉ ở bộ nhớ trong Gate 4. | Nhóm EduTwin | GATE 4 corrective pass; test/lint/build evidence recorded at commit |
| 2026-09-13 | 3.0.10-gate5-frozen-final | Gate 5: Multipart Storage, Physical Table 40, Gemini Multimodal, Resubmit Identity & Formal Technical Freeze | Hoàn thành toàn diện và đóng băng kỹ thuật Gate 5: (1) Thêm Bảng vật lý thứ 40 `attempt_attachments` kèm EF migration `AddAttemptAttachmentsTable` và `AddAttemptAttachmentCheckConstraints`; (2) Upload PNG multipart bounded 5 MB qua `PrepareAttemptAttachmentUploadUseCase` và streaming upload, strict chunk decoding (IHDR/IDAT/IEND/CRC), signed token 24 giờ với unique nonce chống replay/race (HTTP 409 khi duplicate); (3) Tải ảnh fail-closed qua `IAttemptTeacherReviewScopeGuard` (chỉ học sinh sở hữu hoặc giáo viên phụ trách assignment; free-practice trả 404 cho giáo viên); (4) Gemini Multimodal observation đưa ảnh vào AI prompt song song rule-based fallback; (5) Máy trạng thái retry 0..3 với MySQL CHECK `ck_ai_analysis_jobs_retry_count_bounds` và `ck_attempts_status_whitelist` (`AnalysisFailed`), polling wire contract `attemptStatus` và `errorCode`, chuyển `FailedTerminal` / `AnalysisFailed` khi cạn retry cho bài free-practice; (6) UI Resubmit bảo toàn nét vẽ nháp và persist `clientSubmissionId` mới vào `sessionStorage` scoped theo `centerId + userId + subjectId + questionId` qua helper `setAttemptSessionId()`, triệt tiêu hoàn toàn ID drift qua reload/remount. Lưu ý vòng đời: Gate 5 hoàn thiện cơ chế giữ nháp qua quá trình phân tích và chỉ dọn sau terminal success hoặc migrate an toàn sang phiên resubmit mới, chuẩn hóa mô tả sơ bộ xóa sau HTTP 202 từ Gate 4; (7) Bằng chứng kiểm thử (Local Execution Evidence, remote GitHub CI chưa cấu hình): Backend thừa kế từ parent commit `9e76934` (3,208/3,208 non-MySQL tests pass, 45/45 live MySQL integration tests pass trên port 3307), Frontend tại corrective commit `81bcc1e` (46/46 web tests pass bao gồm lifecycle resubmit & IndexedDB, eslint 0 error, build production pass, git diff check sạch). | Nhóm EduTwin | GATE 5 FORMAL FREEZE; local verification only (remote CI not configured); 0 backend code change in doc closeout |
| 2026-09-13 | 3.0.11-gate5-doc-correction | Gate 5: Technical Audit Trail & Identifier Correction for Formal Freeze | Đính chính chính thức cho entry 3.0.10 theo kết quả đối soát codebase thực tế: (1) Migration Gate 5 chính xác gồm `20260913071119_AddAttemptAttachmentsTable` (tạo Bảng 40 cùng 2 CHECK `ck_attempt_attachments_content_type`, `ck_attempt_attachments_file_size_bytes`) và `20260913095744_ExpandGate5StorageFailureStateConstraints` (mở rộng failure states và retries); không tồn tại migration tên `AddAttemptAttachmentCheckConstraints`; (2) Tên CHECK constraints chính xác trong code: `ck_ai_analysis_jobs_retry_count` (tại `AIAnalysisJobConfiguration`) và `ck_attempts_status` (tại `AttemptConfiguration`); (3) Cơ chế upload minh chứng: bounded multipart ingestion với in-memory buffer tối đa 5 MB (không phải streaming zero-buffer); (4) Tình trạng CI: workflow `.github/workflows/ci.yml` có tồn tại trong repo (trigger PR main cho .NET restore/build/test), nhưng freeze SHA trên branch `codex/personal-system-completion` không có workflow run / status attestation từ GitHub CI, đồng thời workflow hiện tại chưa cover frontend, live MySQL (port 3307) và Gate 5 E2E. Toàn bộ bằng chứng test tiếp tục ghi nhận chuẩn xác là Local Execution Evidence. | Nhóm EduTwin | GATE 5 FORMAL FREEZE; audit trail sanitized; 0 code change |
| 2026-09-13 | 3.0.12-gate5-permanent-orphan-corrective | `AttachmentOrphanCleanupWorker.cs`, `AttachmentOrphanCleanupWorkerTests.cs`, `GlobalQueryFilterTests.cs`, `PROJECT_TRACKING.md` | Khắc phục toàn diện phát hiện audit P1 của Codex cho Gate 5: (1) Nâng cấp `AttachmentOrphanCleanupWorker` sử dụng duy nhất 1 constructor DI nhận `IServiceScopeFactory`, quét và dọn sạch các permanent attachments mồ côi (`attempt-attachments`) không có bản ghi trong `attempt_attachments` vượt quá thời gian ân hạn 24h (`_gracePeriod`), xử lý dứt điểm rủi ro crash tiến trình giữa promote và commit; (2) Áp dụng nguyên tắc Fail-Closed: nếu truy vấn `AttemptAttachments.IgnoreQueryFilters()` gặp sự cố hoặc DB unavailable, toàn bộ việc dọn dẹp permanent blob bị hủy trong lượt quét đó, file mồ côi được bảo toàn tuyệt đối, trong khi việc dọn file tạm (`attempt-attachments-temp`) vẫn hoạt động độc lập; (3) Xác thực chặt chẽ định dạng storage key `tenants/{centerId}/attempt-attachments/{nonce}.png`, bỏ qua symlink/reparse point và chống path traversal ra ngoài storage root; (4) Thống nhất đặc tả quyền tải file đính kèm: chính sách `learning.attempts.read_scoped` kết hợp với `IAttemptTeacherReviewScopeGuard` là chuẩn mực, đầy đủ và nhất quán với `GET /learning/attempts/{attemptId}` (Section 52 và 52.2 API_CONTRACTS.md), cho phép CenterManager quản trị trong center mà không đòi hỏi quyền riêng `twin.reasoning.review` của Teacher; (5) Bổ sung bộ kiểm thử 5 ca nghiệp vụ cốt lõi tại `AttachmentOrphanCleanupWorkerTests` (committed 48h giữ, crash orphan 48h xóa, crash orphan mới giữ, temp cũ xóa, temp mới giữ, non-png giữ, key sai giữ) cùng ca kiểm thử DB exception fail-closed; (6) Cập nhật allow-list `GlobalQueryFilterTests` ghi nhận quyền bypass tenant filter hợp lệ của sweeper worker. Bằng chứng kiểm thử: Release build 0 warning/0 error; 3,210/3,210 backend unit tests pass; 45/45 live MySQL integration tests pass trên port 3307; frontend `tsc -b` pass, 46/46 web tests pass, eslint 0 error, build production pass (8.96s); EF model 0 drift. | Nhóm EduTwin | GATE 5 CORRECTIVE APPLIED; 3,210 non-MySQL + 45 live MySQL + 46 web tests pass |
| 2026-09-13 | 3.0.13-gate5-orphan-race-final | `FileSystemAttemptAttachmentStorage.cs`, `AttachmentOrphanCleanupWorker.cs`, `AttachmentStorageOptions.cs`, `AttachmentOrphanCleanupWorkerTests.cs`, `AttachmentOrphanCleanupWorkerMySqlIntegrationTests.cs`, `PROJECT_TRACKING.md` | Khắc phục dứt điểm in-flight promotion timestamp race tại mốc 24h và hoàn thiện kiểm thử multi-tenant live MySQL cho Gate 5: (1) Khóa invariant timestamp tại promotion: `FileSystemAttemptAttachmentStorage` inject `TimeProvider` và thực thi promotion qua staging file `{nonce}.staging.{guid}.tmp` trong thư mục đích `attempt-attachments`, ghi nhận `LastWriteTimeUtc = promotionTimeUtc` trước khi thực hiện atomic rename `File.Move(stagingPath, permanentPath, overwrite: false)`, triệt tiêu hoàn toàn race window giữa move và touch; (2) Bảo vệ in-flight submission gần mốc 24h: sweeper không bao giờ xóa nhầm permanent file đang submit trước khi DB transaction commit; file permanent orphan chỉ bị dọn sau khi vượt quá 24h kể từ thời điểm promotion; (3) Safety margin và validation cấu hình: bảo đảm thời gian lưu file tạm `_tempGracePeriod` tối thiểu 26h (`TokenLifetime` 24h + `TempSafetyMarginHours` 2h) để không bao giờ xóa file tạm trong khi token còn hạn; enforce `MinimumGracePeriodHours = 24` chống cấu hình sai xuống 1h; (4) Dọn staging file quá hạn: worker quét và dọn các file `*.staging.*.tmp` bị bỏ rơi quá 24h do tiến trình crash giữa chừng; (5) Bổ sung test suite regression: `AttachmentOrphanCleanupWorkerTests` bổ sung kiểm thử luồng near-expiry (temp 23h55m -> promote -> sweep trước commit giữ file -> sweep sau 25h xóa orphan) và dọn staging cũ; (6) Bổ sung kiểm thử tích hợp live MySQL đa tenant `AttachmentOrphanCleanupWorkerMySqlIntegrationTests` (port 3307) chứng minh `IgnoreQueryFilters()` đa tenant trên MySQL thật giữ nguyên blob của cả Tenant A và Tenant B đồng thời dọn orphan không tham chiếu. Bằng chứng kiểm thử: Release build 0 warning/0 error; 3,212/3,212 non-MySQL tests pass; 46/46 live MySQL integration tests pass trên port 3307; frontend `tsc -b` pass, 46/46 web tests pass, eslint 0 error, build production pass (9.39s); EF model 0 drift. | Nhóm EduTwin | GATE 5 FINAL TECHNICAL FREEZE; 3,212 non-MySQL + 46 live MySQL + 46 web tests pass; local verification only |
| 2026-09-13 | 3.0.14-gate5-approved-frozen | `PROJECT_TRACKING.md` | Phê duyệt đóng băng kỹ thuật chính thức Gate 5 (Formal Technical Freeze) sau khi rà soát độc lập và xác nhận giải quyết dứt điểm race condition promotion timestamp, grace period 26h, test suite 3,212/3,212 non-MySQL + 46/46 MySQL + 46/46 web tests pass, 0 EF drift, Release build 0 warning/0 error. Bắt đầu thực thi Gate 6. | Nhóm EduTwin / Codex | GATE 5 FORMALLY FROZEN ✅; Gate 6 ACTIVE |
| 2026-09-13 | 3.0.15-gate6-final-rehearsal-freeze | `PROJECT_TRACKING.md`, `docs/verification/R09_FINAL_VERIFICATION_REPORT.md` | Hoàn tất toàn diện Gate 6 (Release Hardening, Clean Rehearsal & R09 Closeout): (1) Đóng băng kỹ thuật chính thức Gate 5 tại commit `9f374af`; (2) Dựng rehearsal stack độc lập từ clean worktree tại SHA `d3f9142` với Docker Compose `edutwin-rehearsal` (MySQL 3308, API 5001, Web 3001, volumes riêng); (3) Khởi tạo fresh DB sạch sẽ: 17 EF migrations (40 domain tables + 1 `__EFMigrationsHistory`), 5 CHECK constraints, PlatformAdmin trong Root Tenant PLATFORM; (4) Kiểm thử tự động vượt qua 100%: 3.212/3.212 backend unit tests pass, 46/46 live MySQL integration tests pass, 46/46 frontend vitest pass, 0 eslint errors/warnings, build production clean (13.58s), EF model 0 drift; (5) Kiểm chứng E2E đa vai trò (PlatformAdmin, CenterManager, Teacher, Student) bảo đảm RBAC và tenant isolation tuyệt đối; kiểm chứng token/session persistence bền vững qua container reboot nóng; (6) Phân tích MySQL EXPLAIN cho các truy vấn trọng yếu đạt index lookup tối ưu; (7) Xuất bản báo cáo nghiệm thu chính thức `docs/verification/R09_FINAL_VERIFICATION_REPORT.md`. Đóng băng kỹ thuật Gate 6 và toàn bộ cột mốc R09. | Nhóm EduTwin / Codex | GATE 6 FORMALLY FROZEN ✅; R09 RELEASE READY 🚀 |
| 2026-09-13 | 3.0.16-gate6-verification-corrective | `AttemptAttachmentsController.cs`, `PROJECT_TRACKING.md`, `docs/verification/R09_FINAL_VERIFICATION_REPORT.md` | Tiếp nhận kết quả rà soát độc lập từ Codex & GPT Web; mở lại Gate 6 ở trạng thái CORRECTIVE PENDING REVIEW: (1) Khắc phục lỗi P0 runtime Kestrel Multipart EOF drain tại `PrepareUpload` bằng `[DisableFormValueModelBinding]` và guard EOF; (2) Triển khai kiểm chứng Real HTTP Multimodal Upload -> Submit Attempt -> Permanent Blob -> Polling; (3) Thực thi failure injection (expired token, duplicate 409, storage outage retries 0..3, resubmit); (4) Kiểm chứng Data Protection keyring qua reboot bằng upload token thực tế; (5) Chuẩn hóa báo cáo R09: ghi nhận đúng `node --test` (Node test runner), sửa mapping Gate 1–4, hạ kết luận EXPLAIN thành smoke verification, và xác định hệ thống là TECHNICAL RELEASE CANDIDATE (chờ UX acceptance R08). | Nhóm EduTwin / Codex | GATE 6 CORRECTIVE IN PROGRESS / PENDING REVIEW; R09 TECHNICAL RELEASE CANDIDATE |
| 2026-09-13 | 3.0.17-gate6-frozen-final | `AttemptAttachmentsController.cs`, `AttemptAttachmentsControllerTests.cs`, `docs/verification/R09_FINAL_VERIFICATION_REPORT.md`, `docs/verification/edutwin_schema_r09.sql`, `PROJECT_TRACKING.md` | Đóng băng kỹ thuật chính thức Gate 6 (Formal Technical Freeze) sau khi hoàn tất trọn vẹn Verification Corrective Pass: (1) Khắc phục lỗi Kestrel Multipart EOF drain bằng `[DisableFormValueModelBinding]` và bổ sung unit test; (2) Chạy thực tế và PASS 100% luồng Real HTTP Multimodal E2E Track 2D (Student login -> multipart prepare-upload -> token 560B -> submit 202 -> `attempt_attachments` DB row -> permanent blob filesystem -> background worker AI processing -> polling -> completed feedback 200); (3) Diễn tập và PASS 100% toàn bộ kịch bản Live Stack Failure Injection: replay token đã dùng -> 409 Conflict (`UPLOAD_TOKEN_ALREADY_USED`), concurrent duplicate submission -> 202 vs 409, tampered token -> 400 Bad Request, storage loss (xóa blob) -> bounded retry 0->1->2->3 với `AttachmentStorageUnavailable` -> terminal `FailedTerminal` / `AnalysisFailed`, resubmit thành công với identity/token mới và giữ nét vẽ; (4) Kiểm chứng Data Protection keyring persistence đúng chuẩn: issue `drawingUploadToken` trước API container restart, restart `edutwin-api`, submit sau restart -> 202 Accepted; (5) Cập nhật và chuẩn hóa toàn diện `R09_FINAL_VERIFICATION_REPORT.md`: ghi nhận đúng `npm test` / Node.js test runner, sửa đúng thứ tự bảng Gate 1–6, hạ kết luận EXPLAIN thành smoke verification, chuẩn hóa trạng thái hệ thống thành TECHNICAL RELEASE CANDIDATE (chờ R08 UX acceptance); (6) Kiểm thử hồi quy toàn diện: 3.213/3.213 non-MySQL tests pass, 46/46 live MySQL integration tests pass, 46/46 web tests pass, eslint 0 error/0 warning, production build clean, 0 EF drift. | Nhóm EduTwin / Codex | GATE 6 FORMALLY FROZEN ✅; R09 TECHNICAL RELEASE CANDIDATE |
| 2026-09-13 | 3.0.18-gate6-audit-corrective-final | `AttemptAttachmentsController.cs`, `AttemptAttachmentsHttpIntegrationTests.cs`, `SubmitAttemptUseCaseTests.cs`, `docs/verification/R09_FINAL_VERIFICATION_REPORT.md`, `docs/verification/edutwin_schema_r09.sql`, `PROJECT_TRACKING.md` | Hoàn tất Gate 6 Final Corrective Pass theo rà soát độc lập của Codex và GPT Web: (1) Cải tiến endpoint `PrepareUpload` theo nguyên tắc Fail-Closed: duyệt toàn bộ multipart stream tới EOF (không break sớm), từ chối section file thứ hai với HTTP 400, drain bounded non-file sections (tối đa 64 KB), validate boundary length <= 128 qua TryParse, bắt trọn gói IOException/InvalidDataException/unexpected EOF thành HTTP 400; (2) Triển khai test suite `AttemptAttachmentsHttpIntegrationTests` (8 tests) qua TestServer / WebApplicationFactory kiểm chứng trực tiếp MVC pipeline và `[DisableFormValueModelBinding]`; (3) Bổ sung unit test `ExecuteAsync_ExpiredUploadToken_ReturnsValidationFailed` trong `SubmitAttemptUseCaseTests`; (4) Chuyển `edutwin_schema_r09.sql` sang UTF-8 without BOM (strip 0xFEFF), xác minh DDL-only (0 INSERT, 0 credentials, 0 seed/user data); (5) Đính chính `R09_FINAL_VERIFICATION_REPORT.md`: sửa lệnh MySQL test trỏ về `EduTwin.BLL.Tests.csproj` với filter `FullyQualifiedName~MySql`, sửa framework thành .NET 10 (`net10.0`), sửa frontend build duration 15.11s, ghi rõ phân loại Local Execution Evidence; (6) Chạy full regression test suites: 3.222 non-MySQL tests pass, 46/46 live MySQL tests pass, 46/46 web tests pass, eslint 0 error, build production pass, EF model 0 drift. Dừng để Codex review, không triển khai enhancement mới. | Nhóm EduTwin / Codex | GATE 6 FINAL CORRECTIVE PASSED; R09 TECHNICAL RELEASE CANDIDATE |
| 2026-09-13 | 3.0.19-post-r09-platform-ops-specs | `ADR-POST-R09-PLATFORM-OPERATIONS.md`, `CONSTITUTION.md`, `PROJECT_REQUIREMENTS.md`, `DATABASE_SCHEMA.md`, `API_CONTRACTS.md`, `UI_UX_SPEC.md`, `MASTER_PLAN.md`, `PROJECT_TRACKING.md` | Checkpoint 1: Khóa toàn diện đặc tả kỹ thuật, bất biến kiến trúc và lộ trình 8 checkpoint cho cột mốc POST-R09-PLATFORM-OPS (Quản trị vận hành nền tảng). Bổ sung primary_manager_user_id (centers), target_center_id (authorization_audit_logs), platform.audit.read, contracts 81..88, ma trận từ chối dữ liệu học thuật; Giai đoạn H-I giữ nguyên DESIGNED / DEFERRED. | Nhóm EduTwin | Checkpoint 1 COMPLETED; zero source code change; bảo tồn file DOCX |
| 2026-09-13 | 3.0.20-post-r09-manager-lifecycle | `Center.cs`, `CenterConfiguration.cs`, `20260913161857_AddPrimaryManagerToCenters.cs`, `EduTwinDbContextModelSnapshot.cs`, `PlatformCenterDtos.cs`, `IPlatformCenterService.cs`, `PlatformCenterService.cs`, `PlatformCentersController.cs`, `CenterManagersModal.tsx`, `PlatformCentersPage.tsx`, `platformApi.ts`, `types/platform.ts`, `PlatformCenterManagerLifecycleTests.cs`, `PROJECT_TRACKING.md` | Checkpoint 3: Triển khai hoàn chỉnh vòng đời CenterManager: (1) Migration `primary_manager_user_id` kèm composite FK `(center_id, primary_manager_user_id)` và preflight backfill SQL; (2) CRUD và lifecycle CenterManager với invariant: cấm khóa primary manager trước transfer, cấm khóa manager active cuối cùng của active center, cấm activate center nếu thiếu active primary manager; (3) Bumping AuthVersion và revoke refresh tokens khi khóa/vô hiệu hóa/đổi primary; (4) Redacted audit log; (5) Frontend UI CenterManagersModal tích hợp; (6) 60/60 targeted platform tests pass, 46/46 web tests pass. | Nhóm EduTwin | Checkpoint 3 COMPLETED |
| 2026-09-14 | 3.0.21-post-r09-teacher-student-lifecycle | `ResetAccountPasswordRequest.cs`, `ResetAccountPasswordResponse.cs`, `DeleteStudentResult.cs`, `IDeleteStudentUseCase.cs`, `DeleteStudentUseCase.cs`, `ResetAccountPasswordResult.cs`, `IResetAccountPasswordUseCase.cs`, `ResetAccountPasswordUseCase.cs`, `TeachersController.cs`, `StudentsController.cs`, `20260914133545_AddTeacherAndStudentResetPasswordPermissions.cs`, `DeleteStudentUseCaseTests.cs`, `ResetAccountPasswordUseCaseTests.cs`, `organizationApi.ts`, `TeacherListPage.tsx`, `StudentListPage.tsx`, `accountLifecycle.test.ts`, `PROJECT_TRACKING.md` | Checkpoint 4: Hoàn thiện toàn diện vòng đời tài khoản Giáo viên và Học sinh: (1) Bổ sung permissions `organization.teachers.reset_password` và `organization.students.reset_password` kèm migration `AddTeacherAndStudentResetPasswordPermissions` (0 pending changes, 40 bảng); (2) DeleteStudentUseCase soft-delete Student + linked User, chuyển ClassStudent sang Removed, thu hồi active refresh tokens, ghi audit log khử khuẩn, bảo toàn 100% attempts/twins/evidence; (3) ResetAccountPasswordUseCase kiểm tra OCC expectedUserRowVersion, mật khẩu >= 12 & <= 200 ký tự, tăng AuthVersion làm mất hiệu lực JWT hiện hành, thu hồi refresh tokens, audit log zero-secrets; (4) Endpoints POST `/teachers/{id}/reset-password`, DELETE `/students/{id}`, POST `/students/{id}/reset-password`; (5) Frontend TeacherListPage và StudentListPage tích hợp Edit, Reset Password và Delete modals kèm OCC và capability gates; (6) 833/833 backend tests pass, 68/68 web tests pass, eslint 0 error, build production pass. | Nhóm EduTwin | Checkpoint 4 COMPLETED |
| 2026-09-14 | 3.0.22-post-r09-part1-corrective-pass | `API_CONTRACTS.md`, `UI_UX_SPEC.md`, `ResetAccountPasswordUseCase.cs`, `ResetAccountPasswordUseCaseTests.cs`, `TeacherListPage.tsx`, `accountLifecycle.test.ts`, `CenterManagerLiveMySqlTests.cs`, `PROJECT_TRACKING.md` | Hoàn tất toàn diện đợt Corrective cho POST-R09-CENTER-MANAGER-OPS Phần 1 (A–D): (1) `ResetAccountPasswordUseCase`: audit AfterData ghi đúng RowVersion sau mutation khớp với NewRowVersion; bổ sung test đối chiếu audit RowVersion với response NewRowVersion tại `ResetAccountPasswordUseCaseTests`; (2) Đồng bộ `API_CONTRACTS.md` Contracts 20.1 và 25.2 với DTO thực tế (`data = { targetUserId, newRowVersion }`, password policy 12–200 ký tự, reason 5–500 ký tự); (3) Hoàn thiện Teacher detail UI theo UX spec: tích hợp `organizationApi.getTeacher()`, nút và modal xem chi tiết trong `TeacherListPage.tsx`, giữ canonical `classCount` và đồng bộ `UI_UX_SPEC.md`; (4) Xóa sạch trailing whitespace (`git diff --check 222e7bb..HEAD` sạch); (5) Áp dụng migration `20260914133545` lên Docker MySQL, chạy runtime authorization bootstrap, xác minh 11 role `SYSTEM_CENTERMANAGER` đều nhận đủ 2 permissions reset-password; (6) Bổ sung và thực thi 5/5 live-MySQL tests tại `CenterManagerLiveMySqlTests.cs` (fresh DB migration, existing DB upgrade, reset password OCC + AuthVersion + refresh-token revoke + audit, student soft-delete preserving historical evidence, concurrent reset/update OCC); (7) Chạy minimal Docker API smoke test thành công 100% (login -> profile -> teacher lifecycle -> student lifecycle); (8) Full regression: .NET 833 Organization tests pass, 5 live MySQL tests pass, 69 web tests pass, eslint 0 error, build production clean, EF model 0 drift. Dừng chờ Codex review, không bắt đầu Phần 2 (E–I). | Nhóm EduTwin / Codex | PHẦN 1 CORRECTIVE VERIFIED; 833 Org + 5 Live MySQL + 69 Web tests pass |
| 2026-09-14 | 3.0.23-post-r09-class-and-membership-lifecycle | `ClassListPage.tsx`, `organizationApi.ts`, `permissions.ts`, `organization.ts`, `classListPage.test.ts`, `UI_UX_SPEC.md`, `PROJECT_TRACKING.md` | Checkpoint 5 (Giai đoạn E): Hoàn thành toàn diện quản lý Lớp học và Thành viên lớp: (1) Bổ sung permissions `organization.classes.update` và `organization.classes.manage_members` trong `permissions.ts`; (2) Mở rộng client `organizationApi.ts` và types `organization.ts` (`getClass`, `updateClass`, `getClassStudents`, `addStudentsToClass`, `removeStudentFromClass`); (3) Nâng cấp `ClassListPage.tsx` thành luồng end-to-end hoàn chỉnh: hiển thị danh sách lớp, filter, phân trang, Modal xem chi tiết lớp học & thành viên (`ClassDetailModal`), Modal cập nhật lớp học (`EditClassModal`) kiểm soát OCC RowVersion, Modal thêm học sinh vào lớp (`AddStudentsModal`) tuyển chọn theo lô và loại trừ học sinh đã có trong lớp, Modal xác nhận xóa học sinh khỏi lớp (`RemoveStudentModal`) cảnh báo xóa mềm chuyển trạng thái sang Removed và bảo toàn 100% lịch sử làm bài (Attempts), điểm số và evidence học thuật; (4) Link truy cập trực tiếp Class Dashboard khi người dùng có quyền; (5) Bổ sung 11 bài kiểm thử tự động `classListPage.test.ts` (80/80 web tests pass 100%); (6) Cập nhật Mục 20.7.5 trong `UI_UX_SPEC.md`; (7) Regression: 833 Org tests pass, 80 web tests pass, eslint 0 error/0 warning, build production clean. | Nhóm EduTwin | Checkpoint 5 (Phase E) COMPLETED; 833 Org + 80 Web tests pass |

Khi chuyển 2.1-draft sang APPROVED/FROZEN, thêm entry ghi reviewer, commit/PR và ngày nhóm xác nhận.

## 18. Post-R08 Extension Milestones & Execution Tracking

Kế hoạch thực thi 6 Gate kỹ thuật nghiêm ngặt (Strict 6-Gate Pipeline) theo Plan v3.1.3-ADDENDUM-03:

| Gate | Nội dung thực thi | Trạng thái | Ghi chú & Bằng chứng |
|---|---|---|---|
| **GATE 1** | Đặc tả tài liệu kỹ thuật (.md), 2 bản ghi ADR độc lập, cập nhật Master Plan | **FROZEN** | Đóng băng chính thức (FROZEN) toàn bộ đặc tả Gate 1: 2 file ADR mới, 7 file đặc tả đồng bộ; 0 file mã nguồn bị sửa; 2 file DOCX được bảo vệ nguyên vẹn; phân biệt rạch ròi AI/network fallback với sự cố lưu trữ AttachmentStorageUnavailable (free-practice FailedTerminal/AnalysisFailed); chuẩn hóa exact schema Bảng 40 (attachment_id, file_name, storage_key VARCHAR(512), file_size_bytes BIGINT, created_by, composite FK attempts, không persist sha256_hash); thiết lập Platform Audit Invariant (CenterId=PLATFORM, cross-tenant TargetUserId=null, TargetId/redacted metadata, không log password) kèm verification plan. |
| **GATE 2** | Track 1 — Platform Admin (5 CHECK constraints, Provisioner, Web UI, Live MySQL Hardening) | **FROZEN** | Đã triển khai đầy đủ 5 CHECK constraints, PlatformAdminProvisioner (fail-closed malformed state), IPlatformCenterService & PlatformCenterService với audit invariant (CenterId=PLATFORM, cross-tenant TargetUserId=null), PlatformCentersController, Frontend PlatformCentersPage.tsx (enforce minLength=12, initialManagerUserRowVersion OCC, zero fallback "1"), đồng bộ đầy đủ API_CONTRACTS.md, cấu hình chuẩn docker-compose.yml (forward PlatformBootstrap__* và --log-bin-trust-function-creators=1) đã qua fresh volume rehearsal, và hoàn thành trọn vẹn audit corrective pass: giải quyết triệt để relational DbCommandInterceptor OCC race -> 409, duplicate 1062 AND ux_centers_center_code -> 409 kèm negative rethrow test, consecutive password resets, tìm kiếm theo manager username/display name, immediate session eviction khi suspend. Toàn bộ 3,121/3,121 .NET tests pass (0 failed, 0 skipped!), 14/14 live MySQL integration tests pass trên port 3307, 21/21 web tests pass (Node.js test runner), eslint 0 error, production build pass, EF model up-to-date (0 pending changes). |
| **GATE 3** | Track 2A/B — Math Toolbar, Evaluation Mode, Rational Normalizer & Calculator Drawer | **FROZEN** | Hoàn thành toàn diện Gate 3 theo Plan v3.1.3-ADDENDUM-03: Migration answer_evaluation_mode & answer_display_latex kèm migration backfill an toàn 20260913044542_BackfillQuestionEvaluationModeMatrix, BigInteger rational normalizer (\frac{a}{b}, số thập phân, hỗn số, số nguyên), Evaluation mode matrix locking, MathInputToolbar 5 tabs, KaTeX preview (trust: false), ScientificCalculatorDrawer, cursor insertion, semantic vs display latex separation. Toàn bộ 3,223/3,223 .NET tests pass (0 skipped), 41/41 live MySQL integration tests pass trên port 3307, 26/26 web tests pass, eslint 0 error, web build clean, EF model 0 drift. |
| **GATE 4** | Track 2C — Vector Scratchpad Canvas & Scoped IndexedDB Cache | **FROZEN** | `ScratchpadCanvasModal.tsx` có bút, tẩy, thước thẳng, compa/tròn, tam giác, trục Oxy, lưới toán/ô ly và history Undo/Redo tối đa 30 trạng thái. Draft vector được IndexedDB lưu theo `draft:{centerId}:{userId}:{clientSubmissionId}`, trong đó `clientSubmissionId` được giữ ổn định qua reload/remount theo scope `centerId + userId + subjectId + questionId`; startup TTL 24 giờ, logout dọn đúng user scope, và submit chỉ xóa draft/session identity sau HTTP 202/200. Giới hạn 400 strokes, 1.000 points/stroke, 20.000 points/draft và 1 MB serialized draft; UI phân biệt IndexedDB durable với memory fallback volatile. PNG xuất trong bộ nhớ, giới hạn 5 MB; chưa upload hay thay đổi backend/schema (Gate 5). Xác minh corrective: Node tests (bao gồm fake IndexedDB), eslint và production build pass. |
| **GATE 5** | Track 2D — Multipart Storage, Bảng vật lý 40, Gemini Multimodal & Review Fallback | **FROZEN** | Khắc phục dứt điểm in-flight promotion timestamp race tại mốc 24h qua cơ chế staging + atomic rename trong `FileSystemAttemptAttachmentStorage`, safety margin file tạm 26h, và kiểm thử tích hợp live MySQL đa tenant `AttachmentOrphanCleanupWorkerMySqlIntegrationTests` (port 3307). Bằng chứng kiểm thử có kiểm chứng (Local Execution Evidence): Release build 0 warning/0 error; 3,212/3,212 non-MySQL unit tests pass; 46/46 live MySQL integration tests pass; frontend `tsc -b` pass, 46/46 web tests pass, eslint 0 error, production build pass (9.39s); EF model 0 drift. Đã được phê duyệt nghiệm thu đóng băng kỹ thuật chính thức (FORMAL FREEZE). |
| **GATE 6** | Post-R08 Extension Verification & Hardening | **FROZEN** | Đã hoàn thành xuất sắc và đóng băng kỹ thuật chính thức Gate 6 sau khi hoàn tất Final Audit Corrective Pass: (1) Sửa lỗi Kestrel multipart streaming bằng `[DisableFormValueModelBinding]` và cơ chế fail-closed parser đọc tới EOF, reject 2nd file 400, boundary <= 128 bytes, bounded non-file drain <= 64 KB; (2) 8 HTTP integration tests qua TestServer kiểm chứng trực tiếp MVC pipeline; (3) Unit test từ chối upload token hết hạn; (4) Chứng minh thực tế 100% luồng Real HTTP E2E Track 2D multimodal (prepare-upload -> token 560B -> submit 202 -> DB row `attempt_attachments` -> permanent blob -> worker -> polling -> terminal Completed feedback 200); (5) Diễn tập trực tiếp và đạt 100% các ca failure injection: replay nonce/token 409 Conflict, concurrent duplicate 409, tampered token 400, storage outage retry 0->1->2->3 sang terminal `AnalysisFailed` / `FailedTerminal` (free practice) hoặc `NeedsTeacherReview` / `FallbackCompleted` (assignment), resubmit identity mới bảo toàn nháp IndexedDB; (6) Xác thực Data Protection keyring bền vững qua restart API bằng `drawingUploadToken` thật (202 Accepted); (7) Chuẩn hóa báo cáo R09: command `EduTwin.BLL.Tests.csproj`, .NET 10, local execution evidence; schema `edutwin_schema_r09.sql` UTF-8 without BOM DDL-only; (8) 3.222 unit + 46 live MySQL + 46 web tests pass, 0 eslint errors, build clean, 0 EF drift. Toàn bộ 6 Gate kỹ thuật đã chính thức FROZEN. Nền tảng đạt trạng thái TECHNICAL RELEASE CANDIDATE (sẵn sàng cho bước nghiệm thu UX R08). |

## 19. Post-R09 Platform Operations Milestones & Execution Tracking

Lộ trình thực hiện củng cố vận hành quản trị nền tảng (POST-R09-PLATFORM-OPS) theo 8 Checkpoints:

| Checkpoint | Nội dung thực thi | Trạng thái | Ghi chú & Bằng chứng |
|---|---|---|---|
| **Checkpoint 1** | `docs(platform): specify post-r09 operational administration` | **COMPLETED** | Hoàn thành toàn diện việc khóa tài liệu authoritative: ADR-POST-R09-PLATFORM-OPERATIONS (APPROVED), CONSTITUTION (Mục 3.4), PROJECT_REQUIREMENTS (Mục 7.12), DATABASE_SCHEMA (primary_manager_user_id & target_center_id), API_CONTRACTS (Contracts 81..88), UI_UX_SPEC (Mục 20.6), MASTER_PLAN (Mục 133), PROJECT_TRACKING. Zero source code change; 2 file DOCX được bảo vệ. |
| **Checkpoint 2** | `test(platform): enforce education-data denial matrix` | **COMPLETED** | Triển khai bộ kiểm thử tự động `PlatformAdminAcademicAccessDenialTests` (23 tests pass 100%) kiểm chứng: (1) Toàn bộ 19 endpoint học thuật (attempts, feedback, attachments, jobs, next-question, digital twin, recommendations, dashboards, review queue, override, goals, assignments) từ chối triệt để PlatformAdmin với HTTP 403 Forbidden fail-closed; (2) Catalog invariant xác nhận PlatformAdmin chỉ có quyền `platform.*` (0 quyền học thuật); (3) Privilege escalation guards: cấm reset mật khẩu PlatformAdmin qua reset-manager endpoint, cấm reset user không phải CenterManager, cấm tạo trung tâm có mã PLATFORM. |
| **Checkpoint 3** | `feat(platform): add center manager lifecycle` | **COMPLETED** | Hoàn thành toàn diện vòng đời CenterManager: (1) Schema migration `20260913161857_AddPrimaryManagerToCenters.cs` bổ sung `primary_manager_user_id VARCHAR(36)` kèm composite FK `(center_id, primary_manager_user_id) -> users(center_id, user_id)` (RESTRICT, nullable), preflight backfill SQL gán sớm nhất active manager; (2) DTOs, IPlatformCenterService và PlatformCenterService: `ListCenterManagersAsync`, `CreateCenterManagerAsync` (password >= 12 chars, username unique in tenant, bump center OCC, redacted audit log), `UpdateCenterManagerStatusAsync` (cấm khóa primary manager trước khi transfer, cấm khóa manager active cuối cùng của active center, bump AuthVersion và revoke refresh tokens khi khóa/vô hiệu hóa), `MakePrimaryCenterManagerAsync` (chỉ active manager, OCC concurrency check, tùy chọn vô hiệu hóa primary cũ và revoke token trong cùng transaction); (3) Invariant: Active center không thể kích hoạt/reactivate nếu thiếu active primary manager; (4) API endpoints trong `PlatformCentersController`: GET `/centers/{centerId}/managers`, POST `/centers/{centerId}/managers`, PATCH `/centers/{centerId}/managers/{userId}/status`, POST `/centers/{centerId}/managers/{userId}/make-primary`; (5) Frontend `CenterManagersModal.tsx` và `PlatformCentersPage.tsx` tích hợp đầy đủ; (6) 60/60 targeted platform tests pass, 46/46 web tests pass, build 0 warnings/errors, git diff --check clean. |
| **Checkpoint 4** | `feat(platform): expose redacted platform audit history` | **COMPLETED** | Hoàn thành toàn diện kiểm toán nền tảng: (1) Migration `20260913163931_AddTargetCenterIdToAuditLogs.cs` bổ sung `target_center_id VARCHAR(36) NULL` kèm FK `(target_center_id) -> centers(center_id)` (RESTRICT, nullable), index `ix_auth_audit_center_target_center_created (center_id, target_center_id, created_at)`; (2) Quyền `platform.audit.read` (IsSensitive = true, IsDelegable = false, PlatformAdmin); (3) DTOs, IPlatformAuditService và PlatformAuditService: scoping invariant chỉ truy vấn audit logs của PLATFORM tenant (`CenterId == PLATFORM`), exclude triệt để tenant-internal audits, lọc đa tiêu chí (ActionType, TargetType, TargetId, TargetCenterId, ActorUserId, TraceId, Date range, Search), phân trang, sanitized DTO allow-list đệ quy khử khuẩn toàn bộ mật khẩu, hash, token, secret, auth version và dữ liệu học thuật; (4) API endpoints trong `PlatformAuditController`: GET `/api/v1/platform/audit-logs` và GET `/api/v1/platform/audit-logs/{id}` trả ProblemDetails chuẩn; (5) Frontend `PlatformAuditLogsPage.tsx` tích hợp bộ lọc, bảng dữ liệu phân màu badge, modal đối chiếu Before/After JSON, copy W3C Trace ID 1-click, immutable (không có sửa/xóa); tabs điều hướng trên `PlatformCentersPage` và link từ HomePage; (6) 67/67 targeted platform tests pass (bao gồm 7/7 PlatformAuditServiceTests), 49/49 web tests pass, build 0 warnings/errors, 0 lint errors, git diff --check clean. |
| **Checkpoint 5** | `feat(platform): update center metadata and safe aggregates` | **COMPLETED** | Hoàn thành toàn diện cập nhật metadata trung tâm và safe aggregates: (1) Safe aggregates (ActiveStudentCount, ActiveTeacherCount, ClassCount, ActiveManagerCount, HasActivePrimaryManager) được tính toán theo lô an toàn (0 N+1, IgnoreQueryFilters + targetCenterIds + !IsDeleted + GroupBy/CountAsync) hoàn toàn loại trừ dữ liệu học thuật; (2) Phương thức `UpdateCenterMetadataAsync` hỗ trợ cập nhật CenterName (3-200 chars) và Timezone, kiểm tra OCC ExpectedRowVersion, bắt buộc Reason (>= 3 chars), bất biến tuyệt đối CenterId/CenterCode, ghi nhận AuthorizationAuditLog với before/after data khử khuẩn; (3) Endpoint PATCH `/api/v1/platform/centers/{centerId}` yêu cầu quyền `platform.centers.manage`; (4) Frontend `PlatformCentersPage.tsx` bổ sung cột Quy mô hiển thị badge 4 chỉ số thống kê, trạng thái Quản lý chính (Active / Chưa kích hoạt), nút Sửa và modal cập nhật metadata với validation và xử lý concurrency conflict; (5) 71/71 targeted platform tests pass (bao gồm 4/4 PlatformCenterMetadataAndAggregatesTests), 49/49 web tests pass, build 0 errors, 0 eslint errors, git diff --check clean. |
| **Checkpoint 6** | `feat(platform): harden suspension and platform self-security` | **COMPLETED** | Hoàn thành toàn diện củng cố quy trình đình chỉ và bảo mật cá nhân PlatformAdmin: (1) Đình chỉ trung tâm yêu cầu Reason bắt buộc (5-500 chars), tức thì tăng AuthVersion của toàn bộ người dùng trong trung tâm, thu hồi mọi active refresh token, ghi nhận số user và token bị hủy vào audit log; kích hoạt lại yêu cầu Quản lý chính Active và không phục hồi phiên cũ; (2) Quyền mới `platform.account.manage_own` (NonDelegable = true, IsSensitive = false, PlatformAdmin only) và migration `20260913165812_AddPlatformAccountManageOwnPermission.cs`; (3) DTOs, IPlatformMeService và PlatformMeService: `GetSecurityProfileAsync` (chỉ metadata an toàn, đếm active session), `ChangePasswordAsync` (yêu cầu mật khẩu hiện tại, password policy >= 12 chars, bump AuthVersion, revoke refresh tokens, redacted audit log), `RevokeSessionsAsync` (bump AuthVersion, revoke refresh tokens, audit log); (4) API endpoints trong `PlatformMeController`: GET `/api/v1/platform/me/security`, POST `/api/v1/platform/me/change-password`, POST `/api/v1/platform/me/revoke-sessions`; (5) Frontend: `PlatformSecurityModal.tsx` và tích hợp nút Bảo mật tài khoản trên navigation tabs của `PlatformCentersPage` và `PlatformAuditLogsPage`; cảnh báo tác động đình chỉ trên modal đổi trạng thái; (6) 82/82 targeted platform tests pass (bao gồm 4/4 PlatformSuspensionHardeningTests và 7/7 PlatformMeServiceTests), 49/49 web tests pass, build 0 errors, 0 eslint errors, git diff --check clean. |
| **Checkpoint 7** | `test(platform): complete relational and browser verification` | **COMPLETED** | Hoàn thành toàn diện kiểm thử quan hệ và trình duyệt: (1) 3.280/3.280 non-MySQL unit/integration tests pass (0 failed, 46 skipped); (2) 14/14 live MySQL integration tests pass trên Docker port 3307 (`PlatformMySqlIntegrationTests`); (3) EF Core drift check: 0 pending model changes (`dotnet ef migrations has-pending-model-changes`); (4) Đã áp dụng trơn tru cả 3 migrations (`20260913161857_AddPrimaryManagerToCenters`, `20260913163931_AddTargetCenterIdToAuditLogs`, `20260913165812_AddPlatformAccountManageOwnPermission`) lên DB MySQL container; (5) Frontend: 49/49 web tests pass (`npm test`), eslint 0 error/0 warning, build production clean (`tsc -b && vite build`); (6) 21/21 kịch bản Live E2E Stack Acceptance Scenarios pass 100% trên stack thực tế (`http://localhost:5000`); (7) Khắc phục triệt để lỗi EF MySQL in-memory `.Contains` bằng Expression tree AST OR-equality builder; tuân thủ chuẩn mực Global Query Filter cho PlatformMeService; `git diff --check` clean. |
| **Checkpoint 8** | `docs(platform): publish operational enhancement closeout` | **COMPLETED** | Hoàn tất đóng lại cột mốc và xuất bản báo cáo nghiệm thu: (1) Xuất bản báo cáo nghiệm thu kỹ thuật chính thức `docs/verification/POST_R09_OPS_VERIFICATION_REPORT.md`; (2) Ghi nhận đầy đủ 8 forward incremental commits từ baseline `77bf38412e99d174ccbb725075dd82a37b84971d`; (3) Tổng hợp bằng chứng kiểm thử 100% đạt chuẩn: 3.280 non-MySQL tests, 14 live MySQL tests, 49 web tests, 0 ESLint errors, build clean, 0 EF drift, 21/21 Live Acceptance Scenarios pass; (4) Tái khẳng định Phase H (Multi-Admin/MFA) và Phase I (Health/Metrics/Quotas) ở trạng thái DESIGNED / DEFERRED; (5) Đăng ký trạng thái kỹ thuật chính thức: `POST-R09 PLATFORM ADMIN OPERATIONAL ENHANCEMENT: TECHNICALLY VERIFIED / UX REVIEW PENDING`. |

## 20. Post-R09 CenterManager Operations Milestones & Execution Tracking

Lộ trình thực hiện củng cố vận hành quản trị trung tâm (POST-R09-CENTER-MANAGER-OPS) theo 11 Checkpoints:

| Checkpoint | Nội dung thực thi | Phạm vi | Trạng thái | Ghi chú & Bằng chứng |
|---|---|---|---|---|
| **Checkpoint 1** | `docs(center-manager): specify post-r09 operational completion` | Phần 1 (A) | **COMPLETED** | Khóa toàn diện đặc tả kỹ thuật, bất biến kiến trúc và lộ trình 11 checkpoint cho cột mốc POST-R09-CENTER-MANAGER-OPS: ADR-POST-R09-CENTER-MANAGER-OPERATIONS (APPROVED), CONSTITUTION (Mục 3.5), PROJECT_REQUIREMENTS (Mục 19), DATABASE_SCHEMA (invariants 40 bảng & permissions mới), API_CONTRACTS (Mục 20.1, 25.1, 25.2), UI_UX_SPEC (Mục 20.7), MASTER_PLAN (Mục 134), PROJECT_TRACKING (Mục 20). Zero source code change; bảo tồn 2 file DOCX. |
| **Checkpoint 2** | `test(center-manager): lock tenant and account-type boundaries` | Phần 1 (B) | **COMPLETED** | Bộ kiểm thử hồi quy bảo mật toàn diện: (1) `CenterManagerSecurityBoundaryTests.cs` (27/27 pass) chứng minh CenterManager bị từ chối 14/14 endpoint `/api/v1/platform/*` với HTTP 403 `AuthPermissionRequired`; (2) Teacher bị từ chối khỏi `/centers/me/dashboard`, `PATCH /centers/me`, CRUD Teacher/Student, phân quyền động; (3) Custom CenterManager role bị giới hạn theo effective permission; (4) Cross-tenant Teacher/Student/Class trả fail-closed NotFound qua `OrganizationOwnershipGuard`; (5) Catalog invariant xác nhận toàn bộ `platform.*` chỉ tương thích `PlatformAdmin`; (6) `TenantAdministratorGuard` bảo vệ bất biến last-admin; (7) Frontend `hardening.test.ts` (56/56 pass) khóa direct-URL `/quan-tri-nen-tang/*` cho CenterManager và bảo vệ boundary của Teacher. |
| **Checkpoint 3** | `feat(center-manager): complete center profile and dashboard UX` | Phần 1 (C) | **COMPLETED** | Hoàn thiện trang Hồ sơ và Giám sát trung tâm: (1) Mở rộng `organizationApi.ts` với `getCurrentCenter()`, `updateCurrentCenter(request)`; (2) Trang canonical `CenterProfilePage.tsx` tại `/quan-ly/trung-tam` với `CenterCode` và `Status` read-only, cập nhật `CenterName` và `Timezone` gửi canonical `RowVersion`, xử lý tức thì cache update không cần F5, thông báo lỗi OCC 409, 403, network và accessibility; (3) Audit và củng cố `CenterDashboardPage.tsx` bổ sung điều hướng hồ sơ trung tâm, capability-aware drilldowns, fallback truy cập; (4) Thêm 4 bài kiểm thử unit `centerProfilePage.test.ts` (60/60 web tests pass), eslint 0 error/0 warning, production build clean. |
| **Checkpoint 4** | `feat(center-manager): complete teacher and student account lifecycle` | Phần 1 (D) | **COMPLETED (CORRECTIVE VERIFIED)** | Hoàn thiện vòng đời Teacher (detail, update, soft-delete với active class guard, reset password an toàn) và Student (detail, update, xem lớp, subject goal, soft-delete bảo toàn 100% evidence/twins/assignments/attempts với `organization.students.delete`, password reset an toàn với `organization.teachers.reset_password` & `organization.students.reset_password`). Bổ sung migration `20260914133545_AddTeacherAndStudentResetPasswordPermissions`, `DeleteStudentUseCase`, `ResetAccountPasswordUseCase`, 28 backend unit tests, UI Modals trên `TeacherListPage` và `StudentListPage`, 9 frontend tests `accountLifecycle.test.ts`. Đã hoàn tất đợt Corrective: audit AfterData ghi đúng RowVersion sau mutation, API_CONTRACTS.md đồng bộ DTO và validation length, Teacher detail UI tích hợp getTeacher/modal và canonical classCount, loại bỏ triệt để trailing whitespace, migration 20260914133545 áp dụng live MySQL và bootstrap đầy đủ 11 role SYSTEM_CENTERMANAGER, 5/5 live MySQL tests pass, Docker API smoke test pass, 833 Org tests pass, 69 web tests pass, eslint 0 error, build production pass, EF model 0 drift. |
| **Checkpoint 5** | `feat(center-manager): complete class management lifecycle` | Phần 2 (E) | **COMPLETED** | Hoàn thiện toàn diện quản lý lớp học và thành viên lớp: (1) Capability gating: classesRead, classesCreate, classesUpdate, classesManageMembers, dashboardsCenterRead/dashboardsTeacherRead; (2) ClassListPage tích hợp actions column (Chi tiết, Sửa, Dashboard); (3) ClassDetailModal hiển thị thông tin lớp và danh sách thành viên kèm tìm kiếm/phân trang; (4) EditClassModal sửa tên lớp, giáo viên, trạng thái bằng RowVersion, môn học/năm học bất biến theo Contract 32; (5) AddStudentsModal tuyển chọn học sinh active theo lô và loại trừ thành viên hiện tại; (6) RemoveStudentModal xóa mềm thành viên, chuyển trạng thái Removed và bảo toàn 100% attempts/evidence; (7) 11 tests tại classListPage.test.ts, 80/80 web tests pass, 833/833 backend org tests pass, eslint 0 error, build production pass. |
| **Checkpoint 6** | `feat(center-manager): complete subject and knowledge graph lifecycle` | Phần 2 (F) | **DEFERRED (PHẦN 2)** | Chuyển sang thực hiện tại Phần 2 theo kế hoạch authoritative. |
| **Checkpoint 7** | `feat(center-manager): complete custom role and rbac matrix ux` | Phần 2 (G) | **DEFERRED (PHẦN 2)** | Chuyển sang thực hiện tại Phần 2 theo kế hoạch authoritative. |
| **Checkpoint 8** | `perf(web): split center admin routes and optimize bundle` | Phần 2 (H) | **DEFERRED (PHẦN 2)** | Chuyển sang thực hiện tại Phần 2 theo kế hoạch authoritative. |
| **Checkpoint 9** | `test(center-manager): complete relational and live e2e verification` | Phần 2 (I) | **DEFERRED (PHẦN 2)** | Chuyển sang thực hiện tại Phần 2 theo kế hoạch authoritative. |
| **Checkpoint 10** | `test(center-manager): complete chrome e2e acceptance` | Phần 2 (I) | **DEFERRED (PHẦN 2)** | Chuyển sang thực hiện tại Phần 2 theo kế hoạch authoritative. |
| **Checkpoint 11** | `docs(center-manager): publish operational completion closeout` | Phần 2 (I) | **DEFERRED (PHẦN 2)** | Chuyển sang thực hiện tại Phần 2 theo kế hoạch authoritative. |
