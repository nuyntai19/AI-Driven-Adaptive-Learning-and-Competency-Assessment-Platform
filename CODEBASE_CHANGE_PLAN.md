# EduTwin — Current-to-Target Codebase Change Plan

> Phiên bản: 1.0-draft
> Audit snapshot: branch feat/center-organization, commit 2d768f270e0395bcafcbcab2305ac3617fb5f9ca
> Ngày: 2026-09-09
> Trạng thái: PLAN ONLY — không khẳng định các thay đổi Target đã được code
> Nguồn: PROJECT_REQUIREMENTS, CONSTITUTION, DATABASE_SCHEMA, API_CONTRACTS, UI_UX_SPEC và MASTER_PLAN

Provenance: `2d768f2` là source-code audit snapshot; `b14f6c4` là documentation rebaseline checkpoint. Approved import snapshot và course repository initial-import SHA còn TBD và phải được ghi riêng trong PROJECT_TRACKING.md.

## 1. Chức năng của tài liệu

Tài liệu này trả lời: source hiện có gì, sai/thiếu gì so với rebaseline, file nào có khả năng phải sửa/tạo và thứ tự migration an toàn. Nó không thay DATABASE_SCHEMA/API_CONTRACTS và không phải allow-list tự động cho bất kỳ task nào. Mỗi task vẫn cần prompt, human owner, exact HEAD, exact allow-list và review.

## 2. Kết luận trạng thái hiện tại

### 2.1. Đã có trong prototype

- ASP.NET Core Web API, EF Core/MySQL, React/Vite.
- 31 bảng đã migration, tenant filter/composite FK/concurrency đáng kể.
- Auth JWT + refresh rotation, users.auth_version và claim auth_version.
- Organization, Knowledge Graph, Curriculum, Question Bank, Assignment.
- Attempt submission, deterministic preliminary graders, durable AI job.
- Gemini adapter, strict parser/semantic validator, retry một lần và rule fallback.
- MasteryCalculator v1 pure function.
- 2.853 test pass/3 skip và frontend build xanh tại audit baseline.

### 2.2. Chưa đạt Target

- Authorization vẫn dựa trên role claim, AuthorizationPolicies và React RoleRoute/allowedRoles tĩnh.
- users.auth_version có trong token nhưng chưa có server gate đối chiếu current DB version trên mọi protected request.
- Login/me chưa trả roles[], effective permissions[] và authorizationVersion.
- Không có bảy bảng v2: permissions, permission_account_types, roles, role_permissions, user_roles, authorization_audit_logs, evidence_assessments.
- Không có màn hình quản trị quyền động.
- AIAnalysisJobProcessor chủ yếu lưu ReasoningAnalysis/trạng thái; chưa có Evidence Gate + completion orchestrator đầy đủ.
- MasteryCalculator hiện có fallback path làm đổi Mastery, trái Target ReviewOnly weight 0.
- Behavior/Risk/History/Recommendation orchestration và dashboard cuối chưa hoàn tất.
- Stakeholder interview, Figma approval, live MySQL audit và course repository chưa có evidence hoàn tất.

Vì vậy: prototype kỹ thuật đã đi tới khoảng P13-T01 lịch sử, nhưng roadmap môn học R00 vẫn IN PROGRESS. Không được quy đổi thành phần trăm tổng cho đến khi course repo/work package/acceptance được xác nhận.

## 3. R02 — Database audit và additive schema

### 3.1. Existing files cần kiểm tra/sửa

| Path | Thay đổi dự kiến |
|---|---|
| src/EduTwin.DAL/Persistence/EduTwinDbContext.cs | Thêm DbSet cho 7 entity v2; giữ global query filter fail-closed |
| src/EduTwin.DAL/IdentityAndTenancy/User.cs | Giữ AuthVersion là nguồn duy nhất; thêm navigation user_roles/audit nếu cần, không đổi account type ngầm |
| src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs | Chỉ thay qua migration mới đã duyệt |
| src/EduTwin.BLL/Seeding/ | Seed deterministic permission catalog, account-type mapping, bootstrap roles/user roles |

### 3.2. New source dự kiến

- src/EduTwin.DAL/IdentityAndTenancy/Permission.cs.
- PermissionAccountType.cs.
- Role.cs.
- RolePermission.cs.
- UserRoleAssignment.cs hoặc tên entity được nhóm khóa trước migration.
- AuthorizationAuditLog.cs.
- src/EduTwin.DAL/AssessmentAndReasoning/EvidenceAssessment.cs.
- Configurations tương ứng dưới src/EduTwin.DAL/Persistence/Configurations/.
- Hai migration additive: Dynamic Authorization và Evidence Governance.

### 3.3. Gate bắt buộc

- INFORMATION_SCHEMA đối chiếu table/column/FK/unique/check/delete/index.
- Composite FK khóa center + account type.
- Composite FK khóa EvidenceAssessment.analysis_id và supersedes_assessment_id cùng attempt_id; BLL + MySQL tests từ chối mismatch dù cùng Center.
- Backfill role_name thành bootstrap role không mở rộng quyền.
- Validation query chứng minh không orphan/mismatch.
- Backup/rollback rehearsal và 3 MySQL integration test không còn skip.

## 4. R03 — Dynamic RBAC backend

### 4.1. Auth/session files phải sửa

| Path | Current | Target |
|---|---|---|
| src/EduTwin.Contracts/IdentityAndTenancy/LoginResponse.cs | UserDto chỉ có Role | accountType, roles[], permissions[], authorizationVersion; role legacy compatibility có thời hạn |
| src/EduTwin.Contracts/IdentityAndTenancy/CurrentUserResponse.cs | Chỉ Role/Status | Cùng capability projection như login |
| src/EduTwin.API/Security/JwtTokenGenerator.cs | role + auth_version claim | Giữ account type/compatibility; phát capability/version theo strategy được duyệt |
| src/EduTwin.BLL/IdentityAndTenancy/LoginUseCase.cs | Tạo response từ RoleName | Load bootstrap/dynamic roles và effective permissions tenant-safe |
| src/EduTwin.BLL/IdentityAndTenancy/RefreshUseCase.cs | Refresh session hiện hữu | Re-evaluate active user/center/auth version/roles trước token mới |
| src/EduTwin.BLL/IdentityAndTenancy/GetCurrentUserUseCase.cs | Validate ba role cứng | Trả account type + dynamic effective permissions |
| src/EduTwin.BLL/IdentityAndTenancy/ClaimsResolver.cs | Parse role/auth_version | Parse identity fail-closed; không coi role là permission |
| src/EduTwin.BLL/IdentityAndTenancy/TenantContext.cs | Chứa Role/AuthVersion | Giữ account type/version; permission evaluator là service riêng |
| src/EduTwin.API/Program.cs | AddPolicy RequireClaim(role) | Đăng ký permission requirement/handler và stale auth_version gate |
| src/EduTwin.BLL/IdentityAndTenancy/AuthorizationPolicies.cs | Bốn policy role tĩnh | Compatibility registry theo slice; xóa sau cutover toàn bộ |

### 4.2. New BLL/API dự kiến

- Permission catalog/list use case.
- Role list/get/create/update/archive use cases.
- Atomic role-permission replacement.
- Atomic user-role replacement.
- Effective permission resolver/evaluator.
- Authorization-version validator đối chiếu JWT claim với users.auth_version.
- Last-admin guard mô phỏng trạng thái sau mutation.
- Audit query/use case.
- src/EduTwin.API/Controllers/AuthorizationController.cs hoặc controllers tách theo contract.
- Permission authorization requirement/handler; cache nếu dùng phải key theo CenterId/UserId/AuthVersion.

### 4.3. Mutation atomic

Password reset, user disable/delete, user-role replace và role-permission replace phải:

1. validate tenant/account type/delegation/self-elevation;
2. validate target aggregate rowVersion và re-read last-admin state với concurrency; không dùng authorizationVersion làm expected concurrency token;
3. mutate;
4. append authorization audit;
5. tăng users.auth_version đúng phạm vi;
6. revoke refresh token theo policy;
7. commit một transaction.

## 5. Cutover authorization ở API hiện hữu

Các controller protected dưới src/EduTwin.API/Controllers/ phải migrate theo module, không đổi toàn bộ một lượt. Mỗi slice:

1. map endpoint sang permission trong API_CONTRACTS;
2. enforce permission + tenant + ownership ở server;
3. thêm 403/404/cross-tenant/direct-request tests;
4. chuyển frontend consumer;
5. chỉ sau đó bỏ legacy policy của slice.

Tuyệt đối không dùng legacy role OR dynamic permission.

Center endpoints chỉ giữ /centers/me; không thêm POST/DELETE Center trong course MVP.

## 6. R04 — Frontend capability cutover

| Path | Current | Target |
|---|---|---|
| web/edutwin-web/src/types/auth.ts | AuthUser có role | accountType, roles, permissions, authorizationVersion |
| web/edutwin-web/src/stores/authStore.ts | Lưu token/user | Thêm capability helpers hoặc selector; không persist secret |
| web/edutwin-web/src/routes/RoleRoute.tsx | allowedRoles | Deprecated dần; thay PermissionRoute |
| web/edutwin-web/src/routes/ProtectedRoute.tsx | Optional allowedRoles | Authentication shell; permission route riêng |
| web/edutwin-web/src/auth/authApi.ts | bootstrap login/refresh/me | Xử lý AUTHORIZATION_VERSION_STALE đúng một lần, không loop |
| web/edutwin-web/src/auth/AuthBootstrap.tsx | Bootstrap session | Tải current effective capability an toàn |
| web/edutwin-web/src/App.tsx | Route groups theo role | Route-to-permission map tập trung |
| web/edutwin-web/src/pages/AuthenticatedHomePage.tsx | Role-based home | Capability-based actions |

New UI: role list/editor, permission matrix, user-role assignment, audit log và trang Không có quyền. Existing page buttons/menu phải dùng hasPermission, nhưng API vẫn là security boundary.

## 7. R05/R06 — Evidence Gate và Twin completion

### 7.1. Existing files cần sửa

| Path | Vấn đề Current | Target |
|---|---|---|
| src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisJobProcessor.cs | Persist analysis/job; chưa gate/twin orchestration | Sau observation/fallback, gọi deterministic gate và completion transaction idempotent |
| src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIReasoningAnalysisBuilder.cs | Xây AI observation | Giữ provenance; không gán trust/mastery |
| src/EduTwin.BLL/AssessmentAndReasoning/Processing/RuleBasedFallbackBuilder.cs | Tạo fallback analysis | Gắn provenance RuleFallback, không giả AI confidence/quality |
| src/EduTwin.BLL/AssessmentAndReasoning/PreliminaryGrading/EssayGrader.cs | Trả IsCorrect = null, Score = 0 | Giữ semantics pending review; không coerce null thành false/neutral |
| src/EduTwin.DAL/AssessmentAndReasoning/ReasoningAnalysis.cs | Có fallback/override fields | Giữ output gốc; phối hợp evidence assessment bằng analysis/override version |
| src/EduTwin.BLL/DigitalTwin/MasteryCalculator.cs | Fallback path có learning rate 0.10 và đổi mastery | Chỉ nhận effective evidence/weight; ReviewOnly/fallback giữ nguyên mastery |
| src/EduTwin.BLL/DigitalTwin/MasteryCalculationInput.cs | IsCorrect hiện là bool, không biểu diễn pending verdict | Orchestrator không gọi calculator khi correctness null; chỉ truyền effective non-null correctness đã qua Gate hoặc HumanConfirmed replay |
| src/EduTwin.BLL/DigitalTwin/DependencyInjection.cs | Mới đăng ký goal use cases | Đăng ký gate/orchestrator/updaters/calculators |
| src/EduTwin.DAL/Persistence/EduTwinDbContext.cs | Chưa có EvidenceAssessment | Thêm DbSet/config/filter |

### 7.2. New BLL dự kiến

- Pure EvidenceGate + input/result/reason codes/policy version.
- Enums riêng EvidenceSourceType, EvidenceTrustLevel, EvidenceDecisionMode.
- Completion orchestrator.
- Behavior Twin updater.
- Risk calculator và Student Twin aggregate updater.
- Idempotency guard cho history/evidence completion.
- Teacher review/override/replay use cases.

### 7.3. Transaction flow

Gemini call luôn ở ngoài DB transaction. Sau khi có validated observation hoặc fallback:

1. Gate structural/semantic/contradiction trước confidence; preliminary correctness null ép ReviewOnly và chờ teacher.
2. Insert ReasoningAnalysis/provenance nếu chưa có.
3. Insert append-only EvidenceAssessment.
4. Update Behavior Twin từ observed telemetry.
5. Chỉ update Knowledge Twin khi reasoningWeight > 0.
6. Recompute goal/risk/history/recommendation nếu input hiệu lực đổi.
7. Update assignment progress/attempt/job terminal.
8. Commit; failure rollback toàn bộ mutation.

## 8. R07 — Recommendation và ML decision

Existing src/EduTwin.DAL/Recommendations/ mới là persistence model. Cần BLL candidate builder, prerequisite readiness, linear fallback, opportunity ranker, deterministic tie-break, question selector, supersession và API query.

AI không chọn thứ hạng. Khi Gemini unavailable hoặc evidence chưa đủ, recommendation vẫn dùng:

- active curriculum/topic order;
- prerequisite đã đạt;
- mastery/evidence hiện có;
- câu hỏi active chưa làm/gần đây;
- deterministic explanation templates.

ML.NET chỉ được thêm sau dataset/label/split/metric/baseline/consent decision. Nếu không vượt baseline, giữ heuristic và vẫn đáp ứng MVP.

## 9. R08 — Màn hình còn thiếu

Current web đã có các trang quản lý Teacher/Student/Class/Knowledge/Curriculum/Question/Assignment và Student Assignment nền. Còn phải bổ sung/hoàn thiện:

- Dynamic permission administration.
- Student Learning Player submit/poll/fallback/review.
- Teacher review queue/detail/override.
- Student Twin, recommendation và dashboard.
- Teacher class dashboard/weak topic/high-risk/gap group.
- Center aggregate dashboard và authorization health.
- Loading/empty/error/stale/forbidden/concurrency/accessibility tests.

Không xây Teacher ranking, Center AI score, Platform Admin hoặc cross-center UI.

## 10. Tests cần bổ sung

- RBAC: account-type mismatch tại API/BLL/DB, self-elevation, over-grant, last-admin, stale token, audit rollback, cross-tenant.
- MySQL: migration/backfill/rollback, constraints, query plans, three skipped tests.
- Evidence: structural/semantic contradiction, confidence 0/49/50/79/80/100/null, three dimensions, policy version và same-attempt FK mismatch.
- Mastery: fallback/ReviewOnly/Essay-null unchanged; reduced/trusted exact decimal; HumanConfirmed replay mới đổi Essay mastery.
- Orchestrator: duplicate job, lost race, rollback injection, restart/reclaim.
- Override: permission/scope/version/replay order/history preservation.
- Frontend: capability route/button, direct URL, stale version no retry loop.
- E2E: AI success và forced outage đều hoàn tất submission; core management vẫn chạy khi Gemini disabled.

## 11. Thứ tự thực hiện không được đảo

1. R00: docs/course repo/provenance/team metadata.
2. R01: stakeholder interview + Figma validation.
3. R02: live DB audit, freeze schema/API, migration.
4. R03: backend RBAC + endpoint cutover.
5. R04: permission UI.
6. R05: Evidence Gate + schema.
7. R06: Twin completion/override.
8. R07: recommendation/ML decision.
9. R08: dashboards/E2E UX.
10. R09: hardening/report/demo.

## 12. Không được sửa hàng loạt

- Không rewrite kiến trúc hoặc đổi .NET/React stack.
- Không sửa migration đã merge.
- Không đổi mọi controller trong một PR.
- Không xóa role compatibility trước cutover.
- Không gộp RBAC, Evidence và dashboard vào một migration/task.
- Không dùng test InMemory thay bằng chứng MySQL cho FK/CHECK/index.
- Không dùng AI-generated code làm contribution nếu owner không hiểu và review.

## 13. Definition of Done cho change plan

Tài liệu chỉ chuyển từ PLAN ONLY khi mỗi dòng Target có task/owner/reviewer/requirement, exact allow-list, approved contract và evidence. Trạng thái implementation thật tiếp tục được ghi ở PROJECT_TRACKING.md, không sửa mục Current thành Done chỉ vì tài liệu đã mô tả.
