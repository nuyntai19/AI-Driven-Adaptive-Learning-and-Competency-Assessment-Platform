# EduTwin — Prompt Templates cho Human, Codex và Gemini

> Phiên bản: 2.1-draft
> Trạng thái: COURSE REBASELINE
> Mục đích: giao việc có kiểm soát, giữ provenance và ngăn scope/schema/authorization drift
> Chủ sở hữu: Team process owner; mọi thành viên có trách nhiệm tuân thủ

## 1. Cách sử dụng

Workflow mặc định cho mỗi work package:

1. Human owner chọn requirement ID, scope, acceptance và độ khó từ PROJECT_TRACKING.md.
2. Codex kiểm tra specification, dependency, security/data risk và viết hoặc review prompt triển khai.
3. Human owner phê duyệt prompt và gửi cho Gemini hoặc implementation agent được chọn.
4. Implementation agent chỉ sửa allow-list, chạy verification rồi dừng với working tree chưa stage.
5. Codex review diff độc lập, nêu finding theo severity và viết remediation prompt nếu cần.
6. Human owner quyết định accept/reject; chỉ gửi closeout prompt riêng khi review APPROVED.
7. Closeout agent chạy lại gate, stage explicit paths, commit/push khi và chỉ khi được ủy quyền rõ.
8. PROJECT_TRACKING.md được cập nhật bằng evidence thật; AI output không được tính là đóng góp cá nhân.

Codex và Gemini có thể đổi vai khi cần, nhưng mỗi prompt phải ghi rõ IMPLEMENTER hay REVIEWER. Không để cùng một lượt vừa tự triển khai, tự duyệt và tự closeout.

Không dùng prompt kiểu “hãy làm Phase này” mà thiếu human owner, requirement ID, expected HEAD, file scope, acceptance và test.

### 1.1. Cách phối hợp Human — Codex — Gemini

| Vai trò | Trách nhiệm | Không được làm |
|---|---|---|
| Human owner | Chốt requirement/scope, hiểu diff, xác nhận acceptance, chịu trách nhiệm commit/demo | Giao toàn bộ Phase mơ hồ hoặc nhận output AI mà không kiểm tra |
| Codex ở vai REVIEWER/PLANNER | Đối chiếu docs–source–diff, phản biện, phát hiện security/data drift, viết prompt/remediation cụ thể | Tự APPROVE thay stakeholder hoặc âm thầm sửa khi prompt chỉ review |
| Gemini/Codex ở vai IMPLEMENTER | Sửa đúng allow-list, thêm test, chạy gate, dừng unstaged để review | Mở rộng scope, đổi schema/API, lặp command/fix vô hạn |
| CLOSEOUT agent | Chỉ verify, stage exact paths, forward commit/push khi có APPROVED + ủy quyền | Sửa code, amend/rebase/force-push hoặc tự bắt đầu task sau |

Một task dùng một prompt riêng cho từng trạng thái: implementation → review → remediation (nếu có) → review lại → closeout. Không dán cùng lúc lệnh “sửa, tự duyệt và commit” cho một agent. Khi đổi Codex/Gemini hoặc hết quota, dùng Template L và luôn kiểm tra lại Git thay vì tin status report.

AI trong sản phẩm và AI Developer là hai khái niệm khác nhau. Gemini runtime chỉ là optional reasoning observation; Codex/Gemini developer chỉ hỗ trợ nhóm viết/review code. Không bên nào được thay con người sở hữu requirement, contribution hoặc quyết định cuối.

## 2. Mandatory Context Preamble

Dán nguyên khối này trước mọi prompt triển khai:

~~~text
BẠN ĐANG TRIỂN KHAI DỰ ÁN EDUTWIN.

AGENT ROLE: {{IMPLEMENTER / REVIEWER / CLOSEOUT}}
HUMAN OWNER: {{tên thành viên chịu trách nhiệm}}
TASK ID: {{Rxx-WPyy hoặc task ID được nhóm duyệt}}
REQUIREMENT IDS: {{LEC/FR/BR/SEC/DATA/AI/UX IDs}}
BASELINE SHA: {{full SHA của repository môn học}}
EXPECTED HEAD: {{full SHA trước khi bắt đầu}}

Trước khi làm bất kỳ thay đổi nào, bắt buộc đọc đầy đủ theo thứ tự:
1. PROJECT_REQUIREMENTS.md
2. CONSTITUTION.md
3. DATABASE_SCHEMA.md nếu task có data/persistence
4. API_CONTRACTS.md nếu task có HTTP/frontend integration
5. UI_UX_SPEC.md nếu task có UI/interaction
6. MASTER_PLAN.md
7. PROJECT_TRACKING.md và TEAM_ASSIGNMENT.md
8. CODEBASE_CHANGE_PLAN.md nếu task thuộc course migration
9. PROMPT_TEMPLATES.md

Thứ tự authority:
Yêu cầu giảng viên/stakeholder đã xác minh > quyết định nhóm đã duyệt > PROJECT_REQUIREMENTS > CONSTITUTION > DATABASE_SCHEMA/API_CONTRACTS > UI_UX_SPEC > MASTER_PLAN > PROJECT_TRACKING > PROMPT_TEMPLATES > source code hiện tại.

Các quy tắc không được vi phạm:
- Không tự ý đổi Database Schema, migration baseline, API contract, enum, thuật toán hoặc kiến trúc.
- Không tạo endpoint/table/package/pattern ngoài task.
- Không sửa file ngoài allow-list.
- Không đưa business logic vào Controller, React component hoặc DAL.
- Không gọi Gemini từ Controller.
- Không nhận centerId từ client; tenant lấy từ JWT/ITenantContext.
- Không bỏ Global Query Filter, ownership guard, transaction, validation hoặc test để làm nhanh.
- Không commit secret, API key, password, JWT key hoặc Refresh Token.
- Không triển khai tính năng ngoài requirement/task.
- Không tự suy đoán yêu cầu khách hàng hoặc đánh dấu stakeholder-validated khi chưa có bằng chứng.
- Không dùng legacy role OR dynamic permission để tạo đường cấp quyền rộng hơn.
- AI analysis không được trực tiếp quyết định mastery/risk/recommendation; phải qua Evidence Gate.
- Auth, RBAC, organization, content, assignment, submission, preliminary grading và deterministic fallback không được phụ thuộc Gemini/Internet.
- Nếu specification và source code mâu thuẫn, specification thắng; báo mâu thuẫn trước khi sửa.
- Nếu cần thay đổi decision đã duyệt, DỪNG và xuất Change Proposal. Không tự triển khai đề xuất.
- IMPLEMENTER không stage/commit/push nếu prompt không phải CLOSEOUT và không có ủy quyền rõ.
- Nếu lặp cùng command/fix hai lần mà không có tiến triển, command chạy quá 10 phút không có output, quota hết hoặc context bị hỏng: DỪNG command, không tiếp tục loop, báo trạng thái read-only.

Trước khi code, hãy trả lời ngắn:
1. Agent role, Task ID, requirement IDs và human owner là gì?
2. Branch/HEAD thực tế có đúng expected HEAD không?
3. Dependencies nào đã phải hoàn thành?
4. File/folder nào sẽ sửa và file nào tuyệt đối không sửa?
5. Invariant/rủi ro cao nhất là gì?
6. Test/gate nào sẽ chạy?
7. Điều kiện nào buộc dừng?
8. Có được stage/commit/push không?

Chỉ bắt đầu sau khi đã nêu tám điểm trên. Nếu agent role là REVIEWER, chỉ đọc và đánh giá; không tự sửa. Nếu là CLOSEOUT, không sửa source.
~~~

## 3. Template A — Phase/Task Implementation tổng quát

~~~text
VAI TRÒ:
Bạn là IMPLEMENTER {{Backend/Frontend/Full-stack}} triển khai đúng một task của EduTwin. Bạn không tự review/closeout.

TASK:
- Task ID: {{Pxx-Tyy}}
- Phase: {{Tên Phase}}
- Human owner: {{tên thành viên}}
- Requirement IDs: {{...}}
- Expected branch/HEAD: {{...}}
- Mục tiêu duy nhất: {{Mục tiêu cụ thể}}

CONTEXT NGHIỆP VỤ:
{{Mô tả use case và người dùng}}

DEPENDENCIES ĐÃ DONE:
{{Danh sách Task/Phase}}

SPECIFICATION PHẢI ĐỐI CHIẾU:
- PROJECT_REQUIREMENTS.md: {{requirement IDs}}
- UI_UX_SPEC.md: {{mục hoặc N/A}}
- CONSTITUTION.md: {{mục}}
- DATABASE_SCHEMA.md: {{table/mục}}
- API_CONTRACTS.md: {{endpoint/mục}}
- MASTER_PLAN.md: {{phase/task}}

FILE/FOLDER ĐƯỢC PHÉP SỬA:
- {{path 1}}
- {{path 2}}

FILE/FOLDER BỊ CẤM:
- Mọi path không nằm trong allow-list.
- Mọi tài liệu authoritative trừ khi task là documentation change đã duyệt.
- Migration đã merge.
- {{path cấm bổ sung}}

YÊU CẦU TRIỂN KHAI:
1. {{Yêu cầu 1}}
2. {{Yêu cầu 2}}
3. {{Yêu cầu 3}}

BUSINESS INVARIANTS:
- {{Invariant 1}}
- {{Invariant 2}}
- {{Tenant/ownership rule}}
- {{Transaction/idempotency rule}}

ACCEPTANCE CRITERIA:
- [ ] {{Tiêu chí quan sát được 1}}
- [ ] {{Tiêu chí quan sát được 2}}
- [ ] Không schema/API drift.
- [ ] Không sửa ngoài allow-list.
- [ ] Không secret/log nhạy cảm.

TEST BẮT BUỘC:
- {{Unit test cases}}
- {{Command build}}
- {{Command test}}
- {{Command/checkpoint local}}

QUY TẮC DỪNG:
Nếu cần sửa ngoài allow-list, thêm package, đổi schema/API hoặc thiếu dependency, dừng và báo BLOCKED kèm lý do. Không tự mở rộng scope.

OUTPUT BÀN GIAO:
1. Tóm tắt kết quả.
2. Danh sách file đã đổi.
3. Business rule đã triển khai.
4. Test/lệnh đã chạy và kết quả.
5. Assumption còn lại.
6. Risk/TODO hợp lệ.
7. Xác nhận không đổi schema/API ngoài task.
8. Xác nhận working tree chưa stage và không commit/push.
~~~

## 4. Template B — Database Entity/Configuration/Migration

Chỉ dùng ở P03–P05 hoặc khi Change Proposal schema đã được duyệt.

~~~text
TASK ID: {{P03/P04/P05 task}}

MỤC TIÊU:
Triển khai chính xác schema đã đóng băng cho các table:
{{Danh sách table}}

NGUỒN SỰ THẬT:
DATABASE_SCHEMA.md mục {{...}}.

ALLOW-LIST:
- src/EduTwin.DAL/{{module}}/**
- {{entity location nếu entity thuộc DAL theo cấu trúc đã duyệt}}
- Migration mới: {{tên migration}}
- tests chỉ khi task yêu cầu

CẤM:
- Sửa migration đã merge.
- Đổi table/column/type/nullability/key/index.
- Dùng Guid/BIGINT khác Hybrid PK Strategy.
- Bỏ center_id, composite tenant FK, audit, soft delete hoặc row_version.
- Dùng cascade delete cho Attempt/Analysis/History.
- Tạo table/column ngoài schema.

CHECKLIST TRIỂN KHAI:
- [ ] Entity mapping snake_case.
- [ ] Precision/length/nullability explicit.
- [ ] PK/alternate key/composite FK.
- [ ] Index/unique/check constraint.
- [ ] Delete behavior explicit.
- [ ] Global filter compatibility.
- [ ] JSON conversion/value comparer nếu cần.
- [ ] UTC/audit/concurrency.
- [ ] Migration có tên nghiệp vụ.

ACCEPTANCE:
- Migration apply từ database trống.
- Apply toàn bộ migration theo thứ tự.
- Schema đối chiếu INFORMATION_SCHEMA.
- Cross-tenant FK fixture bị từ chối.
- Build/test xanh.

OUTPUT:
Liệt kê từng table và xác nhận column/key/index đã map. Nếu provider không hỗ trợ một contract, báo BLOCKED; không tự thay schema.
~~~

## 5. Template C — Backend API Use Case

~~~text
TASK ID: {{...}}
ENDPOINT:
{{METHOD /api/v1/path}}

AUTHORIZATION:
- Required permission: {{permission code hoặc legacy policy trong migration window}}
- Account type/profile prerequisite: {{Student/Teacher/CenterManager/N/A}}

CONTRACT:
API_CONTRACTS.md mục {{...}}.

TABLE:
DATABASE_SCHEMA.md mục {{...}}.

ALLOW-LIST:
- src/EduTwin.API/{{module}}/**
- src/EduTwin.BLL/{{module}}/**
- src/EduTwin.DAL/{{module}}/**
- src/EduTwin.Contracts/{{module}}/**
- tests/EduTwin.BLL.Tests/{{module}}/**

YÊU CẦU LAYER:
- Controller chỉ validate boundary/gọi BLL/map HTTP.
- Business rule và ownership trong BLL.
- Query/persistence trong DAL.
- Không trả Entity.

TENANT/AUTHORIZATION:
- center_id từ ITenantContext.
- Permission/policy: {{...}}.
- Ownership: {{...}}.
- Cross-tenant/missing resource trả 404.
- Test thiếu permission, over-grant/self-elevation nếu liên quan.

TRANSACTION:
{{Không cần / mô tả transaction exact}}

IDEMPOTENCY/CONCURRENCY:
{{clientSubmissionId/rowVersion/không áp dụng}}

REQUEST:
{{Dán JSON request từ API_CONTRACTS.md}}

SUCCESS RESPONSE:
{{Dán JSON response/status}}

ERROR CASES:
- {{400}}
- {{404}}
- {{409}}
- {{422}}

UNIT TEST:
- Happy path.
- Validation boundary.
- Role/ownership.
- Cross-tenant.
- Concurrency/idempotency.
- Transaction rollback nếu áp dụng.

CẤM:
- Đổi status/field/enum.
- Nhận centerId từ body/query.
- Query DbContext trong Controller.
- Tạo endpoint phụ.
~~~

## 6. Template D — BLL Algorithm

Dùng cho Mastery, Risk, Opportunity Gap, DAG, replay hoặc job state machine.

~~~text
TASK ID: {{...}}
ALGORITHM: {{Mastery/Risk/Opportunity/DAG/Replay/Job State}}
VERSION: {{mastery-v1/...}}

FORMULA/INVARIANT:
{{Dán nguyên formula và rule từ CONSTITUTION/MASTER_PLAN}}

INPUT:
{{Danh sách input, unit, range, null semantics}}

OUTPUT:
{{Danh sách output, precision, explanation/breakdown}}

PURE CORE:
- Calculator lõi phải deterministic.
- Không truy cập clock/random/network/database trực tiếp.
- Dependency thời gian qua abstraction khi orchestration cần.
- Decimal rounding chỉ tại boundary đã chỉ định.

ALLOW-LIST:
- src/EduTwin.BLL/{{module}}/**
- src/EduTwin.Contracts/{{module}}/**
- tests/EduTwin.BLL.Tests/{{module}}/**

CẤM:
- Điều chỉnh weight cho “hợp data”.
- Dùng ML/LLM thay heuristic.
- Bỏ calculationVersion/breakdown/explanation.
- Thay đổi tie-break.

TEST MATRIX:
{{Dán toàn bộ boundary cases từ MASTER_PLAN}}

ACCEPTANCE:
- Tất cả test deterministic.
- Sai số decimal <= 0.01 nếu áp dụng.
- Coverage nhóm calculator >=80%.
- Cùng input luôn cùng output.
- Không schema/API drift.

OUTPUT:
Ngoài file/test, cung cấp bảng input → expected → actual cho các case chuẩn.
~~~

## 7. Template E — Gemini/AI Integration

~~~text
TASK ID: {{R05/P12 task}}
MỤC TIÊU: {{Adapter/parser/orchestrator/fallback}}

CONTRACT:
- API_CONTRACTS.md mục 71–73.
- DATABASE_SCHEMA.md mục 33–41.
- CONSTITUTION.md mục AI governance.

ALLOW-LIST:
- src/EduTwin.BLL/AssessmentAndReasoning/**
- src/EduTwin.DAL/AssessmentAndReasoning/**
- src/EduTwin.Contracts/AssessmentAndReasoning/**
- src/EduTwin.API cấu hình DI liên quan
- tests/EduTwin.BLL.Tests/AssessmentAndReasoning/**

CẤM:
- Gọi Gemini trong Controller.
- Lưu raw input/output vào DB.
- Log API key/JWT/password.
- Gửi username/full profile/center name.
- Xoay nhiều API key.
- Retry quá một lần.
- Tạo chatbot/history/vector search.
- Cho AI cập nhật Knowledge Graph.
- Cho Gemini trả mastery delta, risk score, recommendation rank hoặc authorization decision.
- Bỏ qua Evidence Gate để cập nhật Twin trực tiếp.

STRUCTURED OUTPUT:
{{Dán Gemini response schema}}

VALIDATION:
- JSON strict.
- schemaVersion.
- range.
- enum.
- rootCause IDs cùng tenant/subject.
- feedback/language.

FAILURE POLICY:
- Lần 1 fail → retry 1.
- Lần 2 fail → Rule-based fallback.
- reasoningQuality null.
- needsTeacherReview true.
- Job FallbackCompleted.
- Evidence Gate ReviewOnly, reasoningWeight 0 và Knowledge Mastery không đổi.

TEST:
- Valid.
- Malformed.
- Invalid semantic.
- Timeout then success.
- Fail twice.
- Cancellation.
- vi/en.

OUTPUT:
Nêu dữ liệu nào được gửi provider, dữ liệu nào tuyệt đối không gửi, và bằng chứng retry/fallback đúng.
~~~

## 8. Template F — BackgroundService/Job

~~~text
TASK ID: {{P11-Txx}}
MỤC TIÊU: {{Claim/process/recover/poll}}

JOB STATE MACHINE:
{{Dán state machine P11}}

ALLOW-LIST:
- src/EduTwin.API/AssessmentAndReasoning/Background/**
- src/EduTwin.BLL/AssessmentAndReasoning/**
- src/EduTwin.DAL/AssessmentAndReasoning/**
- tests/EduTwin.BLL.Tests/AssessmentAndReasoning/**

INVARIANTS:
- Database là durable source; không dùng in-memory queue làm nguồn duy nhất.
- Mỗi job có DI scope riêng.
- Tenant context dựng từ persisted center_id.
- Lease + concurrency ngăn xử lý trùng.
- Unique analysis theo attempt.
- Terminal job không chạy lại.
- Expired lease được recover.
- Cancellation không mark Completed sai.

CẤM:
- Dùng HttpContext/User claims trong worker.
- Giữ DbContext singleton.
- Busy loop không delay.
- Mark job terminal trước transaction nghiệp vụ.

TEST:
- Competing workers.
- Lease expiry.
- API restart.
- Cancellation.
- Duplicate attempt/job.
- Tenant scope.

OUTPUT:
Mô tả state transition và transaction boundary thực tế.
~~~

## 9. Template G — React Feature

~~~text
TASK ID: {{...}}
FEATURE/PAGE: {{...}}
ROLE: {{...}}

API CONTRACT:
{{Endpoint/DTO mục API_CONTRACTS.md}}

WIREFRAME:
{{Mục MASTER_PLAN hoặc dán wireframe}}

ALLOW-LIST:
- web/edutwin-web/src/features/{{feature}}/**
- web/edutwin-web/src/pages/{{page}}/**
- web/edutwin-web/src/components/{{shared component cụ thể}}/**
- web/edutwin-web/src/api/{{module}}/**
- web/edutwin-web/src/types/{{module}}/**

CẤM:
- Sửa backend/schema/API contract.
- Dùng centerId do user nhập.
- Lưu Refresh Token ở JS storage.
- Nhân bản server state vào Zustand.
- Hardcode mock response khi backend endpoint đã có.
- Hiển thị đáp án trong Student view.
- Thêm i18n framework.

STATE:
- TanStack Query cho server state.
- Zustand chỉ auth/UI state.
- Loading.
- Empty.
- Error + traceId.
- Success.
- Polling/stale nếu áp dụng.

UI:
- 100% label/menu/message tiếng Việt.
- Nội dung Question/Reasoning có thể vi/en.
- Responsive tối thiểu.
- Accessible label.
- Chart có text fallback.

ACCEPTANCE:
- {{User flow}}
- Không duplicate submit.
- Cache key đúng user/subject/resource.
- Logout clear cache.
- Build frontend thành công.

OUTPUT:
Danh sách route/component/query key và các UI state đã xử lý.
~~~

## 10. Template H — Unit Test chuyên biệt

~~~text
TASK ID: {{...}}
TARGET:
{{Class/service/calculator}}

SPEC:
{{Mục formula/invariant}}

ALLOW-LIST:
- tests/EduTwin.BLL.Tests/{{module}}/**
- Chỉ sửa production code nếu phát hiện bug thật và path đó được bổ sung allow-list.

TEST STYLE:
- Arrange/Act/Assert rõ.
- Tên test: Method_Scenario_Expected.
- Không gọi network/Gemini/MySQL thật.
- Không dùng DateTime.UtcNow trực tiếp.
- Không phụ thuộc thứ tự test.
- Mock ở boundary, không mock pure calculator.

CASES BẮT BUỘC:
{{Danh sách happy/boundary/error/tenant/concurrency}}

ACCEPTANCE:
- Test đỏ trước bug fix nếu đây là regression.
- dotnet test xanh.
- Coverage target {{...}}.
- Không test implementation detail vô nghĩa.

OUTPUT:
Bảng test case, invariant được bảo vệ, kết quả.
~~~

## 11. Template I — Code Review

Đây là prompt giao cho Codex hoặc reviewer; mặc định review read-only, không sửa.

~~~text
VAI TRÒ:
Bạn là Principal Software Architect review Task {{Task ID}} của EduTwin.

KHÔNG SỬA CODE. Chỉ review.

ĐỌC:
- Năm specification.
- Git diff của branch/task.
- Test output được cung cấp.

SCOPE EXPECTED:
{{Allow-list và mục tiêu}}

REVIEW THEO THỨ TỰ:
1. Correctness so với acceptance.
2. Schema/API contract drift.
3. Tenant isolation/ownership.
4. Security/secret.
5. Transaction/idempotency/concurrency.
6. Layer dependency/business logic placement.
7. AI validation/fallback nếu có.
8. Test coverage/boundary.
9. Performance/N+1 trong query quan trọng.
10. Maintainability vừa đủ, không over-engineering.

MỖI FINDING PHẢI CÓ:
- Priority: P0/P1/P2/P3.
- File và line.
- Invariant bị vi phạm.
- Kịch bản tái hiện/tác động.
- Hướng sửa ngắn; không viết lại toàn bộ feature.

KẾT LUẬN:
- APPROVED.
- APPROVED WITH NON-BLOCKING NOTES.
- CHANGES REQUIRED.
- BLOCKED BY SPECIFICATION.

Nếu không có finding, nói rõ không có actionable finding. Không bịa lỗi.
~~~

## 12. Template J — Sửa lỗi sau Review

~~~text
TASK:
Sửa đúng các finding đã được chấp nhận cho {{Task ID}}.

FINDINGS ĐƯỢC PHÉP SỬA:
{{Danh sách finding ID}}

ALLOW-LIST:
{{File cụ thể}}

CẤM:
- Refactor ngoài finding.
- Đổi schema/API.
- Sửa test để che bug.
- Giảm validation/authorization.

QUY TRÌNH:
1. Tái hiện bug bằng test nếu phù hợp.
2. Sửa tối thiểu.
3. Chạy test cũ + regression.
4. Báo từng finding → file/test chứng minh.

OUTPUT:
- Finding nào fixed.
- File changed.
- Test before/after.
- Finding nào chưa fixed và lý do.
~~~

## 13. Template K — Change Proposal

AI Developer phải dùng template này thay vì tự thay specification.

~~~text
CHANGE PROPOSAL

Change ID: CP-{{YYYYMMDD-NN}}
Phát hiện tại Task: {{Task ID}}
Trạng thái đề xuất: PENDING

1. Vấn đề
{{Mô tả cụ thể; không nói chung chung}}

2. Bằng chứng
{{Contract/code/provider limitation/test failure}}

3. Frozen decision bị ảnh hưởng
- CONSTITUTION.md mục:
- DATABASE_SCHEMA.md mục:
- API_CONTRACTS.md mục:
- MASTER_PLAN.md mục:

4. Phương án A — Giữ nguyên
- Cách làm:
- Ưu:
- Nhược:
- Tác động:

5. Phương án B — Thay đổi
- Cách làm:
- Ưu:
- Nhược:
- Migration impact:
- API impact:
- Test impact:
- Timeline impact:

6. Khuyến nghị của AI Developer
{{Một phương án + lý do}}

7. Files dự kiến thay đổi nếu được duyệt
{{List}}

8. Quyết định
Chưa được phép triển khai cho đến khi human decision owner và reviewer phê duyệt.
~~~

## 14. Template L — Continuation/Handoff giữ ngữ cảnh

Dùng khi đổi Codex ↔ Gemini, đổi người thực hiện, quota hết hoặc phiên chat bị dài.

~~~text
EDUTWIN HANDOFF

Specification version: 2.1-draft
Source repository/full baseline SHA: {{...}}
Course repository/baseline SHA: {{...}}
Human owner: {{...}}
Requirement IDs: {{...}}
Current branch: {{...}}
Current full HEAD: {{...}}
Current Phase/Task: {{...}}
Last approved Phase: {{...}}
Reviewer status: {{APPROVED/...}}

Đã hoàn thành:
- {{...}}

Chưa hoàn thành:
- {{...}}

Files đã thay đổi:
- {{...}}

Migration hiện tại:
- Last migration: {{...}}
- Database state: {{...}}

API đã hoàn thành:
- {{...}}

Tests:
- Command:
- Passed:
- Failed:
- Skipped và lý do:
- Coverage:

Process status:
- Running command/PID: {{none hoặc chi tiết}}
- Staged files: {{none/list}}
- Untracked/temp files: {{none/list}}
- Commit/push authorization: {{no/yes-closeout-only}}

Decisions tuyệt đối không được quên:
- Multi-tenant center_id từ JWT.
- Permission + tenant + resource scope; không legacy OR dynamic authorization.
- AI analysis phải qua Evidence Gate.
- Không schema/API change ngoài quyết định đã duyệt.
- {{task-specific invariants}}

Known issues:
- {{...}}

Next exact action:
{{Một hành động cụ thể}}

Trước khi tiếp tục, agent mới phải đọc tài liệu authoritative liên quan, kiểm tra branch/HEAD/status và tự xác nhận allow-list. Không tin báo cáo handoff nếu Git/diff/test evidence không khớp.
~~~

## 15. Template M — Phase Acceptance

~~~text
PHASE ACCEPTANCE REVIEW: {{Pxx}}

MỤC TIÊU PHASE:
{{...}}

TASK STATUS:
- {{Task}}: {{Done/Blocked}}

EVIDENCE:
- Build:
- Test:
- Coverage:
- Migration:
- Docker checkpoint:
- API/UI demo:

CHECK:
- [ ] Không schema drift.
- [ ] Không API drift.
- [ ] Tenant isolation.
- [ ] Authorization/ownership.
- [ ] Transaction/idempotency.
- [ ] Error/loading/empty state.
- [ ] No secret.
- [ ] Git diff đúng scope.
- [ ] Definition of Done đạt.

REVIEW FINDINGS:
{{...}}

DECISION:
{{APPROVED / CHANGES REQUIRED / BLOCKED}}

NEXT:
Chỉ cho phép bắt đầu {{next phase}} nếu APPROVED.
~~~

## 16. Template N — Final MVP Acceptance

~~~text
EDUTWIN MVP FINAL ACCEPTANCE

Kiểm tra từ clean clone:
1. Tạo .env local.
2. docker compose up --build.
3. Migration/seed.
4. Login ba role.
5. Multi-tenant Center A/B.
6. Assignment flow.
7. Gemini success flow.
8. Gemini fallback flow.
9. Evidence Gate chứng minh fallback không đổi Knowledge Mastery.
10. Dynamic role/permission UI + API denial + audit.
11. Twin/History/Recommendation.
12. Teacher Override replay.
13. Student/Teacher/Center Dashboard.
14. MySQL constraint/integration verification.
15. Backend/frontend build/test/coverage và GitHub Actions.
16. Secret scan thủ công.

Với mỗi bước ghi:
- Expected.
- Actual.
- Evidence.
- Pass/Fail.

Không sửa code trong quá trình acceptance. Mọi lỗi được tạo finding và quay lại bug-fix task có allow-list.
~~~

## 17. Prompt mẫu hoàn chỉnh — Mastery Calculator

~~~text
TASK ID: P13-T01
MỤC TIÊU: Triển khai pure Mastery Calculator version mastery-v1.

Đọc Mandatory Context Preamble và các tài liệu authoritative liên quan.

DEPENDENCIES: P12 APPROVED.

ALLOW-LIST:
- src/EduTwin.BLL/DigitalTwin/**
- src/EduTwin.Contracts/DigitalTwin/**
- tests/EduTwin.BLL.Tests/DigitalTwin/**

CẤM:
- DATABASE_SCHEMA.md/API_CONTRACTS.md/migration.
- Mọi weight khác Constitution mục 13.
- Database/network/clock trong calculator.

FORMULA:
EvidenceTarget và NewMastery đúng CONSTITUTION.md mục 13.

ACCEPTANCE CASE:
- M=0, R=.20, C=1, T=1, K=1, D=1 → 5.00.
- M=50, R=.80, C=1, T=1, K=1, D=1 → 57.50.
- M=50, R=.80, C=1, T=1, K=1, D=1, Reduced weight=.5 → 53.75.
- M=0, fallback/ReviewOnly, weight=0 → 0.00.
- Clamp và boundary đầy đủ theo P13.

OUTPUT:
Tóm tắt, files, test matrix expected/actual, coverage và xác nhận không đổi formula.
~~~

## 18. Prompt mẫu hoàn chỉnh — Student Learning Player

~~~text
TASK ID: P15-T03 + P15-T04
MỤC TIÊU: Learning Player submit Attempt một lần và polling AI Job.

DEPENDENCIES: P14 APPROVED; backend endpoints 52–54 đã chạy.

ALLOW-LIST:
- web/edutwin-web/src/features/learning/**
- web/edutwin-web/src/pages/student/**
- web/edutwin-web/src/api/learning/**
- web/edutwin-web/src/types/learning/**

CẤM:
- Backend/schema/specification.
- Zustand server-state duplication.
- Refresh Token storage.
- Hardcoded centerId/correct answer.
- Resubmit Attempt khi polling/fetch lỗi.

FLOW:
1. Student nhập finalAnswer/reasoningText/confidence.
2. Sinh clientSubmissionId một lần cho submission.
3. POST /learning/attempts.
4. Lưu attemptId/jobId phù hợp để component remount không resubmit.
5. Poll pollUrl mỗi 3000ms.
6. Dừng terminal.
7. Fetch feedback.
8. Hiển thị fallback/review warning khi cần.

UI STATE:
Idle, Validating, Submitting, Pending, Processing, Completed, FallbackCompleted, FailedTerminal, FetchError.

ACCEPTANCE:
- Double click không tạo Attempt trùng.
- Refresh/re-render không resubmit.
- Polling dừng terminal.
- Error message tiếng Việt.
- Build frontend xanh.

OUTPUT:
Routes/components/query keys/state handling/files changed.
~~~

## 19. Anti-pattern prompts bị cấm

Không giao các prompt sau:

- “Hãy code toàn bộ EduTwin.”
- “Hãy tự thiết kế database tốt nhất.”
- “Nếu thấy cần thì thêm endpoint/table.”
- “Refactor toàn bộ theo Clean Architecture.”
- “Làm cho chạy được, test sau.”
- “Dùng bất kỳ package nào bạn muốn.”
- “Tự sửa các file liên quan.”
- “Bỏ tenant/auth để demo trước.”
- “Cứ chạy/sửa tiếp cho đến khi mọi test pass” mà không có stop condition.
- “Tự commit tất cả thay đổi liên quan.”
- “Ẩn nút là đủ phân quyền.”
- “Dùng AI confidence trực tiếp để cập nhật hồ sơ học sinh.”

Những prompt này phá scope, làm mất traceability và khiến AI Developer tự thay frozen decision.

## 20. Checklist trước khi gửi prompt

- [ ] Có Mandatory Context Preamble.
- [ ] Một Task ID rõ.
- [ ] Có human owner và requirement IDs.
- [ ] Có agent role và branch/expected full HEAD.
- [ ] Dependencies đã APPROVED.
- [ ] Allow-list cụ thể.
- [ ] Contract/schema mục cụ thể.
- [ ] Business invariant.
- [ ] Acceptance observable.
- [ ] Test command/cases.
- [ ] Stop rule.
- [ ] Ghi rõ có hay không quyền stage/commit/push.
- [ ] Handoff output.

## 21. Checklist khi nhận kết quả từ AI Developer

- [ ] AI đã liệt kê files changed.
- [ ] Không file ngoài allow-list.
- [ ] Không migration/schema/API ngoài task.
- [ ] Build/test evidence thật, không chỉ nói “sẽ chạy”.
- [ ] Không TODO che acceptance.
- [ ] Không secret.
- [ ] Có tenant/ownership handling.
- [ ] Có test boundary.
- [ ] Có assumption/risk.
- [ ] Đưa diff cho Codex review trước merge.
- [ ] Báo cáo Git/status/test được kiểm chứng, không chỉ tự tuyên bố.
- [ ] AI không được ghi nhận là người đóng góp thay thành viên.

## 22. Template O — Requirements/Documentation Audit

~~~text
AGENT ROLE: REVIEWER
HUMAN OWNER: {{...}}
TASK ID: {{R00/R01 task}}
EXPECTED BRANCH/FULL HEAD: {{...}}

MỤC TIÊU DUY NHẤT:
Đối chiếu yêu cầu giảng viên/stakeholder, tài liệu authoritative và source hiện tại. Không sửa source/schema/migration.

INPUT EVIDENCE:
- Lecturer notes/recording/date: {{...}}
- Stakeholder interview/approval: {{...}}
- Existing documents: {{...}}
- Source/migration audit commit: {{...}}

CHECK:
1. Requirement nào fact, reported confirmation, hypothesis hoặc chưa xác minh.
2. Tài liệu nào authoritative, stale, duplicate, empty hoặc cần archive.
3. Mâu thuẫn giữa requirements, schema, API, UI và source.
4. Tuyên bố Done nào thiếu test/demo/owner evidence.
5. Quyết định nào cần Change Proposal.

CẤM:
- Không mặc định đồng ý.
- Không tự biến source hiện tại thành requirement.
- Không xóa tài liệu trước khi nội dung duy nhất đã được merge/trace.
- Không stage/commit/push.

OUTPUT:
- Findings theo severity và bằng chứng.
- Assumption/TBD cần con người xác nhận.
- Danh sách file đề xuất update/create/merge/archive/delete và lý do.
- Roadmap thay đổi tăng dần, không rewrite hệ thống.
~~~

## 23. Template P — Dynamic RBAC theo Center

~~~text
AGENT ROLE: IMPLEMENTER
HUMAN OWNER: {{...}}
TASK/REQUIREMENT IDS: {{R02/R03/R04 + SEC/FR IDs}}
EXPECTED BRANCH/FULL HEAD: {{...}}
MIGRATION SLICE: {{catalog/roles/role-permissions/user-roles/audit/cutover/UI}}

SOURCE OF TRUTH:
- PROJECT_REQUIREMENTS.md: dynamic authorization requirements.
- CONSTITUTION.md: Identity và authorization.
- DATABASE_SCHEMA.md: Module 6.
- API_CONTRACTS.md: mục 66–70.
- UI_UX_SPEC.md: capability-first navigation và màn hình quản trị quyền.

INVARIANTS:
- Không có Platform Admin; CenterManager chỉ trong Center của mình.
- Permission catalog source-defined; UI không tạo permission code tùy ý.
- Role có accountType immutable; permission phải có mapping account type và user chỉ nhận role cùng account type.
- Effective permission = union active roles; không explicit deny ở v1.
- Permission + tenant + resource scope đều bắt buộc.
- Student/Teacher target: actor quản trị permission Active + delegable + compatible dù actor không sở hữu operational permission khác account type.
- CenterManager target: permission/effective permission mới phải là subset actor; chặn self-elevation, over-grant và thao tác làm mất CenterManager Active cuối có đủ TenantAdminCorePermissionsV1.
- Mutation + audit + users.auth_version/token invalidation phải atomic; JSON authorizationVersion chỉ là projection của cột này.
- Endpoint đã cutover không dùng legacy role OR dynamic permission.

ALLOW-LIST:
{{explicit paths}}

MIGRATION/COMPATIBILITY:
{{permission_account_types seed, account-type composite FK, backfill, validation query, rollback, endpoint cutover list}}

TEST BẮT BUỘC:
- Same-tenant happy path.
- Missing permission 403.
- Cross-tenant 404.
- Role–permission và user–role account-type mismatch bị API/BLL/DB từ chối.
- Student/Teacher delegation không bị chặn sai bởi actor-own rule.
- CenterManager self-elevation/over-grant denied.
- Last admin + concurrency conflict.
- Audit rollback and token version invalidation.
- UI direct URL, hidden/disabled action và handcrafted API request.

STOP:
Nếu schema/API chưa được duyệt, cần permission mới ngoài catalog, hoặc slice tạo quyền rộng hơn legacy baseline: dừng và xuất Change Proposal. Không stage/commit/push.
~~~

## 24. Template Q — Evidence Gate và Twin Mutation

~~~text
AGENT ROLE: IMPLEMENTER
HUMAN OWNER: {{...}}
TASK/REQUIREMENT IDS: {{R05/R06 + AI/DATA IDs}}
EXPECTED BRANCH/FULL HEAD: {{...}}

MỤC TIÊU:
{{Gate policy / persistence / orchestrator / review projection / replay}}

CONTRACT:
- CONSTITUTION.md mục 12–13.
- DATABASE_SCHEMA.md: evidence_assessments và invariant completion.
- API_CONTRACTS.md: Evidence Gate contract.

DECISION TABLE:
- AI + Trusted + AIWeighted: structurally/semantically valid, không contradiction, confidence 80–100, weight 1.0.
- AI + Reduced + AIWeighted: structurally/semantically valid, không contradiction nghiêm trọng, confidence 50–79, weight 0.5.
- AI + ReviewOnly + AIWeighted: confidence <50, missing/anomaly/contradiction, weight 0.
- RuleFallback + ReviewOnly + DeterministicOnly: AI unavailable/invalid sau retry, weight 0.
- TeacherOverride + Trusted + HumanConfirmed: authorized override, weight 1.0 và replay.

INVARIANTS:
- Gate pure/deterministic; không gọi AI/network/database trong calculator.
- sourceType, trustLevel và decisionMode là ba enum độc lập; Replay chỉ là history event.
- policyVersion và reasonCodes bắt buộc.
- Fallback/ReviewOnly không đổi Knowledge Mastery.
- Dữ liệu quan sát được có thể cập nhật Behavior Twin độc lập.
- Persist analysis + assessment + conditional Twin/history/job state theo transaction đã đặc tả.
- Replay không sửa/xóa AI output hoặc assessment cũ.

TEST:
- Boundary confidence 0, 49, 50, 79, 80, 100 và null.
- Malformed/semantic anomaly/fallback.
- Reduced delta chính xác.
- ReviewOnly mastery unchanged.
- Idempotency, retry, transaction rollback và replay order.
- Permission/resource scope cho review/override.

STOP:
Không tự đổi threshold/weight/formula hoặc backfill trust từ dữ liệu thiếu. Không stage/commit/push.
~~~

## 25. Template R — Weekly Progress và Contribution Evidence

~~~text
WEEK: {{số tuần, from–to}}
TEAM: {{tên nhóm}}
REPOSITORY/BASELINE SHA: {{...}}

LECTURER/STAKEHOLDER INPUT MỚI:
- Date/source/person:
- Requirement/decision IDs:
- Evidence link:
- Impact:

WORK PACKAGE STATUS:
| WP | Human owner | Reviewer | Difficulty points | Planned | Done criteria met | Commit/PR/test evidence | Blocker |
|---|---|---|---:|---:|---:|---|---|

DEMO ĐÃ CHẠY:
- Scenario:
- Expected/actual:
- Evidence:

RISK/DECISION:
- New risk:
- Decision needed:
- Owner/deadline:

CONTRIBUTION:
Chỉ ghi công việc con người có thể giải thích và bảo vệ. Không dùng số commit/dòng code hoặc AI output làm thước đo duy nhất.

NEXT WEEK:
- Work package, owner, reviewer, dependency và acceptance cụ thể.
~~~

## 26. Template S — Strict Closeout sau APPROVED

~~~text
AGENT ROLE: CLOSEOUT
TASK ID/HUMAN OWNER: {{...}}
CODEX/REVIEW VERDICT: APPROVED
EXPECTED BRANCH/FULL HEAD: {{...}}
EXACT ALLOW-LIST: {{paths}}
COMMIT MESSAGE: {{...}}
PUSH TARGET: {{remote/branch}}

CẤM:
- Không sửa source/tài liệu trong closeout.
- Không dùng git add . hoặc wildcard.
- Không amend/reset/revert/rebase/merge/force-push.
- Không đọc/in .env.
- Gate fail thì dừng trước commit.

PRE-COMMIT:
- Verify branch/full HEAD/status/staged/untracked/diff-check/.env tracking.
- Direct scan bytes của allow-list: UTF-8 no BOM, không U+FFFD/merge marker/trailing whitespace, có EOF newline.
- Chạy exact build/targeted/full/EF/frontend/MySQL gates của task.

STAGE:
Chỉ git add -- với từng exact path. Xác minh staged set bằng allow-list equality; yêu cầu không còn unstaged/untracked ngoài ignored files đã biết.

POST-COMMIT/PUSH:
- Verify commit file set, git show --check, full diff check, clean tree và ahead/behind 0 0.
- Báo hash/message/file list/test/push evidence.
- Dừng; không bắt đầu task kế tiếp.
~~~
