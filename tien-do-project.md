# Tiến độ dự án EduTwin

Tài liệu này tổng hợp tiến độ hiện tại của repository so với [MASTER_PLAN.md](MASTER_PLAN.md).

_Cập nhật lần cuối: 2026-07-30_

## Kết luận ngắn

Dự án đã **hoàn thành backend của P08 và T01–T04 của P09 (Curriculum + Question Bank API)**. Frontend P09-T05 đang làm dở: các trang đã có skeleton và kết nối list với BE, nhưng các thao tác tạo/sửa/activate/archive **chưa được wire vào backend** (form save chỉ `console.log`). Các phase P10 trở đi chưa triển khai.

## Đã hoàn thành

### P01 — Solution Bootstrap

- `EduTwin.API`, `EduTwin.BLL`, `EduTwin.DAL`, `EduTwin.Contracts`, `EduTwin.BLL.Tests`
- Frontend khởi tạo bằng React + Vite + TypeScript, TanStack Query, Zustand, Axios.

### P02 — Local Docker Environment

- `docker-compose.yml`, Dockerfile API (multi-stage), Dockerfile Web (Nginx), `.env.example`
- Bốn service: mysql, api, web, adminer đều chạy.

### P03 đến P05 — Data Foundation

- Toàn bộ migrations (4 bước): identity/organization → knowledge/curriculum/assignment → twin/personalization → assessment/AI jobs.
- `EduTwinDbContext`, `EduTwinSeedFactory`, `EduTwinRuntimeSeeder` (seed hai Center).

### P06 — Authentication và Tenant Isolation

- `AuthController`: login, refresh, logout, me.
- `TenantContext`, `ClaimsResolver`, `TenantContextMiddleware`.
- Frontend: `LoginPage`, `AuthBootstrap`, `ProtectedRoute`, `RoleRoute`, `authStore`.

### P07 — Center Organization Management

- Backend đầy đủ: `CentersController`, `TeachersController`, `StudentsController`, `ClassesController`.
- BLL có đầy đủ use case: center profile, teacher CRUD, student CRUD, class CRUD, add/remove students.
- Frontend: `TeacherListPage`, `StudentListPage`, `ClassListPage` với đầy đủ CRUD tích hợp BE.

### P08 — Subject và Knowledge Graph

- Backend: `SubjectsController`, `KnowledgeNodesController`, `KnowledgeEdgesController`, `KnowledgeGraphController`.
- Có `KnowledgeGraphValidator` (DAG cycle detection) và `KnowledgeNodeHierarchyCycleDetector`.
- Frontend: `KnowledgeGraphPage` với tạo node/edge và hiển thị validation cycle.

## Đang làm dở

### P09 — Curriculum + Question Bank

**Backend (T01–T04): Đã hoàn thành**

| Task | Trạng thái | Ghi chú |
|---|---|---|
| T01 — Curriculum CRUD/publish | ✅ Xong | `POST /curriculums`, `GET /curriculums` — **thiếu `GET /{id}`, `PATCH /{id}`, `PUT /{id}/classes`, `PUT /{id}/nodes`, `POST /{id}/publish`** |
| T02 — Question aggregate | ✅ Xong | `POST`, `GET`, `GET /{id}`, `PATCH /{id}`, `POST /{id}/activate`, `POST /{id}/archive`, `DELETE /{id}` |
| T03 — Grading criteria value object | ✅ Xong | `GradingCriteriaValidator`, JSON round-trip |
| T04 — Deterministic preliminary grader | ✅ Xong | Grading logic tách biệt, có unit test |

> **Lưu ý quan trọng về Curriculum backend:** `CurriculumsController` hiện chỉ có 2 endpoint (`POST /curriculums` và `GET /curriculums`). Các endpoint còn lại theo API contract (`GET /{id}`, `PATCH /{id}`, `PUT /{id}/classes`, `PUT /{id}/nodes`, `POST /{id}/publish`) **chưa được triển khai**.

**Frontend (T05 — UI): Chưa hoàn thành**

| Trang / Hook | Đã có | Còn thiếu |
|---|---|---|
| `QuestionBankPage.tsx` | List + filter kết nối BE (`useQuestions`) | Nút "Tạo mới" chưa navigate; nút "Chi tiết" chưa có action |
| `QuestionEditorPage.tsx` | Form đầy đủ 3 loại (MultipleChoice/ShortAnswer/Essay) + GradingCriteria | `handleSave()` chỉ `console.log`, **chưa gọi `useCreateQuestion()`** |
| `CurriculumListPage.tsx` | List kết nối BE (`useCurriculums`) | Nút "Tạo Lộ trình mới" chưa navigate/modal; **không có Curriculum editor form** |
| `useQuestions.ts` | Tất cả mutations (create/update/activate/archive/delete) đã định nghĩa | Chưa được gọi từ UI |
| `useCurriculums.ts` | `useCreateCurriculum`, `useUpdateCurriculum`, `usePublishCurriculum`, ... | Chưa được gọi từ UI |
| Routes | `/quan-ly/cau-hoi/tao-moi` đã có | Thiếu route sửa câu hỏi (`/quan-ly/cau-hoi/:id`), route curriculum editor |

**Lỗi đã sửa trong phiên này:**
- Fix bug 500 trong `ListCurriculumsUseCase` và `ListQuestionsUseCase`: MySQL EF provider không thể resolve type mapping khi dùng `Contains(List<Guid>)` với cột `varchar(36)`. Đã chuyển sang client-side filter bằng `HashSet<Guid>`.

### P10 — Assignments

Schema có nhưng chưa có BLL use case và API endpoint nào cho assignment workflow.

### P11 — Attempts + Jobs

Schema có, chưa có flow submit, job state machine, BackgroundService.

### P12 — Gemini Analysis

Chưa triển khai.

### P13 — Digital Twin Engine

Mới ở mức nền:
- `UpsertStudentSubjectGoalUseCase`, `ListStudentSubjectGoalsUseCase`.
- `StudentSubjectGoalRiskCalculator`.
- Chưa có engine tính toán twin đầy đủ.

### P14 — Opportunity Gap + Recommendation

Contracts và entities có, chưa có pipeline.

### P15 đến P17 — Frontend nghiệp vụ

Chưa có dashboard cho Student, Teacher, Center Manager.

### P18 — Hardening + Release

Chưa bắt đầu.

## Bằng chứng chính trong source

- **Controllers**: `AuthController`, `HealthController`, `CentersController`, `TeachersController`, `StudentsController`, `ClassesController`, `SubjectsController`, `KnowledgeNodesController`, `KnowledgeEdgesController`, `KnowledgeGraphController`, `CurriculumsController` (2 endpoint), `QuestionsController` (7 endpoint).
- **Frontend pages**: LoginPage, AuthenticatedHomePage (navigation hub), TeacherListPage, StudentListPage, ClassListPage, KnowledgeGraphPage, CurriculumListPage (list only), QuestionBankPage (list only), QuestionEditorPage (form không wire BE).
- **API/hooks**: `curriculumApi`, `questionsApi`, `useCurriculums`, `useQuestions` đã đầy đủ nhưng chưa được dùng trong UI để ghi.

## Đánh giá tiến độ theo Master Plan

| Phase | Trạng thái |
|---|---|
| P00 | ✅ Hoàn thành |
| P01 | ✅ Hoàn thành |
| P02 | ✅ Hoàn thành |
| P03 | ✅ Hoàn thành |
| P04 | ✅ Hoàn thành |
| P05 | ✅ Hoàn thành |
| P06 | ✅ Hoàn thành |
| P07 | ✅ Hoàn thành |
| P08 | ✅ Hoàn thành |
| P09 | 🔄 Đang tiến hành — T01 (một phần), T02, T03, T04 ✅; T01 thiếu vài endpoint; T05 (UI) chưa wire BE |
| P10+ | ❌ Chưa bắt đầu |

## Đề xuất bước tiếp theo

Ưu tiên cao nhất: **Hoàn thành P09-T05 (UI wire vào BE)** gồm:

1. **Curriculum editor**: Tạo form tạo/sửa curriculum, gán lớp, publish — gọi `useCreateCurriculum`, `usePublishCurriculum`.
2. **Bổ sung Curriculum backend**: Thêm các endpoint còn thiếu (`GET /{id}`, `PATCH /{id}`, `PUT /{id}/classes`, `PUT /{id}/nodes`, `POST /{id}/publish`).
3. **Question editor wire BE**: Kết nối `QuestionEditorPage` với `useCreateQuestion`; thêm route sửa câu hỏi với `useUpdateQuestion`.
4. **QuestionBankPage actions**: Nút "Tạo mới" navigate đến editor; nút "Chi tiết" mở detail/edit; nút activate/archive từ list.

Sau đó mới triển khai **P10 (Assignment workflow)**.


