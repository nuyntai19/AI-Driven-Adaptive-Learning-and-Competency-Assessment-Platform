# ADR-POST-R09-PLATFORM-OPERATIONS: Platform Administration Operational Hardening, Manager Lifecycle, and Audit Governance

**Status:** APPROVED
**Date:** 2026-09-13
**Scope:** Post-R09 Operational Enhancement (`POST-R09-PLATFORM-OPS`)
**Author:** EduTwin Architecture Team & Codex Review
**Baseline Git Freeze SHA:** `77bf38412e99d174ccbb725075dd82a37b84971d`
**Branch:** `codex/post-r09-platform-ops`

---

## 1. Bối cảnh và Đặt vấn đề (Context and Problem Statement)

Nền tảng EduTwin đã hoàn tất đóng băng kỹ thuật Gate 6 tại Git SHA `77bf38412e99d174ccbb725075dd82a37b84971d` trên kiến trúc Shared Database, Shared Schema đa tenant, với vai trò Quản trị viên Nền tảng (**PlatformAdmin**) thuộc Root Tenant `PLATFORM` (`00000000-0000-0000-0000-000000000001`).

Trong giai đoạn vận hành mở rộng sau R09, đội ngũ quản trị nền tảng đối mặt với các nhu cầu thực tế:
1. **Quản lý Vòng đời CenterManager:** Một trung tâm có thể có nhiều người quản lý (CenterManager) do chuyển giao nhân sự hoặc phân công ca trực. Cần định danh rõ người quản lý chính (**Primary Manager**) đại diện pháp lý và kỹ thuật cho trung tâm, đồng thời có thể tạo thêm manager, khóa/mở khóa, chuyển đổi primary manager và đặt lại mật khẩu một cách an toàn, tránh tình trạng trung tâm đang hoạt động nhưng không còn manager nào khả dụng.
2. **Minh bạch và Truy vết Kiểm toán (Platform Audit Governance):** Mọi hành động của PlatformAdmin (tạo trung tâm, đình chỉ, kích hoạt, đổi manager, reset password) cần được lưu trữ có cấu trúc với khả năng tra cứu, phân trang, lọc theo trung tâm đích (`target_center_id`) và đảm bảo khử khuẩn triệt để dữ liệu nhạy cảm (**Data Redaction** - tuyệt đối không rò rỉ password, hash, token, cookie, secret).
3. **Cập nhật Metadata & Thống kê Vận hành Tối thiểu (Operational Aggregates):** Cho phép hiệu chỉnh tên trung tâm, múi giờ theo OCC; cung cấp các chỉ số tổng hợp số lượng tài khoản (học sinh, giáo viên, lớp, quản lý) mà không được phép tải thực thể chi tiết vào bộ nhớ hay xâm phạm quyền riêng tư của từng cá nhân.
4. **Quy trình Đình chỉ Nghiêm ngặt (Suspension Hardening):** Bắt buộc nhập lý do (`Reason`), tăng tức thì `auth_version` và thu hồi toàn bộ refresh token của trung tâm khi đình chỉ, đồng thời ngăn chặn kích hoạt lại nếu trung tâm thiếu primary manager hợp lệ.
5. **Bảo mật Bản thân Tài khoản PlatformAdmin:** Cung cấp endpoint đổi mật khẩu cá nhân, thu hồi toàn bộ phiên làm việc của chính admin và xem trạng thái bảo mật tài khoản; khóa cứng provisioner không can thiệp nếu admin đã tồn tại.

---

## 2. Nguyên tắc và Ràng buộc Cốt lõi (Decision Drivers)

1. **Bất biến Bất khả Xâm phạm (Core Invariant):**
   $$\text{PlatformAdmin} \iff \text{Root Tenant PLATFORM } (00000000-0000-0000-0000-000000000001)$$
   - Người dùng có account type `PlatformAdmin` chỉ được tồn tại trong `PLATFORM`.
   - Người dùng thuộc tenant thông thường tuyệt đối không thể được gán vai trò `PlatformAdmin`.
   - Không được tạo ordinary user (Teacher, Student, CenterManager) trong `PLATFORM`.
2. **Ranh giới Cách ly Dữ liệu Học thuật (Academic Data Isolation Boundary):**
   PlatformAdmin là quản trị viên vận hành hệ thống, **tuyệt đối không phải là giáo viên hay chuyên viên khảo thí**. PlatformAdmin không có bất kỳ quyền hạn nào đối với dữ liệu học thuật của học sinh:
   - KHÔNG đọc hoặc sửa điểm số (`awarded_score`, override score).
   - KHÔNG đọc nội dung bài làm, câu trả lời, reasoning text hoặc ảnh đính kèm minh chứng.
   - KHÔNG đọc hồ sơ năng lực số Digital Twin (Knowledge Twin, Behavior Twin) hoặc khuyến nghị học tập cá nhân hóa.
   - KHÔNG thực hiện Teacher Review hay Teacher Override.
   - KHÔNG giả danh người dùng (impersonation).
   - KHÔNG xóa cứng trung tâm (hard delete centers).
3. **Optimistic Concurrency Control (OCC) Bắt buộc:**
   Mọi mutation đối với trung tâm hoặc CenterManager đều phải kèm theo `ExpectedRowVersion` (truyền dưới dạng chuỗi hex/string, không dùng JavaScript number cho `ulong`). Stale OCC trả về `409 ConcurrencyConflict`.
4. **Quyết định Hoãn (Deferred Decisions):**
   - **Hoãn Giai đoạn H (Nhiều PlatformAdmin & MFA):** Giữ trạng thái `DESIGNED / DEFERRED`. Chưa triển khai trong milestone này để tránh phá vỡ kiến trúc authentication hiện tại.
   - **Hoãn Giai đoạn I (Health/Queue/Export/Quota):** Giữ trạng thái `DESIGNED / DEFERRED`.
   - **Hoãn "Thời điểm hoạt động gần nhất":** Chưa có định nghĩa telemetry/audit semantics đáng tin cậy.
   - **Không thêm Email/Phone cho trung tâm:** Không mở rộng ngoài domain model hiện tại.

---

## 3. Các Quyết định Kiến trúc Chi tiết (Architectural Decisions)

### 3.1. Cấu trúc Khóa Ngoại và Trường `primary_manager_user_id` trong Bảng `centers`
- Bổ sung cột:
  ```sql
  ALTER TABLE `centers` ADD COLUMN `primary_manager_user_id` VARCHAR(36) NULL;
  ```
- Ràng buộc toàn vẹn:
  - Composite Foreign Key: `(center_id, primary_manager_user_id) REFERENCES users(center_id, user_id) ON DELETE RESTRICT`.
  - `PLATFORM.primary_manager_user_id` luôn là `NULL`.
  - Với mọi trung tâm thông thường đang ở trạng thái `Active`, nghiệp vụ BLL bắt buộc phải có một Primary Manager đang ở trạng thái `Active`.
  - Không cascade delete.
  - Preflight migration kiểm tra đảm bảo mọi trung tâm `Active` hiện hữu đều có CenterManager hợp lệ để backfill (chọn manager tạo sớm nhất); nếu vi phạm, migration dừng và báo lỗi rõ ràng.

### 3.2. Quản lý Vòng đời CenterManager (CenterManager Lifecycle)
- **API Endpoints:**
  - `GET /api/v1/platform/centers/{centerId}/managers`: Liệt kê danh sách manager của trung tâm, kèm cờ `isPrimary: bool` và `rowVersion: string`.
  - `POST /api/v1/platform/centers/{centerId}/managers`: Tạo manager bổ sung cho trung tâm. Kiểm tra trùng `username` (bắt 1062 index `ux_users_center_id_username` $\rightarrow$ 409).
  - `PATCH /api/v1/platform/centers/{centerId}/managers/{userId}/status`: Khóa (`Suspended`) / Mở khóa (`Active`) / Vô hiệu hóa (`Deactivated`).
    * Bất biến 1: Không được vô hiệu hóa primary manager nếu chưa chuyển primary.
    * Bất biến 2: Không được vô hiệu hóa hoặc khóa manager `Active` duy nhất còn lại của một trung tâm đang `Active`.
    * Tăng `users.row_version`, `users.auth_version` và thu hồi toàn bộ refresh token của manager đó.
  - `POST /api/v1/platform/centers/{centerId}/managers/{userId}/make-primary`: Chuyển quyền Primary Manager trong cùng một database transaction. Hỗ trợ tùy chọn `disablePreviousPrimary: bool`.
  - `POST /api/v1/platform/centers/{centerId}/managers/{userId}/reset-password`: Đặt lại mật khẩu (kèm `ExpectedUserRowVersion`, reason), tăng `auth_version`, thu hồi refresh token.

### 3.3. Mô hình Nhật ký Kiểm toán Nền tảng (Platform Audit Logs)
- Quyền mới: `platform.audit.read` (`IsSensitive = true`, `IsDelegable = false`, chỉ cấp cho `PlatformAdmin`).
- Bổ sung trường `target_center_id VARCHAR(36) NULL` vào bảng `authorization_audit_logs`:
  - Cho phép lọc nhật ký kiểm toán theo trung tâm khách hàng bị tác động mà không cần phân tích JSON trong `before_data`/`after_data`.
  - Tạo chỉ mục: `ix_auth_audit_center_target_center_created (center_id, target_center_id, created_at)`.
- API:
  - `GET /api/v1/platform/audit-logs`: Hỗ trợ phân trang, lọc theo `actionType`, `targetType`, `targetId`, `targetCenterId`, `actorUserId`, `traceId`, `fromUtc`, `toUtc`, `search`.
  - `GET /api/v1/platform/audit-logs/{auditId}`: Xem chi tiết.
- Redaction & DTO Allow-list:
  - Tuyệt đối không trả về raw internal state.
  - Mọi trường dữ liệu nhạy cảm (mật khẩu, hash, token, cookie, authorization headers, nội dung học tập) bị loại trừ 100%.

### 3.4. Hiệu chỉnh Metadata Trung tâm & Thống kê An toàn (Safe Aggregates)
- `PATCH /api/v1/platform/centers/{centerId}`:
  - Cho phép sửa: `CenterName`, `Timezone`, `Reason` kèm `ExpectedRowVersion`.
  - Không cho phép sửa: `CenterId`, `CenterCode`, `CreatedAt`.
- Bổ sung Safe Aggregates vào Center Summary:
  - `ActiveStudentCount`: Số học sinh đang hoạt động (`status = 'Active'`).
  - `ActiveTeacherCount`: Số giáo viên đang hoạt động (`status = 'Active'`).
  - `ClassCount`: Số lớp học đang hoạt động (`is_deleted = false`).
  - `ActiveManagerCount`: Số quản lý đang hoạt động (`status = 'Active'`).
  - `HasActivePrimaryManager`: Boolean xác nhận trung tâm có primary manager hợp lệ.
- An toàn truy vấn: Bắt buộc sử dụng `IgnoreQueryFilters()` kèm vị từ `center_id IN (targetCenterIds)` tường minh và phép chiếu count `Select(...)`. Không materialize đối tượng vào RAM.

### 3.5. Quy trình Đình chỉ & Kích hoạt Trung tâm (Suspension Hardening)
- Cập nhật `PATCH /api/v1/platform/centers/{centerId}/status`:
  - Tham số `Reason` bắt buộc khi trạng thái thay đổi (`Active` $\leftrightarrow$ `Suspended`), tối thiểu 5 ký tự, tối đa 500 ký tự.
  - Khi chuyển sang `Suspended`: Tức thì tăng `auth_version` của toàn bộ tài khoản trong trung tâm, thu hồi toàn bộ refresh token còn hiệu lực, ghi nhận số user và token bị hủy vào audit log.
  - Khi kích hoạt lại (`Active`): Kiểm tra bắt buộc trung tâm phải có `primary_manager_user_id` hợp lệ và đang `Active`. Không phục hồi phiên đăng nhập cũ (người dùng phải đăng nhập lại).

### 3.6. Bảo mật Bản thân Tài khoản PlatformAdmin (Self-Security)
- Quyền mới: `platform.account.manage_own`.
- Endpoints:
  - `POST /api/v1/platform/me/change-password`: Yêu cầu mật khẩu hiện tại, áp dụng chính sách $\ge 12$ ký tự, tăng `auth_version`, thu hồi mọi refresh token.
  - `POST /api/v1/platform/me/revoke-sessions`: Tăng `auth_version`, thu hồi toàn bộ refresh token của chính admin.
  - `GET /api/v1/platform/me/security`: Trả về thông tin phiên an toàn (username, auth version, row version, số phiên active; không chứa secret hay bootstrap info).
- Bootstrap Hardening: Khi hệ thống đã có ít nhất 1 PlatformAdmin hợp lệ, `PlatformAdminProvisioner` bỏ qua việc cập nhật mật khẩu, không bao giờ ghi đè mật khẩu của tài khoản đang tồn tại.

---

## 4. Hậu quả & Đánh giá Tác động (Consequences)

### Tích cực (Positive):
1. Hoàn thiện năng lực vận hành thực tế cho giáo dục đa trung tâm: quản lý nhiều manager, chuyển giao người quản lý chính, đặt lại mật khẩu an toàn.
2. Bảo vệ tuyệt đối dữ liệu học thuật của học sinh trước quản trị viên nền tảng (Academic Privacy Invariant).
3. Đảm bảo tính toàn vẹn kiểm toán (Immutable & Redacted Audit Logs).
4. Đình chỉ trung tâm tức thời ngắt phiên làm việc của toàn bộ người dùng, triệt tiêu rủi ro sử dụng trái phép.

### Rủi ro & Giải pháp Kiểm soát (Risks & Mitigations):
1. **Rủi ro chu trình khóa ngoại `centers` $\leftrightarrow$ `users`:** Cột `primary_manager_user_id` nullable và cấu hình `OnDelete(DeleteBehavior.Restrict)`. Migration được kiểm thử up/down trên MySQL thật và kiểm tra EF model snapshot cẩn trọng.
2. **Rủi ro rò rỉ dữ liệu qua Audit Log:** Thiết lập filter allow-list DTO ở tầng BLL trước khi trả về API; không bao giờ lưu trữ credential vào payload audit log.
3. **Rủi ro Race condition chuyển quyền Primary:** Khóa lạc quan OCC trên cả `Center.row_version` và `User.row_version` đảm bảo chỉ duy nhất một request thành công, request xung đột bị từ chối 409.
