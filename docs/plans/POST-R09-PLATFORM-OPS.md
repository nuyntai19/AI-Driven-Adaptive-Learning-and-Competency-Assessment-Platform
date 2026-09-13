# KẾ HOẠCH NÂNG CẤP PLATFORMADMIN — POST-R09-PLATFORM-OPS

> **Tên đề xuất:** `POST-R09-PLATFORM-OPS` — Platform Administration Operational Hardening
> **Thời điểm lập kế hoạch:** 2026-09-13 (22:58:37 +07:00)
> **Trạng thái:** IMPLEMENTATION-READY — USER APPROVAL REQUIRED
> **Căn cứ:** Yêu cầu Product Owner và kết quả rà soát độc lập của Codex
> **Git Base Freeze SHA:** `77bf38412e99d174ccbb725075dd82a37b84971d`
> **Nhánh phát triển dự kiến:** `codex/post-r09-platform-ops`

---

> *Gate 6 đã hoàn tất corrective và được kiểm tra độc lập tại SHA `77bf38412e99d174ccbb725075dd82a37b84971d`. Chỉ được bắt đầu milestone này từ đúng SHA đó; không mở lại hoặc viết lại lịch sử Gate 1–6.*
>
> *Tôi cũng phản biện ba điểm trước khi chốt plan:*
> - *“Thời điểm hoạt động gần nhất” chưa có định nghĩa đáng tin cậy. Không nên lấy tùy tiện `UpdatedAt` của user/class rồi gọi đó là hoạt động. Tạm hoãn cho đến khi có telemetry/audit semantics rõ ràng.*
> - *Email/điện thoại trung tâm hiện không có trong domain. Không nên thêm chỉ để giao diện trông đầy đủ.*
> - *MFA và nhiều PlatformAdmin làm thay đổi authentication, recovery và governance đáng kể. Phải là milestone riêng, không trộn vào CRUD trung tâm.*

---

# Kế hoạch nâng cấp PlatformAdmin

Tên đề xuất:

```text
POST-R09-PLATFORM-OPS
Platform Administration Operational Hardening
```

## Điều kiện bắt đầu

Điều kiện đã được xác nhận trước khi triển khai:

1. Real multipart E2E PASS.
2. Failure-injection PASS.
3. Data Protection restart test PASS.
4. R09 report được sửa factual.
5. Gate 6 quay lại `FROZEN`.
6. Có final Gate 6 SHA.
7. Fetch remote và tạo branch `codex/post-r09-platform-ops` trực tiếp từ SHA đó; không merge/rebase lịch sử khác vào baseline.
8. Không mang theo file untracked ngoài tài liệu DOCX được bảo vệ và chính file kế hoạch này.
9. Không sửa/xóa audit entries cũ; chỉ append correction.

File `docs/EduTwin-Project-Specification-Chi-Tiet-De-Hieu.docx` đang untracked phải được giữ nguyên, không sửa, xóa hoặc stage. File kế hoạch này phải được commit trên branch mới cùng commit tài liệu đầu tiên.

## Phạm vi được phép triển khai

- **IN SCOPE:** Giai đoạn A–G, verification tự động và Chrome E2E.
- **OUT OF SCOPE / DEFERRED:** Giai đoạn H (nhiều PlatformAdmin, MFA) và Giai đoạn I (health/queue/export/quota). Chỉ được ghi quyết định hoãn trong ADR/tracking; không tạo permission, endpoint, UI, migration hoặc mã giả cho H–I trong milestone này.

## Source of truth phải đọc đầy đủ theo thứ tự

1. `CONSTITUTION.md`
2. `PROJECT_REQUIREMENTS.md`
3. `DATABASE_SCHEMA.md`
4. `API_CONTRACTS.md`
5. `UI_UX_SPEC.md`
6. `MASTER_PLAN.md`
7. `PROJECT_TRACKING.md`
8. ADR về PlatformAdmin, RBAC, tenant isolation, audit và session invalidation trong `docs/decisions/`
9. Source/contracts/tests Platform hiện tại

Không được kết luận từ báo cáo cũ hoặc tên file. Phải đối chiếu source thật tại baseline và ưu tiên tái sử dụng service, permission, error code, audit writer và session-revocation semantics đang tồn tại.

---

# Giai đoạn A — Khóa đặc tả và bất biến

## Tài liệu cần sửa

- `CONSTITUTION.md`
- `PROJECT_REQUIREMENTS.md`
- `API_CONTRACTS.md`
- `DATABASE_SCHEMA.md`
- `UI_UX_SPEC.md`
- `MASTER_PLAN.md`
- `PROJECT_TRACKING.md`

Tạo ADR mới:

```text
docs/decisions/ADR-POST-R09-PLATFORM-OPERATIONS.md
```

## Bất biến phải ghi rõ

```text
PlatformAdmin ⇔ Root Tenant PLATFORM
```

PlatformAdmin chỉ được:

- Truy cập metadata vận hành của trung tâm.
- Truy cập thông tin tối thiểu của CenterManager phục vụ quản trị.
- Xem aggregate đã được allow-list.
- Quản lý vòng đời trung tâm và CenterManager.
- Quản lý bảo mật tài khoản PlatformAdmin theo phạm vi riêng.

PlatformAdmin tuyệt đối không được:

- Đọc hoặc sửa điểm.
- Đọc attempt/answer/reasoning/attachment.
- Đọc Digital Twin hoặc recommendation cá nhân.
- Teacher override.
- Giả danh người dùng.
- Tự cấp quyền học thuật.
- Hard-delete trung tâm.
- Chạy truy vấn tenant tùy ý.

---

# Giai đoạn B — Ma trận bảo mật PlatformAdmin

Làm trước các chức năng mới để tạo hàng rào regression.

## Kiểm thử authorization

Dùng tài khoản PlatformAdmin thực, gọi các API:

- Attempts.
- Attempt feedback.
- Attachment download.
- AI analysis status/result.
- Student Digital Twin.
- Teacher-scoped Digital Twin.
- Recommendations.
- Student/class/center educational dashboards.
- Teacher review queue.
- Teacher override.
- Student goals.
- Assignment detail và progress chứa dữ liệu học sinh.

Acceptance:

- Endpoint thiếu permission trả `403`.
- Endpoint có cơ chế fail-closed ownership có thể trả `404`.
- Tuyệt đối không trả một phần payload trước khi từ chối.
- Không để response tiết lộ tài nguyên tồn tại hay không.
- PlatformAdmin chỉ có permission `platform.*`.

## Kiểm thử privilege escalation

- CenterManager không tạo được role `PlatformAdmin`.
- Không gán được quyền `platform.*`.
- Không gán PlatformAdmin vào user tenant thường.
- Không tạo ordinary user trong `PLATFORM`.
- Không di chuyển PlatformAdmin ra khỏi `PLATFORM`.
- Không dùng reset-manager endpoint để reset PlatformAdmin.

---

# Giai đoạn C — Vòng đời CenterManager

## Thay đổi database

Bổ sung vào `centers`:

```text
primary_manager_user_id VARCHAR(36) NULL
```

Ràng buộc:

- Kiểu cột phải khớp chính xác convention Guid hiện tại của repository (`VARCHAR(36)`), không dùng kiểu `UUID` giả định.
- Thiết kế ưu tiên là composite FK `(center_id, primary_manager_user_id)` tham chiếu alternate key `users(center_id, user_id)`, nullable và `ON DELETE RESTRICT`.
- Trước khi chốt migration, phải dựng migration thử và kiểm tra chu trình FK `centers -> users -> centers`, migration up/down và EF model snapshot. Nếu MySQL/EF không thể biểu diễn an toàn, dừng để review ADR; không được âm thầm bỏ FK rồi chỉ dựa vào UI.
- `PLATFORM.primary_manager_user_id` luôn `NULL`.
- Với center thường đang `Active`, BLL bắt buộc có primary manager `Active`.
- Không cascade delete.
- Backfill center hiện có bằng CenterManager hợp lệ được tạo sớm nhất.
- Trước migration phải có preflight query. Nếu center Active không có đúng ứng viên CenterManager hợp lệ để backfill thì migration/closeout phải dừng và báo rõ center vi phạm; không chọn ngẫu nhiên.

Không cần tạo bảng manager mới; CenterManager vẫn là `User` + dynamic role assignment.

## API mới

```http
GET /api/v1/platform/centers/{centerId}/managers
POST /api/v1/platform/centers/{centerId}/managers
PATCH /api/v1/platform/centers/{centerId}/managers/{userId}/status
POST /api/v1/platform/centers/{centerId}/managers/{userId}/make-primary
POST /api/v1/platform/centers/{centerId}/managers/{userId}/reset-password
```

## Chức năng

- Liệt kê CenterManager.
- Tạo manager bổ sung.
- Khóa/mở khóa/vô hiệu hóa manager.
- Chuyển primary manager.
- Tùy chọn chuyển primary rồi vô hiệu hóa manager cũ trong một transaction.
- Reset password.
- Thu hồi phiên sau password/status change.

## Bất biến

- Manager phải thuộc đúng `centerId`.
- Manager phải có account type `CenterManager`.
- Không vô hiệu hóa primary manager nếu chưa chuyển primary.
- Không vô hiệu hóa manager Active cuối cùng của center Active.
- Không kích hoạt center nếu không có primary manager Active.
- Không dùng endpoint này với `PLATFORM`.
- Mọi mutation có `ExpectedRowVersion`.
- Tăng `AuthVersion` và revoke refresh token khi khóa, vô hiệu hóa hoặc reset password.
- Ghi audit log redacted.

## Contract mutation bắt buộc

- `CreateCenterManagerRequest`: username, displayName, password, `ExpectedCenterRowVersion`, reason.
- `UpdateCenterManagerStatusRequest`: status đích, `ExpectedUserRowVersion`, reason.
- `MakePrimaryCenterManagerRequest`: `ExpectedCenterRowVersion`, `ExpectedManagerUserRowVersion`, `DisablePreviousPrimary`, `ExpectedPreviousPrimaryUserRowVersion` khi disable, reason.
- `ResetCenterManagerPasswordRequest` hiện hữu tiếp tục dùng `ExpectedUserRowVersion`, bổ sung reason nếu contract hiện tại chưa có.
- RowVersion truyền trên JSON dưới dạng string, không dùng JavaScript number cho `ulong`.
- Stale OCC trả `409 CONCURRENCY_CONFLICT`; cross-tenant/không đúng account type trả `404` fail-closed; validation trả `400`; đúng MySQL 1062 và đúng named unique index mới trả `409 DUPLICATE_RESOURCE`.
- Mọi thao tác thay đổi primary/status/password phải là một transaction. Không commit một phần rồi mới phát hiện center không còn manager hợp lệ.

---

# Giai đoạn D — Audit log Platform

## Permission mới

```text
platform.audit.read
```

Quyền này:

- `IsSensitive = true`.
- `IsDelegable = false`.
- Chỉ map cho `PlatformAdmin`.

## Mô hình audit cross-tenant

`AuthorizationAuditLog.CenterId` vẫn là `PLATFORM` đối với thao tác của PlatformAdmin. Không được gán `TargetUserId` của ordinary tenant vào quan hệ FK cùng tenant hiện tại.

Để lọc center đích có cấu trúc và không phải parse JSON, ưu tiên bổ sung:

```text
target_center_id VARCHAR(36) NULL
```

- FK tới `centers(center_id)` với delete restrict nếu migration chứng minh an toàn.
- Index tối thiểu `(center_id, target_center_id, created_at)`.
- `TargetId` vẫn mô tả tài nguyên trực tiếp (center/user), còn `TargetCenterId` mô tả tenant bị tác động.
- Không lưu password, hash, token hoặc Authorization header trong `BeforeData`/`AfterData`.

## API

```http
GET /api/v1/platform/audit-logs
GET /api/v1/platform/audit-logs/{auditId}
```

Query hỗ trợ:

- `page`, `pageSize`.
- `actionType`.
- `targetType`.
- `targetId`.
- `actorUserId`.
- `traceId`.
- `fromUtc`, `toUtc`.
- `search`.

## Response

Chỉ trả:

- Người thực hiện.
- Timestamp.
- Action type.
- Target type/id.
- Center bị tác động.
- Trạng thái trước/sau đã redacted.
- Lý do.
- Trace ID.

Không trả raw JSON tùy ý. Backend phải chuyển audit data sang DTO allow-list, loại bỏ:

- Password/password hash.
- Token.
- Secret.
- Cookie.
- Authorization header.
- Nội dung học tập.

## UI

Tạo trang:

```text
/quan-tri-nen-tang/nhat-ky
```

Có:

- Bộ lọc.
- Phân trang.
- Xem chi tiết.
- Copy trace ID.
- Badge theo loại hành động.
- Không có chức năng chỉnh sửa/xóa audit log.

---

# Giai đoạn E — Metadata và số liệu vận hành

## Cập nhật metadata center

API:

```http
PATCH /api/v1/platform/centers/{centerId}
```

Cho sửa:

- `CenterName`
- `Timezone`
- `ExpectedRowVersion`
- `Reason`

Không cho sửa:

- `CenterId`
- `CenterCode`
- `CreatedAt`

Chưa thêm email/điện thoại.

## Aggregate tối thiểu

Bổ sung vào center summary:

- `ActiveStudentCount`
- `ActiveTeacherCount`
- `ClassCount`
- `ActiveManagerCount`
- `HasActivePrimaryManager`

Không trả:

- Điểm trung bình.
- Mastery.
- Tỷ lệ sai.
- AI statistics.
- Attempt statistics.
- Dữ liệu có thể truy ngược đến học sinh.

## Query safety

Cross-tenant aggregate bắt buộc:

```csharp
IgnoreQueryFilters()
+ explicit targetCenterIds
+ explicit !IsDeleted
+ projection chỉ lấy count
```

Không materialize toàn bộ Student/Teacher/Class vào memory.

“Last activity” tạm hoãn vì chưa có activity semantics đáng tin cậy.

---

# Giai đoạn F — Suspension lifecycle

## Nâng cấp API hiện tại

`Reason` phải:

- Bắt buộc khi trạng thái thực sự thay đổi.
- Trim.
- Có giới hạn độ dài.
- Không chứa secret.
- Được ghi audit.

Khi suspend:

- Bump `AuthVersion` toàn bộ user center.
- Revoke active refresh token.
- Ghi số user/token bị ảnh hưởng.
- Không xóa dữ liệu.
- Không cho đăng nhập mới.
- Token cũ thất bại từ request tiếp theo.

Khi reactivate:

- Không phục hồi token cũ.
- Yêu cầu primary manager Active.
- Người dùng đăng nhập lại.
- Ghi audit đầy đủ.

UI phải hiển thị modal cảnh báo tác động và yêu cầu nhập lý do.

---

# Giai đoạn G — Bảo mật tài khoản PlatformAdmin

## Chức năng nên làm ngay

Permission:

```text
platform.account.manage_own
```

API:

```http
POST /api/v1/platform/me/change-password
POST /api/v1/platform/me/revoke-sessions
GET  /api/v1/platform/me/security
```

Yêu cầu:

- Change password cần mật khẩu hiện tại.
- Chính sách mật khẩu nhất quán.
- Tăng `AuthVersion`.
- Revoke refresh token.
- Không log password.
- Không dùng reset-manager endpoint.
- Không để bootstrap secret tự reset tài khoản đã tồn tại.

Semantics endpoint:

- `change-password` xác minh mật khẩu hiện tại, áp dụng password policy hiện hữu, bump `AuthVersion`, revoke toàn bộ active refresh token của chính PlatformAdmin và trả response không chứa password/hash/token.
- `revoke-sessions` bump `AuthVersion`, revoke toàn bộ active refresh token của chính tài khoản và buộc client logout sau response thành công.
- `security` chỉ trả metadata an toàn như username, last login, auth version/row version dạng string và số phiên active; không trả token hash hoặc bootstrap configuration.
- Áp dụng rate limiting theo cơ chế hiện hữu. Nếu repository chưa có rate-limit architecture phù hợp, dừng để đặc tả thay vì tự tạo bộ đếm process-local không bền vững.

## Bootstrap hardening

Thêm trạng thái rõ ràng:

- Provisioning chỉ dùng để tạo admin đầu tiên.
- Khi admin hợp lệ tồn tại, không sửa credential.
- Production thiếu secret khi cần provisioning phải fail closed.
- Không in secret trong health/log/report.

---

# Giai đoạn H — MFA và nhiều PlatformAdmin — DEFERRED, KHÔNG TRIỂN KHAI

Đây là milestone bảo mật riêng trong tương lai. Trong POST-R09-PLATFORM-OPS chỉ ghi ADR/tracking rằng hạng mục này bị hoãn. Không sửa source/schema/catalog/frontend vì Giai đoạn H.

## Thiết kế tham khảo cho milestone tương lai

Các permission sau **chưa được thêm trong milestone hiện tại**:

```text
platform.admins.read
platform.admins.manage
```

Chức năng:

- Liệt kê PlatformAdmin.
- Tạo PlatformAdmin bổ sung.
- Khóa/mở khóa.
- Reset/recovery theo quy trình riêng.
- Không tự vô hiệu hóa chính mình.
- Không vô hiệu hóa PlatformAdmin Active cuối cùng.
- Không xóa Root Tenant.
- Tất cả hành động được audit.

Khi có milestone riêng trong tương lai mới xem xét sửa provisioner:

- Chấp nhận một hoặc nhiều PlatformAdmin hợp lệ.
- Không còn fail chỉ vì có hơn một admin.
- Vẫn fail nếu có ordinary user trong `PLATFORM`.
- Vẫn fail nếu PlatformAdmin nằm ngoài `PLATFORM`.

## MFA/TOTP

Nếu triển khai phải có thiết kế riêng:

- Enrollment.
- Verify challenge khi login.
- Recovery codes.
- Rotate/revoke authenticator.
- Mã hóa secret at rest.
- Không log TOTP secret hoặc recovery code.
- Rate limit.
- Replay prevention.
- Clock-skew policy.
- Bắt buộc MFA cho PlatformAdmin.

Không được triển khai MFA nửa vời bằng một cột plaintext trong `users`.

Gate H bắt buộc giữ trạng thái `DESIGNED / DEFERRED` trong milestone này; không giả vờ đã triển khai.

---

# Giai đoạn I — Dashboard sức khỏe và tiện ích tùy chọn — DEFERRED, KHÔNG TRIỂN KHAI

Chỉ là backlog tham khảo. Không triển khai source/schema/API/UI trong milestone hiện tại.

Có thể thêm:

- API/MySQL/worker/storage health.
- Queue depth.
- Số job retry/terminal failure tổng hợp.
- Cảnh báo center không có primary manager hợp lệ.
- Export CSV metadata trung tâm.

Không đưa:

- Nội dung bài làm.
- Student-level analytics.
- AI reasoning.
- Attachment.
- Digital Twin.

Không nên thêm quota/billing nếu chưa có mô hình sản phẩm rõ ràng.

---

# Ma trận kiểm thử acceptance bắt buộc

Ít nhất phải có các ca sau; không được thay thế toàn bộ bằng mock/InMemory nếu hành vi phụ thuộc MySQL, FK, transaction hoặc OCC:

1. PlatformAdmin bị chặn khỏi từng nhóm educational API ở cả HTTP policy và BLL boundary.
2. CenterManager bị chặn khỏi toàn bộ `/api/v1/platform/*`.
3. Cross-tenant aggregate count chính xác khi caller ở tenant PLATFORM.
4. Aggregate query không leak record ngoài target center set và không tạo N+1.
5. Tạo manager bổ sung thành công và bootstrap role assignment canonical.
6. Duplicate username đúng index trả 409; lỗi DB khác rethrow.
7. Lock/unlock manager bump row version; lock/reset bump AuthVersion và revoke đúng refresh tokens.
8. Không revoke token của user/center khác.
9. Không khóa active manager cuối cùng của active center.
10. Không khóa primary manager trước khi transfer.
11. Hai transfer primary đồng thời: chỉ một request thắng, request stale trả 409.
12. Transfer cross-tenant hoặc target không phải CenterManager trả 404.
13. Consecutive password resets dùng row version mới đều thành công; stale version trả 409.
14. Active center không thể reactivate nếu thiếu active primary manager.
15. PLATFORM luôn có `PrimaryManagerUserId = null` và không nhận ordinary manager.
16. Migration preflight/backfill/up/down chạy trên MySQL thật và không làm hỏng FK.
17. Update metadata giữ nguyên CenterCode/CenterId; stale OCC trả 409.
18. Audit log có actor/time/action/target center/before-after/reason/traceId và immutable.
19. Platform audit query chỉ đọc audit thuộc PLATFORM operations, không đọc tenant-internal audit.
20. Audit redaction loại password/hash/token/secret/cookie/header ở cả response và DB payload mới.
21. PlatformAdmin đổi mật khẩu/revoke sessions khiến access và refresh token cũ mất hiệu lực.
22. Reset-manager endpoint không thể tác động PlatformAdmin.
23. Frontend route capability-first, xử lý loading/empty/error/403/404/409 và không lưu password.

# Quy trình Git và checkpoint

Pre-check bắt buộc:

```powershell
git fetch origin
git branch --show-current
git rev-parse HEAD
git rev-parse origin/codex/personal-system-completion
git status --short
git diff --check
git rev-list --left-right --count origin/codex/personal-system-completion...HEAD
```

Baseline phải là `77bf38412e99d174ccbb725075dd82a37b84971d`, ahead/behind `0 0`; ngoài file DOCX và file plan đã biết không được có artifact lạ. Tạo `codex/post-r09-platform-ops` từ đúng baseline.

Chia forward commit theo checkpoint:

1. `docs(platform): specify post-r09 operational administration`
2. `test(platform): enforce education-data denial matrix`
3. `feat(platform): add center manager lifecycle`
4. `feat(platform): expose redacted platform audit history`
5. `feat(platform): update center metadata and safe aggregates`
6. `feat(platform): harden suspension and platform self-security`
7. `test(platform): complete relational and browser verification`
8. `docs(platform): publish operational enhancement closeout`

Chỉ stage explicit paths; cấm `git add .`, amend, reset, rebase, merge và force-push. Chạy targeted tests sau mỗi checkpoint; chỉ chạy full regression khi checkpoint đã xanh, không lặp full suite vô hạn. Gate đỏ thì không commit/push/freeze.

---

# Verification bắt buộc

## Backend

- Release build 0 warning/error.
- Full non-MySQL suite.
- Full live-MySQL suite.
- Fresh migration từ volume trống.
- EF pending-model check bằng 0.
- Migration rollback test.
- OCC race tests.
- Concurrent primary-manager transfer tests.
- Last-active-manager invariant tests.
- Session eviction tests.
- Audit redaction tests.
- Cross-tenant aggregate tests.
- PlatformAdmin denial matrix.

## Frontend

- TypeScript build.
- `npm test` bằng Node test runner hiện tại.
- ESLint.
- Production build.
- Permission-route tests.
- Modal validation.
- OCC conflict UI.
- Audit filter/pagination.
- Manager lifecycle UI tests.

## Security

- Không có secret trong source/log/report.
- Không commit `.env`.
- Không trả password sau khi tạo/reset.
- Không expose raw audit JSON.
- Không có `IgnoreQueryFilters()` ngoài allow-list đã audit.
- Mọi cross-tenant query có explicit predicate.
- CenterManager không được truy cập `/platform/*`.
- PlatformAdmin không được truy cập educational APIs.

# Yêu cầu Gemini kiểm thử Chrome thực tế

Sau khi toàn bộ automated gate PASS, Gemini bắt buộc tự mở Chrome và kiểm thử trực tiếp qua giao diện thật, không chỉ gọi controller/unit test.

Các kịch bản phải chạy:

1. Đăng nhập PlatformAdmin.
2. Mở danh sách trung tâm.
3. Tìm kiếm/lọc/phân trang.
4. Tạo center mới và manager ban đầu.
5. Sửa tên/múi giờ center.
6. Tạo CenterManager thứ hai.
7. Chuyển primary manager.
8. Thử vô hiệu hóa primary cũ.
9. Thử vô hiệu hóa manager cuối cùng và xác nhận bị chặn.
10. Reset password manager.
11. Đăng nhập bằng manager mới.
12. Suspend center, xác nhận phiên manager cũ bị thu hồi.
13. Reactivate center và đăng nhập lại.
14. Mở lịch sử audit và kiểm tra reason/traceId/before-after.
15. Kiểm tra aggregate count đúng với dữ liệu DB.
16. Đổi mật khẩu PlatformAdmin.
17. Revoke các phiên PlatformAdmin và xác nhận token cũ thất bại.
18. Dùng PlatformAdmin truy cập trực tiếp URL học sinh, bài làm, Digital Twin, review queue và attachment; tất cả phải bị chặn.
19. Dùng CenterManager truy cập URL `/quan-tri-nen-tang/*`; phải bị chặn.
20. Kiểm tra DevTools Network để bảo đảm response không chứa điểm, answer, reasoning, attachment, AI result hoặc password.
21. Đăng xuất và xác nhận không còn dữ liệu nhạy cảm trong UI/cache.

Gemini phải lưu bằng chứng:

- URL/flow được kiểm tra.
- HTTP status quan trọng.
- Screenshot hoặc recording.
- Trace ID khi có lỗi.
- Expected/actual.
- PASS/FAIL từng bước.
- Không ghi credential vào report.

Nếu bất kỳ bước nào fail, không được tuyên bố hoàn tất hoặc push “FROZEN”; phải sửa, chạy lại regression rồi test Chrome lại.

## Thứ tự thực hiện cuối cùng

```text
Gate 6 corrective hoàn tất
→ tạo branch nâng cấp riêng
→ A: đặc tả
→ B: security denial matrix
→ C: manager lifecycle
→ D: audit UI/API
→ E: metadata + aggregate
→ F: suspension hardening
→ G: PlatformAdmin self-security
→ full automated verification
→ Gemini tự mở Chrome test toàn bộ
→ audit độc lập
→ commit/push/freeze
```

Milestone này dừng sau A–G. H–I bắt buộc giữ `DESIGNED / DEFERRED` và chỉ được triển khai trong milestone riêng sau khi có phê duyệt mới.

# Output bàn giao bắt buộc

1. Baseline và final commit SHA.
2. Danh sách forward commit và file thay đổi của từng checkpoint.
3. Migration/schema diff, preflight, up/down và EF drift result.
4. API contract, permission và error mapping mới.
5. Manager lifecycle/primary-manager invariant.
6. Audit scoping và redaction rules.
7. PlatformAdmin educational-data denial matrix.
8. Build, targeted, non-MySQL, live-MySQL và frontend results với Passed/Failed/Skipped thực tế.
9. Chrome E2E từng bước với expected/actual/PASS/FAIL và bằng chứng không chứa secret.
10. `git diff --check`, working-tree status, push output và ahead/behind.
11. Xác nhận `.env` không tracked/không bị đọc ra báo cáo và file DOCX untracked được giữ nguyên.
12. Danh sách nội dung deferred; không tuyên bố MFA/multi-admin/health đã triển khai.

Trạng thái tối đa sau khi tất cả acceptance gate đạt:

```text
POST-R09 PLATFORM ADMIN OPERATIONAL ENHANCEMENT
TECHNICALLY VERIFIED / UX REVIEW PENDING
```

Không được tự tuyên bố `PRODUCTION READY`.
