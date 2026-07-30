BẠN ĐANG TRIỂN KHAI DỰ ÁN EDUTWIN.

Trước khi làm bất kỳ thay đổi nào, bắt buộc đọc đầy đủ theo thứ tự:
1. CONSTITUTION.md
2. DATABASE_SCHEMA.md
3. API_CONTRACTS.md
4. MASTER_PLAN.md
5. PROMPT_TEMPLATES.md

Thứ tự authority:
CONSTITUTION > DATABASE_SCHEMA > API_CONTRACTS > MASTER_PLAN > PROMPT_TEMPLATES > source code hiện tại.

Các quy tắc không được vi phạm:
- Không tự ý đổi Database Schema, migration baseline, API contract, enum, thuật toán hoặc kiến trúc.
- Không tạo endpoint/table/package/pattern ngoài task.
- Không sửa file ngoài allow-list.
- Không đưa business logic vào Controller, React component hoặc DAL.
- Không gọi Gemini từ Controller.
- Không nhận centerId từ client; tenant lấy từ JWT/ITenantContext.
- Không bỏ Global Query Filter, ownership guard, transaction, validation hoặc test để làm nhanh.
- Không commit secret, API key, password, JWT key hoặc Refresh Token.
- Không triển khai tính năng ngoài MVP.
- Nếu specification và source code mâu thuẫn, specification thắng; báo mâu thuẫn trước khi sửa.
- Nếu cần thay đổi frozen decision, DỪNG và xuất Change Proposal. Không tự triển khai đề xuất.

Trước khi code, hãy trả lời ngắn:
1. Task ID bạn hiểu là gì?
2. Dependencies nào đã phải hoàn thành?
3. File/folder nào bạn sẽ sửa?
4. Invariant nào có rủi ro cao nhất?
5. Test nào bạn sẽ chạy?

Chỉ bắt đầu sau khi đã nêu năm điểm trên.

==================================================
A. THÔNG TIN REPOSITORY HIỆN TẠI & TRẠNG THÁI DỰ ÁN
==================================================

Repository:
https://github.com/nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform.git

Trạng thái hiện tại (theo tien-do-project.md):
- Phase P01 - P08: Hoàn thành 100%.
- Phase P09-T02 đến T04: Hoàn thành 100%.
- Phase P09-T01: MỚI HOÀN THÀNH MỘT PHẦN (Chỉ có POST và GET list). Các endpoint còn lại chưa được triển khai.
- Phase P09-T05 (Frontend): Đã có UI list, nhưng chưa wire (kết nối) với các API mutations, form tạo mới chưa kết nối.

Nhiệm vụ tiếp theo cần triển khai: **Hoàn thành toàn bộ Phase P09 (Bổ sung Curriculum Backend API & Frontend UI Wiring)**.

==================================================
B. NHIỆM VỤ HIỆN TẠI: HOÀN THIỆN P09 (BACKEND T01 & FRONTEND T05)
==================================================

VAI TRÒ:
Bạn là Full-stack Developer triển khai hoàn thiện Phase P09 cho dự án EduTwin.

TASK:
- Task ID: P09-Completion
- Phase: P09 — Curriculum + Question Bank
- Mục tiêu duy nhất:
  1. (Backend) Hoàn thiện các API endpoint còn thiếu của `CurriculumsController` (GET by id, PATCH, PUT classes, PUT nodes, POST publish).
  2. (Frontend) Hoàn thiện UI và kết nối API (wire BE) cho tính năng quản lý Lộ trình học (Curriculum) và Ngân hàng câu hỏi (Question Bank).

CONTEXT NGHIỆP VỤ:
Giáo viên và Quản lý trung tâm cần có khả năng tạo, chỉnh sửa, xuất bản (publish) Lộ trình học, gán lớp học và kiến thức vào Lộ trình. Đồng thời, họ cần tạo và quản lý câu hỏi trong Ngân hàng câu hỏi thông qua UI tương tác.

DEPENDENCIES ĐÃ DONE:
- P08 (Subject & Knowledge Graph) đã xong.
- Database Schema của Curriculum và Question đã có.
- API Questions đã có. `useQuestions` và `useCurriculums` (hook) đã có sẵn nhưng chưa được UI gọi.

SPECIFICATION PHẢI ĐỐI CHIẾU:
- API_CONTRACTS.md: Mục 44, 45 (Curriculum Endpoints: `GET /{id}`, `PATCH /{id}`, `PUT /{id}/classes`, `PUT /{id}/nodes`, `POST /{id}/publish`).
- MASTER_PLAN.md: Phase P09.

==================================================
C. ALLOW-LIST & FILE CẤM
==================================================

FILE/FOLDER ĐƯỢC PHÉP SỬA:
Backend:
- `src/EduTwin.API/Controllers/CurriculumsController.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/**` (Tạo các use cases còn thiếu cho Curriculum)
- `src/EduTwin.Contracts/CurriculumAndQuestions/**` (Tạo DTO/Requests nếu thiếu)
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/**`

Frontend:
- `web/edutwin-web/src/pages/CurriculumListPage.tsx`
- `web/edutwin-web/src/pages/CurriculumEditorPage.tsx` [NEW] (Tạo trang/modal chỉnh sửa)
- `web/edutwin-web/src/pages/QuestionBankPage.tsx`
- `web/edutwin-web/src/pages/QuestionEditorPage.tsx`
- `web/edutwin-web/src/App.tsx` (Bổ sung route edit/create nếu cần)
- `web/edutwin-web/src/features/curriculum/**`
- `web/edutwin-web/src/features/questions/**`

FILE/FOLDER BỊ CẤM:
- Mọi path không nằm trong allow-list.
- Các file specification (.md).
- Migration đã merge.
- Không sửa backend Question (P09-T02) vì đã hoàn thành.

==================================================
D. YÊU CẦU TRIỂN KHAI CHI TIẾT
==================================================

1. BACKEND (Curriculum API):
   - Triển khai `GET /api/v1/curriculums/{id}`
   - Triển khai `PATCH /api/v1/curriculums/{id}` (Cập nhật thông tin cơ bản)
   - Triển khai `PUT /api/v1/curriculums/{id}/classes` (Gán lớp học vào lộ trình)
   - Triển khai `PUT /api/v1/curriculums/{id}/nodes` (Gán KnowledgeNodes vào lộ trình)
   - Triển khai `POST /api/v1/curriculums/{id}/publish` (Đổi trạng thái sang Published)
   - Đảm bảo Business logic nằm trong BLL (UseCase), không để trong Controller. Authorization / Tenant / Ownership đầy đủ.

2. FRONTEND (UI Wiring):
   - **Question Bank**:
     - `QuestionBankPage`: Đảm bảo nút "Tạo mới" navigate tới form editor. Nút Activate/Archive gọi mutation tương ứng.
     - `QuestionEditorPage`: Form submit hiện đang chỉ `console.log`. Cần wire vào `useCreateQuestion` (và `useUpdateQuestion` nếu là mode sửa). Chuyển trang/thông báo thành công sau khi save. Thêm Route cho việc Edit Question (ví dụ `/quan-ly/cau-hoi/:id`).
   - **Curriculum**:
     - Tạo UI / Form cho Curriculum Editor (tạo mới và sửa).
     - Wire vào `useCreateCurriculum`, `usePublishCurriculum` (hoặc các hook tương ứng của TanStack query).
     - Cho phép gán Lớp học và publish.

BUSINESS INVARIANTS:
- Chỉ curriculum ở trạng thái `Draft` mới được phép sửa (`PATCH`), gán lớp (`PUT /classes`), gán nodes (`PUT /nodes`).
- Tenant context (`center_id`) luôn tự động lấy từ JWT, client KHÔNG truyền `centerId`.
- Các mutation (update/publish) yêu cầu concurrency control (`rowVersion`).

ACCEPTANCE CRITERIA:
- [ ] Các API endpoints Curriculum còn thiếu trả đúng HTTP Status, có UseCase xử lý bên trong.
- [ ] Frontend Question Editor gọi API create/update thành công, không còn `console.log` dummy.
- [ ] Frontend Curriculum list có thể navigate sang màn tạo mới/chỉnh sửa, gọi API thành công.
- [ ] Không schema/API drift ngoài hợp đồng.
- [ ] `npm run build` frontend không có lỗi TypeScript.
- [ ] `dotnet test` cho các UseCase mới pass.

TEST BẮT BUỘC:
- Unit test cho các Curriculum UseCases (Get, Update, AssignClasses, Publish).
- `dotnet test`
- `npm run build` trong `web/edutwin-web`

QUY TẮC DỪNG:
Nếu cần đổi schema/API hoặc thiếu dependency ngoài P09, dừng và báo BLOCKED kèm lý do. Không tự mở rộng scope.

OUTPUT BÀN GIAO:
1. Tóm tắt kết quả hoàn thành API Curriculum và Frontend Wiring.
2. Danh sách file đã đổi.
3. Test/lệnh đã chạy và kết quả (dotnet test, npm run build).
4. Xác nhận không đổi schema/API ngoài task.
