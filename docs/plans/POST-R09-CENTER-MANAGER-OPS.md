# KẾ HOẠCH NÂNG CẤP CENTERMANAGER — POST-R09-CENTER-MANAGER-OPS

> **Tên milestone:** `POST-R09-CENTER-MANAGER-OPS` — Center Administration End-to-End Completion  
> **Thời điểm lập kế hoạch:** 2026-09-14 (+07:00)  
> **Trạng thái:** IMPLEMENTATION-READY — USER APPROVAL REQUIRED  
> **Căn cứ:** Source audit trực tiếp sau closeout PlatformAdmin và yêu cầu Product Owner  
> **Git base freeze SHA:** `222e7bbf728c27a6437be18ac860e4f04a5b7c5d` (kế thừa trực tiếp `1f1cb9cccaf102f489f29b3296ee92744e5ad401`)  
> **Nhánh dự kiến:** `codex/post-r09-center-manager-ops`

---

## 1. Mục tiêu và phán quyết kiến trúc

CenterManager là account type có phạm vi chức năng rộng nhất **bên trong một Center**, nhưng không phải “super role” kế thừa mọi quyền của Teacher và tuyệt đối không có quyền PlatformAdmin.

Mô hình đúng:

```text
PlatformAdmin  -> quản trị vòng đời tenant/Center, không đọc dữ liệu học thuật
CenterManager  -> quản trị tổ chức, nội dung và giám sát trong đúng Center hiện tại
Teacher        -> giảng dạy và quản lý resource thuộc ownership/phạm vi được giao
Student        -> học tập và truy cập dữ liệu của chính mình
```

Không được triển khai mô hình phân cấp sai:

```text
PlatformAdmin > CenterManager > Teacher > Student
```

CenterManager và Teacher có thể cùng được map một số capability, nhưng:

- không có role inheritance;
- quyền thực tế luôn lấy từ effective permission;
- account type vẫn là domain boundary;
- Teacher vẫn chịu ownership guard;
- CenterManager có tenant-wide scope chỉ với endpoint/capability được đặc tả;
- custom role có thể làm tập quyền của một CenterManager nhỏ hơn system role;
- không gán `dashboards.teacher.read_scoped` cho CenterManager chỉ để tái sử dụng UI; CenterManager dùng `dashboards.center.read` và các composite policy được duyệt.

Mục tiêu milestone:

1. Hoàn thiện toàn bộ luồng CenterManager từ API/BLL đã có tới UI thực tế.
2. Khắc phục contract/permission/UI drift đã audit.
3. Bổ sung các phần quản trị tài khoản còn thiếu theo cơ chế OCC, session eviction và audit.
4. Chứng minh tenant isolation, least privilege và account-type boundary bằng test tự động và Chrome E2E.
5. Không mở rộng sang tính năng PlatformAdmin, Student/Teacher redesign hoặc feature AI mới.

---

## 2. Hiện trạng source đã xác minh tại baseline

### 2.1. Backend đã có

- Center profile: `GET/PATCH /api/v1/centers/me`.
- Center dashboard: `GET /api/v1/centers/me/dashboard`.
- Teacher: list/get/create/update/soft-delete.
- Student: list/get/create/update và subject goals; chưa có delete endpoint.
- Class: list/get/create/update, add/list/remove student membership, class dashboard.
- Subject và Knowledge Graph: backend CRUD tương đối đầy đủ.
- Curriculum: create/list/get/update/replace classes/replace nodes/publish.
- Question bank: create/list/get/update/activate/archive/delete.
- Assignment: create/list/get/update/publish/close/progress.
- Dynamic RBAC: permission catalog, role CRUD/archive, replace role permissions, user-role replacement, effective permission, audit.
- Review/override và scoped academic inspection theo capability hiện hữu.

### 2.2. Frontend đã có nhưng chưa hoàn chỉnh

- Center dashboard đã có.
- Teacher/Student/Class pages chủ yếu mới list + create.
- Không có trang Center profile cho `GET/PATCH /centers/me`.
- `organizationApi.ts` chưa expose đầy đủ get/update/delete/membership operations dù backend đã có.
- Subject chưa có màn hình CRUD hoàn chỉnh.
- Knowledge Graph UI thiên về xem + tạo node/edge; thiếu sửa/xóa đầy đủ.
- Curriculum, Question Bank và Assignment UI tương đối đầy đủ nhưng cần audit capability, OCC, state transition và error handling.
- Authorization UI dùng một route `/quan-ly/phan-quyen` với ba tab, trong khi `UI_UX_SPEC.md` còn mô tả các route khác.
- Authorization audit UI chỉ hiển thị tối đa các sự kiện gần nhất; chưa đạt filter/detail/pagination theo spec.
- Bundle JavaScript chính khoảng 1,48 MB; cần route-level lazy loading, không được che cảnh báo bằng cách chỉ tăng warning threshold.

### 2.3. Drift bắt buộc xử lý

Permission catalog hiện có:

```text
organization.students.delete
```

nhưng source chưa có endpoint/use case/UI tương ứng. Milestone này chọn **triển khai đầy đủ soft-delete Student** thay vì giữ permission mồ côi hoặc hard-delete dữ liệu.

“Quản lý tài khoản” hiện chưa có reset password cho Teacher/Student. Milestone bổ sung hai capability tách biệt:

```text
organization.teachers.reset_password
organization.students.reset_password
```

Hai permission này:

- chỉ tương thích `CenterManager`;
- `IsSensitive = true`;
- `IsDelegable = true` trong phạm vi role CenterManager, nhưng actor chỉ có thể cấp nếu chính actor có quyền đó;
- không map cho Teacher hoặc Student;
- không liên quan endpoint reset CenterManager của PlatformAdmin.

---

## 3. Source of truth bắt buộc đọc đầy đủ

Gemini phải đọc theo thứ tự trước khi sửa code:

1. `CONSTITUTION.md`
2. `PROJECT_REQUIREMENTS.md`
3. `DATABASE_SCHEMA.md`
4. `API_CONTRACTS.md`
5. `UI_UX_SPEC.md`
6. `MASTER_PLAN.md`
7. `PROJECT_TRACKING.md`
8. `docs/plans/POST-R09-PLATFORM-OPS.md` — chỉ tham khảo cấu trúc, không tái triển khai PlatformAdmin
9. ADR liên quan RBAC, tenant isolation, audit, account/session invalidation
10. Source/contracts/tests hiện tại của Center, Teacher, Student, Class, Subject, Knowledge Graph, Curriculum, Question, Assignment và Authorization

Không được xem báo cáo cũ là bằng chứng source. Tên method/file không chứng minh behavior; phải đọc implementation và test thật.

---

## 4. Phạm vi và giới hạn

### IN SCOPE

- Giai đoạn A–I của plan này.
- Tài liệu authoritative và ADR bổ sung.
- Backend corrective/additive cần thiết cho Student soft-delete và reset password Teacher/Student.
- Frontend CenterManager end-to-end.
- Permission catalog/data migration tương ứng.
- Route-level code splitting.
- Unit, relational MySQL, HTTP integration, frontend và Chrome E2E.

### OUT OF SCOPE

- Không sửa lại PlatformAdmin operations đã frozen, trừ test negative chứng minh CenterManager bị chặn.
- Không thiết kế lại toàn bộ UI Teacher/Student.
- Không thêm AI feature, model, recommendation algorithm hoặc Digital Twin semantics mới.
- Không cho CenterManager giả danh người dùng.
- Không hard-delete Teacher, Student, Attempt, Evidence hoặc audit log.
- Không thêm role inheritance.
- Không thêm explicit deny nếu mô hình hiện tại chưa hỗ trợ.
- Không đổi `CenterCode`.
- Không tạo/xóa Center từ CenterManager.
- Không tự động cấp toàn bộ quyền Teacher cho CenterManager.
- Không sửa database ngoài additive/corrective migration thực sự cần thiết.

---

## 5. Bất biến bảo mật bắt buộc

1. Mọi tenant entity được giới hạn bởi `CurrentTenantId` và global query filter phù hợp.
2. Cross-tenant identifier trả `404` fail-closed khi việc trả `403` có thể tiết lộ resource.
3. UI capability-first; ẩn nút không thay thế API authorization.
4. Backend kiểm tra permission + tenant + ownership/account type độc lập.
5. CenterManager không truy cập `/api/v1/platform/*` hoặc `/quan-tri-nen-tang/*`.
6. CenterManager không nhận `platform.*`.
7. Teacher không tự động nhận tenant-wide CenterManager scope.
8. Role chỉ nhận permission tương thích account type.
9. User chỉ nhận role cùng account type và cùng Center.
10. Không self-elevation/over-grant.
11. Sau mọi authorization mutation phải còn ít nhất một active CenterManager có đủ `TenantAdminCorePermissionsV1`.
12. Mọi mutation nhạy cảm dùng OCC RowVersion canonical, không dùng `AuthVersion` làm concurrency token.
13. Khi password/status/role thay đổi làm authorization cũ không còn hợp lệ: tăng `AuthVersion`, revoke refresh token và ghi audit.
14. Audit append-only, không chứa password/hash/token/cookie/header.
15. CenterManager chỉ xem aggregate hoặc academic detail được capability/resource scope cho phép trong chính Center.
16. Không dùng `IgnoreQueryFilters()` trong ordinary CenterManager path trừ allow-list có threat model và explicit tenant predicate được review.

---

# GIAI ĐOẠN A — BASELINE AUDIT VÀ KHÓA ĐẶC TẢ

## A1. Pre-check

```powershell
git fetch origin
git branch --show-current
git rev-parse HEAD
git rev-parse origin/codex/post-r09-platform-ops
git status --short
git diff --check
git rev-list --left-right --count origin/codex/post-r09-platform-ops...HEAD
```

Baseline bắt buộc:

```text
1f1cb9cccaf102f489f29b3296ee92744e5ad401
```

Tạo branch:

```text
codex/post-r09-center-manager-ops
```

Nếu baseline/working tree không đúng, dừng và báo; không merge/rebase/reset để ép trạng thái.

## A2. Cập nhật tài liệu

Sửa có kiểm soát:

- `CONSTITUTION.md`
- `PROJECT_REQUIREMENTS.md`
- `API_CONTRACTS.md`
- `DATABASE_SCHEMA.md`
- `UI_UX_SPEC.md`
- `MASTER_PLAN.md`
- `PROJECT_TRACKING.md`

Tạo ADR:

```text
docs/decisions/ADR-POST-R09-CENTER-MANAGER-OPERATIONS.md
```

ADR phải ghi:

- CenterManager không kế thừa Teacher role.
- Shared capability được map tường minh.
- Phạm vi CenterManager là current Center; PlatformAdmin là control plane riêng.
- Student delete là soft-delete có evidence preservation.
- Password reset Teacher/Student là sensitive mutation, có OCC/audit/session eviction.
- Route `/quan-ly/phan-quyen` dùng tab là canonical V1; sửa spec theo implementation hoặc tách route chỉ khi có lợi ích điều hướng/accessibility rõ ràng.
- Không viết lại lịch sử R03–R09; append Post-R09 amendment.

## A3. Đặc tả chức năng CenterManager canonical

Tài liệu phải liệt kê đủ:

1. Center profile.
2. Center dashboard.
3. Teacher lifecycle.
4. Student lifecycle và subject goals.
5. Class và membership.
6. Subject và Knowledge Graph.
7. Curriculum.
8. Question Bank.
9. Assignment và progress.
10. Dynamic role/permission.
11. User-role/effective permission.
12. Authorization audit.
13. Scoped review/override nếu effective permission cho phép.

Gate A: tài liệu, permission naming, API route, error code và UI route không mâu thuẫn trước khi code.

---

# GIAI ĐOẠN B — SECURITY REGRESSION TRƯỚC FEATURE

Viết test hàng rào trước khi bổ sung UI/mutation.

## B1. CenterManager denial matrix

Chứng minh CenterManager bị chặn khỏi:

- toàn bộ `/api/v1/platform/centers*`;
- `/api/v1/platform/audit-logs*`;
- `/api/v1/platform/me/*`;
- frontend `/quan-tri-nen-tang/*`.

## B2. Teacher/CenterManager boundary

- Teacher có `dashboards.teacher.read_scoped` không đồng nghĩa có `dashboards.center.read`.
- CenterManager dùng center dashboard, không cần được cấp teacher dashboard permission.
- Teacher chỉ thao tác class/student theo ownership.
- CenterManager tenant-wide chỉ khi có đúng capability.
- Custom CenterManager role bị thu hẹp phải bị 403 ở API và ẩn action tương ứng trên UI.

## B3. Tenant isolation matrix

Mỗi nhóm resource phải có cross-tenant test:

- Center profile.
- Teacher/Student.
- Class/membership.
- Subject/node/edge.
- Curriculum/question.
- Assignment/progress.
- User-role/audit.
- Attempt/review/override nếu được expose cho CenterManager.

Gate B: test negative phải đỏ trước corrective phù hợp và xanh sau sửa; không được viết assertion giả như `Assert.True(true)`.

---

# GIAI ĐOẠN C — CENTER PROFILE VÀ DASHBOARD

## C1. Frontend API

Mở rộng organization client:

```text
getCurrentCenter()
updateCurrentCenter(request)
```

## C2. UI

Tạo trang canonical:

```text
/quan-ly/trung-tam
CenterProfilePage.tsx
```

Hiển thị:

- CenterCode read-only.
- CenterName.
- Timezone canonical.
- Status read-only đối với CenterManager.
- RowVersion giữ trong model, không hiển thị như business field.

Luồng:

```text
GET /centers/me
-> sửa CenterName/Timezone
-> PATCH kèm RowVersion
-> backend tenant + validation + OCC
-> trả RowVersion mới
-> cập nhật cache
```

Xử lý 400/403/409/429/network; 409 phải refetch và không tự ghi đè.

## C3. Dashboard

Audit `CenterDashboardPage`:

- aggregate đúng theo subject filter;
- không N+1;
- không hiển thị KPI suy diễn từ page hiện tại như toàn Center;
- chart có table/text fallback;
- trạng thái loading/empty/error/forbidden;
- link drill-down chỉ hiện khi có capability tương ứng.

---

# GIAI ĐOẠN D — TEACHER VÀ STUDENT ACCOUNT LIFECYCLE

## D1. Teacher UI completion

Mở rộng API client và UI cho:

- get detail;
- update display name/department/status bằng RowVersion;
- soft-delete có confirmation;
- reset password an toàn;
- hiển thị class count và chặn xóa khi còn active class.

Không hard-delete User/Teacher.

## D2. Student soft-delete

Bổ sung contract/use case/API/UI cho:

```http
DELETE /api/v1/students/{studentId}
Policy: organization.students.delete
```

Semantics:

1. Target phải cùng Center và account type Student.
2. Soft-delete `Student` và linked `User`; không xóa vật lý.
3. Chuyển active class membership sang trạng thái Removed theo canonical model.
4. Tăng RowVersion/AuthVersion phù hợp và revoke active refresh tokens.
5. Giữ nguyên Attempt, Answer, Evidence, Twin history, AssignmentTarget và audit history.
6. Không cascade/xóa historical evidence.
7. Retry hợp lệ phải idempotent theo contract hoặc trả 404 fail-closed nhất quán; quyết định ghi rõ trong API contract.
8. Ghi authorization audit đã redacted.
9. Cross-tenant ID trả 404.

Nếu relational constraints thực tế khiến semantics trên không an toàn, dừng Gate D để sửa ADR; không bỏ FK hoặc hard-delete để làm test xanh.

## D3. Student UI completion

- get detail;
- update full name/grade/status bằng RowVersion;
- xem lớp hiện tại;
- xem/cập nhật goal theo subject nếu có capability;
- soft-delete nếu có `organization.students.delete`;
- hiển thị rõ hậu quả và preservation policy.

## D4. Reset password Teacher/Student

Permission mới:

```text
organization.teachers.reset_password
organization.students.reset_password
```

Endpoint:

```http
POST /api/v1/teachers/{teacherId}/reset-password
POST /api/v1/students/{studentId}/reset-password
```

Request tối thiểu:

```json
{
  "newPassword": "<not logged>",
  "expectedUserRowVersion": "12",
  "reason": "Lý do quản trị"
}
```

Bắt buộc:

- password policy canonical;
- target cùng tenant và đúng account type;
- OCC trên User RowVersion;
- hash bằng password hasher hiện hữu;
- tăng User RowVersion và AuthVersion;
- revoke refresh tokens;
- audit chỉ chứa target ID/reason/metadata allow-list, tuyệt đối không password/hash;
- stale version trả 409;
- non-target account type/cross-tenant trả 404;
- không cho dùng endpoint này với CenterManager hoặc PlatformAdmin.

Permission catalog/seed/model snapshot/migration phải đồng bộ; số bảng vẫn 40.

---

# GIAI ĐOẠN E — CLASS VÀ MEMBERSHIP

Hoàn thiện `ClassListPage` thành luồng end-to-end:

- list/filter/pagination;
- class detail;
- create;
- update name/teacher/status bằng RowVersion;
- subject read-only khi contract cấm đổi sau evidence/assignment;
- list class members;
- add nhiều students;
- remove membership bằng confirmation;
- link class dashboard khi có capability;
- xử lý member đã tồn tại, stale OCC và ownership/tenant failure.

Luồng canonical:

```text
chọn Subject + Teacher cùng Center
-> tạo Class
-> mở detail
-> add/remove Student membership
-> backend kiểm tra tenant/ownership
-> giữ lịch sử AssignmentTarget
-> trả projection mới
-> refresh cache liên quan
```

Không tạo hard-delete Class; dùng trạng thái Archived theo model hiện hành.

---

# GIAI ĐOẠN F — SUBJECT VÀ KNOWLEDGE GRAPH

## F1. Subject management

Tạo màn hình/section CRUD Subject:

- list active/inactive;
- create;
- get detail;
- update bằng RowVersion;
- soft-delete với confirmation;
- hiển thị 409 nếu đã có evidence/dependency không cho xóa.

## F2. Knowledge Node/Edge

Mở rộng frontend client và UI:

- create/update/delete node;
- create/update/delete edge;
- capability cho từng action;
- RowVersion canonical;
- cycle error dễ hiểu;
- subject/tenant mismatch fail-closed;
- delete node có evidence/dependency trả 409 và không làm mất dữ liệu;
- refresh graph atomically sau mutation.

Không đổi graph semantics hoặc AI recommendation trong milestone này.

---

# GIAI ĐOẠN G — CURRICULUM, QUESTION VÀ ASSIGNMENT HARDENING

Không viết lại các module đang hoạt động. Audit và bổ sung phần thiếu:

## Curriculum

- Teacher owner và CenterManager scope đúng contract.
- Draft-only mutation.
- Replace classes/nodes atomically.
- Publish confirmation và RowVersion.
- Không tự chọn Teacher đầu tiên hoặc dùng CenterManager UserId làm TeacherId.

## Question Bank

- MCQ/ShortAnswer/Essay và evaluation mode đúng.
- Không leak `IsCorrect` qua student projection.
- Activate/archive/delete theo state machine và capability.
- Delete giữ evidence/history theo contract.

## Assignment

- Draft/create/update.
- Target summary trước publish.
- Publish atomic snapshot.
- Close theo RowVersion.
- Progress đúng Center và không N+1.
- CenterManager không được nhìn ngoài tenant.

Chỉ sửa khi có finding tái hiện được bằng test; không refactor hàng loạt vì thẩm mỹ.

---

# GIAI ĐOẠN H — DYNAMIC RBAC VÀ AUTHORIZATION AUDIT UX

## H1. Role management

Giữ route canonical V1:

```text
/quan-ly/phan-quyen
```

Ba tab:

1. Role & permission.
2. User-role assignment.
3. Authorization audit.

Bổ sung/kiểm tra:

- role search/filter/account type/status;
- pagination nếu tập dữ liệu không nhỏ;
- system role read-only;
- account type immutable;
- permission matrix nhóm theo module/resource;
- chỉ hiển thị Active + Delegable + Compatible permission;
- role CenterManager chỉ nhận subset quyền actor có;
- sensitive badge;
- affected-user count;
- no-op submit disabled;
- OCC 409 refetch/compare;
- archive theo capability.

## H2. User-role assignment

- tìm user cùng Center;
- lọc theo account type/status;
- chỉ role Active cùng account type;
- effective permission giải thích role nguồn;
- dùng User RowVersion;
- reason bắt buộc;
- cảnh báo self-change;
- backend chặn self-elevation/last-admin;
- mutation thành công cập nhật RowVersion + AuthorizationVersion từ server;
- session stale xử lý hữu hạn, không refresh loop.

## H3. Audit

Mở rộng frontend API/UI theo query backend:

- page/pageSize;
- from/to;
- actor;
- target user/role;
- action type;
- permission;
- detail before/after;
- copy TraceId;
- empty/error/loading;
- không edit/delete;
- không raw secret.

Nếu API hiện tại thiếu filter/detail cần thiết, bổ sung contract và backend trong phạm vi authorization audit của current Center; không tái sử dụng Platform audit endpoint.

---

# GIAI ĐOẠN I — NAVIGATION, PERFORMANCE, E2E VÀ CLOSEOUT

## I1. Capability-first navigation

- Menu/page/button đều dùng effective permission.
- Direct URL vẫn bị backend từ chối.
- Không hard-code `user.role === CenterManager` trừ account-type boundary được đặc tả.
- Một custom CenterManager role chỉ thấy chức năng được cấp.
- Không xuất hiện Platform navigation.

## I2. Route-level code splitting

Dùng `React.lazy`/dynamic import cho các management page lớn:

- Authorization management.
- Knowledge Graph.
- Curriculum editor/list.
- Question editor/list.
- Assignment editor/progress.
- Center dashboards và organization management pages nếu bundle analysis chứng minh lợi ích.

Yêu cầu:

- Có Suspense fallback accessible.
- Không tăng bundle warning threshold để che vấn đề.
- Ghi before/after bundle size thực tế.
- Không làm hỏng route guard hoặc preload auth state.

## I3. UX chung

Mọi màn hình phải có:

- loading skeleton/progress;
- empty state;
- retryable network error;
- 403 access denied;
- 404 fail-closed;
- 409 OCC refetch;
- 429 rate limit nếu có;
- keyboard navigation;
- label, focus trap, Escape cho modal;
- responsive desktop/tablet/mobile;
- không hiển thị raw ProblemDetails/stack trace.

---

# 6. Ma trận acceptance tự động bắt buộc

## Backend/API

1. Center profile GET/PATCH tenant-safe và OCC.
2. Center dashboard aggregate đúng, không cross-tenant/N+1.
3. Teacher get/create/update/delete/reset-password đúng permission và tenant.
4. Teacher còn active class không bị soft-delete trái rule.
5. Student get/create/update/goal/delete/reset-password đúng permission và tenant.
6. Student soft-delete bảo toàn Attempt/Evidence/Twin history/AssignmentTarget.
7. Student soft-delete remove active memberships và revoke session.
8. Reset password không log password/hash; stale RowVersion trả 409.
9. Reset endpoint từ chối sai account type và Platform/CenterManager target.
10. Class update/member add/remove đúng tenant/ownership/history.
11. Subject/node/edge mutation đúng capability, cycle/evidence guard.
12. Curriculum/question/assignment state machine không regression.
13. CenterManager không vào Platform API.
14. Teacher không có center-wide permission ngầm.
15. Custom CenterManager role bị thu hẹp đúng API behavior.
16. Cross-tenant ID trả 404 fail-closed.
17. Role/permission account-type compatibility.
18. Self-elevation/over-grant/last-admin bị chặn.
19. Authorization mutation tăng AuthVersion/revoke token đúng target.
20. Authorization audit append-only, tenant-safe và redacted.
21. Permission catalog không còn permission mồ côi sau milestone.
22. EF model snapshot/migration và runtime catalog đồng bộ.

## Live MySQL

Các behavior phụ thuộc transaction/FK/OCC/unique index phải chạy trên MySQL thật:

- concurrent Student delete/update;
- concurrent password reset;
- membership mutation;
- role permission/user-role replacement;
- last-admin race;
- permission seed/migration fresh + upgrade + downgrade;
- 0 pending model changes.

Không thay toàn bộ relational test bằng EF InMemory.

## Frontend

- API contract tests.
- Capability route/action matrix.
- Center profile 409 behavior.
- Teacher/Student/Class mutation UI.
- Student delete confirmation.
- Password reset redaction và reason validation.
- Knowledge Graph update/delete/cycle errors.
- Authorization filters/OCC/last-admin errors.
- Lazy-route loading và error boundary.
- Node test suite, TypeScript, ESLint và production build đều xanh.

---

# 7. Chrome E2E bắt buộc cho Gemini

Sau automated gate PASS, Gemini phải tự mở Chrome và kiểm thử trên Docker live stack. Không được chỉ gọi API hoặc unit test.

## Persona A — Full system CenterManager

1. Đăng nhập CenterManager system role.
2. Mở và cập nhật hồ sơ Center; kiểm tra RowVersion mới không cần F5.
3. Mở center dashboard, đổi subject filter, đối chiếu aggregate với DB/API.
4. Tạo Teacher, xem detail, sửa, reset password và đăng nhập thử bằng credential mới.
5. Tạo Student, gán lớp, sửa hồ sơ, cập nhật goal, reset password.
6. Tạo Class, chọn Subject/Teacher, thêm và loại Student, sửa/Archive lớp.
7. Tạo/sửa/xóa Subject theo rule.
8. Tạo/sửa/xóa Knowledge Node và Edge; thử tạo cycle và xác nhận bị chặn.
9. Tạo Curriculum Draft, liên kết lớp/node, publish.
10. Tạo Question cho MCQ/ShortAnswer/Essay, activate/archive.
11. Tạo Assignment, xem target summary, publish, xem progress, close.
12. Tạo custom role Student/Teacher/CenterManager với permission compatible.
13. Gán/thu hồi role cho user; xác nhận UI thay đổi theo capability.
14. Thử tự nâng quyền và làm mất last tenant admin; phải bị chặn.
15. Xem authorization audit với filter/detail/TraceId.
16. Xem review/override chỉ khi account có capability tương ứng.
17. Soft-delete Student test; xác nhận không còn trong active list nhưng historical evidence vẫn tồn tại qua đường được phép.
18. Soft-delete Teacher không còn active class; trường hợp còn class phải bị 409.

## Persona B — Restricted custom CenterManager

1. Đăng nhập tài khoản CenterManager có tập quyền bị thu hẹp.
2. Menu chỉ hiện module được cấp.
3. Direct URL tới module không có quyền trả access denied.
4. Handcrafted API request trả 403/404 phù hợp.
5. Không nhìn thấy hoặc gán permission vượt quyền actor.

## Persona C — Teacher boundary

1. Teacher chỉ thấy lớp sở hữu.
2. Không thấy Center profile edit/RBAC/center dashboard nếu không có capability.
3. Không truy cập Student/Class ngoài ownership.
4. Không dùng reset-password hoặc student-delete của CenterManager.

## Persona D — Cross-tenant và Platform boundary

1. CenterManager A dùng ID Center B cho từng nhóm resource; không được leak.
2. CenterManager truy cập `/quan-tri-nen-tang/*`; bị chặn.
3. CenterManager gọi `/api/v1/platform/*`; bị chặn.
4. DevTools Network không chứa resource center khác, password/hash/token hoặc raw secret audit.

## Viewport/accessibility

Chạy tối thiểu:

- Desktop 1440×900.
- Tablet 768×1024.
- Mobile 390×844.
- Keyboard Tab/Shift+Tab/Enter/Escape.
- Focus trap modal và focus return.

## Evidence

Lưu vào:

```text
docs/verification/post-r09-center-manager-ops/
```

Phải có:

- checklist expected/actual/PASS/FAIL;
- screenshot/recording không chứa credential;
- HTTP status quan trọng;
- TraceId khi có lỗi;
- before/after bundle size;
- link commit/test tương ứng.

Nếu một flow fail, không được tuyên bố frozen; sửa rồi chạy targeted regression và E2E liên quan lại.

---

# 8. Verification và đồng bộ database

## Backend

```powershell
dotnet build EduTwin.sln --configuration Release --no-restore --maxcpucount:1
dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --configuration Release --no-build --maxcpucount:1
dotnet tool restore
$env:ConnectionStrings__Default="Server=dummy;Database=dummy"
dotnet ef migrations has-pending-model-changes --project src/EduTwin.DAL --startup-project src/EduTwin.DAL --context EduTwinDbContext --configuration Release --no-build
```

Chạy riêng live-MySQL suite với secret lấy từ environment; không in credential trong command output/report.

## Frontend

```powershell
npm --prefix web/edutwin-web test
npm --prefix web/edutwin-web run lint
npm --prefix web/edutwin-web run build
```

## Database/schema

Nếu permission catalog hoặc model thay đổi:

1. Tạo forward migration có tên rõ.
2. Fresh-volume migration PASS.
3. Existing-volume upgrade PASS.
4. Down/up rehearsal PASS nếu policy cho phép.
5. EF pending model changes = 0.
6. `DATABASE_SCHEMA.md`, model snapshot và `docs/verification/edutwin_schema_r09.sql` đồng bộ.
7. Tổng số bảng vẫn 40 application tables, trừ khi có ADR và phê duyệt mới.
8. Docker MySQL schema đối chiếu information_schema với SQL export.

## Git hygiene

- `git diff --check` sạch.
- Không stage `.env`, credential, video chứa password hoặc file lock.
- Không đọc/in nội dung `.env` vào report.
- Giữ file DOCX ngoài allow-list nếu đang untracked.
- Không `git add .`.
- Không amend/reset/rebase/merge/force-push.

---

# 9. Checkpoint và commit strategy

Chỉ commit khi gate tương ứng xanh:

1. `docs(center-manager): specify post-r09 operational completion`
2. `test(center-manager): lock tenant and account-type boundaries`
3. `feat(center-manager): complete center profile and dashboard UX`
4. `feat(center-manager): complete teacher and student account lifecycle`
5. `feat(center-manager): complete class and membership operations`
6. `feat(center-manager): complete subject and knowledge graph operations`
7. `fix(center-manager): harden curriculum question and assignment flows`
8. `feat(center-manager): complete dynamic authorization UX and audit`
9. `perf(web): split center management routes and bundles`
10. `test(center-manager): complete mysql and chrome e2e verification`
11. `docs(center-manager): publish operational closeout`

Stage explicit path theo từng checkpoint. Không dùng commit message để che việc test chưa chạy.

Tài liệu closeout không tự ghi hash của chính commit chứa nó. Dùng `Current` hoặc tạo forward documentation commit kế tiếp để tránh self-referential hash loop.

---

# 10. Output bàn giao bắt buộc

1. Baseline SHA, branch và final SHA.
2. Danh sách commit/file theo từng checkpoint.
3. Requirement/API/UI/permission matrix trước và sau.
4. Chức năng backend đã tái sử dụng và phần mới bổ sung.
5. Student soft-delete evidence-preservation proof.
6. Teacher/Student password-reset session-eviction và audit proof.
7. Tenant/account-type/ownership denial matrix.
8. Migration, permission seed, schema SQL, Docker MySQL và EF drift evidence.
9. Build/test thực tế: Passed/Failed/Skipped, không chỉ ghi “all pass”.
10. Frontend lint/build/test và bundle before/after.
11. Chrome E2E theo bốn persona, ba viewport và keyboard.
12. Git status, diff check, push output, ahead/behind.
13. Xác nhận `.env` không tracked và không secret trong report.
14. Danh sách deferred/finding còn mở; không tuyên bố ngoài bằng chứng.

Trạng thái tối đa sau khi tất cả gate đạt:

```text
POST-R09 CENTER MANAGER END-TO-END COMPLETION
TECHNICALLY VERIFIED / CHROME E2E PASS / UX ACCEPTANCE PENDING
```

Không được tự tuyên bố `PRODUCTION READY`. Chỉ Product Owner được chốt `UX ACCEPTED` và `OFFICIALLY CLOSED` sau khi xem bằng chứng.

---

# 11. Thứ tự thực hiện cuối cùng

```text
đọc source of truth
-> xác minh baseline
-> A: khóa tài liệu/ADR
-> B: security regression
-> C: center profile/dashboard
-> D: teacher/student lifecycle
-> E: class/membership
-> F: subject/knowledge graph
-> G: harden curriculum/question/assignment
-> H: RBAC/audit UX
-> I: navigation/performance
-> full automated verification
-> fresh Docker/MySQL rehearsal
-> Gemini tự mở Chrome chạy toàn bộ E2E
-> audit độc lập
-> forward commits/push
-> closeout report
```

Gemini phải dừng tại gate đầu tiên bị lỗi, báo nguyên nhân và bằng chứng. Không được chạy vòng lặp test vô hạn, không tự giảm assertion, bỏ test, nới permission hoặc thay đổi requirement để hợp thức hóa implementation.
