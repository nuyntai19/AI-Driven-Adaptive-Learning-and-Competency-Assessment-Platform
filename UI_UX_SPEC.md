# EduTwin — UI/UX and Capability Specification

> Phiên bản: 2.1-draft
> Trạng thái: COURSE REBASELINE — cần Figma và stakeholder validation
> Ngôn ngữ UI: Tiếng Việt
> Security boundary: ASP.NET Core API, không phải React UI
> Chủ sở hữu: Frontend/UX owner; stakeholder phê duyệt workflow

## 1. Mục tiêu

UI phải giúp đúng người hoàn thành đúng công việc, đồng thời phản ánh effective permission. Việc ẩn menu/nút không thay thế authorization server-side.

Mọi màn hình phải có:

- loading state;
- empty state;
- validation state;
- recoverable error state;
- unauthorized/forbidden state;
- concurrency conflict state khi mutation dùng row version;
- success feedback không làm mất dữ liệu đang nhập;
- keyboard focus và label hợp lý.

## 2. Nguyên tắc thiết kế

1. Capability first: quyết định hiển thị dựa trên permission, không so sánh role name rải rác.
2. Tenant clear: luôn hiển thị center hiện hành; không cho người dùng nhập center ID.
3. Safe mutation: thao tác nguy hiểm cần confirmation và mô tả hậu quả.
4. Explainable AI: phân biệt AI output, Evidence Gate decision và teacher override.
5. Progressive disclosure: dashboard ưu tiên hành động; chi tiết calculation mở khi cần.
6. Accessible by default: semantic HTML, keyboard, label, focus, contrast và aria-live.
7. Responsive: desktop cho quản trị/bảng lớn; mobile vẫn hoàn tất luồng học sinh.
8. No secret persistence: không lưu refresh token, password hoặc API key trong browser storage.
9. AI optional by operation: các màn hình quản lý, giao/nộp bài, chấm sơ bộ và fallback recommendation không bị khóa chỉ vì Gemini/Internet unavailable.

Ba câu hỏi định hướng thông tin, phải được kiểm chứng trong Figma walkthrough:

- Student: “Em yếu ở đâu, nên làm gì tiếp theo và có nguy cơ không đạt mục tiêu không?”
- Teacher: “Ai cần hỗ trợ, lỗi chung là gì và nên giao gì tiếp theo?”
- CenterManager: “Ai được làm gì, lớp nào cần chú ý và toàn Center đang đạt mục tiêu ra sao?”

## 3. Auth user và capability model

Frontend auth state mục tiêu:

~~~text
AuthUser
├── userId
├── centerId
├── centerName
├── accountType
├── roles[]
├── permissions[]
├── authorizationVersion
└── status
~~~

authorizationVersion là projection của duy nhất users.auth_version. Frontend không tự tăng, không lưu một version song song và phải bootstrap lại session khi server báo AUTHORIZATION_VERSION_STALE.

Các helper chuẩn:

~~~text
hasPermission(code)
hasAnyPermission(codes)
canAccessRoute(route)
canActOn(resourceScope)
~~~

Không tạo điều kiện mới kiểu user.role === CenterManager trong page/component sau khi module đã migrate.

## 4. Sitemap

~~~text
/dang-nhap
/
├── /lop-hoc
│   ├── /lop-hoc/:id
│   └── /lop-hoc/:id/hoc-sinh
├── /giao-vien
├── /hoc-sinh
├── /mon-hoc
├── /so-do-kien-thuc
├── /chuong-trinh
├── /ngan-hang-cau-hoi
├── /bai-tap
│   ├── /bai-tap/tao
│   ├── /bai-tap/:id
│   └── /bai-tap/:id/chinh-sua
├── /hoc-tap
│   ├── /hoc-tap/bai-tap
│   └── /hoc-tap/bai-tap/:id
├── /phan-tich-can-xem-xet
├── /digital-twin
├── /khuyen-nghi
├── /quan-tri-quyen
│   ├── /quan-tri-quyen/vai-tro
│   ├── /quan-tri-quyen/vai-tro/:id
│   ├── /quan-tri-quyen/nguoi-dung
│   └── /quan-tri-quyen/nhat-ky
└── /cai-dat
~~~

Route không có permission phải đưa về trang Không có quyền, không âm thầm chuyển về dashboard làm người dùng hiểu nhầm.

## 5. Permission catalog cho UI

Permission catalog do backend trả. Nhóm code khởi tạo:

### Organization

- organization.center.read
- organization.center.update
- organization.teachers.read
- organization.teachers.create
- organization.teachers.update
- organization.teachers.delete
- organization.students.read
- organization.students.create
- organization.students.update
- organization.students.delete
- organization.classes.read
- organization.classes.create
- organization.classes.update
- organization.classes.manage_members

### Academic content

- knowledge.subjects.read
- knowledge.subjects.create
- knowledge.subjects.update
- knowledge.subjects.delete
- knowledge.nodes.read
- knowledge.nodes.create
- knowledge.nodes.update
- knowledge.nodes.delete
- knowledge.edges.read
- knowledge.edges.create
- knowledge.edges.update
- knowledge.edges.delete
- curriculum.curriculums.read
- curriculum.curriculums.create
- curriculum.curriculums.update
- curriculum.curriculums.publish
- curriculum.questions.read
- curriculum.questions.create
- curriculum.questions.update
- curriculum.questions.publish

### Assignment and learning

- assignments.assignments.read
- assignments.assignments.create
- assignments.assignments.update
- assignments.assignments.publish
- assignments.assignments.close
- learning.attempts.submit
- learning.attempts.read_own
- learning.attempts.read_scoped

### AI, Twin and dashboard

- twin.reasoning.review
- twin.reasoning.override
- twin.student.read_own
- twin.student.read_scoped
- recommendations.student.read_own
- dashboards.student.read_own
- dashboards.teacher.read_scoped
- dashboards.center.read

### Authorization

- authorization.permissions.read
- authorization.roles.read
- authorization.roles.create
- authorization.roles.update
- authorization.roles.archive
- authorization.roles.manage_permissions
- authorization.user_roles.read
- authorization.user_roles.assign
- authorization.audit.read

Catalog này phải đồng bộ với API_CONTRACTS.md. Permission code chỉ đổi qua contract change.

## 6. Application shell

### 6.1. Header

- Logo/tên EduTwin.
- Center hiện hành.
- Tên user.
- Danh sách role ngắn gọn.
- Menu tài khoản/logout.
- Không hiển thị token hoặc raw identifier không cần thiết.

### 6.2. Navigation

- Menu được tạo từ route-to-permission map tập trung.
- Không duplicate permission condition trong nhiều component.
- Nếu user mất quyền trong phiên, lần refresh/401/authorization-version mismatch phải cập nhật menu.
- Deep link vẫn phải được API bảo vệ.

### 6.3. Unauthorized page

Hiển thị:

- Không có quyền thực hiện thao tác.
- Resource không được tiết lộ nếu backend trả 404 chống enumeration.
- Nút quay lại an toàn.
- Trace ID để hỗ trợ, không hiện stack trace.

## 7. Dynamic Role Management

### 7.1. Role list

Route: /quan-tri-quyen/vai-tro
Permission: authorization.roles.read

Hiển thị:

- tên/code role;
- account type;
- built-in/custom;
- active/archived;
- số permission;
- số user đang được gán;
- updated time và row version ẩn trong model;
- tìm kiếm, lọc account type/trạng thái và pagination.

Actions:

- Tạo role nếu có authorization.roles.create.
- Sửa nếu có authorization.roles.update.
- Archive nếu có authorization.roles.archive.
- Xem permission nếu chỉ có authorization.roles.read.

### 7.2. Create/Edit role

Fields:

- Role name.
- Role code.
- Account type, bắt buộc khi tạo và chỉ đọc khi sửa.
- Description.
- Status.
- Permission matrix nhóm theo module.

Validation:

- name/code required và giới hạn độ dài;
- code chuẩn hóa, unique trong center;
- actor không chọn permission mình không được phép ủy quyền;
- permission không tương thích account type của role bị ẩn khỏi lựa chọn hoặc disabled kèm giải thích;
- API vẫn xác minh compatibility; UI không tự suy diễn catalog;
- không submit khi không thay đổi;
- row-version conflict mở dialog tải lại/so sánh, không tự ghi đè.

Permission matrix:

- Có ô tìm kiếm.
- Nhóm theo module/resource.
- Hiển thị mô tả hành động.
- Hiển thị account type được phép cho từng permission.
- Permission nhạy cảm có cảnh báo.
- Preview số user bị ảnh hưởng trước khi lưu.

### 7.3. User-role assignment

Route: /quan-tri-quyen/nguoi-dung
Permissions: authorization.user_roles.read và authorization.user_roles.assign

Hiển thị:

- user, account type, status;
- role trực tiếp;
- effective permission;
- lần cập nhật gần nhất.

Khi lưu:

- chỉ chọn role trong cùng center và cùng account type với user;
- không gửi accountType từ form assignment; backend lấy từ user/role canonical;
- cảnh báo nếu thay đổi chính user hiện tại;
- chặn self-elevation;
- chặn làm mất CenterManager cuối cùng có đủ TenantAdminCorePermissionsV1;
- hiển thị session của target có thể phải đăng nhập lại;
- ghi audit.

TenantAdminCorePermissionsV1 gồm chín capability: authorization.permissions.read, authorization.roles.read/create/update/archive/manage_permissions, authorization.user_roles.read/assign và authorization.audit.read. UI có thể cảnh báo trước, nhưng backend mới là nơi mô phỏng effective permission sau mutation và quyết định 409 LAST_TENANT_ADMIN.

### 7.4. Effective permission preview

Phải phân biệt:

- permission được cấp bởi role nào;
- permission không có;
- resource vẫn bị ownership giới hạn;
- permission bị vô hiệu vì role/user archived.

V1 không hiển thị explicit deny vì mô hình không hỗ trợ deny.

### 7.5. Authorization audit

Route: /quan-tri-quyen/nhat-ky
Permission: authorization.audit.read

Filters:

- thời gian;
- actor;
- target user;
- target role;
- action;
- permission.

Detail:

- before/after;
- trace ID;
- timestamp UTC;
- lý do nếu bắt buộc.

Audit không có nút edit/delete.

## 8. Organization screens

Các trang Center, Teacher, Student, Class và Subject:

- route theo permission;
- action buttons theo capability;
- query vẫn tenant-safe;
- create/update form dùng server validation;
- delete là soft-delete/archive có confirmation;
- stale row version hiển thị conflict;
- Teacher chỉ thấy scope BLL cho phép dù có route permission.

Course MVP không có màn hình tạo/xóa Center hoặc chuyển sang Center khác. Center được provision bằng seed/migration/deployment; CenterManager chỉ cập nhật profile Center hiện hành khi có organization.center.update.

## 9. Knowledge, curriculum, question và assignment

- Knowledge Graph hiển thị node/edge và cảnh báo cycle.
- Curriculum có state Draft/Published; mutation bị khóa sau publish theo contract.
- Question editor không lộ correct answer sang Student projection.
- Assignment editor hiển thị target summary trước publish.
- Publish là atomic action có confirmation.
- Student assignment page không hiển thị management actions.

## 10. Student Learning Player

Màn hình cần:

- question content;
- final answer;
- reasoning text khi bắt buộc;
- confidence input 0–100;
- timer/time spent;
- submit state chống double submit;
- job-processing state sau HTTP 202;
- fallback/review state không làm mất bài;
- feedback chỉ hiển thị khi terminal.

Không hiển thị prompt nội bộ, model raw response hoặc correct answer trước submit.

## 11. AI Review and Evidence

### 11.1. Review queue

Hiển thị:

- Student/Class/Subject.
- Question.
- AI provider và model.
- Reasoning quality.
- Analysis confidence.
- Evidence decision.
- Reason codes.
- Fallback flag.
- Waiting time.

### 11.2. Review detail

Ba khối tách biệt:

1. Evidence gốc: answer, reasoning, score, confidence, time.
2. AI observation: quality, error type, feedback và AI confidence.
3. System decision: source type, trust level, decision mode, effective weight, policy version và reason codes.

Teacher override form:

- effective correctness;
- reasoning quality;
- error type;
- feedback;
- reason bắt buộc;
- override version.

Sau submit phải hiển thị replay result, Twin delta và recommendation thay đổi.

Replay được hiển thị như một history event, không phải evidence source/trust. Khi Gemini lỗi, UI phải nói rõ “phân tích AI chưa khả dụng; kết quả tạm dùng quy tắc xác định trước và đang chờ giáo viên xem xét”, không ngụ ý AI đã chấm.

## 12. Digital Twin and recommendation

Student view:

- mastery theo topic;
- trend theo thời gian;
- evidence count;
- mục tiêu/risk;
- recommendation;
- giải thích ngắn, dễ hiểu;
- trạng thái teacher review.
- nếu chưa đủ evidence tin cậy, dùng course order/active question để đề xuất bước tiếp theo và giải thích bằng template deterministic; không để trang trống vì AI lỗi.

Teacher view:

- effective evidence và history;
- calculation version/breakdown;
- không nhầm AI confidence với mastery confidence;
- filter weak topic/high risk.

Center view:

- aggregate đã tối thiểu hóa dữ liệu cá nhân;
- không dùng AI để xếp hạng giáo viên.

## 13. Common component states

| State | Hành vi |
|---|---|
| Initial loading | Skeleton hoặc status có aria-live |
| Background fetching | Giữ dữ liệu cũ, disable mutation/filter gây race khi cần |
| Empty | Nêu rõ chưa có dữ liệu và action phù hợp nếu có quyền |
| Validation error | Gắn lỗi đúng field và summary |
| 401 | Thử refresh đúng một lần, sau đó về login |
| 403 | Hiển thị Không có quyền |
| 404 | Không suy luận resource center khác |
| 409 concurrency | Cho tải lại/so sánh, không overwrite |
| 409 duplicate/state | Giải thích business conflict |
| 500 | Thông báo chung + trace ID |
| AI processing | Poll có backoff/timeout và trạng thái rõ |
| AI fallback | Cho biết hệ thống dùng fallback và có thể cần teacher review |

## 14. Figma workflow

Mỗi flow quan trọng phải có:

1. low-fidelity wireframe;
2. clickable prototype;
3. review với stakeholder đại diện;
4. feedback log;
5. revision;
6. approval status;
7. link trong bảng dưới.

| Flow | Figma URL | Reviewer | Date | Status |
|---|---|---|---|---|
| Login và navigation | TBD | TBD | TBD | PENDING |
| Dynamic role management | TBD | TBD | TBD | PENDING |
| Teacher assignment workflow | TBD | TBD | TBD | PENDING |
| Student learning player | TBD | TBD | TBD | PENDING |
| AI review/override | TBD | TBD | TBD | PENDING |
| Student Twin/recommendation | TBD | TBD | TBD | PENDING |

## 15. Accessibility acceptance

- Form control có label.
- Action chỉ dùng icon phải có accessible name.
- Modal giữ focus và trả focus khi đóng.
- Error/success state dùng aria-live hợp lý.
- Không chỉ dùng màu để truyền trạng thái.
- Keyboard hoàn tất được form chính.
- Contrast đạt mức cơ bản WCAG AA cho text/action chính.
- Table lớn có heading và mobile alternative.

## 16. Responsive acceptance

- 360 px: Student learning và core actions dùng được.
- 768 px: Tablet navigation/forms không overflow.
- 1280 px trở lên: Admin tables/dashboards sử dụng không gian hiệu quả.
- Không ẩn action bắt buộc chỉ vì mobile; dùng menu hoặc stacked layout.

## 17. Frontend test acceptance

- Route permission test.
- Menu/button capability test.
- Unauthorized deep-link test.
- Role editor validation/concurrency test.
- Role editor/assignment account-type filtering và mismatch error test.
- Self-elevation/last-admin API error rendering.
- Learning submit/poll/fallback test.
- Evidence review/override test.
- Không lưu refresh token vào localStorage/sessionStorage/indexedDB/cookie do JavaScript tạo.

## 18. Definition of UX Done

- Requirement/permission IDs được map.
- Figma đã review hoặc có lý do được duyệt khi chưa có.
- Happy, empty, loading, error, unauthorized và conflict states hoàn tất.
- API là source of truth.
- Accessibility/responsive checks pass.
- Stakeholder feedback và evidence được ghi trong PROJECT_TRACKING.md.

## 19. Current/target implementation boundary

Tại baseline 2d768f2, React đang dùng RoleRoute/ProtectedRoute và điều kiện allowedRoles tĩnh; auth DTO chưa có roles[], permissions[] hoặc authorizationVersion. Các màn hình role management, unauthorized chuyên biệt, review queue, Twin/recommendation dashboard cuối và capability-first navigation là TARGET, chưa được tuyên bố implemented.

Cutover phải theo từng slice:

1. Backend trả accountType, roles, effective permissions và users.auth_version.
2. Frontend thêm hasPermission/canAccessRoute tập trung nhưng giữ compatibility route cho module chưa migrate.
3. Chuyển từng route/action sang permission; endpoint tương ứng phải đã enforce server-side.
4. Chỉ xóa RoleRoute/allowedRoles của slice sau test direct URL và handcrafted request.

Không dùng biểu thức role OR permission để “chạy tạm”, vì sẽ tạo đường cấp quyền rộng hơn contract.
