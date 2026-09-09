# EduTwin — Project Requirements and SRS

> Phiên bản: 2.1-draft
> Trạng thái: COURSE REBASELINE — chờ nhóm và stakeholder xác nhận
> Chủ sở hữu: Nhóm EduTwin
> Phạm vi: yêu cầu môn học, nghiệp vụ, bảo mật và acceptance criteria
> Cập nhật đối chiếu Week 1 gần nhất: 2026-09-09

## 1. Mục đích

Tài liệu này trả lời bốn câu hỏi:

1. Vì sao EduTwin cần tồn tại?
2. Ai sử dụng và họ cần đạt kết quả gì?
3. Hệ thống bắt buộc phải làm gì?
4. Dựa vào bằng chứng nào để tuyên bố một yêu cầu đã hoàn thành?

Database và source code không được dùng để giả mạo yêu cầu khách hàng. Mọi requirement phải có nguồn và trạng thái.

## 2. Quy ước requirement

### 2.1. Loại requirement

| Prefix | Loại |
|---|---|
| LEC | Yêu cầu giảng viên/môn học |
| FR | Functional requirement |
| BR | Business rule |
| SEC | Security requirement |
| NFR | Non-functional requirement |
| UX | UI/UX requirement |
| DATA | Data integrity requirement |
| AI | AI/Evidence requirement |

### 2.2. Trạng thái

| Trạng thái | Ý nghĩa |
|---|---|
| LECTURER-CONFIRMED | Người học ghi nhận trực tiếp từ giảng viên |
| USER-CONFIRMED | Thành viên dự án xác nhận đã nhận chỉ dẫn này; cần bổ sung provenance khi có |
| STAKEHOLDER-VALIDATED | Người dùng đại diện đã xác nhận |
| APPROVED | Nhóm đã duyệt để triển khai |
| PRODUCT-HYPOTHESIS | Giả thuyết cần kiểm chứng |
| PENDING-VALIDATION | Chưa đủ bằng chứng |
| IMPLEMENTED | Source đã có nhưng chưa mặc nhiên đồng nghĩa stakeholder đã xác nhận |
| VERIFIED | Có test/demo/acceptance evidence |
| DEFERRED | Không thuộc lần phát hành hiện tại |

Một requirement chỉ được xem là Done khi vừa IMPLEMENTED vừa VERIFIED.

## 3. Nguồn yêu cầu và provenance

### 3.1. Baseline trước môn học

Prototype đã tồn tại trước học kỳ tại:

~~~text
Repository:
nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform

Baseline branch:
feat/center-organization

Baseline commit:
2d768f270e0395bcafcbcab2305ac3617fb5f9ca
~~~

Documentation rebaseline bắt đầu tại checkpoint `b14f6c4171dc55043a3bb910332061c55ba66a7f` trên repository prototype. Sau khi correction được nhóm duyệt, snapshot code + tài liệu được nhập nguyên trạng thành commit đầu của repository môn học; full SHA import được ghi trong PROJECT_TRACKING.md. Điểm đóng góp chỉ bắt đầu sau commit initial import, không bắt đầu sau source-code snapshot hoặc documentation checkpoint.

### 3.2. Yêu cầu giảng viên đã ghi nhận ở tuần 1

Nguồn: ba bản ghi âm do thành viên nhóm ghi lại và các thông tin giảng viên mà Tuấn Tài báo cáo trực tiếp. Ngày chính xác và mức bằng chứng phải được bổ sung trong PROJECT_TRACKING.md.

Quy ước provenance: `LECTURER-CONFIRMED` chỉ dùng khi transcript/biên bản hiện có xác nhận nội dung; `LECTURER-REPORTED-BY-STUDENT` là lời giảng viên được thành viên báo lại nhưng chưa có đoạn transcript/biên bản tương ứng; `USER-CONFIRMED` là quyết định/quy trình do người dùng xác nhận, không tự nâng thành yêu cầu giảng viên.

| ID | Yêu cầu | Trạng thái |
|---|---|---|
| LEC-001 | Dùng Visual Studio 2022 trở lên và nền tảng .NET | LECTURER-CONFIRMED |
| LEC-002 | Sử dụng C#, OOP, debug, LINQ và Entity Framework Core | LECTURER-CONFIRMED |
| LEC-003 | Có thể chọn ASP.NET Core MVC hoặc Web API | LECTURER-CONFIRMED |
| LEC-004 | Database phải có hơn 20 bảng và ràng buộc dữ liệu chặt chẽ | LECTURER-CONFIRMED |
| LEC-005 | Phải khảo sát khách hàng/người dùng, không tự suy đoán yêu cầu | LECTURER-CONFIRMED |
| LEC-006 | Phải có tài liệu đặc tả đối tượng sử dụng, chức năng, quy trình và phân quyền | LECTURER-CONFIRMED |
| LEC-007 | Nên dùng Figma và lấy xác nhận trước khi lập trình giao diện | LECTURER-CONFIRMED |
| LEC-008 | Phân quyền phải linh động và có màn hình cấp quyền | LECTURER-CONFIRMED |
| LEC-009 | Repository GitHub phải có đủ năm thành viên và được dùng để theo dõi tiến độ | LECTURER-CONFIRMED |
| LEC-010 | Nhóm báo cáo hằng tuần: đã làm gì, phần trăm hoàn thành và vướng mắc | LECTURER-CONFIRMED |
| LEC-011 | Báo cáo cuối kỳ phải có phân công và tỷ lệ đóng góp | LECTURER-CONFIRMED |
| LEC-012 | Phân công phải xét độ khó; nhóm phải thống nhất hoặc chia gói cân bằng | LECTURER-CONFIRMED |
| LEC-013 | Tích hợp AI/Machine Learning tốt có thể được đánh giá cao | LECTURER-REPORTED-BY-STUDENT |
| LEC-014 | Nhóm được tiếp tục prototype nhưng phải tạo repository mới để theo dõi quá trình môn học | USER-CONFIRMED |

LEC-013 là lời Tuấn Tài báo lại từ phần hướng dẫn chưa có transcript trong ba đoạn hiện lưu; LEC-014 là xác nhận của người dùng về chỉ dẫn của giảng viên. Cả hai cần ghi tuần/nguồn bổ sung trong tracking khi có bằng chứng.

### 3.3. Mức đáp ứng Week 1 tại thời điểm rebaseline

| Yêu cầu | Bằng chứng hiện có | Trạng thái trung thực | Việc còn thiếu |
|---|---|---|---|
| .NET, C#, EF Core, LINQ, Web API | Source ASP.NET Core/EF Core; build và test baseline xanh | IMPLEMENTED/VERIFIED ở prototype | Import baseline sang course repo và tiếp tục evidence theo tuần |
| Database hơn 20 bảng, có ràng buộc | 31 bảng đã migration; static audit có FK/unique/check/index | IMPLEMENTED một phần | Chạy live MySQL INFORMATION_SCHEMA, EXPLAIN và 3 integration test đang skip |
| Đặc tả actor, chức năng, quy trình, quyền | Bộ Markdown và DOCX hiện hành | DOCUMENTED | Nhóm/giảng viên/stakeholder review và ký xác nhận |
| Khảo sát người dùng | Stakeholder và bộ câu hỏi đã xác định | NOT STARTED | Phỏng vấn CenterManager, Teacher, Student; lưu biên bản/evidence |
| Figma và xác nhận UI | Sitemap/flow/state đã đặc tả | NOT STARTED | Tạo prototype, walkthrough và approval link |
| Phân quyền linh động và màn hình cấp quyền | Schema/API/UI target đã đặc tả | NOT IMPLEMENTED | R02–R04: migration, backend/API, UI và security test |
| GitHub theo dõi năm thành viên | Prototype có Git history; baseline SHA đã ghi | PARTIAL | Tạo course repo, thêm đủ thành viên, import baseline minh bạch |
| Báo cáo tuần và tỷ lệ đóng góp | Tracking/template và assignment plan | DOCUMENTED | Điền metadata, bằng chứng thật và xác nhận mỗi tuần |
| AI/ML là điểm cộng | Gemini adapter/retry/fallback có trong prototype | IMPLEMENTED một phần | Evidence Gate, offline-safe flow, evaluation/ML decision gate |

Vì các dòng khảo sát, Figma, course repository, RBAC động và live MySQL audit chưa hoàn tất, không được tuyên bố đồ án đã đáp ứng đầy đủ Week 1 chỉ dựa trên số test hoặc số bảng.

## 4. Product charter

### 4.1. Vấn đề

Trung tâm giáo dục thường theo dõi điểm số nhưng thiếu:

- bằng chứng về cách học sinh suy luận;
- khả năng phân biệt sai do kiến thức, kỹ năng, hành vi hay trình bày;
- khả năng chọn nội dung học tiếp theo có giải thích;
- công cụ giám sát AI và sửa kết quả thiếu tin cậy;
- phân quyền chi tiết theo công việc thực tế.

### 4.2. Tuyên bố sản phẩm

EduTwin là nền tảng multi-tenant giúp trung tâm quản lý tổ chức và nội dung học, thu thập bằng chứng từ quá trình làm bài, đánh giá độ tin cậy của evidence, cập nhật Learning Digital Twin bằng thuật toán tái lập và đưa ra hành động học tập có thể giải thích.

### 4.3. Mục tiêu

- G1: Cách ly dữ liệu tuyệt đối giữa các center.
- G2: Quản lý organization, knowledge, curriculum, question và assignment thống nhất.
- G3: Ghi nhận cả kết quả và quá trình suy luận/hành vi.
- G4: Tiếp tục hoạt động khi Gemini chậm, lỗi hoặc trả dữ liệu sai.
- G5: Không cho AI thiếu tin cậy tự thay đổi mạnh hồ sơ học sinh.
- G6: Cho giáo viên review, override và replay có audit.
- G7: Cho CenterManager cấu hình role/quyền trong center bằng UI.
- G8: Tạo recommendation dựa trên thuật toán có thể giải thích.
- G9: Cung cấp bằng chứng tiến độ và đóng góp học thuật cho năm thành viên.

AI là enhancement, không phải operational core: nếu Gemini/Internet dừng, người dùng vẫn đăng nhập, quản trị quyền/tổ chức, quản lý nội dung, giao/nộp bài, nhận chấm sơ bộ deterministic, lưu Attempt, xem trạng thái, cập nhật Behavior Twin từ telemetry và nhận recommendation fallback từ dữ liệu đã có. Knowledge Mastery chỉ thay đổi khi evidence đạt policy; hệ thống không bịa Reasoning Analysis để giữ luồng chạy.

### 4.4. Ngoài phạm vi course baseline

- Platform/System Admin quản lý nhiều center.
- Thanh toán, subscription và billing.
- Public registration.
- OCR chữ viết tay.
- RAG/vector search.
- Multi-provider AI orchestration.
- Giám sát giáo viên bằng AI.
- Native mobile application.
- Tự tạo permission code động từ UI.

## 5. Stakeholder

| Stakeholder | Mục tiêu chính | Trạng thái khảo sát |
|---|---|---|
| CenterManager | Quản lý center, tài khoản, role/quyền, lớp và dashboard | PENDING-VALIDATION |
| Teacher | Quản lý nội dung/bài tập, theo dõi học sinh và review AI | PENDING-VALIDATION |
| Student | Làm bài, xem phản hồi và nhận lộ trình phù hợp | PENDING-VALIDATION |
| Lecturer | Đánh giá quy trình phân tích, .NET/EF, database, teamwork và sản phẩm | LECTURER-CONFIRMED một phần |
| Nhóm phát triển | Xây dựng, kiểm thử, trình bày và bảo trì hệ thống | APPROVED |

Không được chuyển cột PENDING-VALIDATION thành STAKEHOLDER-VALIDATED nếu chưa có biên bản/phỏng vấn hoặc xác nhận Figma.

Câu hỏi kết quả phải được kiểm chứng với từng actor:

- Student: “Em yếu ở đâu, nên học/làm gì tiếp theo và có nguy cơ không đạt mục tiêu môn học không?”
- Teacher: “Học sinh nào cần hỗ trợ, lỗi chung là gì và nên giao hoạt động nào tiếp theo?”
- CenterManager: “Ai được làm gì trong Center, lớp nào cần chú ý và mức đạt mục tiêu toàn Center ra sao?”

Không dùng AI để chấm hiệu suất hoặc xếp hạng giáo viên.

## 6. Actors và mô hình quyền

### 6.1. Account type

EduTwin giữ ba account type để nhận biết domain context:

- Student.
- Teacher.
- CenterManager.

Account type không phải toàn bộ authorization, nhưng là ranh giới domain bắt buộc: Student, Teacher và CenterManager có profile/quy trình khác nhau nên dynamic role không được dùng để đổi chéo loại tài khoản.

### 6.2. Dynamic role

- Mỗi center sở hữu role riêng.
- CenterManager có thể tạo/sửa/archive role trong center.
- Một user có thể có nhiều role.
- Mỗi role thuộc đúng một account type; account type của role immutable sau khi tạo.
- Mỗi permission khai báo các account type được phép nhận.
- User chỉ được gán role cùng account type; độ linh động nằm trong phạm vi nhiệm vụ của loại tài khoản, không phải biến Student thành Teacher/CenterManager.
- Permission code do hệ thống định nghĩa vì mỗi permission phải được API/BLL thực thi.
- CenterManager chọn permission từ catalog và gán vào role.
- Không có Platform Admin trong course scope.

### 6.3. Effective authorization

Mọi thao tác protected phải thỏa:

~~~text
Authenticated
AND active Center
AND active User
AND required Permission
AND same Tenant
AND Ownership or Resource Scope
~~~

Thiếu một điều kiện phải fail closed.

## 7. Functional requirements

### 7.1. Authentication và session

| ID | Requirement | Priority |
|---|---|---|
| FR-AUTH-001 | Người dùng đăng nhập bằng center code, username và password | Must |
| FR-AUTH-002 | Hệ thống phát access token ngắn hạn và refresh token HttpOnly | Must |
| FR-AUTH-003 | Refresh token được rotate và có thể revoke | Must |
| FR-AUTH-004 | Center/User bị khóa phải mất khả năng truy cập | Must |
| FR-AUTH-005 | Auth response cung cấp account type, roles, effective permissions và authorization version | Must |
| FR-AUTH-006 | Thay đổi role/quyền làm session/cache quyền cũ mất hiệu lực theo chính sách | Must |
| FR-AUTH-007 | authorizationVersion trong API và auth_version trong JWT cùng ánh xạ tới users.auth_version; access token stale bị từ chối server-side | Must |

### 7.2. Dynamic RBAC

| ID | Requirement | Priority |
|---|---|---|
| FR-RBAC-001 | CenterManager xem catalog permission do hệ thống cung cấp | Must |
| FR-RBAC-002 | CenterManager tạo role trong center của mình | Must |
| FR-RBAC-003 | CenterManager sửa tên, mô tả, trạng thái và permission của role | Must |
| FR-RBAC-004 | CenterManager gán nhiều role cho user cùng center | Must |
| FR-RBAC-005 | CenterManager xem effective permission của một user | Must |
| FR-RBAC-006 | Hệ thống ngăn cross-tenant role/permission assignment | Must |
| FR-RBAC-007 | Hệ thống ngăn tự nâng quyền; role CenterManager chỉ nhận subset quyền actor có, còn role Student/Teacher chỉ nhận permission Active, delegable và tương thích account type | Must |
| FR-RBAC-008 | Hệ thống bảo vệ CenterManager cuối cùng có quyền quản trị | Must |
| FR-RBAC-009 | Mọi thay đổi authorization được lưu audit append-only | Must |
| FR-RBAC-010 | UI menu/page/button phản ánh effective permission | Must |
| FR-RBAC-011 | API vẫn kiểm tra permission, tenant và ownership độc lập với UI | Must |
| FR-RBAC-012 | Hệ thống chỉ cho role nhận permission tương thích account type và chỉ gán role cho user cùng account type | Must |
| FR-RBAC-013 | Sau mọi mutation phải còn ít nhất một CenterManager Active có đủ TenantAdminCorePermissionsV1 | Must |

### 7.3. Organization

| ID | Requirement | Priority |
|---|---|---|
| FR-ORG-001 | CenterManager quản lý hồ sơ center của mình | Must |
| FR-ORG-002 | User có quyền phù hợp quản lý teacher/student theo center | Must |
| FR-ORG-003 | User có quyền phù hợp quản lý class và membership | Must |
| FR-ORG-004 | Teacher chỉ truy cập student/class theo ownership được phép | Must |
| FR-ORG-005 | Hệ thống quản lý subject theo center | Must |
| FR-ORG-006 | Cross-tenant identifier không làm lộ sự tồn tại resource | Must |
| FR-ORG-007 | Center được provision bằng seed/migration/deployment; course MVP không có UI/API tạo/xóa Center hoặc quản lý Center khác | Must |

### 7.4. Knowledge, curriculum và question bank

| ID | Requirement | Priority |
|---|---|---|
| FR-CONTENT-001 | Teacher có quyền tạo cấu trúc Knowledge Graph theo subject | Must |
| FR-CONTENT-002 | Prerequisite graph không được tạo cycle | Must |
| FR-CONTENT-003 | Curriculum liên kết class và knowledge node cùng tenant/subject | Must |
| FR-CONTENT-004 | Question hỗ trợ MultipleChoice, ShortAnswer và Essay | Must |
| FR-CONTENT-005 | Question lưu grading criteria có schema/version rõ ràng | Must |
| FR-CONTENT-006 | Draft/publish/archive tuân theo state machine và row version | Must |

### 7.5. Assignment và learning

| ID | Requirement | Priority |
|---|---|---|
| FR-LEARN-001 | Teacher tạo assignment draft từ question bank | Must |
| FR-LEARN-002 | Publish assignment tạo target snapshot atomically | Must |
| FR-LEARN-003 | Student chỉ thấy assignment được giao và đang khả dụng | Must |
| FR-LEARN-004 | Submission có client submission ID để bảo vệ idempotency | Must |
| FR-LEARN-005 | Student gửi final answer, reasoning, confidence, time và hành vi cần thiết | Must |
| FR-LEARN-006 | API nhận submission và trả trạng thái AI job bất đồng bộ | Must |

### 7.6. AI Reasoning và Evidence

| ID | Requirement | Priority |
|---|---|---|
| FR-AI-001 | Gemini được truy cập qua IAIService, không gọi trực tiếp từ Controller | Must |
| FR-AI-002 | AI response phải qua structural và semantic validation | Must |
| FR-AI-003 | Lỗi tạm thời được retry tối đa một lần | Must |
| FR-AI-004 | Sau retry thất bại, hệ thống tạo deterministic fallback và teacher-review state | Must |
| FR-AI-005 | AI success tạo ReasoningAnalysis; fallback deterministic tạo record/provenance rõ và không giả là AI success | Must |
| FR-AI-006 | Evidence Gate đánh giá confidence, correctness, score, time, behavior và override | Must |
| FR-AI-007 | Gate lưu riêng source type, trust level và decision mode; fallback là RuleFallback + ReviewOnly + DeterministicOnly | Must |
| FR-AI-008 | Mỗi decision lưu version, weight và reason codes | Must |
| FR-AI-009 | AI confidence thấp không được tác động reasoning vào mastery | Must |
| FR-AI-010 | Teacher xem queue, override analysis và kích hoạt replay | Must |
| FR-AI-011 | AI chỉ phân tích reasoning và soạn phản hồi; chấm sơ bộ deterministic/teacher decision mới sở hữu điểm và kết quả cuối | Must |
| FR-AI-012 | Khi preliminary `isCorrect = null` (điển hình Essay chưa chấm), Evidence phải ReviewOnly, reasoning weight bằng 0 và Knowledge Mastery không đổi cho tới Teacher HumanConfirmed + replay | Must |

### 7.7. Digital Twin và recommendation

| ID | Requirement | Priority |
|---|---|---|
| FR-TWIN-001 | Behavior Twin cập nhật từ telemetry ngay cả khi Gemini lỗi | Must |
| FR-TWIN-002 | Knowledge Twin dùng effective evidence đã qua gate | Must |
| FR-TWIN-003 | Mỗi thay đổi Twin có calculation version, breakdown và history | Must |
| FR-TWIN-004 | Teacher override tạo replay deterministic và history mới | Must |
| FR-TWIN-005 | Risk được tính từ mastery, goal, remaining time và rule đã version | Must |
| FR-TWIN-006 | Opportunity Gap xếp hạng topic/question bằng thuật toán deterministic | Must |
| FR-TWIN-007 | Gemini có thể diễn giải nhưng không quyết định ranking | Must |
| FR-TWIN-008 | Recommendation cũ được supersede, không ghi đè mất lịch sử | Must |
| FR-TWIN-009 | Student thấy recommendation và lý do dễ hiểu | Must |

### 7.8. Dashboard

| ID | Requirement | Priority |
|---|---|---|
| FR-DASH-001 | Student xem assignment, mastery trend và recommendation của chính mình | Must |
| FR-DASH-002 | Teacher xem class, weak topic, risk student và review queue trong ownership | Must |
| FR-DASH-003 | CenterManager xem aggregate của center mà không xem center khác | Must |
| FR-DASH-004 | Dashboard không query N+1 và có pagination/filter phù hợp | Must |

### 7.9. Course governance

| ID | Requirement | Priority |
|---|---|---|
| FR-COURSE-001 | Có repository môn học với transparent baseline | Must |
| FR-COURSE-002 | Mỗi task có human owner và requirement ID | Must |
| FR-COURSE-003 | Có báo cáo tiến độ hằng tuần và blocker | Must |
| FR-COURSE-004 | Có bằng chứng commit/review/test của từng thành viên | Must |
| FR-COURSE-005 | Có Figma/prototype và stakeholder feedback | Must |
| FR-COURSE-006 | Báo cáo cuối có bảng phân công, trọng số và tỷ lệ đóng góp | Must |

## 8. Business rules

| ID | Rule |
|---|---|
| BR-001 | Client không được quyết định center ID của operation |
| BR-002 | Resource khác center phải bị loại bởi database, query filter và BLL guard |
| BR-003 | Permission không thay thế ownership |
| BR-004 | Permission có thể được gán động; permission code phải do source hỗ trợ |
| BR-005 | Effective permission là hợp của các active role; v1 không có explicit deny |
| BR-006 | Endpoint đã migrate dùng permission; endpoint chưa migrate dùng legacy policy; không dùng legacy OR permission |
| BR-007 | Actor có authorization.roles.manage_permissions được quản trị permission delegable cho role Student/Teacher; với role CenterManager, permission mới phải là subset effective permission của actor |
| BR-008 | Center phải luôn còn ít nhất một CenterManager Active có đủ chín TenantAdminCorePermissionsV1: permissions.read, roles.read/create/update/archive/manage_permissions, user_roles.read/assign và audit.read |
| BR-009 | Evidence gốc và history không được hard-delete |
| BR-010 | AI output không được dùng nếu chưa qua validation và Evidence Gate |
| BR-011 | ReviewOnly có AI reasoning weight bằng 0 |
| BR-012 | Teacher override không sửa record AI gốc; tạo effective/replay history |
| BR-013 | Recommendation ranking không phụ thuộc lời văn do Gemini tạo |
| BR-014 | Mọi calculation persisted phải có version và breakdown |
| BR-015 | Không tuyên bố requirement đã được stakeholder xác nhận nếu thiếu evidence |
| BR-016 | Dynamic role chỉ linh động quyền trong account type; đổi account type phải qua use case riêng và data migration được phê duyệt |
| BR-017 | users.auth_version là authorization version duy nhất; password reset, user status, user-role và role-permission mutation phải bump version theo phạm vi ảnh hưởng |
| BR-018 | Replay là event/history; không được lưu như evidence source hoặc trust level |
| BR-019 | Center lifecycle ngoài profile hiện hành thuộc deployment/seed trong course MVP, không thuộc tenant-admin UI |

## 9. Security requirements

| ID | Requirement |
|---|---|
| SEC-001 | Multi-tenant fail closed khi tenant/user/role context thiếu hoặc malformed |
| SEC-002 | Composite tenant FK bảo vệ cross-tenant relationship |
| SEC-003 | Authorization kiểm tra server-side cho mọi protected endpoint |
| SEC-004 | Cross-tenant lookup trả 404 khi cần chống enumeration |
| SEC-005 | Không log secret, token, password hoặc raw sensitive payload |
| SEC-006 | Refresh token lưu hash, rotate và chống reuse |
| SEC-007 | Role mutation có optimistic concurrency |
| SEC-008 | Role assignment, permission mutation và denied escalation có audit |
| SEC-009 | Thay đổi quyền làm token/cache cũ không tiếp tục dùng quyền đã thu hồi |
| SEC-010 | Các permission nhạy cảm có test self-elevation, last-admin và cross-tenant |
| SEC-011 | UI không được coi là security boundary |
| SEC-012 | AI prompt/response được giới hạn dữ liệu cần thiết và có retention policy |
| SEC-013 | Role–permission và user–role lệch account type phải bị chặn ở API/BLL và bằng relational constraint |

## 10. Data requirements

| ID | Requirement |
|---|---|
| DATA-001 | Database có hơn 20 bảng có mục đích nghiệp vụ; không tạo bảng rỗng để đủ số lượng |
| DATA-002 | Type, nullability, max length, precision và enum được map explicit |
| DATA-003 | Business range có validation tại request, BLL và CHECK khi provider hỗ trợ |
| DATA-004 | Mutable aggregate có audit, soft delete và row version |
| DATA-005 | Evidence/audit/history là append-only |
| DATA-006 | Unique key phải có race-safe database enforcement |
| DATA-007 | Delete behavior phải explicit và bảo toàn evidence |
| DATA-008 | Migrations apply được từ database trống và upgrade được từ baseline |
| DATA-009 | Schema thực tế phải đối chiếu migration snapshot và INFORMATION_SCHEMA |
| DATA-010 | Query trọng yếu phải được kiểm tra EXPLAIN trên MySQL thật |
| DATA-011 | Permission applicability được chuẩn hóa bằng permission_account_types; role_permissions và user_roles dùng composite FK để khóa account type |

## 11. Non-functional requirements

| ID | Requirement/acceptance |
|---|---|
| NFR-001 | Release build không warning/error |
| NFR-002 | Unit, integration, frontend và selected E2E gate chạy tự động trong CI |
| NFR-003 | Không merge khi migration/model pending hoặc diff check lỗi |
| NFR-004 | API collection có pagination; không tải không giới hạn |
| NFR-005 | Background job có lease, idempotency, retry giới hạn và lost-race handling |
| NFR-006 | AI outage không chặn hoàn tất submission |
| NFR-007 | Calculation cùng input/version cho cùng output |
| NFR-008 | Mọi lỗi API dùng Problem Details và stable error code |
| NFR-009 | UI có loading, empty, error, unauthorized và retry state |
| NFR-010 | Giao diện chính đáp ứng keyboard, label và contrast cơ bản |
| NFR-011 | Tài liệu có owner, version/status phù hợp; change log tập trung append-only trong PROJECT_TRACKING.md |
| NFR-012 | Không dependency vào internet/Gemini cho kịch bản fallback demo |
| NFR-013 | Gemini outage không làm auth, RBAC, organization, content, assignment, submission, preliminary grading hoặc deterministic recommendation unavailable |

## 12. Use case cấp cao

### UC-01 — CenterManager cấu hình role

1. CenterManager có authorization.roles.create và authorization.roles.manage_permissions đăng nhập.
2. Hệ thống tải permission catalog.
3. Actor chọn account type khi tạo role; account type không sửa được sau khi tạo.
4. UI chỉ cho chọn permission vừa được phép ủy quyền vừa tương thích account type của role.
5. Backend kiểm tra tenant, account-type compatibility, delegation policy theo target account type và last-admin invariant.
6. Transaction lưu role/permission và authorization audit.
7. UI hiển thị effective permission preview.

Kết quả lỗi phải rõ cho validation/concurrency nhưng không làm lộ dữ liệu center khác.

### UC-02 — Gán role cho user

1. Actor tìm user trong cùng center.
2. Actor chỉ được thấy/chọn các active role cùng account type với user.
3. Backend kiểm tra same-center, account-type compatibility và không self-elevate.
4. Transaction thay assignment, ghi audit và tăng authorization version.
5. Session/cache quyền cũ bị invalid theo policy.

### UC-03 — Student hoàn tất attempt

1. Student mở assignment được giao.
2. Student gửi answer, reasoning, confidence, time, behavior và client submission ID.
3. BLL preliminary grade và kiểm tra availability/idempotency.
4. Attempt và AI job được commit atomically.
5. API trả 202; Gemini không nằm trong HTTP transaction.

### UC-04 — AI processing và Evidence Gate

1. Worker claim lease.
2. Gemini trả structured observation hoặc lỗi; HTTP submission không chờ Gemini.
3. Output được validate; lỗi retry tối đa một lần.
4. Sau lỗi cuối, deterministic fallback được tạo và đánh dấu review; luồng vẫn kết thúc mà không giả AI output.
5. ReasoningAnalysis/provenance phù hợp được lưu.
6. Evidence Gate kiểm tra contradiction/semantic trước confidence và đánh giá source, trust level, decision mode, weight/reasons.
7. Twin Orchestrator chỉ dùng effective evidence.
8. History lưu toàn bộ calculation.

### UC-05 — Teacher override và replay

1. Teacher có permission và ownership mở review queue.
2. Teacher xem AI output, confidence, evidence decision và attempt.
3. Teacher nhập override/reason.
4. Transaction kiểm tra override version.
5. Evidence mới được tạo; Twin/recommendation được replay.
6. History/audit không mất dữ liệu cũ.

## 13. Evidence Gate v1

Ba chiều không được trộn:

| Dimension | Giá trị v1 | Trả lời câu hỏi |
|---|---|---|
| sourceType | AI, RuleFallback, TeacherOverride | Evidence đến từ đâu? |
| trustLevel | Trusted, Reduced, ReviewOnly | Evidence được tin đến mức nào? |
| decisionMode | AIWeighted, DeterministicOnly, HumanConfirmed | Quyết định được hình thành theo cơ chế nào? |

| Trường hợp | sourceType | trustLevel | decisionMode | reasoning weight |
|---|---|---|---|---:|
| AI hợp lệ, không contradiction, confidence 80–100 | AI | Trusted | AIWeighted | 1.00 |
| AI hợp lệ, không contradiction nghiêm trọng, confidence 50–79 | AI | Reduced | AIWeighted | 0.50 |
| AI confidence dưới 50 hoặc anomaly/contradiction | AI | ReviewOnly | AIWeighted | 0.00 |
| Gemini không khả dụng, dùng rule fallback | RuleFallback | ReviewOnly | DeterministicOnly | 0.00 |
| Teacher xác nhận/sửa có permission, scope và lý do | TeacherOverride | Trusted | HumanConfirmed | 1.00 |

Preliminary `isCorrect = null` giữ nguyên provenance source thực tế nhưng bắt buộc `ReviewOnly`, reasoning weight `0.00` và `requiresTeacherReview = true`. AI vẫn có thể tạo observation cho Essay, nhưng không được chuyển `null` thành `false`, không dùng giá trị neutral thay thế và không kích hoạt Knowledge Mastery. Teacher grade/override tạo EvidenceAssessment `TeacherOverride + Trusted + HumanConfirmed`, sau đó replay mới được phép cập nhật Mastery.

Replay là event trong Twin history, không phải source/trust. Threshold là configuration có policy version, không hard-code rải rác. Structural validation, semantic validation và deterministic contradiction check chạy trước confidence. Behavior telemetry vẫn có thể cập nhật khi reasoning weight bằng 0; correctness chỉ được dùng khi là giá trị quan sát non-null.

## 14. ML.NET decision gate

Không thêm ML chỉ để có nhãn Machine Learning. Chỉ mở task ML khi có:

1. prediction target/label rõ;
2. dataset có nguồn và consent phù hợp;
3. dữ liệu train/validation tách rõ;
4. deterministic baseline;
5. metric và acceptance threshold;
6. model/version/feature logging;
7. fallback khi model không khả dụng.

Ứng viên phù hợp: dự báo risk hoặc xác suất hoàn thành, không thay thế authorization hay chấm điểm cuối.

## 15. Requirement validation plan

Nhóm cần tối thiểu:

- phỏng vấn hoặc walkthrough với đại diện CenterManager;
- phỏng vấn Teacher về assignment, review và override;
- thử prototype với Student;
- Figma review cho role management và learning flow;
- security walkthrough cho role–permission/user–role lệch account type;
- ghi câu hỏi, câu trả lời, thay đổi và ngày xác nhận;
- liên kết evidence trong PROJECT_TRACKING.md.

## 16. Definition of Ready

Một implementation task chỉ Ready khi:

- requirement ID và source rõ;
- actor và permission rõ;
- happy path/error path rõ;
- table/API/UI liên quan được khóa;
- migration/compatibility plan rõ nếu đổi data;
- acceptance có thể kiểm thử;
- human owner và reviewer đã có.

## 17. Definition of Done

- Requirement được triển khai đúng contract.
- Unit test pass.
- MySQL integration test pass khi liên quan ràng buộc/provider.
- Frontend test/E2E pass khi liên quan UI/authorization.
- Không cross-tenant leak.
- Không gán được role/permission lệch account type ở UI, API, BLL hoặc database.
- Docs/traceability/tracking được cập nhật.
- Human owner giải thích được thiết kế và demo được.
- Reviewer độc lập approve.

## 18. Dữ liệu còn thiếu, không được tự bịa

- Mã sinh viên/GitHub/kỹ năng/giờ khả dụng của Tuấn Tài, Thịnh, Khoa, Thành Tài và Sơn.
- Tên và URL course repository.
- Ngày chính xác của tuần 1 và lịch học kỳ.
- Stakeholder interview records.
- Figma link và approval.
- Tiêu chí chấm/format báo cáo chính thức nếu giảng viên bổ sung.

Các mục này phải được cập nhật trước khi tài liệu chuyển từ draft sang FROZEN.
