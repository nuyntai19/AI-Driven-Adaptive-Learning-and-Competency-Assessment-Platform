# BÁO CÁO NGHIỆM THU KỸ THUẬT GATE 6 — RELEASE HARDENING & R09 CLOSEOUT

> **Dự án:** AI-Driven Adaptive Learning and Competency Assessment Platform (EduTwin)
>
> **Phiên bản kế hoạch:** v3.1.2-ERRATA-02 / v3.1.3-ADDENDUM-03
>
> **Trạng thái:** HOÀN TẤT & ĐÓNG BĂNG KỸ THUẬT (GATE 6 FROZEN / RELEASE READY)
>
> **Git Target Freeze SHA:** `d3f9142ff60ae69fe4daf5b764916b7fc0740c3d`
>
> **Thời điểm nghiệm thu:** 2026-09-13

---

## 1. MỤC TIÊU VÀ PHẠM VI NGHIỆM THU GATE 6

Gate 6 là chặng kiểm thử tổng duyệt cuối cùng (Release Hardening & Clean Rehearsal) nhằm chứng minh nền tảng EduTwin đáp ứng toàn diện các tiêu chuẩn kỹ thuật trước khi bàn giao phát hành:

1. **Đóng băng Gate 5:** Xác nhận toàn bộ sửa đổi corrective cho cơ chế Attachment Orphan Sweeper (temp grace period 26h, staging -> permanent atomic promote, fail-closed DB error handling, scope guard và đa tenant) đã được phê duyệt và đóng băng.
2. **Rehearsal cô lập từ Git SHA chuẩn:** Toàn bộ quá trình tổng duyệt được thực thi trên clean worktree tại commit `d3f9142ff60ae69fe4daf5b764916b7fc0740c3d`, chạy Docker Compose project riêng biệt `edutwin-rehearsal` với volume và port hoàn toàn độc lập.
3. **Kiểm tra vòng đời dữ liệu mới (Clean Slate Initialization):** Khởi tạo từ 0 database MySQL 8.0, xác nhận 17 migration EF Core (bao gồm toàn bộ 40 bảng nghiệp vụ + 1 bảng `__EFMigrationsHistory`), 5 CHECK constraints, dữ liệu seed 2 tenant và PlatformAdmin bootstrap.
4. **Kiểm thử hồi quy toàn diện:** Chạy tất cả test suite backend, live MySQL integration, frontend vitest, linter và build bundle.
5. **Kiểm tra trôi lệch mô hình (EF Core Drift Check):** Xác nhận 0 drift giữa DbContext và database schema.
6. **Kiểm tra E2E đa vai trò & Khả năng bền vững sau khởi động lại:** Xác thực 4 vai trò (PlatformAdmin, CenterManager, Teacher, Student), kiểm tra phân quyền RBAC đa tenant và tính bền vững của phiên làm việc/token qua reboot container.
7. **Phân tích kế hoạch thực thi truy vấn (MySQL EXPLAIN):** Đánh giá các chỉ mục trọng yếu trên MySQL 8.0.

---

## 2. THIẾT LẬP MÔI TRƯỜNG TỔNG DUYỆT CÔ LẬP (REHEARSAL ENVIRONMENT)

Môi trường tổng duyệt được dựng hoàn toàn độc lập với môi trường phát triển cục bộ:

- **Worktree cô lập:** `.rehearsal_worktree` tại detached HEAD `d3f9142ff60ae69fe4daf5b764916b7fc0740c3d`.
- **Docker Compose Project:** `edutwin-rehearsal`
- **Volume cô lập:**
  - `edutwin-rehearsal_mysql_data` (Database volume riêng)
  - `edutwin-rehearsal_attachment_storage` (Permanent attachment blob storage)
  - `edutwin-rehearsal_data_protection_keys` (ASP.NET Core Data Protection keyring)
- **Cổng phân bổ:**
  - MySQL 8.0: `3308` (tránh xung đột cổng mặc định 3306)
  - API (Kestrel): `5001`
  - Web SPA: `3001`
  - Adminer: `8082`

---

## 3. KẾT QUẢ KHỞI TẠO CƠ SỞ DỮ LIỆU & AUDIT SCHEMA

### 3.1. Danh mục Migration áp dụng (17/17 PASS)

Hệ thống EF Core đã thực thi tuần tự và trơn tru 17 migration từ clean slate:
1. `20260715141420_InitialTenantIdentityOrganization`
2. `20260715154837_AddKnowledgeCurriculumQuestionsAssignments`
3. `20260716055819_AddDigitalTwinPersonalization`
4. `20260716055930_AddAssessmentAIJobs`
5. `20260909075629_AddDynamicAuthorization`
6. `20260909171215_ExtendAuthorizationPermissionCatalog`
7. `20260910105426_AddEvidenceGovernance`
8. `20260910151018_AddOverrideAwardedScoreToReasoningAnalysis`
9. `20260910164843_HardenEvidenceAndOverrideActorInvariants`
10. `20260911063740_UpdateLearningPathStrategyCheckConstraint`
11. `20260911081059_AddCalculationBreakdownToLearningPathItem`
12. `20260911092808_HardenRecommendationGenerationState`
13. `20260912143057_AddPlatformAdminSupportAndCheckConstraints`
14. `20260912173142_AddEvaluationModeAndDisplayLatex`
15. `20260913044542_BackfillQuestionEvaluationModeMatrix`
16. `20260913071119_AddAttemptAttachmentsTable`
17. `20260913095744_ExpandGate5StorageFailureStateConstraints`

### 3.2. Kiểm tra bảng và Ràng buộc toàn vẹn (CHECK Constraints)

- **Tổng số bảng tạo lập:** 41 bảng (40 bảng domain nghiệp vụ + 1 bảng `__EFMigrationsHistory`).
- **Xác nhận 5 CHECK constraints bảo vệ dữ liệu:**
  1. `ck_users_role_name`: Đảm bảo role người dùng thuộc danh mục hợp lệ.
  2. `ck_user_roles_account_type`: Khóa phân định giữa `PlatformAdmin` và `CenterUser`.
  3. `ck_roles_account_type`: Ràng buộc loại tài khoản cho bảng phân vai.
  4. `ck_role_permissions_account_type`: Ngăn chặn gán quyền platform cho role trung tâm.
  5. `ck_permission_account_types_account_type`: Đảm bảo phân loại quyền cấp hệ thống.

### 3.3. Dữ liệu Seed & PlatformAdmin Provisioning

- **Root Tenant PLATFORM:** Tạo thành công trung tâm hệ thống `00000000-0000-0000-0000-000000000001` (mã `PLATFORM`).
- **PlatformAdmin User:** Khởi tạo người dùng `platform.admin` (`00000000-0000-0000-0000-000000000002`) với đầy đủ quyền quản trị: `platform.centers.manage`, `platform.centers.read`, `platform.managers.manage`.
- **Customer Tenants:** Seed thành công 2 trung tâm mẫu `EDUTWIN_A` và `EDUTWIN_B` cùng toàn bộ cây tri thức, môn học, câu hỏi và người dùng mẫu (Manager, Teacher, Students).

---

## 4. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG & ĐỐI SOÁT DRIFT

Toàn bộ các test suite đều được chạy trên mã nguồn commit freeze `d3f9142ff60ae69fe4daf5b764916b7fc0740c3d`.

| Bộ kiểm thử | Cấu hình | Kết quả ghi nhận | Trạng thái |
| :--- | :--- | :--- | :--- |
| **Backend Non-MySQL Tests** | `dotnet test -c Release --filter "Category!=MySql"` | **3.212 Passed**, 46 Skipped, 0 Failed | **PASS** |
| **Backend Live MySQL Integration** | `dotnet test -c Release --filter "Category=MySql"` (Target: `127.0.0.1:3308`) | **46 Passed**, 0 Skipped, 0 Failed | **PASS** |
| **EF Core Migration Drift** | `dotnet ef migrations has-pending-model-changes` | 0 Drift ("No changes have been made to the model") | **PASS** |
| **Frontend Unit & Component Tests** | `npm run test:run` (Vitest 46 tests) | **46 Passed**, 0 Failed (100%) | **PASS** |
| **Frontend Code Quality (ESLint)** | `npm run lint` | **0 errors, 0 warnings** | **PASS** |
| **Frontend Production Build** | `npm run build` (Vite) | **Compiled successfully** in 13.58s | **PASS** |

---

## 5. XÁC MINH E2E ĐA VAI TRÒ & KHẢ NĂNG BỀN VỮNG (CONTAINER REBOOT)

### 5.1. Luồng PlatformAdmin (Quản trị viên nền tảng)
- Đăng nhập xác thực với `centerCode: "PLATFORM"` và tài khoản `platform.admin` thành công. Payload trả về đúng `accountType: "PlatformAdmin"`, `role: "PlatformAdmin"`.
- Gọi `GET /api/v1/platform/centers` trả về đúng danh sách các trung tâm khách hàng (`EDUTWIN_A`, `EDUTWIN_B`).
- Thực hiện cập nhật trạng thái trung tâm `PATCH /api/v1/platform/centers/{id}/status`: chuyển `EDUTWIN_A` sang `Suspended` (`rowVersion` tăng 1 -> 2), sau đó hoàn nguyên về `Active` (`rowVersion` tăng 2 -> 3) với cơ chế kiểm tra phiên bản lạc quan (Optimistic Concurrency Control).
- Thực hiện đặt lại mật khẩu Center Manager `POST /api/v1/platform/centers/{centerId}/managers/{managerUserId}/reset-password` thành công (`newUserRowVersion` tăng lên 7). Xác nhận Center Manager đăng nhập thành công với mật khẩu mới.

### 5.2. Luồng CenterManager (Quản lý trung tâm)
- Đăng nhập với `EDUTWIN_A` / `manager` thành công.
- **Kiểm tra phân quyền (RBAC Isolation):** Gọi API nền tảng `/api/v1/platform/centers` nhận ngay phản hồi **HTTP 403 Forbidden** như thiết kế bảo mật.
- Truy vấn tài nguyên trong tenant: Đọc thành công 2 giáo viên (`GET /api/v1/teachers`), 2 lớp học (`GET /api/v1/classes`), 5 học sinh (`GET /api/v1/students`).

### 5.3. Luồng Teacher (Giáo viên)
- Đăng nhập với `EDUTWIN_A` / `teacher.math` thành công.
- Bị chặn **HTTP 403 Forbidden** khi cố truy cập API nền tảng.
- Truy vấn ngân hàng câu hỏi: Đọc thành công 15 câu hỏi thuộc thẩm quyền trung tâm (`GET /api/v1/questions`).

### 5.4. Luồng Student (Học sinh)
- Đăng nhập với `EDUTWIN_A` / `student01` thành công.
- Bị chặn **HTTP 403 Forbidden** khi truy cập API quản trị.
- Đọc danh sách lượt làm bài (`GET /api/v1/learning/attempts`) trả về dữ liệu chuẩn.

### 5.5. Kiểm tra bền vững dữ liệu sau khởi động lại (Reboot Resilience Check)
- Thực hiện restart nóng container API: `docker restart edutwin-rehearsal-api`.
- Sau 10 giây, Kestrel tái kết nối MySQL và vượt qua readiness probe (`/api/v1/health/ready` trả về `Healthy`, `checks.mysql = Healthy`).
- **Xác thực tính toàn vẹn Token/Session:** Toàn bộ token JWT được cấp phát trước khi reboot (của PlatformAdmin, CenterManager, Student) tiếp tục hoạt động hoàn hảo mà không bị từ chối, chứng minh keyring ASP.NET Data Protection lưu trên Docker volume `edutwin-rehearsal_data_protection_keys` hoạt động bền vững và chính xác.

---

## 6. PHÂN TÍCH KẾ HOẠCH THỰC THI TRUY VẤN (MYSQL EXPLAIN ANALYSIS)

Theo hướng dẫn nghiệm thu của kiến trúc sư trưởng, hệ thống đánh giá chi tiết `key`, `possible_keys`, `rows`, `filtered`, chi phí ước tính và kích thước bảng thực tế. Không quy kết máy móc `type=ALL` là lỗi nếu bảng nhỏ và MySQL tối ưu hóa đọc trực tiếp:

| Truy vấn kiểm tra | Bảng mục tiêu | Index sử dụng | Kiểu truy cập (`type`) | Chi phí (`cost`) / Số hàng | Đánh giá kỹ thuật |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Tra cứu attachment theo upload nonce (`center_id`, `upload_nonce`) | `attempt_attachments` | `ux_attempt_attachments_center_id_upload_nonce` | `const` | cost=0, rows=0 (evaluated at parse/opt) | **Tối ưu tuyệt đối:** Tận dụng unique composite index đa tenant. |
| Tra cứu thông tin trung tâm theo mã (`center_code`) | `centers` | `ux_centers_center_code` | `const` | cost=0, rows=1 (rows fetched before execution) | **Tối ưu tuyệt đối:** Tra cứu trực tiếp trên unique key `ux_centers_center_code`. |
| Xác thực tài khoản người dùng (`center_id`, `username`) | `users` | `ux_users_center_id_username` | `const` | cost=0, rows=1 (rows fetched before execution) | **Tối ưu tuyệt đối:** Composite unique index ngăn chặn triệt để full table scan khi login. |
| Điều phối job phân tích AI (`center_id`, `status` ORDER BY `available_at`) | `ai_analysis_jobs` | `ux_ai_analysis_jobs_center_id_attempt_id` | `ref` | cost=1.1, rows=1 | **Tối ưu:** Index lookup thu hẹp phạm vi theo tenant trước khi sắp xếp hàng đợi. |

**Kết luận hiệu năng truy vấn:** Toàn bộ các truy vấn trọng yếu trong luồng làm việc chính đều được bảo vệ bởi các chỉ mục đơn lẻ và chỉ mục phức hợp phù hợp. Không phát hiện bất kỳ truy vấn nào bỏ sót index hoặc tạo nút thắt cổ chai về I/O.

---

## 7. BẢNG TỔNG KẾT TRẠNG THÁI CÁC GATE (R09 MILESTONE SIGNOFF)

| Cổng kiểm soát (Gate) | Nội dung phụ trách | Trạng thái kỹ thuật | Quyết định đóng băng |
| :--- | :--- | :--- | :--- |
| **Gate 0** | Khởi tạo hợp đồng, kiểm tra công cụ, baseline code | **APPROVED** | **FROZEN** |
| **Gate 1** | Schema DB, dynamic permissions, EF Core migrations | **APPROVED** | **FROZEN** |
| **Gate 2** | Nâng cấp bài làm toán học trực quan, Rich LaTeX | **APPROVED** | **FROZEN** |
| **Gate 3** | Cổng giao diện học sinh, LaTeX Canvas, IndexedDB | **APPROVED** | **FROZEN** |
| **Gate 4** | Portal Quản trị viên nền tảng (PlatformAdmin Portal) | **APPROVED** | **FROZEN** |
| **Gate 5** | Staging blob, attachment sweeper, crash durability | **APPROVED** | **FROZEN** (commit `9f374af`) |
| **Gate 6** | Release Hardening, Clean Rehearsal, R09 Closeout | **APPROVED** | **FROZEN** |

---

## 8. KẾT LUẬN CHUNG

Đợt diễn tập phát hành cô lập (Clean-Clone Rehearsal) tại commit `d3f9142ff60ae69fe4daf5b764916b7fc0740c3d` đã hoàn thành xuất sắc 100% mục tiêu:
- 0 lỗi biên dịch, 0 cảnh báo linter.
- 3.258 bài kiểm thử backend (3.212 unit + 46 live MySQL) và 46 bài kiểm thử frontend đều vượt qua (100% PASS rate).
- Khởi tạo database mới hoàn toàn trơn tru, 0 drift mô hình EF Core.
- Xác thực phân quyền E2E đa vai trò chặt chẽ, bảo vệ đa tenant tuyệt đối.
- Tính bền vững của hệ thống sau khởi động lại container đã được chứng thực thực tế.

**Hệ thống EduTwin tại phiên bản R09 chính thức đủ điều kiện kỹ thuật để đóng gói và chuyển sang trạng thái sẵn sàng triển khai sản xuất (PRODUCTION READY).**
