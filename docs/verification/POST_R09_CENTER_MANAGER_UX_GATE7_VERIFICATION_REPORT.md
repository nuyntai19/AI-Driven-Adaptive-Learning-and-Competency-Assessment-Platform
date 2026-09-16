# BÁO CÁO NGHIỆM THU KỸ THUẬT GATE 7 — LEARNING SUPERVISION & DYNAMIC RBAC RESKIN

> **Dự án:** AI-Driven Adaptive Learning and Competency Assessment Platform (EduTwin)
> **Kế hoạch chuẩn:** [CENTER-MANAGER-LOVABLE-REDESIGN-PLAN.md](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/docs/plans/CENTER-MANAGER-LOVABLE-REDESIGN-PLAN.md) — Checkpoint 7
> **Branch thực thi:** `codex/post-r09-center-manager-ux-redesign`
> **Trạng thái chính thức:**
> ```text
> GATE 7 — CODE & LOCAL AUTOMATED VERIFICATION PASS
> BACKEND TESTS: 3420/3420 PASS (100% NON-MYSQL BLL & API REGRESSION) + 57/57 TARGETED PASS
> FRONTEND TESTS: 198/198 PASS (100% NODE TEST RUNNER — 16 GATE 7 TESTS)
> ESLINT & TS COMPILATION: 0 ERRORS, 0 WARNINGS
> VITE PRODUCTION BUILD: PASS (Entry chunk 396.60 KB <= 434.5 KB Budget)
> GIT DIFF INTEGRITY: PASS (0 WHITESPACE OR CONFLICT ERRORS)
> CHROME E2E / RUNTIME: STRICTLY DEFERRED TO GATE 8 (EXPLICIT CRITERIA)
> FORMAL GATE 7 CLOSEOUT: READY FOR CODEX REVIEW & GATE 8 CONVERGENCE
> ```
> **Thời điểm nghiệm thu:** 2026-09-16

---

## 1. TỔNG QUAN VÀ TRẠNG THÁI NGHIỆM THU

Cột mốc Gate 7 (Learning Supervision & Dynamic RBAC Reskin) hoàn tất việc nâng cấp toàn diện giao diện Dark Enterprise SaaS cho khối Giám sát Học tập (Review Queue, Digital Twin Học sinh) và Hệ thống Phân quyền Động (Dynamic RBAC), đồng thời thắt chặt các bất biến bảo mật then chốt ở cả hai tầng Backend (BLL) và Frontend:

1. **Khôi phục Triệt để Actor Isolation (Tách biệt Giao diện Hoàn toàn):**
   - **`ReviewQueuePage`:**
     - `CenterManager`: Sử dụng giao diện hiện đại Master/Detail (`CenterManagerReviewQueueView`), tích hợp bộ chọn lớp phân trang và tìm kiếm server-side, đối chiếu 3 nguồn dữ liệu, và ngăn kéo xem bản vẽ nháp vector.
     - `Teacher`: Giữ nguyên 100% giao diện legacy (`TeacherReviewQueueLegacyView`) từ commit `51d1d19` trước Gate 7 (bảng danh sách đơn giản, không áp theme CenterManager, không thay đổi markup hay trải nghiệm của giáo viên).
     - Điều hướng phân giải chế độ xem chuẩn hóa qua helper hàm `resolveReviewQueueViewMode`.
   - **`TeacherStudentTwinPage`:**
     - `CenterManager`: Sử dụng giao diện hiện đại (`CenterManagerStudentTwinView`) với nhãn KPI giới hạn đơn môn *"Độ thuần thục trung bình trong môn đang chọn"*, bọc trong `<CenterManagerThemeScope>`.
     - `Teacher`: Giữ nguyên 100% giao diện legacy (`TeacherStudentTwinLegacyView`) từ commit `51d1d19` với nhãn KPI *"Độ thuần thục tổng thể"*, điều hướng về Dashboard lớp học, và không bị bọc theme CenterManager.
     - Điều hướng phân giải chế độ xem chuẩn hóa qua helper hàm `resolveStudentTwinViewMode`.

2. **Cơ chế OCC Fail-Closed, Validation uint32 & Xử lý Refetch Failure:**
   - **Loại bỏ hoàn toàn fallback giả `?? 0`:** Giá trị token `analysisOverrideVersion` được kiểm tra nghiêm ngặt bằng validator canonical `isValidOccVersion` (`typeof v === "number" && Number.isSafeInteger(v) && v >= 0 && v <= 4294967295`). Các giá trị âm, float, overflow (> 4294967295) hoặc non-numeric đều bị từ chối fail-closed.
   - **Giao diện hiển thị chuẩn xác:** Sử dụng `formatOccVersionLabel`, hiển thị `Phiên bản OCC: #<token>` khi hợp lệ và `Phiên bản OCC: Không khả dụng` khi token thiếu hoặc không hợp lệ. Nút mở Override trên trang chi tiết bị khóa (`disabled`) khi token không phải uint hợp lệ.
   - **Xử lý xung đột HTTP 409 & Refetch Failure An toàn:** Khi gặp lỗi 409, modal hiển thị `ConcurrencyBanner` kèm `traceId` và nút refetch. Hàm refetch được bọc an toàn qua `executeOccRefetchWrapper` (`throwOnError: true`). Nếu refetch thất bại (mạng lỗi, server lỗi hoặc không có dữ liệu), trạng thái `isConflict` được **giữ nguyên**, submit form tiếp tục bị **khóa chặt**. Chỉ khi refetch thành công và token mới được xác nhận hợp lệ thì mới xóa cờ xung đột và mở khóa form.

3. **Tìm kiếm Lớp học Chuẩn Server-Side (`Search` Param):**
   - Bổ sung trường canonical `Search` (giới hạn `[MaxLength(100)]`) vào `ClassListQuery.cs`.
   - BLL `ListClassesUseCase.cs` lọc trực tiếp trên cơ sở dữ liệu (`c.ClassName.Contains(search)`).
   - Frontend `ClassListParams` và `organizationApi.listClasses` truyền tham số `search`.
   - `ReviewQueuePage` đưa `classSearchTerm` vào queryKey `["reviewQueueClasses", classSearchTerm, classPage]`, đồng thời duy trì bộ đệm `classCache` dạng Map để lưu trữ danh sách lớp qua các trang và kết quả tìm kiếm.

4. **Làm sạch Hợp đồng DTO (DTO Cleanup):**
   - Xóa bỏ hoàn toàn trường thừa `mode?: string` khỏi `EvidenceDecisionDto` trong `types/reviews.ts`.
   - Loại bỏ fallback `evidence.mode` và fallback dữ liệu giả `"Rule-based"`. Khi thiếu contract, UI hiển thị trạng thái an toàn *"Chưa xác định"*.

5. **Ngăn kéo Tệp Nháp Vẽ Vector (Scratchpad Drawer):**
   - Guarded nghiêm ngặt bởi quyền `learning.attempts.read_scoped`.
   - Đọc stream nhị phân (blob) kèm Bearer token qua `GET /api/v1/learning/attempts/{id}/attachment`.
   - Quản lý vòng đời `URL.createObjectURL` và `URL.revokeObjectURL` chặt chẽ, chống rò rỉ bộ nhớ.
   - Phân biệt rõ ràng các mã trạng thái HTTP: 404 (Không có tệp đính kèm hoặc ngoài phạm vi), 503 (Lưu trữ tạm thời không khả dụng), 403 (Từ chối truy cập).

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
| **Actor Isolation: Review Queue** | **ĐẠT** | CenterManager: Master/Detail + Drawer; Teacher: 100% legacy table từ commit 51d1d19 |
| **Actor Isolation: Student Twin** | **ĐẠT** | CenterManager: KPI "Độ thuần thục trung bình trong môn đang chọn"; Teacher: 100% legacy view |
| **OCC Fail-Closed & 409 Refetch** | **ĐẠT** | uint32 validation; token thiếu/sai format khóa submit; refetch thất bại giữ nguyên conflict |
| **Server-Side Class Search** | **ĐẠT** | Bổ sung `Search` vào `ClassListQuery`, BLL filter, API query, và cache tích lũy Map |
| **DTO Cleanup** | **ĐẠT** | Xóa `mode?: string`, xóa fallback `evidence.mode` và dữ liệu giả "Rule-based" |
| **Dual Scope Review Queue (BLL)** | **ĐẠT** | Teacher giới hạn theo lớp phụ trách; CenterManager xem toàn trung tâm; Actor khác 404 |
| **Backend System Role Protection** | **ĐẠT** | Chặn sửa/archive và chặn gán quyền cho System Role (`InvalidStateTransition`) |
| **Backend Self-Role Assignment Protection** | **ĐẠT** | Chặn Actor tự sửa vai trò của bản thân (`actorId == userId` -> `InvalidStateTransition`) |
| **Scratchpad Attachment Drawer** | **ĐẠT** | Gated `learning.attempts.read_scoped`, blob URL lifecycle, xử lý phân biệt 404/503/403 |
| **Dynamic RBAC Reskin & Guardrail 2** | **ĐẠT** | Bảo toàn 100% logic cũ, khóa quyền ngoài tập giao delegable, giữ nguyên quyền cũ khi lưu |
| **Backend Tests (`dotnet test`)** | **PASS** | **3420/3420 non-MySQL PASS 100%** + **57/57 targeted tests PASS** |
| **Frontend Tests (`npm test`)** | **PASS** | **198/198 tests PASS 100%** (16 tests Gate 7 hoàn chỉnh, 182 tests Gate 1-6) |
| **ESLint (`npm run lint`)** | **PASS** | **0 errors, 0 warnings** |
| **TypeScript Compilation (`tsc -b`)** | **PASS** | **0 lỗi biên dịch** |
| **Vite Production Build (`npm run build`)** | **PASS** | **396.60 KB** bundle chính (gzip 125.44 KB) <= budget 434.5 KB; KaTeX cô lập 262.60 KB |
| **Git Diff Integrity (`git diff --check`)** | **PASS** | **0 lỗi thụt lề, trailing whitespace hay conflict markers** |
| **Chrome E2E Runtime Verification** | **DEFERRED** | **Hoãn nghiêm ngặt sang Gate 8** (Không tuyên bố pass Chrome E2E ở Gate 7) |

---

## 3. CHI TIẾT CÁC THAY ĐỔI THEO FILE

### 3.1. Backend C# (BLL & Contracts & Unit Tests)
- `src/EduTwin.Contracts/Organization/ClassListQuery.cs`:
  - Bổ sung trường `public string? Search { get; set; }` kèm validation `[MaxLength(100)]`.
- `src/EduTwin.BLL/Organization/ListClassesUseCase.cs`:
  - Lọc server-side theo từ khóa tìm kiếm: `query.Search` áp dụng trên `c.ClassName.Contains(search)`.
- `tests/EduTwin.BLL.Tests/Organization/ListClassesUseCaseTests.cs`:
  - Bổ sung bài kiểm thử xác nhận tìm kiếm lớp theo từ khóa server-side hoạt động chính xác.
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
- `web/edutwin-web/src/utils/reviewQueueHelpers.ts`:
  - Tách các hàm helper canonical dùng chung giữa components và tests: `isValidOccVersion` (uint32 range), `formatOccVersionLabel`, `resolveReviewQueueViewMode`, `resolveStudentTwinViewMode`, và `executeOccRefetchWrapper`.
- `web/edutwin-web/src/types/organization.ts` & `src/api/organizationApi.ts`:
  - Bổ sung tham số `search?: string;` vào `ClassListParams` và truyền trong query params của `organizationApi.listClasses`.
- `web/edutwin-web/src/types/reviews.ts`:
  - Xóa bỏ trường legacy `mode?: string;` khỏi `EvidenceDecisionDto`.
  - Hợp đồng DTO đầy đủ các trường chuẩn: `decisionMode`, `reasoningWeight`, `reasonCodes`, `policyVersion`, và `analysisOverrideVersion`.
- `web/edutwin-web/src/components/TeacherOverrideModal.tsx`:
  - Sử dụng `isValidOccVersion` để kiểm tra token uint32 an toàn.
  - Sử dụng `formatOccVersionLabel` để hiển thị nhãn phiên bản OCC thay cho fallback giả.
  - Xử lý `handleRefetch` với khả năng giữ nguyên `isConflict` khi refetch thất bại. Khóa nút submit nếu token thiếu hoặc không hợp lệ.
- `web/edutwin-web/src/components/ScratchpadAttachmentDrawer.tsx`:
  - Component ngăn kéo xem bản vẽ nháp vector, kiểm tra quyền `learning.attempts.read_scoped`, quản lý blob URL với `URL.revokeObjectURL`, zoom controls (+/-/reset), và phân biệt các lỗi HTTP 404, 503, 403.
- `web/edutwin-web/src/pages/ReviewQueuePage.tsx`:
  - Tách bạch 2 view độc lập hoàn toàn với `resolveReviewQueueViewMode`:
    - `CenterManagerReviewQueueView`: Master/Detail với cột trái danh sách bài duyệt và cột phải đối chiếu 3 nguồn dữ liệu, Scratchpad drawer, queryKey `["reviewQueueClasses", classSearchTerm, classPage]`, bộ đệm Map `classCache`, hiển thị nhãn phiên bản chuẩn và khóa nút override khi token không hợp lệ. Bọc refetch qua `executeOccRefetchWrapper`.
    - `TeacherReviewQueueLegacyView`: Giữ nguyên 100% giao diện legacy dạng bảng trước Gate 7.
- `web/edutwin-web/src/pages/TeacherStudentTwinPage.tsx`:
  - Tách bạch 2 view độc lập hoàn toàn với `resolveStudentTwinViewMode`:
    - `CenterManagerStudentTwinView`: Giao diện hiện đại, nhãn KPI *"Độ thuần thục trung bình trong môn đang chọn"*, bọc trong `<CenterManagerThemeScope>`.
    - `TeacherStudentTwinLegacyView`: Giữ nguyên 100% giao diện legacy dạng bảng trước Gate 7 với nhãn KPI *"Độ thuần thục tổng thể"* và liên kết Dashboard lớp học.
- `web/edutwin-web/src/pages/AuthorizationManagementPage.tsx`:
  - Bọc trong `<CenterManagerThemeScope data-actor="center-manager">`.
  - Triển khai Guardrail 2 ma trận quyền: Chỉ cho phép toggle trên tập giao $\text{Active} \cap \text{Compatible} \cap \text{Delegable} \cap \text{Actor's Effective Permissions}$. Các quyền đã được gán trước đó nằm ngoài tập giao được hiển thị read-only (locked) và bảo lưu nguyên vẹn trong payload lưu.
- `web/edutwin-web/tests/learningSupervisionGate7.test.ts`:
  - 16 bài kiểm thử tự động toàn diện kiểm thử trực tiếp mã nguồn sản phẩm:
    1. CenterManager nhận toàn bộ bài duyệt của trung tâm qua phạm vi centerId.
    2. Teacher chỉ nhận bài duyệt thuộc các lớp do chính mình phụ trách.
    3. Unauthorized actor (Student/Parent) bị từ chối truy cập review queue (fail-closed).
    4. Giao diện đối chiếu 3 nguồn dữ liệu đầy đủ.
    5. Xóa bỏ hoàn toàn nút thử nghiệm prototype 409 khỏi Review Queue.
    6. Ngăn kéo tệp nháp vector bị chặn khi thiếu quyền `learning.attempts.read_scoped`.
    7. Quản lý vòng đời URL.revokeObjectURL chống rò rỉ bộ nhớ.
    8. Bộ chọn lớp học tích lũy bộ đệm Map giữ nguyên lớp đang chọn qua các trang.
    9. Bộ chọn lớp hỗ trợ tìm kiếm server-side với query param `search`.
    10. Bắt buộc nhập lý do ghi đè tối thiểu 3 ký tự trước khi gửi can thiệp.
    11. Hợp đồng DTO không chứa trường thừa `mode` và không sử dụng fallback giả mạo.
    12. Bó hẹp năng lực và nhãn KPI trên môn học đang chọn.
    13. Actor isolation: `resolveReviewQueueViewMode` phân tách CenterManager modern view và Teacher legacy view.
    14. Actor isolation: `resolveStudentTwinViewMode` phân tách CenterManager single-subject KPI view và Teacher legacy view.
    15. OCC Fail-Closed: `isValidOccVersion` kiểm tra uint32 chuẩn xác, từ chối số âm, số thực, overflow, và non-numeric; `formatOccVersionLabel` hiển thị "Không khả dụng" khi token sai/thiếu.
    16. OCC 409 Workflow & Production Helper: Kiểm thử trực tiếp `reconcileAttemptOccVersion` - bảo đảm đối soát đúng attemptId, tuyệt đối không fallback sang res.data[0] khi attempt biến mất (đóng modal, đặt selectedReview null, thông báo người dùng), fail-closed khi hàng đợi rỗng hoặc thiếu attempt, áp dụng token mới nguyên tử trước khi mở khóa (`setOverrideVersion` trước `setIsConflict(false)`), và giữ nguyên conflict khóa submit khi refetch thất bại.

---

## 4. KẾT QUẢ KIỂM THỬ VÀ HIỆU NĂNG

### 4.1. Backend Unit & Integration Tests
```text
Passed!  - Failed: 0, Passed: 3420, Skipped: 0, Total: 3420, Duration: 36 s - EduTwin.BLL.Tests.dll (net10.0)
Targeted Tests:
Passed!  - Failed: 0, Passed: 57, Skipped: 0, Total: 57, Duration: 4 s - EduTwin.BLL.Tests.dll (net10.0)
Platform Privilege Escalation Tests:
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 3 s - EduTwin.BLL.Tests.dll (net10.0)
```
- **100% passed**, 0 failures, 0 regressions.

### 4.2. Frontend Tests (`npm test`)
```text
# tests 198
# suites 0
# pass 198
# fail 0
# cancelled 0
# skipped 0
# todo 0
# duration_ms 2593.3357
```
- Tăng từ 182 tests (Gate 6) lên **198 tests** (Gate 7: 16 bài kiểm thử mở rộng). 100% passed.

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
dist/assets/index-Cm4kyhIL.js                         396.60 kB │ gzip: 125.44 kB
dist/assets/ReviewQueuePage-D-6zH6GL.js                42.89 kB │ gzip:  10.00 kB
dist/assets/TeacherStudentTwinPage-DHh-Y_7e.js         21.65 kB │ gzip:   3.12 kB
dist/assets/AuthorizationManagementPage-0m2p7Jnl.js    58.51 kB │ gzip:  13.50 kB
✓ built in 9.63s
```
- Entry bundle `396.60 kB` nằm an toàn dưới ngưỡng ngân sách quy định (`434.50 kB`).
- Không có route chunk nào vượt quá ngân sách cho phép.

---

## 5. RÀNG BUỘC KỸ THUẬT VỀ CHROME E2E

> [!IMPORTANT]
> **Ràng buộc Gate 7:** Theo chỉ đạo của Codex R2, Gate 7 **KHÔNG** tuyên bố pass Chrome E2E. Toàn bộ hoạt động kiểm thử tích hợp trên trình duyệt Chrome (bao gồm ghi hình tương tác và kiểm tra đối chiếu Actor Isolation giữa CenterManager, Teacher và Student) được chuyển giao chính thức sang **Gate 8 (UX Acceptance & Automated Regression Gate)**.

---

## 6. KẾT LUẬN

Gate 7 đã hoàn thành trọn vẹn toàn bộ các điểm phản biện vòng cuối của Codex:
- Đã sửa hàm OCC refetch qua `executeOccRefetchWrapper` và `reconcileAttemptOccVersion`: đối soát đúng attemptId, tuyệt đối không fallback sang `res.data[0]` khi lượt làm bị xử lý bởi phiên làm việc khác; tự động đóng modal, hủy lựa chọn và hiển thị thông báo người dùng rõ ràng.
- Nâng cấp contract `TeacherOverrideModalProps.onRefetch?: () => Promise<number>`, bảo đảm modal nhận fresh OCC token và thực hiện `setOverrideVersion(freshVersion)` nguyên tử trước khi gọi `setIsConflict(false)`, loại bỏ hoàn toàn khả năng mở khóa nhầm với token cũ trong chu kỳ render của React.
- Đã chuẩn hóa `handleLegacyRefetch` của Teacher view fail-closed khi hàng đợi rỗng hoặc không tìm thấy lượt làm đang mở.
- Đã xóa triệt để fallback `?? 0` trên giao diện, áp dụng validator canonical uint32 (`Number.isSafeInteger(v) && v >= 0 && v <= 4294967295`), khóa nút Override ngay từ trang chi tiết khi token không hợp lệ.
- Nâng cao chất lượng test bằng cách kiểm thử trực tiếp các helper hàm sản phẩm (`reviewQueueHelpers.ts`), bao phủ test token âm/float/overflow, attempt biến mất, hàng đợi rỗng, và thứ tự áp dụng token mở khóa.
- Cập nhật chuẩn xác queryKey `["reviewQueueClasses", classSearchTerm, classPage]` và nhãn KPI *"Độ thuần thục trung bình trong môn đang chọn"* trên báo cáo nghiệm thu.
- Làm sạch hoàn toàn trailing whitespace.

Mã nguồn sẵn sàng cho vòng nghiệm thu của Codex mà **không push** lên remote repository.
