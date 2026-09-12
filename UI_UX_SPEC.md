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
├── /quan-tri-nen-tang
│   └── /quan-tri-nen-tang/trung-tam
└── /cai-dat
~~~

Route không có permission phải đưa về trang Không có quyền, không âm thầm chuyển về dashboard làm người dùng hiểu nhầm.

## 5. Permission catalog cho UI

Permission catalog do backend trả. Nhóm code khởi tạo:

### Platform Administration (Chỉ dành cho Root Tenant PLATFORM)

- platform.centers.read
- platform.centers.manage

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
- gửi rowVersion hiện tại của target User để chống lost update; không gửi authorizationVersion như concurrency token;
- cảnh báo nếu thay đổi chính user hiện tại;
- chặn self-elevation;
- chặn làm mất CenterManager cuối cùng có đủ TenantAdminCorePermissionsV1;
- hiển thị session của target có thể phải đăng nhập lại;
- ghi audit.

Sau mutation thành công, UI thay cả rowVersion và authorizationVersion bằng giá trị server trả về. rowVersion phục vụ conflict/reload; authorizationVersion báo session/cache quyền cũ đã stale và có thể yêu cầu bootstrap lại.

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

Với Essay/preliminary `isCorrect = null`, trước teacher review UI phải hiển thị trạng thái “Chờ giáo viên chấm”, không hiển thị như câu sai và không hiển thị Mastery delta. AI observation nếu có vẫn tách riêng; chỉ HumanConfirmed replay mới sinh Mastery delta.

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
- Evidence review/override test, gồm Essay pending correctness không bị hiển thị là sai và không có Mastery delta trước review.
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

## 20. Đặc tả UI/UX Bổ sung Hậu R08 (Post-R08 Extension Specs)

### 20.1. Màn hình Quản trị Trung tâm Nền tảng (`PlatformCentersPage.tsx`)
- **Route:** `/quan-tri-nen-tang/trung-tam`.
- **Yêu cầu phân quyền:** Phải có quyền `platform.centers.read` hoặc `platform.centers.manage`, và người dùng thuộc Root Tenant `PLATFORM` (`AccountType === 'PlatformAdmin'`).
- **Thành phần giao diện:**
  - Tiêu đề & Breadcrumb: "Quản trị Nền tảng" / "Danh sách Trung tâm Giáo dục".
  - Thanh tác vụ: Ô tìm kiếm (mã trung tâm, tên trung tâm, tên người quản lý), bộ lọc trạng thái (`Tất cả`, `Đang hoạt động`, `Tạm ngưng`), nút "Thêm trung tâm mới" (yêu cầu quyền `platform.centers.manage`).
  - Bảng dữ liệu: Cột Mã trung tâm, Tên trung tâm, Trạng thái (Badge xanh `Hoạt động` / Badge vàng `Tạm ngưng`), Quản lý chính (Username, Họ tên, Email), Số điện thoại, Ngày tạo, Nút thao tác (Đổi trạng thái, Đặt lại mật khẩu).
  - Empty State: Khi hệ thống chưa có trung tâm thường nào (`items: []`), hiển thị hình minh họa, dòng thông báo "Chưa có trung tâm giáo dục nào được khởi tạo trên nền tảng" và nút kêu gọi hành động "Tạo trung tâm đầu tiên".
  - Modal tạo trung tâm mới: Form nhập Mã trung tâm, Tên trung tâm, Địa chỉ, Số điện thoại, Thông tin quản lý ban đầu (Username, Email, Họ và tên). Hiển thị mật khẩu tạm thời được hệ thống sinh ngẫu nhiên sau khi tạo thành công kèm nút sao chép an toàn.
  - Modal chuyển đổi trạng thái: Hộp thoại xác nhận chuyển sang `Tạm ngưng` hoặc `Kích hoạt lại`. Gửi kèm `rowVersion`. Khi phát sinh xung đột đồng thời (HTTP 409), hiển thị thông báo "Dữ liệu trung tâm đã bị thay đổi bởi tác vụ khác. Vui lòng tải lại dữ liệu mới nhất" và tự động kích hoạt query refetch.
  - Modal đặt lại mật khẩu ban đầu: Hộp thoại xác nhận đặt lại mật khẩu tài khoản quản lý trung tâm, sinh mật khẩu an toàn mới và cảnh báo các phiên làm việc hiện tại của tài khoản này sẽ lập tức bị thu hồi (auth_version bump).

### 20.2. Thanh Công Cụ Toán Học Trực Quan (`MathInputToolbar.tsx`) & Xem Trước KaTeX (`MathFormulaPreview.tsx`)
- **Vị trí tích hợp:** Trình soạn thảo câu hỏi (`QuestionEditorPage.tsx`) và Trình làm bài của học sinh (`LearningPlayerPage.tsx`).
- **Cấu trúc 5 Tabs biểu tượng toán học:**
  1. *Cơ bản:* $\pm, \times, \div, \sqrt{x}, x^2, x^n, \frac{a}{b}, =, \neq$.
  2. *Đại số:* $\le, \ge, \approx, \infty, \pi, \alpha, \beta, \theta, |x|$.
  3. *Giải tích:* $\int, \frac{d}{dx}, \sum, \lim_{x \to x_0}$.
  4. *Tập hợp & Logic:* $\in, \notin, \subset, \cup, \cap, \emptyset, \forall, \exists, \implies, \iff$.
  5. *Hình học & Lượng giác:* $\sin, \cos, \tan, \cot, \angle, \Delta, \perp, \parallel, ^\circ$.
- **Hành vi người dùng:** Khi nhấp vào nút biểu tượng, chèn mã LaTeX tương ứng vào vị trí con trỏ chuột hiện tại của textarea.
- **Xem trước công thức thời gian thực (`MathFormulaPreview.tsx`):**
  - Hiển thị song song hoặc ngay bên dưới ô nhập liệu.
  - Sử dụng thư viện `katex` với tùy chọn cấu hình an toàn tuyệt đối `trust: false` nhằm ngăn chặn script injection.
  - Xử lý lỗi cú pháp mượt mà: Nếu mã LaTeX chưa hoàn chỉnh khi đang gõ, hiển thị công thức thô màu xám nhạt thay vì báo lỗi đỏ gắt gỏng.

### 20.3. Ngăn Kéo Máy Tính Khoa Học (`ScientificCalculatorDrawer.tsx`)
- **Vị trí & Cơ chế mở:** Nút nổi hoặc icon máy tính trên thanh công cụ học tập `LearningPlayerPage`; mở ngăn kéo trượt mượt mà (slide-over drawer) từ cạnh phải màn hình.
- **Bàn phím & Chức năng tính toán:**
  - Bàn phím số 0-9, dấu chấm thập phân, dấu âm $\pm$.
  - Bốn phép tính cơ bản: $+ - \times \div$.
  - Hàm lượng giác: $\sin, \cos, \tan$ (hỗ trợ chuyển đổi đơn vị Deg/Rad qua toggle switch).
  - Hàm logarit: $\ln, \log_{10}$.
  - Căn bậc hai $\sqrt{x}$, lũy thừa $x^y, x^2$, nghịch đảo $1/x$, hằng số $\pi, e$.
- **Tính toán thuần túy (No Auto-Solver Invariant):** Động cơ tính toán (`calculatorEngine.ts`) chỉ thực thi tính giá trị biểu thức số học tức thời; tuyệt đối không cung cấp tính năng giải phương trình, tích phân ký hiệu hoặc tự động làm hộ bài tập.

### 20.4. Bảng Vẽ Nháp Vector Toàn Màn Hình (`ScratchpadCanvasModal.tsx`)
- **Cơ chế kích hoạt:** Nút "Bảng vẽ nháp" với biểu tượng bút vẽ nổi bật trong khu vực trả lời bài tập. Nhấp vào mở modal toàn màn hình (fullscreen canvas).
- **Bộ công cụ vẽ trực quan:**
  - Bút vẽ tự do (Freehand Pen): Lựa chọn màu sắc (Đen, Xanh dương, Đỏ, Xanh lá), điều chỉnh nét vẽ (Mảnh, Vừa, Dày).
  - Tẩy nét vẽ (Eraser) và Xóa toàn bộ bảng vẽ (Clear All kèm xác nhận).
  - Lịch sử Undo / Redo: Lưu tối đa 30 trạng thái vẽ, cho phép hoàn tác/lặp lại thao tác mượt mà.
  - Bật/tắt lưới nền (Grid Toggle): Lưới ô ly chuẩn học sinh THPT, lưới tọa độ, hoặc nền trắng trơn.
  - Thước đo và hình học mẫu: Thước thẳng, compa vẽ hình tròn, tam giác, và hệ trục tọa độ Oxy với mũi tên định hướng.
- **Lưu trữ nháp cục bộ IndexedDB Scoped:**
  - Lưu tự động sau mỗi nét vẽ vào IndexedDB của trình duyệt với khóa định danh phân lập chặt chẽ:
    `draft:${centerId}:${userId}:${clientSubmissionId}`
  - Không mất bản vẽ khi học sinh lỡ tay reload trang hoặc chuyển tab.
  - Vòng đời dọn dẹp nháp:
    - Khi người dùng Đăng xuất (Logout) $\to$ Xóa toàn bộ drafts của user.
    - Khi khởi động ứng dụng $\to$ Quét và dọn dẹp các drafts quá hạn (TTL > 24 giờ).
    - Khi Nộp bài thành công (Server trả mã HTTP 202 hoặc HTTP 200 Replay) $\to$ Xóa draft tương ứng trong IndexedDB.
    - Khi gặp lỗi mạng hoặc lỗi validation $\to$ Giữ nguyên draft để học sinh không bị mất dữ liệu.
- **Xuất minh chứng:** Xuất dữ liệu hình ảnh thành định dạng `image/png` blob nén với dung lượng $\le 5\text{MB}$.

### 20.5. Xem Minh Chứng Đa Phương Thức & Xử Lý Sự Cố Lưu Trữ Bền Vững
- **Hàng đợi giáo viên (`ReviewQueue`) & Teacher Override:**
  - Danh sách bài nộp hiển thị huy hiệu (Badge) "Có bản vẽ nháp" đối với các attempt có `hasAttachment === true`.
  - Nhấp vào huy hiệu hoặc nút "Xem bản vẽ" mở modal hiển thị ảnh nháp kích thước lớn, tải an toàn qua API đính kèm có xác thực token Bearer.
- **Xử lý sự cố lưu trữ bền vững đối với bài tự do (Free-Practice Terminal State):**
  - Khi bài tự do gặp lỗi hạ tầng lưu trữ và hết số lần retry bền vững, trạng thái bài làm chuyển thành `AnalysisFailed`.
  - Giao diện học sinh hiển thị thông báo thân thiện: "Không thể xử lý bản vẽ nháp do sự cố hạ tầng lưu trữ. Kết quả bài nộp tạm thời chưa được phân tích."
  - Cung cấp nút hành động "Thử nộp lại" (Resubmit) tự động gán `ClientSubmissionId` mới, giúp học sinh gửi lại bài dễ dàng mà không làm ô nhiễm Hàng đợi duyệt của giáo viên.
