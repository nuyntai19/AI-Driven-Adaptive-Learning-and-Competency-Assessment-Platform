# EduTwin — Master Development Plan

> Phiên bản: 1.0  
> Trạng thái: FROZEN  
> Phương châm: Data First, Plan Second  
> Phạm vi: MVP chạy Local bằng Docker Compose  
> Vai trò Codex: Architect, Specification Manager, Code Reviewer, Acceptance Reviewer  

## 1. Mục tiêu của Master Plan

Tài liệu này phân rã EduTwin thành các Phase nhỏ, tuần tự và có thể nghiệm thu độc lập. Sau mỗi Phase:

- Hệ thống phải build/test được.
- Phần đã hoàn thành phải chạy được trong phạm vi checkpoint.
- Có thể commit và mở Pull Request mà không phụ thuộc code chưa tồn tại ở Phase sau.
- Không được để migration/API/schema ở trạng thái nửa vời.

Claude/Gemini chỉ triển khai Task ID được giao. Codex review theo Constitution, Database Schema, API Contracts và Definition of Done trong tài liệu này.

## 2. Kết quả cuối của MVP

Kịch bản demo cuối:

1. Center Manager đăng nhập vào Center A.
2. Center Manager tạo Teacher, Student và Class.
3. Teacher tạo Subject/Knowledge Graph hoặc dùng Seed Data.
4. Teacher tạo Question, tạo Assignment và giao cho Class/Gap Group.
5. Student đăng nhập, mở Assignment, trả lời và nhập reasoning_text.
6. API trả 202 cùng AI Job; UI polling trạng thái.
7. Gemini trả structured JSON hoặc hệ thống fallback.
8. BLL cập nhật Knowledge Twin, Behavior Twin, Goal/Risk, History và Recommendation.
9. Student thấy Radar/Line/Opportunity Action thay đổi.
10. Teacher thấy lớp, high-risk Students, weak Topics và Gap Groups.
11. Teacher Override một analysis; hệ thống replay và cập nhật lại Twin.
12. Center Manager thấy tổng quan toàn trung tâm.
13. Đăng nhập Center B chứng minh không đọc được dữ liệu Center A.

## 3. Nguyên tắc lập kế hoạch

- Hoàn thành data model trước business API.
- Hoàn thành backend contract trước màn hình phụ thuộc contract đó.
- Mỗi thuật toán lõi phải có Unit Test trước khi tích hợp UI.
- Gemini là dependency không tin cậy; fallback phải được hoàn thành cùng phase AI.
- Không gọi Gemini trong transaction.
- Không tạo “temporary endpoint” ngoài API_CONTRACTS.md.
- Không trì hoãn tenant isolation đến cuối.
- UI chỉ tích hợp endpoint đã có contract và backend checkpoint.
- Mỗi Phase có branch/commit riêng.

## 4. Sơ đồ phụ thuộc Phase

~~~mermaid
flowchart LR
    P00["P00 Governance"] --> P01["P01 Solution"]
    P01 --> P02["P02 Docker"]
    P02 --> P03["P03 Data A"]
    P03 --> P04["P04 Data B"]
    P04 --> P05["P05 Data C + Seed"]
    P05 --> P06["P06 Auth + Tenant"]
    P06 --> P07["P07 Organization"]
    P07 --> P08["P08 Knowledge Graph"]
    P08 --> P09["P09 Curriculum + Questions"]
    P09 --> P10["P10 Assignments"]
    P10 --> P11["P11 Attempts + Jobs"]
    P11 --> P12["P12 Gemini"]
    P12 --> P13["P13 Twin Engine"]
    P13 --> P14["P14 Opportunity Gap"]
    P14 --> P15["P15 Student App"]
    P15 --> P16["P16 Teacher App + Override"]
    P16 --> P17["P17 Center App"]
    P17 --> P18["P18 Hardening + Release"]
~~~

## 5. Quy ước Task

Task ID: P{phase}-T{sequence}.

Mỗi prompt giao Task phải chứa:

- Context.
- Task ID và mục tiêu.
- Dependencies đã Done.
- File/folder được phép sửa.
- File/folder bị cấm.
- Database/API contract liên quan.
- Business rules.
- Acceptance criteria.
- Test/lệnh xác minh.
- Output format khi bàn giao.

Template nằm trong PROMPT_TEMPLATES.md.

## 6. Branch và commit

| Phase | Branch gợi ý | Commit gợi ý |
|---|---|---|
| P00 | docs/spec-baseline | docs: freeze edutwin mvp specifications |
| P01 | feat/solution-bootstrap | chore: bootstrap modular monolith solution |
| P02 | feat/local-containers | chore: add local docker environment |
| P03 | feat/data-identity | feat: add tenant and organization schema |
| P04 | feat/data-learning | feat: add knowledge content and assignment schema |
| P05 | feat/data-twin | feat: add twin assessment schema and seed data |
| P06 | feat/auth-tenancy | feat: add jwt auth and tenant isolation |
| P07 | feat/organization | feat: add center organization management |
| P08 | feat/knowledge-graph | feat: add subject and knowledge graph |
| P09 | feat/question-bank | feat: add curriculum and question bank |
| P10 | feat/assignments | feat: add assignment workflow |
| P11 | feat/attempt-jobs | feat: add attempt and analysis job workflow |
| P12 | feat/gemini-analysis | feat: add gemini reasoning analysis |
| P13 | feat/digital-twin | feat: add explainable twin calculations |
| P14 | feat/recommendation | feat: add opportunity gap recommendations |
| P15 | feat/student-app | feat: add student learning experience |
| P16 | feat/teacher-app | feat: add teacher analytics and override |
| P17 | feat/center-app | feat: add center management dashboard |
| P18 | release/mvp | chore: harden and release edutwin mvp |

# Phase P00 — Governance Baseline

## 7. Mục tiêu

Đóng băng năm specification, thiết lập cơ chế change control và bảo đảm AI Developer không triển khai từ mô tả cũ trong DOCX.

Dependencies: không.

## 8. Tasks

### P00-T01 — Đặt năm specification tại repository root

Kết quả:

- CONSTITUTION.md.
- DATABASE_SCHEMA.md.
- API_CONTRACTS.md.
- MASTER_PLAN.md.
- PROMPT_TEMPLATES.md.

Acceptance:

- Chỉ có đúng một baseline version cho MVP.
- Mọi file có trạng thái FROZEN.
- README tương lai chỉ link đến năm file, không sao chép nội dung gây drift.

### P00-T02 — Lập Specification Compliance Checklist

Không tạo file thứ sáu. Checklist được dùng trực tiếp từ cuối mỗi tài liệu.

Acceptance:

- Người triển khai biết thứ tự ưu tiên tài liệu.
- Change Proposal template có sẵn.

### P00-T03 — Review scope cũ

Đánh dấu EduTwin-Overview.docx là tài liệu vision, không phải implementation authority.

## 9. Business rules

- Không source code.
- Không tự sửa frozen decision.
- Không import FastAPI/Next.js/Supabase từ tài liệu vision cũ.

## 10. Definition of Done

- Năm file được commit.
- Product Owner xác nhận baseline.
- AI Developer prompt bắt buộc đọc năm file.

## 11. Checkpoint/commit gate

- Kiểm tra Git diff chỉ có tài liệu.
- Commit docs baseline.
- Không bắt đầu P01 khi còn câu hỏi kiến trúc chưa khóa.

# Phase P01 — Solution Bootstrap

## 12. Mục tiêu

Khởi tạo repository buildable với đúng project vật lý và dependency direction.

Dependencies: P00.

## 13. Tasks

### P01-T01 — Backend solution structure

Tạo solution và project:

- EduTwin.API.
- EduTwin.BLL.
- EduTwin.DAL.
- EduTwin.Contracts.
- EduTwin.BLL.Tests.

Project references phải đúng CONSTITUTION.md. Bật nullable, implicit usings, warnings hợp lý và deterministic build.

### P01-T02 — Module folder skeleton

Tạo folder/namespace nhất quán cho:

- IdentityAndTenancy.
- Organization.
- KnowledgeGraph.
- CurriculumAndQuestions.
- Assignments.
- AssessmentAndReasoning.
- DigitalTwin.
- Recommendations.
- Dashboards.

Chỉ skeleton cần thiết; không tạo hàng trăm class rỗng.

### P01-T03 — Frontend bootstrap

Khởi tạo React + Vite + TypeScript, TailwindCSS, Axios, Zustand, TanStack Query, Recharts.

Thiết lập folder:

~~~text
web/edutwin-web/src/
├── app/
├── api/
├── auth/
├── components/
├── features/
├── layouts/
├── pages/
├── routes/
├── stores/
└── types/
~~~

### P01-T04 — Health endpoint skeleton

Thêm /api/v1/health/live. Chưa yêu cầu database.

### P01-T05 — CI baseline

GitHub Actions trên Pull Request vào main:

- Restore.
- Build.
- Test.

Không CD, không frontend test bắt buộc.

## 14. Architecture rules

- API không reference implementation detail ngoài BLL/Contracts.
- BLL không reference API.
- DAL không reference API/BLL. Repository/query abstraction thuộc DAL và được BLL sử dụng theo dependency direction đã khóa; business rule không được đặt trong DAL.
- Contracts không reference project khác.
- Không thêm MediatR/CQRS nếu chưa được duyệt.

## 15. Tests

- Một smoke Unit Test chứng minh test project chạy.
- Dependency direction được self-review.

## 16. Definition of Done

- dotnet build thành công.
- dotnet test thành công.
- npm build frontend thành công.
- /health/live trả Healthy khi chạy API local.
- Không có business feature giả.

## 17. Checkpoint/commit gate

Demo:

- Chạy API.
- Mở health endpoint.
- Build frontend.

Commit độc lập trước P02.

# Phase P02 — Local Docker Environment

## 18. Mục tiêu

Chạy bốn service mysql, api, web, adminer bằng Docker Compose với cấu hình an toàn.

Dependencies: P01.

## 19. Tasks

### P02-T01 — Dockerfile API

- Multi-stage build.
- Runtime non-development phù hợp.
- Environment configuration.
- Health endpoint.

### P02-T02 — Dockerfile Web

- Build React SPA.
- Serve static production build bằng web server phù hợp.
- API base URL qua environment/build configuration có tài liệu.

### P02-T03 — Docker Compose

Bốn service duy nhất:

- mysql.
- api.
- web.
- adminer.

mysql có persistent volume và healthcheck. api phụ thuộc mysql service_healthy. Không thêm Redis.

### P02-T04 — Environment template

Tạo .env.example không secret. Cấu hình:

- MySQL database/user/password placeholder.
- ConnectionStrings.
- JWT issuer/audience/signing key placeholder.
- Gemini API key/model placeholder.
- Seed password placeholder.

### P02-T05 — Readiness

Thêm /api/v1/health/ready kiểm tra MySQL khi DAL được nối. Ở cuối phase có thể trả Unhealthy nếu schema chưa có, nhưng connection phải xác minh được.

## 20. Business/operational rules

- Không hardcode localhost bên trong container.
- Không commit .env.
- Gemini không phải readiness dependency.
- Container restart không được tự drop database.
- Adminer chỉ phục vụ local.

## 21. Definition of Done

- docker compose config hợp lệ.
- Bốn container start.
- mysql healthy.
- API live endpoint healthy.
- Web mở được.
- Adminer kết nối MySQL bằng network service name.

## 22. Checkpoint/commit gate

Từ workspace sạch:

1. Copy .env.example thành .env và điền local secret.
2. docker compose up --build.
3. Kiểm tra live/ready, web, adminer.
4. docker compose down không làm mất volume.

# Phase P03 — Data Foundation A: Tenant, Identity, Organization

## 23. Mục tiêu

Triển khai schema Module 1 và EF Core foundation mà chưa mở business endpoint.

Dependencies: P02.

## 24. Tasks

### P03-T01 — DbContext và conventions

- MySQL provider tương thích .NET/EF Core 10 đã được xác minh trước khi cài.
- snake_case mapping.
- Guid VARCHAR(36).
- UTC DateTime policy.
- Decimal precision.
- row_version concurrency.
- audit/soft-delete abstraction thực dụng.

### P03-T02 — Module 1 entities/configurations

Triển khai:

- centers.
- users.
- refresh_tokens.
- teachers.
- students.
- subjects.
- classes.
- class_students.

### P03-T03 — Tenant-safe keys

- Unique alternate key center_id + entity_id.
- Composite foreign key theo DATABASE_SCHEMA.md.
- Delete behavior explicit; không dùng default mơ hồ.

### P03-T04 — Migration Module 1

Tạo migration có tên nghiệp vụ. Chưa seed password trong migration text.

### P03-T05 — DAL design-time support

Migration command chạy được ngoài API container và trong workflow local được tài liệu hóa trong README hiện hữu hoặc comment ngắn tại compose; không tạo specification thứ sáu.

## 25. Logic cần chú ý

- Center không phải tenant child.
- User uniqueness theo Center.
- Teacher/Student dùng PK/FK trùng User ID.
- Subject tenant-owned.
- Class bắt buộc một Subject và Teacher cùng Center.

## 26. Tests/verification

- Migration apply trên database trống.
- INFORMATION_SCHEMA xác nhận PK, unique, FK, CHECK.
- Cross-tenant composite FK thử bằng data fixture phải fail.
- Migration rollback ở Development được kiểm tra nếu provider hỗ trợ.

## 27. Definition of Done

- Tất cả bảng Module 1 đúng type/key/index.
- Database tạo từ migration thành công.
- API ready endpoint Healthy sau migration.
- Không endpoint CRUD ngoài health.
- Không có seed dữ liệu thật.

## 28. Commit gate

- Xóa database Development có chủ ý.
- Apply migration từ đầu.
- Build/test.
- Commit migration cùng entity/config.

# Phase P04 — Data Foundation B: Knowledge, Curriculum, Questions, Assignments

## 29. Mục tiêu

Triển khai schema Module 2 và Module 3.

Dependencies: P03.

## 30. Tasks

### P04-T01 — Knowledge Graph schema

- knowledge_nodes.
- knowledge_edges.
- Hierarchy/edge indexes.
- CHECK self-loop và range.

### P04-T02 — Curriculum/Question schema

- curriculums.
- curriculum_classes.
- curriculum_nodes.
- questions.
- question_options.
- question_knowledge_nodes.

### P04-T03 — Assignment schema

- assignments.
- assignment_questions.
- assignment_targets.
- student_assignment_progress.

### P04-T04 — JSON mapping

- grading_criteria dùng typed value object/serializer boundary rõ.
- Không dùng dynamic tùy tiện trong BLL.

### P04-T05 — Migration Module 2/3

Migration độc lập, không sửa migration P03.

## 31. Logic cần chú ý

- node_id/question_id là BIGINT nhưng API sau này serialize string.
- Topic type chưa thể enforce hoàn toàn ở DB; ghi rõ BLL invariant.
- MultipleChoice option count/correct count xử lý BLL.
- Assignment Target là snapshot.
- Curriculum không có upload/OCR.

## 32. Tests/verification

- Unique question option label.
- Unique graph edge.
- Assignment question/target không cross tenant.
- CHECK difficulty, score, time.
- JSON grading criteria lưu/đọc round-trip.

## 33. Definition of Done

- Module 2/3 migration apply trên database Module 1.
- Schema từ database trống chạy qua P03+P04.
- Không cycle logic ở phase này; chỉ schema.
- Không tạo endpoint ngoài contract.

## 34. Commit gate

- Apply tất cả migration.
- Inspect schema qua Adminer.
- Build/test.
- Commit.

# Phase P05 — Data Foundation C: Twin, Assessment, AI Jobs, Seed

## 35. Mục tiêu

Hoàn tất toàn bộ schema và Seed Data deterministic trước khi mở business API.

Dependencies: P04.

## 36. Tasks

### P05-T01 — Digital Twin schema

- student_subject_goals.
- student_twins.
- knowledge_twins.
- behavior_twins.
- twin_update_history.
- learning_paths.
- learning_path_items.
- recommendations.

### P05-T02 — Assessment/AI schema

- attempts.
- reasoning_analyses.
- ai_analysis_jobs.
- Unique idempotency/attempt/job/analysis constraints.
- Lease/recovery indexes.

### P05-T03 — Migration Module 4/5

Migration riêng, đúng delete behavior.

### P05-T04 — Deterministic seed

Hai Center, hai Subject logic, ba Topic mỗi Subject, DAG, 30 logical Questions, User/Class/Goal mock.

Sử dụng fixed seed cho Bogus. Không log/commit seed password thật.

### P05-T05 — Data integrity audit

Đối chiếu từng table/column/index với DATABASE_SCHEMA.md.

## 37. Logic cần chú ý

- 30 logical Question có thể clone cho hai Center.
- Không seed Attempt trên profile demo chính nếu cần thể hiện Mastery từ 0.
- Seed DAG phải hợp lệ.
- AI job không lưu raw AI payload.
- History append-only.

## 38. Tests/verification

- Reset database rồi apply tất cả migration.
- Seed chạy hai lần không duplicate.
- Count dữ liệu theo Center.
- Truy vấn Center A/B độc lập.
- Attempt/Analysis/Job unique constraint đúng.
- Check range Mastery/Risk.

## 39. Definition of Done

- Toàn bộ 31 table trong DATABASE_SCHEMA.md tồn tại.
- Seed hai tenant hoạt động.
- Database schema audit không có drift.
- Docker fresh start tạo môi trường demo dữ liệu.

## 40. Commit gate

Tag nội bộ gợi ý: data-baseline-v1.  
Từ Phase sau, schema frozen; mọi table/column mới cần Change Proposal.

# Phase P06 — Authentication và Tenant Isolation

## 41. Mục tiêu

Hoàn thiện JWT + Refresh Token, Role + Ownership foundation và tenant isolation end-to-end.

Dependencies: P05.

## 42. Tasks

### P06-T01 — Tenant context

- ITenantContext scoped.
- Resolve Center từ centerCode trong login.
- Resolve CenterId/UserId/Role từ validated JWT cho request authenticated.
- Explicit tenant execution scope cho BackgroundService tương lai.

### P06-T02 — Global Query Filters

- Tenant filter cho mọi table center_id.
- Soft delete filter cho MTA.
- Test filter coverage.
- Không dùng request body centerId.

### P06-T03 — Login

Implement POST /auth/login:

- Center active.
- User active.
- Password verify.
- Claims: sub, center_id, role, auth_version.
- Refresh Token hash persistence.

### P06-T04 — Refresh rotation/logout

- Atomic rotation.
- Reuse prevention.
- Revoke logout.
- Cookie policy theo API_CONTRACTS.md.

### P06-T05 — Authorization policies

- Role policies.
- Ownership guard interfaces cho Teacher/Class/Student.
- Cross-tenant resource trả 404.

### P06-T06 — Frontend auth foundation

- Login page cơ bản.
- Access Token in-memory.
- Refresh cookie flow.
- Protected route.
- Logout.

## 43. Logic/transactions

- Refresh rotation là transaction.
- User/Center disabled chặn login/refresh.
- Không trả thông tin phân biệt centerCode/username/password sai.
- Background tenant context chưa xử lý job nhưng abstraction phải sẵn sàng.

## 44. Unit tests

- Login success/failure.
- Refresh rotate/reuse.
- Disabled User/Center.
- Role policy.
- Tenant A không query Tenant B.
- Soft-deleted record không xuất hiện.
- Ownership guard.

## 45. Definition of Done

- Bốn auth endpoint khớp contract.
- Hai Center login độc lập.
- ID Center B qua token Center A trả 404.
- Refresh Token không xuất hiện trong response body/localStorage.
- Không query DbContext trực tiếp từ Controller.

## 46. Demo/commit gate

1. Login Center A.
2. Gọi /auth/me.
3. Refresh.
4. Logout.
5. Chứng minh token/ID tenant B không truy cập được.

# Phase P07 — Center Organization Management

## 47. Mục tiêu

Hoàn thiện Center, Teacher, Student, Class, membership và Student Subject Goal API/UI quản trị cơ bản.

Dependencies: P06.

## 48. Tasks

### P07-T01 — Center profile

- GET/PATCH /centers/me.
- Concurrency rowVersion.

### P07-T02 — Teacher management

- Create/list/get/update/delete.
- User + Teacher transaction.
- Active Class guard khi delete.

### P07-T03 — Student management

- Create/list/get/update.
- User + Student + membership + Student Twin transaction.
- Teacher ownership.

### P07-T04 — Class management

- CRUD Class.
- Add/remove/list Students.
- Membership history semantics.

### P07-T05 — Subject Goal

- Upsert/list goal.
- Initial predicted score 0.
- Initial Risk theo frozen formula.

### P07-T06 — Center/Teacher management UI tối thiểu

- Center Manager: Teacher/Class lists và forms.
- Teacher: Student/Class list.
- Chưa xây Dashboard analytics.

## 49. Logic cần chú ý

- Username unique trong Center.
- Teacher chỉ thêm Student vào Class mình quản lý.
- Remove membership không xóa Assignment Target cũ.
- Subject Class không đổi sau khi có Assignment.
- Không public register.

## 50. Tests

- Create Student transaction rollback nếu class invalid.
- Teacher cannot manage another Teacher’s Class.
- Duplicate username.
- rowVersion conflict.
- Goal range.
- Cross-tenant IDs.

## 51. Definition of Done

- Endpoint 12–35 liên quan organization khớp contract.
- UI quản lý hiển thị loading/empty/error.
- Tạo được Center Manager → Teacher → Student → Class flow.
- Không có Dashboard aggregate giả.

## 52. Commit gate

Chạy flow quản trị bằng API và UI, restart containers, dữ liệu còn nguyên.

# Phase P08 — Subject và Knowledge Graph

## 53. Mục tiêu

CRUD Subject/Knowledge Graph và bảo đảm DAG.

Dependencies: P07.

## 54. Tasks

### P08-T01 — Subject service/API

CRUD theo contract; chặn xóa khi có evidence.

### P08-T02 — Node service/API

- Create/list/update/delete.
- Node hierarchy cycle detection.
- Topic validation.

### P08-T03 — Edge service/API

- Create/update/delete.
- PrerequisiteOf DAG cycle detection.
- Deterministic traversal.

### P08-T04 — Graph query

GET graph projection; tránh N+1.

### P08-T05 — Graph management UI

MVP có thể dùng danh sách/tree thay vì graph canvas phức tạp. Phải cho Teacher tạo node/edge và thấy validation cycle.

## 55. Algorithm

Cycle detector:

- Xét subgraph cùng Center + Subject.
- Khi thêm source → target, kiểm tra có path target → source hay không.
- Từ chối self-loop.
- Traversal có visited set.
- Không phụ thuộc thứ tự record.

## 56. Unit tests

- Empty graph.
- Linear chain.
- Diamond DAG hợp lệ.
- Direct cycle.
- Indirect cycle.
- Self-loop.
- Cross-subject edge.
- Soft-deleted node.

## 57. Definition of Done

- Endpoint 36–43 khớp contract.
- Không tạo được cycle qua API hoặc seed path.
- Graph của Center A không chứa node Center B.
- Query graph không lộ deleted node.

## 58. Commit gate

Demo tạo Topic A → B → C, thử thêm C → A nhận 409 DAG_CYCLE_DETECTED.

# Phase P09 — Curriculum và Question Bank

## 59. Mục tiêu

Cho Teacher tạo curriculum thủ công và Question thuộc ba loại.

Dependencies: P08.

## 60. Tasks

### P09-T01 — Curriculum

- CRUD/publish.
- Ordered node mappings.
- Assign Classes cùng Subject.
- Không upload.

### P09-T02 — Question aggregate

- Create/update/list/get.
- Options và knowledge mappings.
- Activate/archive.
- Teacher-facing và Student-facing projections tách biệt.

### P09-T03 — Grading criteria value object

- Validate schemaVersion/requiredIdeas/commonErrors/scoringNotes.
- JSON round-trip.

### P09-T04 — Deterministic preliminary grader

- MultipleChoice: option/canonical label.
- ShortAnswer: normalized exact/canonical comparison ở mức MVP.
- Essay: isCorrect có thể null hoặc theo criteria đã định; AI phân tích reasoning sau.
- Không dùng AI tại Controller.

### P09-T05 — UI

- Curriculum editor cơ bản.
- Question list/filter.
- Question editor thay đổi theo QuestionType.
- Không hiển thị LaTeX editor/upload.

## 61. Business rules

- Active MultipleChoice có ít nhất 2 options và đúng 1 correct.
- correctAnswer/solution không bao giờ nằm trong Student DTO trước submit.
- Question active mới được giao.
- Primary topic phải là Topic cùng Subject.
- reasoningRequired mặc định true.

## 62. Unit tests

- Mỗi QuestionType valid/invalid.
- Multiple correct options bị từ chối.
- Topic type mismatch.
- Student projection không lộ answer.
- Teacher ownership.
- Grading criteria parser.

## 63. Definition of Done

- Endpoint 44–48 khớp contract.
- Seed 30 logical Questions đọc được.
- Teacher tạo đủ ba loại Question.
- Student-facing projection không lộ đáp án.

## 64. Commit gate

Demo tạo và activate một câu mỗi loại, xem dưới Teacher/Student role.

# Phase P10 — Assignment Workflow

## 65. Mục tiêu

Cho Teacher tạo Assignment, chọn Questions, giao toàn Class/nhóm Student và theo dõi Progress.

Dependencies: P09.

## 66. Tasks

### P10-T01 — Assignment draft

- Create/update/list/get.
- Validate Class ownership.
- Validate Question active và cùng Subject.

### P10-T02 — Publish transaction

Trong một transaction:

1. Validate Draft + rowVersion.
2. Chốt ordered Questions.
3. Materialize Assignment Targets.
4. Tạo Student Assignment Progress.
5. Set Published/published_at.

### P10-T03 — Close và overdue semantics

- Close thủ công.
- Overdue là derived/projection hoặc cập nhật có kiểm soát; không cần Quartz.

### P10-T04 — Student assignment query

- Chỉ target Student.
- Student-facing Question DTO.
- Không lộ answer.

### P10-T05 — Assignment UI

- Teacher wizard: Class → Questions → Target → Review → Publish.
- Student assignment list/detail.
- Teacher progress table.

## 67. Logic cần chú ý

- Target WholeClass lấy active membership tại thời điểm publish.
- SelectedStudents phải thuộc Class.
- GapGroup UI gửi snapshot Student IDs.
- Membership bị remove sau publish không xóa Target.
- Published Assignment không sửa Questions/Targets; muốn đổi phải tạo Assignment mới hoặc policy change được duyệt.

## 68. Unit tests

- Publish toàn Class.
- Publish subset.
- Empty Question/Target bị từ chối.
- Student ngoài Class bị từ chối.
- Question khác Subject bị từ chối.
- Publish hai lần.
- Transaction rollback nếu một target invalid.

## 69. Definition of Done

- Endpoint 49–51 khớp contract.
- Progress total snapshot đúng.
- Student chỉ thấy Assignment được giao.
- Teacher khác không xem/sửa Assignment.

## 70. Commit gate

Demo Teacher tạo Assignment 3 Questions cho 5 Students và Student thấy đúng nội dung.

# Phase P11 — Attempt Submission và Durable AI Job

## 71. Mục tiêu

Thiết lập luồng async hoàn chỉnh nhưng chưa gọi Gemini thật: submit, persist, job state, polling và Rule-based stub.

Dependencies: P10.

## 72. Tasks

### P11-T01 — Attempt validation

- Student/Question/Assignment ownership.
- reasoning_required.
- Input limits.
- clientSubmissionId idempotency.
- Preliminary grading.

### P11-T02 — Attempt + Job transaction

- Insert Attempt PendingAnalysis.
- Insert Job Pending.
- Update Assignment Progress sang InProgress.
- Return 202.

### P11-T03 — Job state machine

Allowed transitions:

~~~text
Pending → Processing
Processing → Completed
Processing → Pending (retry một lần)
Processing → FallbackCompleted
Processing → FailedTerminal
Expired Processing lease → Pending
~~~

Mọi transition khác bị từ chối.

### P11-T04 — BackgroundService foundation

- Poll/claim job.
- Create scoped service.
- Rehydrate tenant context từ job.center_id.
- Lease/rowVersion.
- Graceful cancellation.
- Recovery expired lease.

### P11-T05 — Temporary Rule-based processor

Để checkpoint chạy end-to-end trước Gemini:

- Tạo fallback Reasoning Analysis.
- reasoning_quality null.
- needs_teacher_review true.
- Chưa cập nhật Twin cho đến P13; có thể chỉ hoàn tất Attempt/Analysis/Job.

Đây không phải temporary endpoint và phải được thay bằng orchestration chính ở P12/P13.

### P11-T06 — Polling và Attempt summary API

- GET job status.
- GET attempts.
- Feedback endpoint hoàn chỉnh được hoãn đến P14 vì contract cuối cần cả twinChange và Recommendation.

## 73. Logic/transactions

- Không gọi external service trong transaction.
- Idempotent submit.
- Unique Analysis/Job theo Attempt.
- Hai worker không xử lý cùng job.
- Job terminal không chạy lại.

## 74. Unit tests

- Duplicate same payload.
- Duplicate changed payload.
- Job transitions.
- Lease expiry.
- Worker tenant scope.
- Cancellation.
- Rule fallback contract.

## 75. Definition of Done

- POST Attempt trả 202/pollUrl.
- Polling đi đến FallbackCompleted trong checkpoint.
- Restart API không mất Pending Job.
- Attempt/Job không duplicate.
- Không có in-memory-only queue là nguồn dữ liệu duy nhất.

## 76. Commit gate

Submit một Attempt, restart API giữa chừng, xác nhận job được recover và terminal.

# Phase P12 — Gemini Reasoning Analysis

## 77. Mục tiêu

Tích hợp Gemini qua IAIService, structured JSON, retry một lần và fallback.

Dependencies: P11.

## 78. Tasks

### P12-T01 — AI contracts

- IAIService.
- AnalyzeReasoning request/response typed.
- schemaVersion ai-analysis-v1.
- Provider-agnostic BLL.

### P12-T02 — Gemini adapter

- Build minimal prompt/context.
- Structured output JSON Schema.
- Timeout/cancellation.
- Không log secret.
- Model/API key từ configuration.

### P12-T03 — Parser và semantic validator

- Strict JSON.
- Enum/range.
- Root cause node tồn tại/cùng tenant/subject.
- Feedback cùng language.
- Không chấp nhận hallucinated node ID.

### P12-T04 — Retry/fallback orchestration

- First failure → one retry.
- Second failure → Rule-based fallback.
- Retry count persisted.
- Attempt/Job status đúng.

### P12-T05 — Observability

Log:

- correlationId.
- centerId, attemptId, jobId.
- provider/model.
- latency.
- token usage nếu provider trả.
- outcome/error code.

Raw payload mặc định không log.

## 79. Logic cần chú ý

- Gemini output không phải trusted data.
- Không cập nhật Knowledge Graph.
- Không lưu raw response trong DB.
- AI availability không ảnh hưởng readiness.
- Một API key; không rotation.

## 80. Unit tests

Mock IAIService:

- Valid output.
- Malformed JSON.
- Out-of-range quality.
- Invalid enum.
- Cross-tenant/root node.
- Timeout first call then success.
- Fail twice then fallback.
- Language vi/en.

## 81. Definition of Done

- Gemini thật có thể tạo Completed job.
- Khi tắt key/mô phỏng lỗi, flow FallbackCompleted.
- reasoning_analyses đúng contract.
- Retry đúng một lần.
- Log không có API key/JWT/password.

## 82. Commit gate

Chạy hai demo:

1. Gemini success.
2. Forced failure → fallback + review queue flag.

# Phase P13 — Digital Twin Engine

## 83. Mục tiêu

Hoàn thiện Mastery, Behavior, Risk, Twin History và transaction hoàn tất job.

Dependencies: P12.

## 84. Tasks

### P13-T01 — Mastery calculator

Pure function theo CONSTITUTION.md:

- Normalize inputs.
- Reasoning path.
- Fallback path.
- Difficulty multiplier.
- Clamp.
- Calculation version mastery-v1.
- Human-readable explanation.

### P13-T02 — Time/confidence factors

Time quality:

- 1.00 nếu 50%–150% estimated time.
- 0.60 nếu >150% đến 200%.
- 0.30 nếu >200%.
- 0 nếu skipped.

Confidence calibration:

~~~text
1 - Abs(confidence/100 - correctness)
~~~

Nếu correctness null, K=0.5 neutral.

### P13-T03 — Behavior Twin updater

Incremental aggregate hoặc deterministic query phải cho cùng kết quả:

- average time.
- skip rate.
- change answer rate.
- average confidence.
- calibration.
- attempt count.

### P13-T04 — Risk calculator

- Weighted topic mastery.
- Predicted score.
- Score gap/time pressure.
- Risk v1.

### P13-T05 — Completion transaction

Sau Analysis:

- Upsert Knowledge/Behavior Twin.
- Update Goal/Risk.
- Insert History.
- Update Student Twin aggregate.
- Update Assignment Progress.
- Mark Attempt/Job terminal.

Recommendation chưa tạo đến P14; transaction interface phải có extension point rõ.

### P13-T06 — Twin/history query và feedback mapping

Hoàn thiện Student Twin/History endpoint và mapping twinChange. Feedback endpoint chỉ được đánh dấu contract-complete ở P14 sau khi Recommendation đã tồn tại.

## 85. Numeric acceptance scenarios

Difficulty D=1.0:

| Scenario | M cũ | R | C | T | K | M mới kỳ vọng |
|---|---:|---:|---:|---:|---:|---:|
| Đúng nhưng reasoning kém | 0 | 0.20 | 1 | 1 | 1 | 5.00 |
| Đúng, reasoning tốt | 50 | 0.80 | 1 | 1 | 1 | 57.50 |
| Fallback đúng | 0 | null | 1 | 1 | — | 2.50 |
| Fallback sai/skipped | 40 | null | 0 | 0 | — | 36.00 |

Sai số decimal cho test: tối đa 0.01.

## 86. Unit tests

- R boundary 0/39/40/59/60/79/80/100/null.
- Difficulty 1–5.
- Clamp 0/100.
- Mastery tăng/giảm.
- Behavior aggregate.
- Risk remainingDays 0/180/>180.
- No Topic evidence.
- Transaction rollback.
- History breakdown/explanation.

## 87. Definition of Done

- BLL lõi coverage >=80% cho Mastery/Risk/Behavior.
- Feedback hiển thị delta/explanation.
- History append-only.
- AI completion không mở transaction khi đang gọi Gemini.
- Job terminal chỉ sau commit thành công.

## 88. Commit gate

Submit nhiều Attempt với reasoning khác nhau và đối chiếu Mastery/History bằng API/Adminer.

# Phase P14 — Opportunity Gap, Recommendation, Learning Path

## 89. Mục tiêu

Đề xuất Topic và Question tiếp theo bằng heuristic giải thích được.

Dependencies: P13.

## 90. Tasks

### P14-T01 — Candidate builder

- Active Topics.
- Mastery <80.
- Prerequisite readiness >=60.
- Exclude invalid/deleted nodes.

### P14-T02 — Linear fallback

Dưới 3 Attempts trong Subject:

- order_index.
- difficulty.
- chưa làm/gần đây.

### P14-T03 — Opportunity calculator

- Expected Score Gain.
- Recent Reasoning Average.
- Prerequisite Readiness.
- Learning effort.
- Normalize 0–100.
- Tie-break deterministic.
- calculation version opportunity-v1.

### P14-T04 — Question selector

- Topic đã chọn.
- Active.
- Difficulty phù hợp Mastery.
- Tránh lặp Question gần đây.
- Nếu hết Question, vẫn trả Topic với questionId null và explanation.

### P14-T05 — Persistence

- Supersede Recommendation cũ.
- Insert Recommendation mới.
- Regenerate active Learning Path/version.
- Tích hợp vào completion transaction P13.

### P14-T06 — API

- next-question.
- active recommendation.
- accept/dismiss.
- active learning path.
- Hoàn thiện GET Attempt feedback đúng toàn bộ contract gồm analysis, twinChange và recommendation.

## 91. Unit tests

- Attempt count 0/2/3.
- No prerequisites.
- Unmet prerequisites.
- All Topics mastery >=80.
- Equal scores/tie-break.
- Zero/very low estimated effort.
- No Question available.
- Recommendation supersede.
- Cross-tenant candidates excluded.

## 92. Definition of Done

- Dưới 3 Attempts dùng LinearFallback.
- Attempt thứ 3 chuyển OpportunityGap.
- Response có breakdown/explanation.
- Recommendation không chọn Topic khóa bởi prerequisite.
- BLL lõi Opportunity coverage >=80%.

## 93. Commit gate

Demo cùng Student trước/sau Attempt thứ 3 và giải thích vì sao Topic được chọn.

# Phase P15 — Student Application

## 94. Mục tiêu

Hoàn thiện trải nghiệm Student từ đăng nhập đến Dashboard cập nhật sau AI.

Dependencies: P14.

## 95. Tasks

### P15-T01 — Student shell

- Navigation tiếng Việt.
- Subject selector.
- Profile/logout.
- Protected route theo role.

### P15-T02 — Assignment list/detail

- Loading/empty/error.
- Progress.
- Student-safe Question.

### P15-T03 — Learning player

- Render ba QuestionType.
- finalAnswer + reasoningText.
- Time/confidence/answerChanges.
- Client submission UUID.
- Disable duplicate submit.

### P15-T04 — Job polling

- TanStack Query refetchInterval 3000.
- Stop terminal.
- Survive component remount bằng Attempt/Job ID trong route/state hợp lý.
- Status message tiếng Việt.
- Retry fetch status, không resubmit Attempt.

### P15-T05 — Feedback

- Correctness/score.
- Reasoning quality/error/feedback.
- Twin delta/explanation.
- Recommendation action.
- Fallback/needs review warning.

### P15-T06 — Student Dashboard

- Header Goal/Remaining Days.
- Radar Mastery.
- Line History.
- Opportunity Action.
- Text fallback cho charts.

## 96. Student wireframes

### Dashboard

~~~text
┌─────────────────────────────────────────────────────────────────┐
│ EduTwin | Môn: [Toán ▼]                         [Đăng xuất]      │
├───────────────────────────────┬─────────────────────────────────┤
│ Mục tiêu: 8.5                 │ Còn lại: 120 ngày               │
│ Dự báo: 5.2                   │ Risk: 26.4                      │
├───────────────────────────────┼─────────────────────────────────┤
│ Radar: Mastery theo Topic     │ Line: Tiến bộ theo thời gian    │
│ [chart + danh sách số liệu]   │ [chart + danh sách số liệu]     │
├─────────────────────────────────────────────────────────────────┤
│ NÊN HỌC TIẾP: Mũ và Logarit — Opportunity 82.4                 │
│ Vì: cơ hội tăng điểm cao, prerequisite đã đạt                   │
│                                      [Làm câu tiếp theo]        │
└─────────────────────────────────────────────────────────────────┘
~~~

### Learning Player

~~~text
┌─────────────────────────────────────────────────────────────────┐
│ Bài luyện: Mũ và Logarit             Câu 1/3 | 02:45            │
├─────────────────────────────────────────────────────────────────┤
│ Nội dung câu hỏi                                                │
│ ( ) A   (•) B   ( ) C   ( ) D                                  │
│                                                                 │
│ Trình bày cách giải                                             │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ Em đặt điều kiện rồi...                                     │ │
│ └─────────────────────────────────────────────────────────────┘ │
│ Mức tự tin: [80%]                              [Nộp bài]        │
├─────────────────────────────────────────────────────────────────┤
│ Đang phân tích tư duy... trạng thái Processing                  │
└─────────────────────────────────────────────────────────────────┘
~~~

## 97. UI acceptance

- Không có nút/menu tiếng Anh trừ nội dung học.
- Không lộ correct answer trước submit.
- Polling dừng terminal.
- Refresh page không tạo Attempt mới.
- Chart vẫn đọc được khi Recharts lỗi/không có data.
- Mobile tối thiểu không overflow nghiêm trọng.
- Error có traceId khi cần hỗ trợ.

## 98. Definition of Done

- Kịch bản Student end-to-end chạy.
- Không lưu refresh token phía JS.
- Zustand không duplicate server state.
- TanStack Query cache key có subject/user context.
- Loading/empty/error/fallback states đủ.

## 99. Commit gate

Quay/ghi checklist demo Student từ login đến Dashboard thay đổi.

# Phase P16 — Teacher Dashboard, Gap Groups và Override

## 100. Mục tiêu

Hoàn thiện analytics cho Teacher, giao bài từ Gap Group và Teacher Override có replay.

Dependencies: P15.

## 101. Tasks

### P16-T01 — Class Dashboard query

- Overview.
- High-risk Students.
- Weak Topics.
- Dynamic Gap Groups.
- Không N+1.

### P16-T02 — Review queue

- Chỉ Analysis needs review trong Class owner.
- Filter/pagination.
- Hiển thị Question/Submission/AI result.

### P16-T03 — Override service

- Validate overrideVersion.
- Lưu override fields.
- Effective values.
- Ownership.

### P16-T04 — Deterministic replay

Trong transaction:

1. Load Attempts Student + Topic theo created_at/attempt_id.
2. Baseline Mastery 0.
3. Apply effective analysis tuần tự.
4. Rebuild Knowledge Twin.
5. Recompute Behavior/Goal/Risk.
6. Insert History TeacherOverride/Replay.
7. Supersede/recompute Recommendation/Path.
8. Commit override version.

### P16-T05 — Teacher UI

- Class selector.
- Summary cards.
- High-risk table.
- Weak Topic Bar chart.
- Gap Group list + “Giao bài tập”.
- Review queue + Override form/result.

## 102. Teacher wireframe

~~~text
┌─────────────────────────────────────────────────────────────────┐
│ Teacher Dashboard | Lớp: [Toán 12A ▼]                          │
├─────────────┬─────────────┬─────────────┬──────────────────────┤
│ Sĩ số 30    │ Dự báo 6.15 │ Mastery 61% │ Hoàn thành 78%       │
├───────────────────────────────┬─────────────────────────────────┤
│ Học sinh rủi ro cao           │ Topic yếu — Bar chart           │
│ An 73.1 | Bình 72.1           │ Logarit 42%                     │
├─────────────────────────────────────────────────────────────────┤
│ Gap Group: 5 em dưới 60% ở Logarit   [Giao bài cho nhóm]        │
├─────────────────────────────────────────────────────────────────┤
│ Hàng chờ giáo viên duyệt: 3                                  → │
└─────────────────────────────────────────────────────────────────┘
~~~

## 103. Unit tests

- Dashboard tính Student no evidence là 0.
- Teacher khác bị chặn.
- Gap Group threshold.
- Override conflict.
- Replay order deterministic.
- Replay same input same output.
- Transaction rollback không để half-updated Twin.
- Recommendation được tính lại.

## 104. Definition of Done

- Endpoint 59–62 khớp contract.
- Override không xóa AI result gốc.
- Replay result có explanation/history.
- Nút Gap Group tạo Assignment prefilled Student IDs.
- Dashboard query acceptable với seed/demo scale.

## 105. Commit gate

Demo fallback item → Teacher Override → Mastery/Risk/Recommendation thay đổi trên Student và Teacher Dashboard.

# Phase P17 — Center Manager Dashboard và B2B SaaS Proof

## 106. Mục tiêu

Hoàn thiện Center Dashboard mức vừa phải và chứng minh Multi-tenant.

Dependencies: P16.

## 107. Tasks

### P17-T01 — Center aggregate query

- Teacher/Student/Class counts.
- Mastery by Subject.
- High-risk count by Class.
- Class ranking.

### P17-T02 — Center Dashboard UI

- Summary cards.
- Mastery by Subject chart/table.
- High-risk by Class.
- Class ranking.
- Subject filter.

### P17-T03 — Tenant isolation demonstration

- Seed Center A/B.
- Login switch.
- Cache clear theo logout/tenant.
- Cross-tenant URL/ID không lộ data.

### P17-T04 — Management navigation

Liên kết Center Dashboard với Teacher/Class/Student management P07.

## 108. Center wireframe

~~~text
┌─────────────────────────────────────────────────────────────────┐
│ Center Dashboard | EduTwin Center A | Môn: [Tất cả ▼]          │
├──────────────┬──────────────┬──────────────────────────────────┤
│ Teachers: 8  │ Students:120 │ Classes:10                       │
├───────────────────────────────┬─────────────────────────────────┤
│ Mastery trung bình theo môn   │ High-risk theo lớp              │
│ Toán 61% | Anh 58%            │ Toán 12A: 7/30                  │
├─────────────────────────────────────────────────────────────────┤
│ Xếp hạng lớp                                                   │
│ 1. Toán 12A | Mastery 71.4 | Hoàn thành 86.7%                 │
└─────────────────────────────────────────────────────────────────┘
~~~

## 109. Logic

- Không đánh giá Teacher bằng AI.
- Aggregate bao gồm Student chưa có evidence theo quy tắc contract.
- Query filter tenant trước group.
- Frontend query cache bị xóa khi logout/login tenant khác.

## 110. Definition of Done

- Endpoint 12–14 hoàn chỉnh.
- Center A/B có số liệu riêng.
- Cross-tenant ID trả 404.
- Dashboard đúng bốn nhóm thông tin đã khóa.

## 111. Commit gate

Demo song song hai Center bằng hai session/browser profile; không có data leakage.

# Phase P18 — Hardening, Coverage, CI và MVP Release

## 112. Mục tiêu

Đưa hệ thống từ feature-complete thành demo-ready, reproducible và có thể review.

Dependencies: P17.

## 113. Tasks

### P18-T01 — Contract audit

Đối chiếu:

- Endpoint/method/status.
- JSON field/type/enum.
- Authorization.
- Database schema/migration.
- Student-safe projections.

### P18-T02 — Test coverage

Ưu tiên:

- Mastery.
- Risk.
- Opportunity.
- AI parser.
- DAG.
- Job state.
- Override replay.
- Tenant guards.

BLL lõi >=80%.

### P18-T03 — Security review

- No secret in Git/log.
- Password/refresh hash.
- Cookie policy.
- CORS allow-list.
- JWT validation.
- Cross-tenant attempts.
- Over-posting/unknown centerId.

### P18-T04 — Reliability review

- Docker clean start.
- MySQL restart.
- API restart with Pending/Processing job.
- Gemini timeout/failure.
- Duplicate submit.
- Concurrent override.

### P18-T05 — Performance sanity

Với demo data:

- Dashboard không N+1 rõ ràng.
- Index query trọng yếu được kiểm tra EXPLAIN.
- Polling endpoint nhẹ.
- Job worker có batch/delay hợp lý, không busy loop.

Không đặt performance SLA giả nếu chưa đo.

### P18-T06 — UX polish

- Responsive.
- Empty/error/loading.
- Vietnamese copy.
- Chart legends/tooltips/text fallback.
- Demo accounts rõ.

### P18-T07 — CI final

- Pull Request main chạy build/test.
- Không CD.
- README hiện hữu có quick start, demo accounts lấy từ env/seed policy và link năm specification.

### P18-T08 — Release rehearsal

Chạy toàn bộ kịch bản mục 2 từ môi trường sạch ít nhất hai lần.

## 114. Final Definition of Done

- docker compose up --build chạy bốn service.
- Migration + seed thành công.
- Ba role login.
- Full learning flow success với Gemini.
- Full learning flow fallback khi Gemini lỗi.
- Twin/History/Recommendation cập nhật.
- Teacher Override/replay hoạt động.
- Ba Dashboard đúng contract.
- Center A/B cách ly.
- BLL core coverage >=80%.
- CI xanh.
- Không endpoint/schema drift.
- Không secret.
- Không tính năng ngoài scope làm cản trở demo.

## 115. Release gate

Chỉ gắn tag mvp-v1.0.0 khi Codex review:

- Architecture.
- Database.
- API contract.
- Unit tests.
- Tenant isolation.
- Demo checklist.

## 116. Rollback

- Source rollback theo commit Phase.
- Migration rollback chỉ Development và có backup/volume policy.
- Không dùng git reset --hard để xử lý lỗi review.
- Nếu Phase fail, giữ Phase trước làm stable checkpoint.

# UI/UX Specification tích hợp

## 117. Hệ thống màn hình

### Public/Auth

- Đăng nhập.
- Trạng thái phiên hết hạn.

### Center Manager

- Center Dashboard.
- Teacher list/create/edit.
- Student list/create/edit.
- Class list/create/edit/membership.
- Subject list.

### Teacher

- Teacher Dashboard theo Class.
- Student list/detail Twin.
- Knowledge Graph manager.
- Curriculum manager.
- Question Bank/editor.
- Assignment wizard/progress.
- AI Review Queue/Override.

### Student

- Student Dashboard theo Subject.
- Assignment list/detail.
- Learning Player.
- Analysis processing.
- Feedback.
- Twin History/Learning Path.

## 118. Design system tối thiểu

- Desktop-first nhưng usable trên tablet/mobile.
- Màu trạng thái nhất quán:
  - Success/Strong: xanh lá.
  - Warning/Acceptable: vàng/cam.
  - Risk/Poor: đỏ.
  - Neutral/Unknown: xám.
- Không dùng màu là tín hiệu duy nhất; luôn có text/icon.
- Number format nhất quán 0–100 và 0–10.
- Chart tooltip hiển thị số chính xác tối đa 2 decimal.
- Form có label, validation message, disabled/submitting state.
- Confirm action cho publish, close, archive, override.

## 119. UI state matrix

Mọi page data-driven phải có:

| State | Yêu cầu |
|---|---|
| Loading | Skeleton/spinner có nhãn |
| Empty | Giải thích và action phù hợp |
| Error | Thông báo tiếng Việt; traceId nếu có |
| Success | Data + last generated/updated time |
| Stale/Polling | Không xóa data cũ; hiển thị đang cập nhật |
| Forbidden | Điều hướng an toàn, không lộ resource |

# Traceability

## 120. Requirement → Phase

| Requirement | Phase |
|---|---|
| .NET Modular Monolith 3 Layer | P01 |
| Docker 4 services | P02 |
| 5 DB modules | P03–P05 |
| Multi-tenant | P03, P06, P17, P18 |
| JWT + Refresh | P06 |
| Center/Teacher/Student/Class | P07 |
| Knowledge DAG | P08 |
| Three Question types | P09 |
| Assignment/Gap target | P10, P16 |
| Attempt + reasoning_text | P11 |
| BackgroundService + polling | P11, P15 |
| Gemini structured JSON | P12 |
| Rule fallback | P11–P12 |
| Knowledge/Behavior Twin | P13 |
| Risk | P13 |
| Twin History | P13 |
| Opportunity Gap | P14 |
| Student Dashboard | P15 |
| Teacher Dashboard | P16 |
| Teacher Override replay | P16 |
| Center Dashboard | P17 |
| BLL coverage >=80% | P13–P14, P16, P18 |
| CI build/test | P01, P18 |

## 121. Table → Phase

| Tables | Phase |
|---|---|
| centers, users, refresh_tokens, teachers, students, subjects, classes, class_students | P03 |
| knowledge_nodes, knowledge_edges | P04 |
| curriculums, mappings, questions, options, assignments, targets/progress | P04 |
| goals, twins, history, paths, recommendations | P05 |
| attempts, reasoning_analyses, ai_analysis_jobs | P05 |
| Seed data | P05 |

## 122. Endpoint → Phase

| Endpoint group | Phase |
|---|---|
| /auth | P06 |
| /centers/me profile | P07 |
| /teachers, /students, /classes, /goals | P07 |
| /subjects, /knowledge | P08 |
| /curriculums, /questions | P09 |
| /assignments | P10 |
| /learning attempts/jobs/feedback | P11–P13 |
| /learning next-question | P14 |
| /students/me dashboard/twin/path | P13–P15 |
| Teacher dashboard/review/override | P16 |
| Center dashboard | P17 |

# Risk Register

## 123. Rủi ro và biện pháp

| Rủi ro | Xác suất | Tác động | Giảm thiểu | Phase kiểm soát |
|---|---|---|---|---|
| Scope mở rộng tất cả môn | Cao | Cao | Generic architecture, chỉ seed Toán/Anh | P05 |
| Provider EF MySQL chưa tương thích | Trung bình | Cao | Verify compatibility trước P03 | P03 |
| Tenant leakage | Trung bình | Rất cao | Filter + composite FK + guard + tests | P03/P06/P18 |
| Gemini timeout/JSON sai | Cao | Cao | Structured output, retry 1, fallback | P12 |
| Background job xử lý trùng | Trung bình | Cao | Lease + rowVersion + unique attempt | P11 |
| Teacher Override làm sai Twin | Trung bình | Cao | Replay deterministic + transaction | P16 |
| Dashboard query chậm/N+1 | Trung bình | Trung bình | Projection/index/EXPLAIN | P16–P18 |
| Correct answer lộ cho Student | Trung bình | Cao | Separate projection + contract audit | P09/P18 |
| Docker startup race | Trung bình | Trung bình | Healthcheck/service_healthy | P02 |
| AI Developer đổi schema | Cao | Cao | Frozen docs + prompt allow-list | Mọi Phase |
| Demo phụ thuộc internet/Gemini | Cao | Cao | Fallback path demo-ready | P12/P18 |

# Review Protocol

## 124. Review sau mỗi Task

Codex kiểm tra:

1. Scope/file allow-list.
2. Architecture direction.
3. Schema/API drift.
4. Tenant/authorization.
5. Transaction/idempotency.
6. Error handling.
7. Test adequacy.
8. Secret/logging.
9. Build/test evidence.

Kết quả review:

- APPROVED.
- APPROVED WITH NON-BLOCKING NOTES.
- CHANGES REQUIRED.
- BLOCKED BY SPECIFICATION.

## 125. Review sau mỗi Phase

- Task checklist.
- Docker/local checkpoint.
- Migration state.
- API smoke.
- Unit tests.
- Git diff/commit scope.
- Không bắt đầu Phase sau nếu Phase trước CHANGES REQUIRED.

# Final Demo Script

## 126. Chuẩn bị

- Clean clone.
- .env từ template.
- Docker Compose up.
- Migration/seed.
- Center A/B accounts.
- Gemini success mode và forced fallback switch cấu hình cho demo.

## 127. Demo 1 — Multi-tenant

- Login Center A, ghi nhận counts.
- Login Center B, counts khác.
- Dùng ID Center A trong token Center B → 404.

## 128. Demo 2 — Learning AI success

- Teacher publish Assignment.
- Student submit answer + reasoning.
- UI polling.
- Feedback AI.
- Twin Radar/Line thay đổi.
- Opportunity recommendation xuất hiện.

## 129. Demo 3 — Fallback và Override

- Force Gemini failure.
- Attempt FallbackCompleted.
- Review queue.
- Teacher Override.
- Replay cập nhật Twin/Risk/Recommendation.

## 130. Demo 4 — Management analytics

- Teacher Dashboard high-risk/weak Topic/Gap Group.
- Giao Assignment từ Gap Group.
- Center Dashboard aggregate.

## 131. Tiêu chí dừng

Không thêm tính năng mới trong tuần/ngày chốt demo. Chỉ sửa:

- Bug blocker.
- Data/contract mismatch.
- Tenant/security defect.
- Demo reproducibility.
- UI readability nghiêm trọng.
