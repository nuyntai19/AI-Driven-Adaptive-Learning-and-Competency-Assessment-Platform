# BÁO CÁO NGHIỆM THU KỸ THUẬT GATE 7 — LEARNING SUPERVISION & DYNAMIC RBAC RESKIN

> **Dự án:** AI-Driven Adaptive Learning and Competency Assessment Platform (EduTwin)  
> **Kế hoạch chuẩn:** [CENTER-MANAGER-LOVABLE-REDESIGN-PLAN.md](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/docs/plans/CENTER-MANAGER-LOVABLE-REDESIGN-PLAN.md) — Checkpoint 7  
> **Branch thực thi:** `codex/post-r09-center-manager-ux-redesign`  
> **Trạng thái chính thức:**
> ```text
> GATE 7 — CODE & LOCAL AUTOMATED VERIFICATION PASS
> BACKEND TESTS: 3420/3420 PASS (100% NON-MYSQL BLL & API REGRESSION)
> FRONTEND TESTS: 193/193 PASS (100% NODE TEST RUNNER)
> ESLINT & TS COMPILATION: 0 ERRORS, 0 WARNINGS
> VITE PRODUCTION BUILD: PASS (Entry chunk 396.60 KB <= 434.5 KB Budget)
> GIT DIFF INTEGRITY: PASS (0 WHITESPACE OR CONFLICT ERRORS)
> CHROME E2E / RUNTIME: STRICTLY DEFERRED TO GATE 8 (EXPLICIT CRITERIA)
> FORMAL GATE 7 CLOSEOUT: PENDING REVIEW & GATE 8 CONVERGENCE
> ```
> **Thời điểm nghiệm thu:** 2026-09-16  

---

## 1. TỔNG QUAN VÀ TRẠNG THÁI NGHIỆM THU

Cột mốc Gate 7 (Learning Supervision & Dynamic RBAC Reskin) hoàn tất việc nâng cấp toàn diện giao diện Dark Enterprise SaaS cho khối Giám sát Học tập (Review Queue, Digital Twin Học sinh) và Hệ thống Phân quyền Động (Dynamic RBAC), đồng thời thắt chặt các bất biến bảo mật then chốt ở cả hai tầng Backend (BLL) và Frontend:

1. **Review Queue Phân quyền Dual Scope (`ListTeacherReviewQueueUseCase`):**
   - **Teacher:** Bị giới hạn nghiêm ngặt trong phạm vi các lớp do chính mình phụ trách (`target.Assignment.Class.TeacherId == actorId`).
   - **CenterManager:** Được phép truy vấn toàn bộ các bài cần duyệt trên mọi lớp học thuộc trung tâm hiện tại (`target.Assignment.Class.CenterId == actorCenterId`).
   - **Actor khác:** Fail-closed (`NotFound`).

2. **Master/Detail Review Queue & Đối chiếu 3 Nguồn (Reconciliation):**
   - Cột trái: Danh sách bài tập chờ duyệt kèm bộ chọn lớp học phân trang server-side (`pageSize: 20`) và cache tích lũy Map, xóa bỏ giới hạn cứng `pageSize: 50`.
   - Cột phải: Đối chiếu 3 nguồn thông tin chuẩn xác:
     - Bài làm gốc của học sinh + Nút mở ngăn kéo nháp vẽ vector (Scratchpad Drawer).
     - Quan sát suy luận từ AI (AI Observation & Confidence).
     - Kết quả chấm quy tắc xác định & Form can thiệp chuyên môn (Deterministic Fallback & Teacher Override).
   - **Loại bỏ hoàn toàn nút prototype "Mô phỏng 409"** khỏi giao diện sản phẩm.

3. **Cơ chế OCC Đồng bộ & Phiên bản Chuẩn:**
   - Phiên bản đồng thời bắt đầu chuẩn xác từ `review.evidence?.analysisOverrideVersion ?? 0` (thay vì giá trị 0 tùy tiện).
   - Tự động bắt mã lỗi `409 Concurrency Conflict`, hiển thị `ProblemDetails` có `traceId`, và cung cấp nút refetch dữ liệu thật.
   - Bắt buộc nhập lý do ghi đè (tối thiểu 3 ký tự) trước khi gửi yêu cầu.

4. **Ngăn kéo Tệp Nháp Vẽ Vector (Scratchpad Drawer):**
   - Guarded nghiêm ngặt bởi quyền `learning.attempts.read_scoped`.
   - Đọc stream nhị phân (blob) kèm Bearer token qua `GET /api/v1/learning/attempts/{id}/attachment`.
   - Quản lý vòng đời `URL.createObjectURL` và `URL.revokeObjectURL` chặt chẽ, chống rò rỉ bộ nhớ.
   - Phân biệt rõ ràng các mã trạng thái HTTP: 404 (Không có tệp đính kèm hoặc ngoài phạm vi), 503 (Lưu trữ tạm thời không khả dụng), 403 (Từ chối truy cập).

5. **Chỉ số Digital Twin Bó hẹp Theo Môn Học (Single-Subject KPI):**
   - Chuẩn hóa nhãn KPI thành *"Độ thuần thục trung bình trong môn đang chọn"* trên `TeacherStudentTwinPage`, loại bỏ từ ngữ gây hiểu lầm *"tổng thể"*.
   - Chỉ số năng lực trung bình và số lượng bằng chứng được tính toán nghiêm ngặt trên tập chủ đề thuộc `subjectId` đang chọn.

6. **Hệ thống Phân quyền Động (Dynamic RBAC Reskin & Guardrail 2):**
   - Giữ nguyên 100% logic nghiệp vụ 2048 dòng của `AuthorizationManagementPage.tsx`, khoác áo Enterprise Dark Theme thông qua `<CenterManagerThemeScope data-actor="center-manager">`.
   - **Guardrail 2 Ma trận quyền:** Cho phép bật/tắt (toggle) DUY NHẤT trên tập giao:
     $$\text{Active} \cap \text{Compatible} \cap \text{Delegable} \cap \text{Actor's Effective Permissions}$$
   - Bất kỳ quyền nào đã được gán trước đó nằm ngoài tập giao này **BẮT BUỘC** hiển thị ở trạng thái chỉ đọc (locked badge) và **BẮT BUỘC ĐƯỢC BẢO LƯU NGUYÊN VẸN** trong payload gửi lên backend khi lưu (không bị xóa ngầm).

7. **Bất biến Bảo vệ Vai trò Hệ thống và Tự Gán Quyền ở Backend:**
   - `UpdateAuthorizationRoleUseCase` & `ReplaceRolePermissionsUseCase`: Chặn sửa tên hoặc sửa quyền của System Role (`role.IsSystemRole == true` trả về `ErrorCodes.InvalidStateTransition`).
   - `ReplaceUserRolesUseCase`: Chặn việc người dùng tự thay đổi vai trò của chính mình (`actorId == userId` trả về `ErrorCodes.InvalidStateTransition`).

---

## 2. BẢNG TỔNG KẾT TRẠNG THÁI KỸ THUẬT

| Hạng mục | Trạng thái | Ghi chú kỹ thuật |
|---|:---:|---|
| **Dual Scope Review Queue (BLL)** | **ĐẠT** | Teacher giới hạn theo lớp phụ trách; CenterManager xem toàn trung tâm; Actor khác 404 |
| **Backend System Role Protection** | **ĐẠT** | Chặn sửa/archive và chặn gán quyền cho System Role (`InvalidStateTransition`) |
| **Backend Self-Role Assignment Protection** | **ĐẠT** | Chặn Actor tự sửa vai trò của bản thân (`actorId == userId` -> `InvalidStateTransition`) |
| **Master/Detail Review Queue UI** | **ĐẠT** | Layout đối chiếu 3 nguồn, Dark theme, xóa triệt để nút test 409 |
| **Server-Side Class Selector & Cache** | **ĐẠT** | Phân trang `pageSize: 20`, tích lũy Map cache bảo toàn lựa chọn, tìm kiếm client/server |
| **OCC Override Version Sync** | **ĐẠT** | Khởi tạo từ `evidence.analysisOverrideVersion`, refetch khi 409, bắt buộc lý do ghi đè |
| **Scratchpad Attachment Drawer** | **ĐẠT** | Gated `learning.attempts.read_scoped`, blob URL lifecycle, xử lý phân biệt 404/503/403 |
| **Single-Subject Digital Twin KPI** | **ĐẠT** | Giới hạn theo môn đang chọn, nhãn chuẩn xác, bọc trong CenterManagerThemeScope |
| **Dynamic RBAC Reskin & Guardrail 2** | **ĐẠT** | Bảo toàn 100% logic cũ, khóa quyền ngoài tập giao delegable, giữ nguyên quyền cũ khi lưu |
| **Backend Tests (`dotnet test`)** | **PASS** | **3420 / 3420 tests non-MySQL PASS 100%** (36s duration) |
| **Frontend Tests (`npm test`)** | **PASS** | **193 / 193 tests PASS 100%** (11 tests Gate 7, 182 tests Gate 1-6) |
| **ESLint (`npm run lint`)** | **PASS** | **0 errors, 0 warnings** |
| **TypeScript Compilation (`npx tsc -b`)** | **PASS** | **0 lỗi biên dịch** |
| **Vite Production Build (`npm run build`)** | **PASS** | **396.60 KB** bundle chính (gzip 125.45 KB) <= budget 434.5 KB; KaTeX cô lập 262.60 KB |
| **Git Diff Integrity (`git diff --check`)** | **PASS** | **0 lỗi thụt lề, trailing whitespace hay conflict markers** |
| **Chrome E2E Runtime Verification** | **DEFERRED** | **Hoãn nghiêm ngặt sang Gate 8** (Không tuyên bố pass Chrome E2E ở Gate 7) |

---

## 3. CHI TIẾT CÁC THAY ĐỔI THEO FILE

### 3.1. Backend C# (BLL & Unit Tests)
- `src/EduTwin.BLL/AssessmentAndReasoning/ReviewQueue/ListTeacherReviewQueueUseCase.cs`:
  - Hỗ trợ dual-role querying: `CenterManager` lọc theo trung tâm (`target.Assignment.Class.CenterId == actorCenterId`); `Teacher` lọc theo lớp phụ trách (`target.Assignment.Class.TeacherId == actorId`).
- `src/EduTwin.BLL/IdentityAndTenancy/UpdateAuthorizationRoleUseCase.cs`:
  - Bổ sung kiểm tra bất biến `if (role.IsSystemRole) return UpdateAuthorizationRoleResult.Failure(ErrorCodes.InvalidStateTransition);`.
- `src/EduTwin.BLL/IdentityAndTenancy/ReplaceRolePermissionsUseCase.cs`:
  - Bổ sung kiểm tra bất biến `if (role.IsSystemRole) return ReplaceRolePermissionsResult.Failure(ErrorCodes.InvalidStateTransition);`.
- `src/EduTwin.BLL/IdentityAndTenancy/ReplaceUserRolesUseCase.cs`:
  - Bổ sung kiểm tra bất biến `if (currentActorId.HasValue && currentActorId.Value == targetUserId) return ReplaceUserRolesResult.Failure(ErrorCodes.InvalidStateTransition);`.
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/ReviewQueue/ListTeacherReviewQueueUseCaseTests.cs`:
  - Thêm test case xác nhận `CenterManager` nhận đủ bài toàn trung tâm và `Student` bị từ chối fail-closed.
- `tests/EduTwin.BLL.Tests/IdentityAndTenancy/AuthorizationRoleUseCaseTests.cs`:
  - Bổ sung 2 bài test xác nhận `UpdateAuthorizationRoleUseCase` và `ReplaceRolePermissionsUseCase` từ chối sửa System Role.
- `tests/EduTwin.BLL.Tests/IdentityAndTenancy/UserAuthorizationUseCaseTests.cs`:
  - Bổ sung bài test xác nhận `ReplaceUserRolesUseCase` từ chối tự thay đổi vai trò bản thân. Cập nhật `ReplaceCenterManagerRoles_PreservesLastAdministrator` sử dụng ID người dùng mục tiêu riêng biệt.
- `tests/EduTwin.BLL.Tests/Platform/PlatformPrivilegeEscalationTests.cs`:
  - Seed target user tách biệt với actor user để kiểm thử leo thang đặc quyền mà không vướng guardrail tự sửa vai trò.

### 3.2. Frontend TypeScript / React
- `web/edutwin-web/src/types/reviews.ts`:
  - Đồng bộ hợp đồng DTO đầy đủ 10 trường cho `EvidenceDecisionDto`, bao gồm `decisionMode`, `reasoningWeight`, `reasonCodes`, `policyVersion`, và `analysisOverrideVersion`.
- `web/edutwin-web/src/components/ScratchpadAttachmentDrawer.tsx`:
  - Component ngăn kéo xem bản vẽ nháp vector, kiểm tra quyền `learning.attempts.read_scoped`, quản lý blob URL với `URL.revokeObjectURL`, zoom controls (+/-/reset), và phân biệt các lỗi HTTP 404, 503, 403.
- `web/edutwin-web/src/components/TeacherOverrideModal.tsx`:
  - Khởi tạo `overrideVersion` từ `review.evidence?.analysisOverrideVersion ?? 0`, xử lý lỗi 409 Concurrency Conflict và cung cấp nút refetch dữ liệu.
- `web/edutwin-web/src/pages/ReviewQueuePage.tsx`:
  - Bố cục Master/Detail với cột trái danh sách bài duyệt và cột phải đối chiếu 3 nguồn dữ liệu.
  - Xóa bỏ nút prototype "Mô phỏng 409".
  - Bộ chọn lớp học phân trang server-side (`pageSize: 20`) với bộ đệm `classCache` dạng Map để đảm bảo lớp đang chọn không bị biến mất giữa các trang.
  - Bọc toàn bộ trang trong `<CenterManagerThemeScope data-actor="center-manager">`.
- `web/edutwin-web/src/pages/TeacherStudentTwinPage.tsx`:
  - Đổi nhãn KPI thành *"Độ thuần thục trung bình trong môn đang chọn"*, bó hẹp tính toán trong các topic của môn đang chọn.
  - Bọc trang trong `<CenterManagerThemeScope data-actor="center-manager">`.
- `web/edutwin-web/src/pages/AuthorizationManagementPage.tsx`:
  - Bọc trong `<CenterManagerThemeScope data-actor="center-manager">`.
  - Triển khai Guardrail 2 ma trận quyền: Chỉ cho phép toggle trên tập giao $\text{Active} \cap \text{Compatible} \cap \text{Delegable} \cap \text{Actor's Effective Permissions}$. Các quyền đã được gán trước đó nằm ngoài tập giao được hiển thị read-only (locked) và bảo lưu nguyên vẹn trong payload lưu.
- `web/edutwin-web/tests/learningSupervisionGate7.test.ts`:
  - 11 bài kiểm thử tự động xác nhận toàn bộ các ràng buộc năng lực, hợp đồng DTO, OCC token, phân trang bộ chọn lớp, ma trận quyền Guardrail 2 và bất biến bảo vệ vai trò.

---

## 4. KẾT QUẢ KIỂM THỬ VÀ HIỆU NĂNG

### 4.1. Backend Unit & Integration Tests
```text
Passed!  - Failed: 0, Passed: 3420, Skipped: 0, Total: 3420, Duration: 36 s - EduTwin.BLL.Tests.dll (net10.0)
```
- **100% passed**, 0 failures, 0 regressions.

### 4.2. Frontend Tests (`npm test`)
```text
# tests 193
# suites 0
# pass 193
# fail 0
# cancelled 0
# skipped 0
# todo 0
# duration_ms 2531.7638
```
- Tăng từ 182 tests (Gate 6) lên **193 tests** (Gate 7). 100% passed.

### 4.3. Code Quality (ESLint & TypeScript)
```text
$ npm run lint
> eslint .
(0 errors, 0 warnings)

$ npx tsc -b
(0 compilation errors across all projects)
```

### 4.4. Production Build & Bundle Budget
```text
dist/assets/index-BdmX35FX.js                         396.60 kB │ gzip: 125.45 kB
dist/assets/ReviewQueuePage-5qn-C8b9.js                33.03 kB │ gzip:   8.51 kB
dist/assets/AuthorizationManagementPage-CnwD_c8z.js    58.51 kB │ gzip:  13.50 kB
✓ built in 15.56s
```
- Entry bundle `396.60 kB` nằm an toàn dưới ngưỡng ngân sách quy định (`434.50 kB`).
- Không có route chunk nào vượt quá ngân sách cho phép.

---

## 5. RÀNG BUỘC KỸ THUẬT VỀ CHROME E2E

> [!IMPORTANT]
> **Ràng buộc Gate 7:** Theo chỉ đạo của Codex R2, Gate 7 **KHÔNG** tuyên bố pass Chrome E2E. Toàn bộ hoạt động kiểm thử tích hợp trên trình duyệt Chrome (bao gồm ghi hình tương tác và kiểm tra đối chiếu Actor Isolation giữa CenterManager, Teacher và Student) được chuyển giao chính thức sang **Gate 8 (UX Acceptance & Automated Regression Gate)**.

---

## 6. KẾT LUẬN

Gate 7 đã hoàn thành trọn vẹn toàn bộ các yêu cầu kỹ thuật, bảo vệ chặt chẽ các bất biến nghiệp vụ và bảo mật, vượt qua 100% các bài kiểm thử hồi quy backend và frontend. Trạng thái mã nguồn sẵn sàng cho việc nghiệm thu cục bộ và chuyển sang Gate 8.
