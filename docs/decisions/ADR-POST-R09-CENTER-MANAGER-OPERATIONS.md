# ADR-POST-R09-CENTER-MANAGER-OPERATIONS: Center Administration End-to-End Completion, Account Lifecycle Governance, and Tenant-Scoped Authority

**Status:** APPROVED  
**Date:** 2026-09-14  
**Scope:** Post-R09 Center Operations Milestone (`POST-R09-CENTER-MANAGER-OPS`)  
**Author:** EduTwin Architecture Team & Codex Review  
**Baseline Git Freeze SHA:** `222e7bbf728c27a6437be18ac860e4f04a5b7c5d` (kế thừa `1f1cb9cccaf102f489f29b3296ee92744e5ad401`)  
**Branch:** `codex/post-r09-center-manager-ops`  

---

## 1. Bối cảnh và Đặt vấn đề (Context and Problem Statement)

Sau khi hoàn tất củng cố năng lực Quản trị Nền tảng (**PlatformAdmin**) tại milestone `POST-R09-PLATFORM-OPS`, hệ thống đã phân lập tuyệt đối mặt bằng điều khiển (Control Plane) khỏi dữ liệu giáo dục đa trung tâm. Bước tiếp theo là hoàn thiện toàn diện năng lực của người Quản lý Trung tâm (**CenterManager**) — tác nhân quản trị cao nhất bên trong một Center:

1. **Ranh giới Phân quyền và Mô hình Vai trò (Role Boundary Invariant):**
   Một số thiết kế ban đầu giả định phân cấp hình tháp `PlatformAdmin > CenterManager > Teacher > Student`. Đây là quan niệm sai lầm:
   - `CenterManager` không phải "super role" tự động kế thừa toàn bộ quyền của `Teacher`.
   - `CenterManager` không thể tự ý chấm bài hay can thiệp vào lớp học thuộc quyền sở hữu riêng của giáo viên nếu không được cấp capability cụ thể.
   - `CenterManager` tuyệt đối không có quyền truy cập vào các API hoặc giao diện quản trị nền tảng (`/api/v1/platform/*`, `/quan-tri-nen-tang/*`).
   - Mọi quyền hạn phải xuất phát từ **effective permissions** động; `AccountType` là ranh giới bất biến.

2. **Khắc phục Drift trong Permission Catalog và Vòng đời Tài khoản (Account Lifecycle):**
   - Permission catalog hiện có quyền `organization.students.delete`, nhưng backend và frontend chưa có use case tương ứng. Cần hiện thực hóa tính năng xóa học sinh theo cơ chế **soft-delete bảo toàn chứng cứ học thuật** (evidence preservation).
   - Chưa có cơ chế đặt lại mật khẩu an toàn cho giáo viên (`Teacher`) và học sinh (`Student`) từ phía quản lý trung tâm khi có sự cố quên mật khẩu hoặc bàn giao tài khoản. Cần bổ sung 2 quyền chuyên biệt có kiểm soát OCC và thu hồi phiên.

3. **Hoàn thiện Giao diện Hồ sơ Trung tâm (Center Profile UX):**
   - Backend đã có `GET /api/v1/centers/me` và `PATCH /api/v1/centers/me` nhưng frontend thiếu màn hình quản trị canonical tại `/quan-ly/trung-tam`. Cần xây dựng màn hình này với cơ chế OCC `RowVersion` chuẩn.

---

## 2. Nguyên tắc và Ràng buộc Cốt lõi (Decision Drivers)

1. **Ranh giới Account Type và Non-Inheritance:**
   $$\text{PlatformAdmin} \neq \text{CenterManager} \neq \text{Teacher} \neq \text{Student}$$
   - Không tồn tại quan hệ kế thừa vai trò (no role inheritance).
   - Quyền hạn thực tế của người dùng luôn được tính từ tập `effective permissions` hợp lệ được gán trong trung tâm hiện tại.
   - Các capability dùng chung (ví dụ xem ngân hàng câu hỏi, xem chương trình học) phải được ánh xạ tường minh trong bảng `role_permissions` với kiểm tra tính tương thích `permission_account_types`.
2. **Cách ly Tenant Tuyệt đối (Strict Tenant Isolation):**
   - Mọi truy vấn và mutation đều bị ràng buộc chặt chẽ bởi `CurrentTenantId`.
   - Truy vấn định danh cross-tenant phải trả về `404 Not Found` (fail-closed) để ngăn ngừa kỹ thuật rò rỉ sự tồn tại của tài nguyên (resource enumeration).
   - `IgnoreQueryFilters()` bị cấm trong mọi luồng thông thường của CenterManager.
3. **Bảo tồn Bằng chứng Học thuật khi Xóa (Evidence Preservation Invariant):**
   - Xóa học sinh (`Student`) và giáo viên (`Teacher`) **bắt buộc là Soft-Delete** (`is_deleted = 1`, `deleted_at = UTC`).
   - Tuyệt đối không xóa vật lý (Hard-Delete).
   - Giữ nguyên 100% dữ liệu lịch sử liên quan: `attempts`, `attempt_attachments`, `reasoning_analyses`, `evidence_assessments`, `student_twins`, `knowledge_twins`, `behavior_twins`, `twin_update_history`, `assignment_targets`, `student_assignment_progress`.
4. **Quản trị Phiên và Kiểm toán Khử khuẩn (Session Eviction & Redacted Audit):**
   - Mọi thao tác thay đổi mật khẩu, trạng thái tài khoản hoặc thay đổi vai trò:
     * Tăng tức thì `users.auth_version`.
     * Thu hồi toàn bộ `refresh_tokens` còn hiệu lực của đối tượng đích.
     * Ghi nhật ký vào `authorization_audit_logs` với dữ liệu đã khử khuẩn (redacted — 0 mật khẩu, 0 password hash, 0 token, 0 secret).
5. **Duy trì Bất biến Cấu trúc Cơ sở dữ liệu (40 Application Tables Invariant):**
   - Giữ nguyên tổng số 40 bảng nghiệp vụ trong EF Core Model Snapshot. Không sinh thêm bảng mới ngoài phạm vi phê duyệt.

---

## 3. Các Quyết định Kiến trúc Chi tiết (Architectural Decisions)

### 3.1. Phân định Rõ Ràng Ranh giới và Quyền hạn của CenterManager
- **Phạm vi thẩm quyền:**
  - `CenterManager` chỉ có thẩm quyền bên trong trung tâm mà tài khoản trực thuộc (`user.CenterId`).
  - `CenterManager` bị cấm truy cập toàn bộ các endpoint `/api/v1/platform/*` và các route giao diện `/quan-tri-nen-tang/*`.
  - Không gán quyền `dashboards.teacher.read_scoped` cho `CenterManager`; thay vào đó, `CenterManager` sử dụng `dashboards.center.read` để xem báo cáo tổng quan cấp trung tâm.
  - Giáo viên (`Teacher`) thao tác bài tập, lớp học, học sinh dựa trên ownership (`class.TeacherId == currentUser.UserId`); `CenterManager` có thể xem/quản lý toàn trung tâm nếu có quyền tương ứng.

### 3.2. Triển khai Soft-Delete Học sinh (`DELETE /api/v1/students/{studentId}`)
- **Quyền yêu cầu:** `organization.students.delete` (đã có sẵn trong permission catalog).
- **Hành vi nghiệp vụ:**
  1. Kiểm tra học sinh tồn tại, thuộc trung tâm hiện tại và có `AccountType == 'Student'`. Nếu không khớp, trả về `404 Not Found`.
  2. Cập nhật `students.is_deleted = 1`, `students.deleted_at = UtcNow`, `students.deleted_by = actorUserId`.
  3. Cập nhật tài khoản người dùng liên kết `users.is_deleted = 1`, `users.deleted_at = UtcNow`, `users.deleted_by = actorUserId`.
  4. Cập nhật các bản ghi thành viên lớp đang hoạt động (`class_students.status == 'Active'`) chuyển sang `status = 'Removed'`, `removed_at = UtcNow`.
  5. Tăng `users.row_version` và `users.auth_version`.
  6. Thu hồi toàn bộ refresh token của học sinh đó.
  7. Bảo toàn toàn bộ các bản ghi `attempts`, `assignment_targets`, `student_assignment_progress`, `student_twins`.
  8. Ghi nhận nhật ký kiểm toán `authorization_audit_logs` với `action_type = 'STUDENT_SOFT_DELETED'`.

### 3.3. Đặt lại Mật khẩu Giáo viên & Học sinh (Reset Password Endpoints)
- **Bổ sung hai quyền mới vào catalog:**
  1. `organization.teachers.reset_password`:
     - Phân loại: `IsSensitive = true`, `IsDelegable = true` (trong phạm vi phân quyền của CenterManager).
     - Tương thích: Chỉ gắn cho `AccountType == 'CenterManager'`.
  2. `organization.students.reset_password`:
     - Phân loại: `IsSensitive = true`, `IsDelegable = true` (trong phạm vi phân quyền của CenterManager).
     - Tương thích: Chỉ gắn cho `AccountType == 'CenterManager'`.
- **API Endpoints:**
  - `POST /api/v1/teachers/{teacherId}/reset-password`
  - `POST /api/v1/students/{studentId}/reset-password`
- **Payload Request:**
  ```json
  {
    "newPassword": "SecurePassword123!",
    "expectedUserRowVersion": "12",
    "reason": "Yêu cầu cấp lại mật khẩu từ phụ huynh / giáo viên"
  }
  ```
- **Hành vi bảo mật:**
  - Kiểm tra độ dài mật khẩu tối thiểu 8 ký tự theo chuẩn chính sách.
  - Sử dụng `IPasswordHasher` hiện hành để băm mật khẩu mới (`PasswordHash`).
  - Kiểm tra OCC trên `users.row_version` qua `expectedUserRowVersion`. Nếu lệch, trả về `409 ConcurrencyConflict`.
  - Tăng `users.row_version` và `users.auth_version`.
  - Thu hồi toàn bộ active refresh tokens của user.
  - Ghi nhận `authorization_audit_logs` với `action_type = 'TEACHER_PASSWORD_RESET'` hoặc `'STUDENT_PASSWORD_RESET'`. Payload `after_data` tuyệt đối không chứa `newPassword` hay `passwordHash`.
  - Ngăn chặn tuyệt đối việc sử dụng endpoint này để can thiệp vào tài khoản `CenterManager` hoặc `PlatformAdmin`.

### 3.4. Hồ sơ Trung tâm (Center Profile) và Điều hướng Canonical
- **Trang canonical:** `/quan-ly/trung-tam` (`CenterProfilePage.tsx`).
- **Endpoints:** `GET /api/v1/centers/me` và `PATCH /api/v1/centers/me`.
- **Trường dữ liệu:**
  - `CenterCode`: Read-only.
  - `Status`: Read-only đối với CenterManager.
  - `CenterName`: Cho phép cập nhật.
  - `Timezone`: Cho phép cập nhật (danh sách IANA timezones hợp lệ).
  - `RowVersion`: Gửi kèm trong `PATCH` payload để bảo vệ chống ghi đè đồng thời (OCC).
- **Cập nhật cache tức thì:** Sau khi `PATCH` thành công, frontend cập nhật state cục bộ và cache React Query ngay lập tức mà không cần người dùng tải lại trang.

### 3.5. Chuẩn hóa Cấu trúc Tab Phân quyền Động (`/quan-ly/phan-quyen`)
- Giữ nguyên cấu trúc route canonical V1 tại `/quan-ly/phan-quyen` với 3 tabs chức năng:
  1. **Vai trò & Quyền hạn (Roles & Permissions):** Quản lý vai trò động, ma trận quyền tương thích account type, huy hiệu sensitive.
  2. **Gán vai trò Người dùng (User-Role Assignment):** Gán/thu hồi vai trò cùng account type, bảo vệ bất biến `LastTenantAdmin` (không cho phép gỡ bỏ quyền quản trị cuối cùng của trung tâm).
  3. **Nhật ký Kiểm toán (Authorization Audit Logs):** Xem lịch sử cấp/thu hồi quyền, reset password, đổi trạng thái; hỗ trợ lọc theo thời gian, hành động, actor, target và sao chép TraceId.

---

## 4. Hậu quả và Đánh giá Tác động (Consequences)

### Tích cực
- Hoàn thiện toàn bộ vòng đời quản trị nhân sự (Teacher, Student) trong trung tâm.
- Giải quyết triệt để vấn đề permission catalog mồ côi (`organization.students.delete`).
- Khóa chặt ranh giới domain giữa CenterManager và Teacher, chấm dứt hoàn toàn nguy cơ vượt quyền hoặc kế thừa sai lệch.
- Bảo toàn tuyệt đối lịch sử học tập và mô hình Learning Digital Twin của học sinh kể cả khi học sinh nghỉ học.
- Đảm bảo 100% tuân thủ OCC, Session Eviction và Audit Redaction.

### Giới hạn & Điểm lưu ý
- Không triển khai hard-delete; việc dọn dẹp vật lý cơ sở dữ liệu nếu có chỉ được thực hiện ở cấp lưu trữ lạnh (cold storage archive) ngoài phạm vi ứng dụng.
- Số lượng bảng vật lý giữ nguyên 40 bảng (0 model drift).
