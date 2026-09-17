# KẾ HOẠCH NGHIỆM THU GATE 8 — UX ACCEPTANCE & AUTOMATED REGRESSION GATE

> **Dự án:** AI-Driven Adaptive Learning and Competency Assessment Platform (EduTwin)
> **Kế hoạch chuẩn:** [CENTER-MANAGER-LOVABLE-REDESIGN-PLAN.md](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/docs/plans/CENTER-MANAGER-LOVABLE-REDESIGN-PLAN.md) — Checkpoint 8
> **Branch thực thi:** `codex/post-r09-center-manager-ux-redesign`
> **Mục tiêu:** Nghiệm thu toàn diện trải nghiệm người dùng (UX Acceptance), xác nhận không có bất kỳ hồi quy kỹ thuật nào (Automated Regression), và kiểm chứng trực quan tương phản Sáng / Tối (Light / Dark Mode) cùng phân lập diễn viên (Actor Isolation).

---

## 1. TIÊU CHÍ & SỐ LIỆU HỒI QUY TỰ ĐỘNG (AUTOMATED REGRESSION CRITERIA)

### 1.1. Backend Test Suite (`EduTwin.BLL.Tests`)
- **Lệnh thực thi:** `dotnet test --filter "Category!=MySql" --no-build`
- **Số liệu baseline:**
  - **Tổng số tests:** 3.477 tests
  - **Passed:** 3.421 passed (100% logic nghiệp vụ BLL)
  - **Failed:** 0 failed
  - **Skipped:** 56 skipped
- **Lý giải 56 tests bị skipped:**
  - Toàn bộ 56 tests được đánh dấu `Category=MySql` (thuộc `PlatformMySqlIntegrationTests` và `RecommendationMySqlConcurrencyTests`).
  - Các test này là bài kiểm thử tích hợp thực tế với cơ sở dữ liệu MySQL trực tiếp (kiểm tra deadlock, pessimistic row lock, relational unique constraints, và migrations thật).
  - Khi chạy unit test cô lập cục bộ, cờ `--filter "Category!=MySql"` được cấu hình chuẩn để bỏ qua các test phụ thuộc database ngoài; 100% logic nghiệp vụ BLL (3.421 tests) đều vượt qua thành công với 0 lỗi.
  - *Ghi chú môi trường thẩm định:* Trong một số môi trường sandbox (như Codex subagent), việc khởi tạo tiến trình con có thể bị giới hạn quyền hệ thống (`spawn EPERM`); điều này thuộc về giới hạn sandbox của môi trường chạy lệnh, không phản ánh lỗi logic của test suite.

### 1.2. Frontend Test Suite (`edutwin-web`)
- **Lệnh thực thi:** `npm --prefix web/edutwin-web test`
- **Số liệu baseline:**
  - **Tổng số tests:** 210 tests (bao gồm 12 bài test Pre-Gate 8 trong `centerManagerUIRefinement.test.ts`).
  - **Passed:** 210 passed (100%)
  - **Failed:** 0 failed
  - **Skipped:** 0 skipped
  - *Ghi chú môi trường thẩm định:* Tương tự backend, runner vitest phụ thuộc vào quyền spawn process cục bộ của môi trường; việc biên dịch TypeScript và phân tích tĩnh linter độc lập là căn cứ xác minh kỹ thuật tin cậy.

### 1.3. Phân tích Tĩnh & Biên dịch Mã nguồn
- **Linter:** `npm --prefix web/edutwin-web run lint` $\implies$ **0 errors, 0 warnings**.
- **TypeScript Compile:** `npx tsc -b --force` (chạy tại thư mục `web/edutwin-web`) $\implies$ **0 errors**.
- **Vite Production Build:** `npm --prefix web/edutwin-web run build` $\implies$ **Pass**, Entry chunk $\le 398\text{ KB}$, không phát sinh chunk bất thường.
- **Git Whitespace & Diff:** `git diff --check` và `git show --check HEAD` $\implies$ **0 whitespace errors**.

---

## 2. KẾ HOẠCH SỬA CÁC BANNER LIGHT MODE (PRE-E2E REFINEMENT)

Khắc phục triệt để các màu chữ sáng cố định (`text-amber-200`, `text-rose-300`, v.v.) trong các banner thông báo trạng thái và lỗi:

1. **[AssignmentEditorPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/AssignmentEditorPage.tsx#L563):**
   - Banner chế độ chỉ xem (`isReadOnly`):
     - Đổi `text-amber-200` $\to$ `text-amber-900 dark:text-amber-200 font-medium`.
     - Đổi `text-amber-100` $\to$ `text-amber-950 dark:text-amber-100 font-bold`.
     - Đổi `text-amber-300` $\to$ `text-amber-800 dark:text-amber-300 font-semibold`.
   - Banner fail-closed guard & form error: Đổi `text-rose-200`, `text-rose-300` $\to$ `text-rose-900 dark:text-rose-200`, `text-rose-800 dark:text-rose-300`.

2. **[KnowledgeGraphPage.tsx](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/pages/KnowledgeGraphPage.tsx#L658):**
   - Banner lỗi sửa Node (`editNodeError`): Đổi `text-rose-300` $\to$ `text-rose-800 dark:text-rose-300`, `text-rose-200/80` $\to$ `text-rose-900 dark:text-rose-200/80`.
   - Banner lỗi sửa Edge (`editEdgeError`): Đổi `text-rose-300` $\to$ `text-rose-800 dark:text-rose-300`, `text-rose-200/80` $\to$ `text-rose-900 dark:text-rose-200/80`.
   - Banner tạo & xóa Node/Edge: Đổi sang `text-rose-800 dark:text-rose-300`.
   - Huy hiệu loại nút: Đổi sang `text-*-700 dark:text-*-300`.

3. **Các trang CenterManager khác:**
   - Đồng bộ màu cảnh báo lỗi trên `QuestionEditorPage.tsx`, `CurriculumEditorPage.tsx`, `QuestionBankPage.tsx`, `AssignmentListPage.tsx`, `AssignmentProgressPage.tsx`.

---

## 3. MA TRẬN KIỂM CHỨNG TRÌNH DUYỆT CHROME E2E (E2E ACCEPTANCE MATRIX)

Kiểm thử tự động trên trình duyệt theo **13 kịch bản chi tiết** (bao gồm 11 luồng CenterManager + 1 kịch bản mobile responsive + 1 kịch bản Actor Isolation cho Teacher):

| STT | Route URL | Persona | Permission Code Canonical ([permissions.ts](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/web/edutwin-web/src/auth/permissions.ts)) | Giao diện & Chức năng mong đợi | Trạng thái API | Tiêu chí Light / Dark | Mobile (390x844) |
|---|---|---|---|---|---|---|---|
| **1** | `/quan-ly/tong-quan-trung-tam` | CenterManager | `dashboards.center.read` | Dashboard hiển thị các KPI cards thật từ DTO, biểu đồ độ thuần thục theo môn | 200 OK | Tương phản tốt, số liệu rõ nét, toggle Dark/Light chuyển mượt | Thu gọn thành 1 cột, không tràn ngang |
| **2** | `/quan-ly/trung-tam` | CenterManager | `organization.center.read`, `organization.center.manage` | Hiển thị mã trung tâm (chỉ đọc), hộp lưu ý Quản trị viên, form đổi tên trung tâm | 200 OK | Chữ vàng đậm trên nền amber ở Light, hộp mã trung tâm nền dịu mắt | Form stack dọc, nút bấm dễ chạm |
| **3** | `/kien-thuc/do-thi` | CenterManager | `knowledge.subjects.read`, `knowledge.nodes.read`, `knowledge.edges.read` | Canvas DAG topological layout phân tầng, đường cong Bezier mượt, inspector bên phải, zoom controls | 200 OK | Nền canvas tự chuyển màu theo theme, thẻ SVG chữ tối trên nền sáng hoặc chữ sáng trên nền tối; không có nút tạo trùng | Inspector chuyển thành drawer trượt từ dưới/phải |
| **4** | `/quan-ly/giao-trinh` | CenterManager | `curriculum.curriculums.read` | Bảng danh sách giáo trình phân trang, lọc theo trạng thái (Draft, Published, Archived) | 200 OK | Dropdown `select` chữ rõ ràng, không trắng trên trắng | Bảng có thanh cuộn ngang mượt |
| **5** | `/quan-ly/giao-trinh/tao-moi` | CenterManager | `curriculum.curriculums.create`, `knowledge.nodes.read` | Editor tạo giáo trình, bộ chọn canonical Knowledge Nodes và lớp học | 200 OK | Form input và select có viền tương phản rõ, modal xác nhận chuẩn | Form co giãn phù hợp |
| **6** | `/quan-ly/cau-hoi` | CenterManager | `curriculum.questions.read` | Danh sách câu hỏi, lọc môn học / độ khó, huy hiệu chấm điểm và KaTeX formula | 200 OK | Huy hiệu chế độ chấm (`TextExact`) và KaTeX đọc rõ ở cả Light & Dark | Thẻ câu hỏi hiển thị gọn gàng |
| **7** | `/quan-ly/cau-hoi/tao-moi` | CenterManager | `curriculum.questions.create` | Question Editor có toolbar KaTeX, chọn mode TextExact / NumericRational / Manual | 200 OK | Nút toolbar toán học và preview tương phản chuẩn | Toolbar responsive, không che khuất màn hình |
| **8** | `/quan-ly/bai-tap` | CenterManager | `assignments.assignments.read` | Danh sách bài tập phân trang server-side, bộ lọc lớp học có cache | 200 OK | Trạng thái Draft/Published có badge màu chuẩn | Phân trang hiển thị đầy đủ |
| **9** | `/quan-ly/bai-tap/tao-moi` | CenterManager | `assignments.assignments.create`, `organization.classes.read` | Wizard 3 bước: Thông tin chung & Lớp $\to$ Chọn câu hỏi $\to$ Xem lại | 200 OK | Viên thuốc bước kích hoạt (`1. Thông tin chung & Lớp học`) xanh dịu, chữ rõ nét | Thanh bước cho phép cuộn ngang |
| **10** | `/quan-ly/duyet-bai` | CenterManager | `twin.reasoning.review`, `twin.reasoning.override` | Master/Detail Review Queue: Cột trái danh sách lượt làm, cột phải đối chiếu 3 nguồn và form override | 200 OK | Chữ trong panel đối chiếu rõ ràng, nút override OCC có hiệu lực | Chuyển thành ngăn kéo xem chi tiết |
| **11** | `/quan-ly/phan-quyen` | CenterManager | `authorization.roles.read`, `authorization.user_roles.read`, `authorization.audit.read`, `authorization.permissions.read` | Giao diện 3 tabs: Vai trò & Ma trận quyền, Gán người dùng, Nhật ký kiểm toán | 200 OK | Ma trận quyền checkbox rõ ràng, trace ID hiển thị monospace | Tab điều hướng co giãn |
| **12** | **Mobile (390x844)** | CenterManager | Tất cả quyền | Điều hướng thu gọn thành hamburger menu, KPI cards stack 1 cột dọc | 200 OK | Tương phản tốt, nút bấm ngón tay thuận tiện | Không bị tràn ngang màn hình |
| **13** | **Actor Isolation (Teacher)** | Teacher (`teacher.math`) | Quyền Teacher legacy | Truy cập `/quan-ly/bai-tap` và `/kien-thuc/do-thi` | 200 OK | **Hiển thị 100% giao diện legacy cũ của Teacher**, KHÔNG có sidebar CenterManager, KHÔNG dính theme | Giao diện giáo viên co giãn bình thường |

---

## 4. KẾ HOẠCH THỰC THI & CHUYỂN GIAO (DELIVERABLES)

1. **Commit Kế hoạch:** Lưu và commit kế hoạch này vào `docs/plans/POST-R09-CENTER-MANAGER-UX-GATE8-PLAN.md`.
2. **Sửa Banner Contrast:** Cập nhật `AssignmentEditorPage.tsx`, `KnowledgeGraphPage.tsx` và các trang liên quan.
3. **Kiểm thử E2E & Chụp ảnh đối chứng:**
   - Chạy kiểm thử tự động trên Chrome qua công cụ browser automation.
   - Chụp ảnh màn hình đối chứng cho các màn hình đại diện trọng yếu.
   - Lưu trữ toàn bộ ảnh vào thư mục `docs/verification/post-r09-center-manager-ux-redesign/`.
4. **Báo cáo Nghiệm thu:**
   - Lập báo cáo [POST_R09_CENTER_MANAGER_UX_GATE8_VERIFICATION_REPORT.md](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/docs/verification/POST_R09_CENTER_MANAGER_UX_GATE8_VERIFICATION_REPORT.md).
   - Push commit lên `origin/codex/post-r09-center-manager-ux-redesign`.
