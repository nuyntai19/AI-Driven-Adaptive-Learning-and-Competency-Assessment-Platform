# EduTwin — Prompt Templates cho Claude/Gemini

> Phiên bản: 1.0  
> Trạng thái: FROZEN  
> Mục đích: giao việc có kiểm soát, giữ ngữ cảnh và ngăn schema/architecture drift  

## 1. Cách sử dụng

Mỗi lần giao việc:

1. Chỉ giao một Task ID hoặc một nhóm task rất nhỏ cùng Phase.
2. Dán Mandatory Context Preamble.
3. Dán template chuyên biệt.
4. Điền đầy đủ placeholder trong dấu {{...}}.
5. Nêu file/folder được phép sửa bằng allow-list.
6. Yêu cầu AI Developer dừng nếu cần sửa ngoài allow-list.
7. Sau khi code xong, dùng Code Review Template cho Codex/AI Reviewer.

Không dùng prompt kiểu “hãy làm Phase này” mà thiếu file scope, acceptance và test.

## 2. Mandatory Context Preamble

Dán nguyên khối này trước mọi prompt triển khai:

~~~text
BẠN ĐANG TRIỂN KHAI DỰ ÁN EDUTWIN.

Trước khi làm bất kỳ thay đổi nào, bắt buộc đọc đầy đủ theo thứ tự:
1. CONSTITUTION.md
2. DATABASE_SCHEMA.md
3. API_CONTRACTS.md
4. MASTER_PLAN.md
5. PROMPT_TEMPLATES.md

Thứ tự authority:
CONSTITUTION > DATABASE_SCHEMA > API_CONTRACTS > MASTER_PLAN > PROMPT_TEMPLATES > source code hiện tại.

Các quy tắc không được vi phạm:
- Không tự ý đổi Database Schema, migration baseline, API contract, enum, thuật toán hoặc kiến trúc.
- Không tạo endpoint/table/package/pattern ngoài task.
- Không sửa file ngoài allow-list.
- Không đưa business logic vào Controller, React component hoặc DAL.
- Không gọi Gemini từ Controller.
- Không nhận centerId từ client; tenant lấy từ JWT/ITenantContext.
- Không bỏ Global Query Filter, ownership guard, transaction, validation hoặc test để làm nhanh.
- Không commit secret, API key, password, JWT key hoặc Refresh Token.
- Không triển khai tính năng ngoài MVP.
- Nếu specification và source code mâu thuẫn, specification thắng; báo mâu thuẫn trước khi sửa.
- Nếu cần thay đổi frozen decision, DỪNG và xuất Change Proposal. Không tự triển khai đề xuất.

Trước khi code, hãy trả lời ngắn:
1. Task ID bạn hiểu là gì?
2. Dependencies nào đã phải hoàn thành?
3. File/folder nào bạn sẽ sửa?
4. Invariant nào có rủi ro cao nhất?
5. Test nào bạn sẽ chạy?

Chỉ bắt đầu sau khi đã nêu năm điểm trên.
~~~

## 3. Template A — Phase/Task Implementation tổng quát

~~~text
VAI TRÒ:
Bạn là {{Backend/Frontend/Full-stack}} Developer triển khai đúng một task của EduTwin.

TASK:
- Task ID: {{Pxx-Tyy}}
- Phase: {{Tên Phase}}
- Mục tiêu duy nhất: {{Mục tiêu cụ thể}}

CONTEXT NGHIỆP VỤ:
{{Mô tả use case và người dùng}}

DEPENDENCIES ĐÃ DONE:
{{Danh sách Task/Phase}}

SPECIFICATION PHẢI ĐỐI CHIẾU:
- CONSTITUTION.md: {{mục}}
- DATABASE_SCHEMA.md: {{table/mục}}
- API_CONTRACTS.md: {{endpoint/mục}}
- MASTER_PLAN.md: {{phase/task}}

FILE/FOLDER ĐƯỢC PHÉP SỬA:
- {{path 1}}
- {{path 2}}

FILE/FOLDER BỊ CẤM:
- Mọi path không nằm trong allow-list.
- Năm file specification.
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

ROLE:
{{Student/Teacher/CenterManager}}

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
- Role policy: {{...}}.
- Ownership: {{...}}.
- Cross-tenant/missing resource trả 404.

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
TASK ID: {{P12-Txx}}
MỤC TIÊU: {{Adapter/parser/orchestrator/fallback}}

CONTRACT:
- API_CONTRACTS.md mục 66–67.
- DATABASE_SCHEMA.md mục 33–35.
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
Chưa được phép triển khai cho đến khi Product Owner và Codex phê duyệt.
~~~

## 14. Template L — Continuation/Handoff giữ ngữ cảnh

Dùng khi đổi Claude ↔ Gemini hoặc phiên chat bị dài.

~~~text
EDUTWIN HANDOFF

Baseline specification version: 1.0 FROZEN
Current branch: {{...}}
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
- Coverage:

Decisions tuyệt đối không được quên:
- Multi-tenant center_id từ JWT.
- Không schema/API change.
- {{task-specific invariants}}

Known issues:
- {{...}}

Next exact action:
{{Một hành động cụ thể}}

Trước khi tiếp tục, agent mới phải đọc lại năm specification và tự xác nhận allow-list.
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
9. Twin/History/Recommendation.
10. Teacher Override replay.
11. Student/Teacher/Center Dashboard.
12. dotnet build/test/coverage.
13. GitHub Actions.
14. Secret scan thủ công.

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

Đọc Mandatory Context Preamble và năm specification.

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
- M=0, R=null, C=1, T=1, D=1 → 2.50.
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

Những prompt này phá scope, làm mất traceability và khiến AI Developer tự thay frozen decision.

## 20. Checklist trước khi gửi prompt

- [ ] Có Mandatory Context Preamble.
- [ ] Một Task ID rõ.
- [ ] Dependencies đã APPROVED.
- [ ] Allow-list cụ thể.
- [ ] Contract/schema mục cụ thể.
- [ ] Business invariant.
- [ ] Acceptance observable.
- [ ] Test command/cases.
- [ ] Stop rule.
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
