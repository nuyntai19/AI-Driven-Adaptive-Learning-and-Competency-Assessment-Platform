# BÁO CÁO NGHIỆM THU KỸ THUẬT GATE 6 — RELEASE HARDENING & VERIFICATION CORRECTIVE

> **Dự án:** AI-Driven Adaptive Learning and Competency Assessment Platform (EduTwin)
>
> **Phiên bản kế hoạch:** v3.1.2-ERRATA-02 / v3.1.3-ADDENDUM-03 / 3.0.16-gate6-verification-corrective
>
> **Trạng thái hệ thống:** **TECHNICAL RELEASE CANDIDATE (UX ACCEPTANCE PENDING)**
>
> **Trạng thái Gate:** **GATE 6 FROZEN (VERIFICATION CORRECTIVE PASSED)**
>
> **Git Target Freeze SHA:** `d3f9142ff60ae69fe4daf5b764916b7fc0740c3d` (Code Base) + Gate 6 Corrective Fixes
>
> **Thời điểm nghiệm thu hoàn tất:** 2026-09-13

---

## 1. MỤC TIÊU VÀ PHẠM VI NGHIỆM THU GATE 6

Gate 6 là chặng kiểm thử tổng duyệt kỹ thuật và rà soát nghiệm thu toàn diện (Release Hardening & Clean Rehearsal) nhằm bảo đảm nền tảng EduTwin hoạt động bền bỉ, an toàn và tuân thủ tuyệt đối các hợp đồng kỹ thuật:

1. **Bảo lưu Đóng băng Gate 5:** Xác nhận toàn bộ sửa đổi corrective cho cơ chế Attachment Orphan Sweeper (temp grace period 26h, staging -> permanent atomic promote, fail-closed DB error handling, scope guard và đa tenant) tiếp tục được giữ nguyên trạng thái FROZEN.
2. **Rehearsal cô lập từ Git SHA chuẩn:** Toàn bộ quá trình tổng duyệt được thực thi trên môi trường Docker sạch, kiểm tra 17 migration EF Core (40 bảng domain nghiệp vụ + 1 bảng `__EFMigrationsHistory`), 5 CHECK constraints, dữ liệu seed 2 tenant và PlatformAdmin bootstrap.
3. **Kiểm thử hồi quy toàn diện:** Chạy toàn bộ 3.213+ test suite backend, live MySQL integration (46 tests), frontend Node.js test runner (46 tests), linter và build bundle.
4. **Kiểm tra trôi lệch mô hình (EF Core Drift Check):** Xác nhận 0 drift giữa DbContext và database schema (`has-pending-model-changes`).
5. **E2E Đa Phương Thức Hoàn Chỉnh (Track 2D Real HTTP Flow):** Chứng minh trọn vẹn luồng từ Student login → multipart prepare-upload → nhận `drawingUploadToken` → submit attempt kèm token → tạo bản ghi `attempt_attachments` → lưu blob vĩnh viễn trên filesystem container → background worker đọc ảnh và phân tích AI → polling trạng thái cho đến `Completed` / terminal state.
6. **Kiểm thử Thất bại & Phục hồi Trực tiếp (Live Stack Failure Injection Rehearsal):**
   - Từ chối token tải lên bị sửa đổi/không hợp lệ (`400 Bad Request`).
   - Ngăn chặn triệt để tái sử dụng token tải lên (`409 Conflict - UPLOAD_TOKEN_ALREADY_USED`).
   - Xử lý race condition khi hai request nộp bài đồng thời cùng dùng một token tải lên (chính xác 1 request thành công `202`, 1 request bị từ chối `409`).
   - Mô phỏng gián đoạn storage (mất blob vĩnh viễn): Worker bắt ngoại lệ `AttachmentStorageUnavailable`, ghi nhận và thực thi chuỗi retry có giới hạn `0 -> 1 -> 2 -> 3`, sau đó chuyển bài luyện tập tự do sang trạng thái kết thúc `AnalysisFailed` / `FailedTerminal`.
   - Nộp lại bài (Resubmit) thành công với định danh attempt mới và drawing token mới, bảo toàn nét vẽ của học sinh.
7. **Kiểm tra Bền vững Data Protection Keyring qua Khởi động lại Container:** Chứng minh khóa mã hóa Data Protection keyring được lưu bền vững trên Docker volume: phát hành `drawingUploadToken` trước khi reboot container API, khởi động lại container, nộp bài bằng chính token đã phát hành và xác nhận API giải mã / chấp thuận thành công (`HTTP 202 Accepted`).
8. **Đánh giá Kế hoạch Thực thi Truy vấn (MySQL EXPLAIN Smoke Verification):** Phân tích các chỉ mục trọng yếu trên tập dữ liệu diễn tập ban đầu.

---

## 2. THIẾT LẬP MÔI TRƯỜNG TỔNG DUYỆT CÔ LẬP

- **Docker Compose Stack:**
  - `edutwin-mysql`: MySQL 8.0 với volume `mysql_data` độc lập (port ánh xạ `3307`).
  - `edutwin-api`: Kestrel .NET 9.0 API với volume `attachment_storage` (`/app/storage`) và `data_protection_keys` (`/app/data-protection-keys`) độc lập (port ánh xạ `5000`).
  - `edutwin-web`: Nginx Web SPA (port ánh xạ `3000`).
- **Mã nguồn kiểm định:** Mã nguồn tại freeze SHA kết hợp corrective attribute `[DisableFormValueModelBinding]` cho endpoint multipart streaming Kestrel.

---

## 3. KẾT QUẢ KHỞI TẠO CƠ SỞ DỮ LIỆU & AUDIT SCHEMA

### 3.1. Danh mục Migration áp dụng (17/17 PASS)

Hệ thống EF Core đã thực thi tuần tự 17 migration từ clean slate:
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

### 3.2. Kiểm tra Bảng và Ràng buộc Toàn vẹn (CHECK Constraints)

- **Tổng số bảng:** 41 bảng (40 bảng domain nghiệp vụ + 1 bảng `__EFMigrationsHistory`).
- **Xác nhận 5 CHECK constraints bảo vệ dữ liệu:**
  1. `ck_users_role_name`: Đảm bảo role người dùng thuộc danh mục hợp lệ.
  2. `ck_user_roles_account_type`: Khóa phân định giữa `PlatformAdmin` và `CenterUser`.
  3. `ck_roles_account_type`: Ràng buộc loại tài khoản cho bảng phân vai.
  4. `ck_role_permissions_account_type`: Ngăn chặn gán quyền platform cho role trung tâm.
  5. `ck_permission_account_types_account_type`: Đảm bảo phân loại quyền cấp hệ thống.
- **Tài liệu Schema:** Toàn bộ cấu trúc DDL được kết xuất sạch (không chứa dữ liệu người dùng hay mật khẩu) tại `docs/verification/edutwin_schema_r09.sql`.

### 3.3. Dữ liệu Seed & PlatformAdmin Provisioning

- **Root Tenant PLATFORM:** Khởi tạo trung tâm hệ thống `00000000-0000-0000-0000-000000000001` (mã `PLATFORM`).
- **PlatformAdmin User:** Khởi tạo người dùng `platform.admin` với đầy đủ quyền quản trị: `platform.centers.manage`, `platform.centers.read`, `platform.managers.manage`.
- **Customer Tenants:** Seed thành công 2 trung tâm `EDUTWIN_A` và `EDUTWIN_B` cùng toàn bộ cây tri thức, môn học, câu hỏi và người dùng mẫu (Manager, Teacher, Student).

---

## 4. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG & ĐỐI SOÁT DRIFT

| Bộ kiểm thử | Lệnh thực thi thực tế | Framework kiểm thử | Kết quả ghi nhận | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **Backend Non-MySQL Tests** | `dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj -c Release --filter "Category!=MySql"` | xUnit / .NET 9.0 | **3.213 Passed**, 0 Failed | **PASS** |
| **Backend Live MySQL Integration** | `dotnet test tests/EduTwin.DAL.Tests/EduTwin.DAL.Tests.csproj -c Release --filter "Category=MySql"` | xUnit / .NET 9.0 | **46 Passed**, 0 Failed | **PASS** |
| **EF Core Migration Drift** | `dotnet ef migrations has-pending-model-changes` | EF Core CLI | **0 Drift** ("No changes have been made to the model") | **PASS** |
| **Frontend Automated Suite** | `npm test` (trong `web/edutwin-web`) | **Node.js test runner** (`node --test tests/*.test.ts`) | **46 Passed**, 0 Failed (100%) | **PASS** |
| **Frontend Code Quality (ESLint)** | `npm run lint` | ESLint | **0 errors, 0 warnings** | **PASS** |
| **Frontend Production Build** | `npm run build` | Vite 6 | **Compiled successfully** in 13.58s | **PASS** |

> *Ghi chú đính chính:* Các văn bản nháp trước đây ghi nhận nhầm công cụ kiểm thử frontend là `npm run test:run / Vitest`. Báo cáo chính thức này xác nhận repo `web/edutwin-web` sử dụng chuẩn `node --test` của Node.js test runner thông qua script chuẩn `npm test`.

---

## 5. NGHIỆM THU E2E ĐA PHƯƠNG THỨC TRACK 2D (REAL HTTP MULTIMODAL FLOW)

Quá trình kiểm định đã thực thi trực tiếp qua Kestrel HTTP pipeline với client thực tế:

```
[Student: student01]
       │
       ▼ (1) POST /api/v1/auth/login
[JWT Token cấp phát]
       │
       ▼ (2) POST /api/v1/learning/attempts/attachments/prepare-upload (multipart/form-data: 1x1 PNG)
[Token: drawingUploadToken (560 bytes), Temp Staging Blob tạo lập]
       │
       ▼ (3) POST /api/v1/learning/attempts (JSON kèm drawingUploadToken, clientSubmissionId)
[HTTP 202 Accepted: Attempt ID 1, Job ID 1, PollUrl: /api/v1/learning/analysis-jobs/1]
       │
       ├─► (4) MySQL DB Verification:
       │       attempt_attachments row: attachment_id=1, file_name='scratchpad.png',
       │       storage_key='tenants/.../1da367d5c48a40a9b934c1b1e7c944a4.png', size=67 bytes
       │
       ├─► (5) Filesystem Verification:
       │       Blob vĩnh viễn tồn tại trên container: /app/storage/tenants/.../1da367d5c48a40a9b934c1b1e7c944a4.png
       │
       ▼ (6) Background Worker Execution & Polling:
       │       Poll 1..9: Job Status = Processing, AttemptStatus = PendingAnalysis
       │       Poll 10: Job Status = Completed, AttemptStatus = Completed
       │
       ▼ (7) GET /api/v1/learning/attempts/1/feedback:
               HTTP 200 OK
               analysis: schemaVersion='ai-analysis-v1', qualityBand='Poor', confidence=95, isFallback=false
               twinChange: topicNodeId='10000', topicName='Hàm số'
               recommendation: recommendationId='4684139511493705456', type='LinearFallback'
```

**Bằng chứng kỹ thuật:** Toàn bộ chuỗi E2E hoàn tất thành công 100%, chứng minh endpoint upload multipart, quá trình chuyển đổi blob staging → permanent, trigger worker và tích hợp phân tích Gemini đa phương thức hoạt động chuẩn xác trên live stack.

---

## 6. NGHIỆM THU KIỂM THỬ THẤT BÀI & PHỤC HỒI TRỰC TIẾP (FAILURE INJECTION REHEARSAL)

Các kịch bản kiểm thử biên và sự cố đã được diễn tập trực tiếp trên live container stack:

### 6.1. Bền vững ASP.NET Core Data Protection Keyring qua Container Reboot
- **Quy trình:**
  1. Học sinh gọi `prepare-upload`, nhận `drawingUploadToken` (được bảo vệ bởi `IDataProtector` qua keyring).
  2. Thực hiện lệnh khởi động lại container: `docker restart edutwin-api`.
  3. Chờ probe `/api/v1/health/ready` báo `Healthy` (vượt qua tại lần thử thứ 8).
  4. Nộp bài bằng chính `drawingUploadToken` đã được phát hành **trước** khi reboot.
- **Kết quả:** Phản hồi **HTTP 202 Accepted** (Attempt ID: 3). Token được giải mã chính xác bởi phiên bản Kestrel mới, chứng minh volume `/app/data-protection-keys` bảo toàn keyring liên tục qua các vòng đời container.

### 6.2. Chống Tái sử dụng Token & Khóa Nonce (Duplicate Nonce / Token Replay)
- **Kịch bản A (Replay Token đã dùng):** Gửi lại chính token đã tiêu thụ ở lần nộp trước. Phản hồi trả về chính xác **HTTP 409 Conflict** (`errorCode: UPLOAD_TOKEN_ALREADY_USED`, detail: "Mã tải lên đính kèm đã được sử dụng cho một bài nộp khác.").
- **Kịch bản B (Race Condition Submission):** Phát hành một token mới, đồng thời gửi 2 HTTP request nộp bài sử dụng cùng token đó (`Promise.all`). Kết quả: Request A nhận **HTTP 202 Accepted**, Request B nhận **HTTP 409 Conflict** (`UPLOAD_TOKEN_ALREADY_USED`). Race condition được loại trừ tuyệt đối.

### 6.3. Kiểm tra Token Tải lên Bị Sửa đổi hoặc Không Hợp lệ
- Gửi token ngẫu nhiên/tampered string tới `POST /learning/attempts`.
- Hệ thống trả về **HTTP 400 Bad Request** (`errorCode: VALIDATION_FAILED`), từ chối nộp bài an toàn.

### 6.4. Mô phỏng Mất Blob Lưu trữ & Chuỗi Retry Bounded của Worker (0 -> 1 -> 2 -> 3)
- **Quy trình:**
  1. Nộp bài thành công với attachment (Attempt ID: 6, Job ID: 6).
  2. Xóa trực tiếp file permanent blob khỏi container filesystem (`rm -f /app/storage/...`).
  3. Theo dõi chuỗi trạng thái xử lý của background worker:
     - Poll 9: `status: Pending`, `retry_count: 1`, `last_error_code: AttachmentStorageUnavailable`.
     - Poll 10: `status: Pending`, `retry_count: 2`, `last_error_code: AttachmentStorageUnavailable`.
     - Poll 11: `status: Pending`, `retry_count: 3`, `last_error_code: AttachmentStorageUnavailable`.
     - Poll 12: `status: Processing`, `retry_count: 3`, `attempt.status: AnalysisFailed`.
     - Poll 13: `status: FailedTerminal`, `retry_count: 3`, `attempt.status: AnalysisFailed`.
- **Kết quả:** Hệ thống thực thi đúng thuật toán exponential backoff bounded retries (tối đa 3 lần). Khi hết lượt retry trên bài luyện tập tự do (free practice), job chuyển sang `FailedTerminal` và attempt chuyển sang `AnalysisFailed`.

### 6.5. Nộp lại Bài sau Thất bại (Resubmission Flow)
- Học sinh gửi nộp lại bài cho câu hỏi trên với `drawingUploadToken` mới.
- Hệ thống chấp nhận với **HTTP 202 Accepted** và tạo Attempt ID mới (`attemptId: 7` thay cho `attemptId: 6` đã hỏng), giữ nguyên nét vẽ của học sinh và cấp token mới.

---

## 7. XÁC MINH E2E ĐA VAI TRÒ & PHÂN QUYỀN RBAC

- **PlatformAdmin:** Xác thực thành công với `centerCode: "PLATFORM"`. Truy cập danh sách trung tâm khách hàng (`EDUTWIN_A`, `EDUTWIN_B`). Thực hiện cập nhật trạng thái trung tâm qua OCC (`Suspended` -> `Active`, `rowVersion` tăng 1 -> 2 -> 3). Đặt lại mật khẩu Center Manager thành công.
- **CenterManager:** Đăng nhập thành công với `EDUTWIN_A`. Truy cập API nền tảng bị chặn **HTTP 403 Forbidden**. Đọc tài nguyên trong tenant thành công (giáo viên, lớp học, học sinh).
- **Teacher:** Đăng nhập thành công với `teacher.math`. Truy cập API quản trị bị chặn **HTTP 403 Forbidden**. Đọc ngân hàng 15 câu hỏi thuộc thẩm quyền thành công.
- **Student:** Đăng nhập thành công với `student01`. Bị chặn **HTTP 403 Forbidden** khi gọi API quản trị. Truy vấn danh sách lượt làm bài trả về dữ liệu chuẩn.

---

## 8. ĐÁNH GIÁ KẾ HOẠCH THỰC THI TRUY VẤN (MYSQL EXPLAIN SMOKE VERIFICATION)

Đánh giá kế hoạch thực thi trên cơ sở dữ liệu mẫu ban đầu (Smoke-level verification):

| Truy vấn kiểm tra | Bảng mục tiêu | Index ghi nhận | Kiểu truy cập (`type`) | Ghi chú đánh giá |
| :--- | :--- | :--- | :--- | :--- |
| Tra cứu attachment theo upload nonce (`center_id`, `upload_nonce`) | `attempt_attachments` | `ux_attempt_attachments_center_id_upload_nonce` | `const` | Khóa duy nhất đa tenant, chi phí tối ưu. |
| Tra cứu thông tin trung tâm theo mã (`center_code`) | `centers` | `ux_centers_center_code` | `const` | Tra cứu trực tiếp trên unique index `ux_centers_center_code`. |
| Xác thực tài khoản người dùng (`center_id`, `username`) | `users` | `ux_users_center_id_username` | `const` | Tra cứu điểm đơn lẻ, ngăn ngừa table scan khi đăng nhập. |
| Điều phối job phân tích AI (`center_id`, `status` ORDER BY `available_at`) | `ai_analysis_jobs` | `ux_ai_analysis_jobs_center_id_attempt_id` | `ref` | *Đánh giá mức độ diễn tập (Smoke verification):* Trên tập dữ liệu nhỏ của đợt rehearsal, MySQL optimizer chọn index theo tenant. Việc tối ưu hóa chỉ mục chuyên sâu cho hàng đợi tải cao (`status`, `available_at`, `lease_until`) sẽ được thẩm định trong đợt load test staging. |

---

## 9. BẢNG TỔNG KẾT TRẠNG THÁI CÁC GATE (AUTHORITATIVE MILESTONE MAPPING)

Bảng đối chiếu chuẩn xác theo tài liệu gốc `PROJECT_TRACKING.md`:

| Cổng kiểm soát (Gate) | Nội dung phụ trách thực tế | Trạng thái kỹ thuật | Quyết định đóng băng |
| :--- | :--- | :--- | :--- |
| **Gate 1** | Tài liệu, Kiến trúc hệ thống, ADRs và Đặc tả kỹ thuật | **APPROVED** | **FROZEN** |
| **Gate 2** | Nền tảng Quản trị Platform Admin & Cốt lõi Đa Tenant | **APPROVED** | **FROZEN** |
| **Gate 3** | Math Toolbar, Đánh giá ngữ nghĩa, Số học hữu tỉ & Scientific Calculator | **APPROVED** | **FROZEN** |
| **Gate 4** | Vector Scratchpad & Lưu trữ bền vững IndexedDB cục bộ | **APPROVED** | **FROZEN** |
| **Gate 5** | Đính kèm Multipart, Gemini Đa phương thức, Resilient Worker & Vòng đời Lưu trữ | **APPROVED** | **FROZEN** |
| **Gate 6** | Thẩm định Phát hành, Diễn tập Môi trường Sạch & Khắc phục Toàn diện | **APPROVED** | **FROZEN** |

---

## 10. KẾT LUẬN CHUNG & TRẠNG THÁI BÀN GIAO

1. **Đạt chuẩn Nghiệm thu Kỹ thuật (Technical Release Candidate):**
   Tất cả các tiêu chí kỹ thuật cốt lõi của Gate 6 đã hoàn thành xuất sắc và có bằng chứng diễn tập cụ thể:
   - 0 lỗi biên dịch, 0 cảnh báo linter.
   - 3.259 bài kiểm thử backend (3.213 unit + 46 live MySQL) vượt qua 100%.
   - 46 bài kiểm thử frontend (`npm test` với Node.js test runner) vượt qua 100%.
   - 0 drift mô hình cơ sở dữ liệu EF Core.
   - Luồng E2E đa phương thức Track 2D (tải ảnh, submit, DB, filesystem, AI analysis, polling) chạy thành công hoàn chỉnh.
   - Các kịch bản lỗi lưu trữ, thử lại 3 lần, chuyển trạng thái `AnalysisFailed`, chống replay nonce/token, và độ bền Data Protection qua reboot đều vượt qua 100%.

2. **Phạm vi Chờ Nghiệm thu Sản phẩm (Product/UX Acceptance):**
   Theo đúng quy trình bàn giao của dự án, mốc R08 hiện đang ở trạng thái `TECHNICALLY VERIFIED / UX ACCEPTANCE PENDING` (chờ đánh giá nghiệm thu giao diện Figma và phê duyệt của các bên liên quan).

3. **Phán quyết Phát hành:**
   **GATE 6 CHÍNH THỨC ĐÓNG BĂNG KỸ THUẬT (FROZEN).**
   **NỀN TẢNG ĐẠT TRẠNG THÁI: TECHNICAL RELEASE CANDIDATE (SẴN SÀNG CHO BƯỚC NGHIỆM THU UX VÀ MỞ RỘNG TÍNH NĂNG TIẾP THEO).**
