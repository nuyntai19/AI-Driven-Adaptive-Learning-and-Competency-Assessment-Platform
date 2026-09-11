# EduTwin — Hiến pháp kỹ thuật

> Phiên bản: 2.1-draft
> Trạng thái: COURSE REBASELINE — source migration chưa hoàn tất
> Baseline v1: 2026-07-15
> Re-baseline bắt đầu: 2026-09-08; làm rõ course architecture: 2026-09-09
> Chủ sở hữu quyết định: Nhóm EduTwin; giảng viên/stakeholder sở hữu yêu cầu tương ứng
> AI reviewer: Codex/Gemini chỉ tư vấn và kiểm tra, không sở hữu quyết định

## 1. Mục đích và hiệu lực

Tài liệu này là nguồn invariant kỹ thuật cao nhất của EduTwin, nhưng không đứng trên yêu cầu môn học hoặc yêu cầu stakeholder đã được xác minh.

README.md là cổng vào repository. Thành viên nhóm và AI hỗ trợ phải đọc tài liệu authoritative theo thứ tự:

1. PROJECT_REQUIREMENTS.md.
2. CONSTITUTION.md.
3. DATABASE_SCHEMA.md nếu task có data/persistence.
4. API_CONTRACTS.md nếu task có HTTP/frontend integration.
5. UI_UX_SPEC.md nếu task có UI/interaction.
6. MASTER_PLAN.md.
7. PROJECT_TRACKING.md và TEAM_ASSIGNMENT.md.
8. CODEBASE_CHANGE_PLAN.md khi lập task migration từ prototype.
9. PROMPT_TEMPLATES.md.

Năm file kỹ thuật cốt lõi giữ thứ tự CONSTITUTION → DATABASE_SCHEMA → API_CONTRACTS → MASTER_PLAN → PROMPT_TEMPLATES. Các file requirements, UI, tracking, phân công và code-change plan bổ sung bằng chứng môn học hoặc điều phối; chúng không tạo schema/API thứ hai.

Thứ tự ưu tiên khi có mâu thuẫn:

1. Yêu cầu giảng viên đã được ghi nhận và xác nhận.
2. Yêu cầu stakeholder đã được xác minh.
3. Quyết định thay đổi đã được nhóm phê duyệt bằng văn bản.
4. PROJECT_REQUIREMENTS.md.
5. CONSTITUTION.md.
6. DATABASE_SCHEMA.md và API_CONTRACTS.md; conflict giữa hai file phải dừng để xử lý.
7. UI_UX_SPEC.md.
8. MASTER_PLAN.md.
9. PROJECT_TRACKING.md.
10. PROMPT_TEMPLATES.md.
11. Source code hiện tại.

Source code không được dùng để hợp thức hóa một hành vi trái specification. Khi phát hiện mâu thuẫn, AI Developer phải dừng phần bị ảnh hưởng, ghi đề xuất thay đổi và chờ phê duyệt.

## 2. Tuyên ngôn sản phẩm

EduTwin là nền tảng học tập thích ứng, đánh giá năng lực và quản trị trung tâm theo mô hình Multi-tenant dành cho giáo dục THPT. Nền tảng thu thập bằng chứng từ bài làm, cách trình bày lời giải và hành vi học tập để xây dựng Learning Digital Twin có thể giải thích.

AI là khả năng nâng cao chất lượng phân tích reasoning và phản hồi, không phải lõi vận hành hay điều kiện sống còn. Authentication, phân quyền, quản lý dữ liệu, giao bài, nộp bài, chấm sơ bộ deterministic, lưu evidence, cập nhật hành vi, xem tiến độ và tạo khuyến nghị fallback phải hoạt động khi Gemini hoặc Internet không khả dụng.

Luồng giá trị trung tâm:

~~~text
CenterManager cấu hình tổ chức và quyền trong Center
→ Giáo viên quản lý nội dung và giao bài
→ Học sinh nộp đáp án, reasoning_text và telemetry
→ BLL chấm sơ bộ, lưu Attempt và khởi tạo xử lý phân tích
→ Gemini tạo observation nếu khả dụng; nếu không, deterministic fallback hoàn tất luồng
→ Evidence Gate đánh giá nguồn, độ tin cậy và chế độ quyết định
→ BLL cập nhật Behavior Twin; Knowledge Twin chỉ đổi khi evidence được phép
→ Recommendation engine dùng dữ liệu đã xác thực và luôn có linear/rule fallback
→ Student, Teacher và CenterManager nhận dashboard/hành động phù hợp quyền
~~~

Gemini chỉ cung cấp một observation có thể sai hoặc vắng mặt. Chấm điểm sơ bộ, Evidence Gate, dữ liệu quan sát được, thuật toán BLL có version và Teacher Override mới là nguồn quyết định. AI không tự giải bài thay học sinh, không là người chấm cuối, không cấp quyền và không trực tiếp quyết định mastery, risk hoặc recommendation.

## 3. Phạm vi MVP đã khóa

### 3.1. Có trong MVP

- Multi-tenant B2B SaaS theo mô hình Shared Database, Shared Schema.
- Ba account type: Student, Teacher, CenterManager.
- Dynamic role và permission theo từng Center; một user có thể có nhiều role cùng account type.
- CenterManager đảm nhận tenant administration trong Center của mình; không có Platform/System Admin.
- Center được provision bằng migration/seed/deployment có kiểm soát; course MVP không có UI/API cho CenterManager tạo, xóa hoặc quản lý Center khác.
- Permission catalog do source định nghĩa; CenterManager cấu hình role và assignment qua UI.
- Quản lý Center, User, Teacher, Student, Class và Class membership.
- Subject và Knowledge Graph dạng DAG.
- Question Bank với MultipleChoice, ShortAnswer và Essay.
- Tất cả loại câu hỏi có thể yêu cầu reasoning_text.
- Assignment cho toàn Class hoặc nhóm Student.
- Learning Mode; không có Exam Mode.
- Gemini Reasoning Analysis trả structured JSON.
- BackgroundService xử lý AI job bền vững qua bảng ai_analysis_jobs.
- Rule-based fallback khi AI thất bại.
- Knowledge Twin, Behavior Twin, Student Subject Goal và Twin Update History.
- Mastery heuristic ưu tiên reasoning_quality.
- Risk Score theo mục tiêu điểm từng môn.
- Opportunity Gap theo Topic và Recommendation cho Topic + Question.
- Student Dashboard, Teacher Dashboard và Center Dashboard.
- Teacher Override và deterministic replay.
- React + Vite, Zustand, TanStack Query, TailwindCSS, Axios.
- Docker Compose gồm mysql, api, web, adminer.
- Unit Test cho BLL lõi; coverage tối thiểu 80% cho nhóm thuật toán lõi.
- GitHub Actions chạy dotnet build và dotnet test khi Pull Request vào main.

### 3.2. Không có trong MVP

- Exam Mode và thi thử toàn đề.
- Public registration và email verification.
- Global Super Admin.
- OCR, ảnh bài làm, chữ viết tay và LaTeX rendering.
- PDF/Video import, vector search, RAG và recommendation tài liệu/video.
- SignalR, Redis, message broker và Quartz.NET.
- Multi-model ensemble hoặc tự động xoay nhiều API key.
- Rate limiting, payment, subscription và billing.
- Teacher Twin, Center Twin và AI chấm hiệu suất giáo viên.
- Full i18n; UI chỉ dùng tiếng Việt.
- Full browser/device matrix và cloud-scale load test; selected integration, frontend và E2E security test vẫn bắt buộc.
- Cloud deployment/CD; chỉ yêu cầu cấu trúc container cloud-ready.

## 4. Stack bắt buộc

### 4.1. Backend

- .NET 10 LTS.
- ASP.NET Core Web API.
- Entity Framework Core 10.
- MySQL 8.x, InnoDB, utf8mb4.
- JWT Access Token + Refresh Token.
- FluentValidation hoặc validation tương đương tại boundary.
- ILogger/Serilog cho structured logging.
- xUnit và Moq cho Unit Test.
- Gemini là AI provider duy nhất của MVP, được che sau IAIService.

Không được chuyển sang FastAPI, Node.js backend, Supabase hoặc PostgreSQL.

### 4.2. Frontend

- ReactJS + Vite.
- TypeScript.
- TailwindCSS.
- Axios.
- Zustand cho client state.
- TanStack Query cho server state, cache và polling.
- Recharts cho Radar, Line và Bar chart.

Không dùng Redux, Next.js hoặc SignalR nếu chưa có quyết định thay đổi được phê duyệt.

### 4.3. Hạ tầng

- Docker Compose.
- MySQL 8.x.
- Adminer.
- Git/GitHub.
- GitHub Actions cho CI cơ bản.

Mọi secret phải đến từ environment variable hoặc local secret store; không commit API key, JWT key hoặc mật khẩu database.

## 5. Kiến trúc bắt buộc

EduTwin là Modular Monolith với ba project vật lý chính và một project contract:

~~~text
src/
├── EduTwin.API/          Presentation Layer
├── EduTwin.BLL/          Business Logic Layer
├── EduTwin.DAL/          Data Access Layer
└── EduTwin.Contracts/    DTO, request/response contract, enum contract

tests/
└── EduTwin.BLL.Tests/

web/
└── edutwin-web/
~~~

Chiều dependency duy nhất:

~~~text
EduTwin.API → EduTwin.BLL → EduTwin.DAL
EduTwin.API → EduTwin.Contracts
EduTwin.BLL → EduTwin.Contracts
EduTwin.DAL không tham chiếu API
~~~

Không tạo dependency vòng. Presentation không được chứa business rule. DAL không được gọi AI hoặc quyết định Mastery. BLL không được trả EF Entity trực tiếp cho API.

## 6. Module logic

Mỗi project backend phải chia folder/namespace theo cùng feature:

- IdentityAndTenancy.
- Organization.
- KnowledgeGraph.
- CurriculumAndQuestions.
- Assignments.
- AssessmentAndReasoning.
- DigitalTwin.
- Recommendations.
- Dashboards.

Một module chỉ giao tiếp với module khác qua BLL service/interface rõ ràng. Không truy cập repository của module khác từ Controller.

## 7. Quy tắc ba Layer

### 7.1. Presentation Layer

Được phép:

- Authentication/authorization boundary.
- Parse request, model validation và mapping DTO.
- Gọi đúng một orchestration service của BLL.
- Chuyển business exception thành HTTP response chuẩn.
- Correlation ID, logging scope và response headers.

Bị cấm:

- Tính Mastery, Risk, Opportunity Score.
- Truy vấn DbContext trực tiếp.
- Chứa tenant filter thủ công rải rác.
- Gọi Gemini trực tiếp.
- Trả stack trace hoặc secret cho client.

### 7.2. Business Logic Layer

Chịu trách nhiệm:

- Tất cả invariant và authorization theo ownership.
- Transaction boundary.
- Mastery, Risk, Opportunity Gap và deterministic replay.
- DAG cycle detection.
- AI orchestration và fallback policy.
- Chuyển Entity thành contract/DTO.
- Quyết định trạng thái Assignment, Attempt, Job, Recommendation.

BLL service phải có interface và tập trung theo use case. Pattern chỉ dùng khi giải quyết vấn đề thật; cấm tạo abstraction không có consumer.

### 7.3. Data Access Layer

Chịu trách nhiệm:

- EF Core DbContext, Entity Configuration và Migration.
- Repository thực dụng cho aggregate/query phức tạp.
- Query object/projection phục vụ Dashboard.
- Global Query Filter cho tenant và soft delete.
- Transaction implementation.
- Seed Data deterministic.

Generic Repository không được che toàn bộ khả năng của EF Core một cách máy móc. Có thể dùng repository theo aggregate hoặc query service chuyên biệt.

## 8. Multi-tenant là invariant an toàn cấp 0

Mô hình bắt buộc: Shared Database, Shared Schema, tenant discriminator center_id.

Quy tắc:

- Mọi bảng tenant-owned phải có center_id.
- center_id lấy từ JWT claim và ITenantContext, không lấy từ request body.
- DbContext phải áp dụng Global Query Filter theo center_id.
- Soft-delete filter và tenant filter phải đồng thời có hiệu lực.
- BLL phải kiểm tra ownership tại use case nhạy cảm.
- IgnoreQueryFilters chỉ được dùng trong hạ tầng có lý do ghi chú rõ; MVP không có Global Admin nên mặc định cấm trong business flow.
- Foreign key tenant-scoped phải dùng composite alternate key khi DATABASE_SCHEMA.md yêu cầu.
- Cache key, log scope và file path phải mang CenterId nếu có tenant data.
- BackgroundService phải dựng TenantContext từ center_id của job trước khi xử lý.
- Hai Center seed phải được dùng trong test cách ly tenant.

Không endpoint nào cho phép client đổi center_id. Truy cập ID hợp lệ nhưng thuộc Center khác phải trả 404 để không làm lộ sự tồn tại.

## 9. Identity và authorization

- Access Token sống ngắn; Refresh Token sống dài hơn và được rotate.
- Chỉ lưu hash của Refresh Token.
- Password phải hash bằng cơ chế chuẩn của ASP.NET Core Identity hoặc PasswordHasher tương đương; không tự thiết kế thuật toán.
- Account type hợp lệ: Student, Teacher, CenterManager. Account type mô tả domain context, không được dùng như toàn bộ permission model sau cutover.
- Permission code là catalog do hệ thống định nghĩa và phải có server-side enforcement.
- Permission applicability theo account type phải được lưu quan hệ chuẩn hóa, không giấu trong JSON nếu cần relational join/FK.
- Role thuộc đúng một Center và đúng một account type; role code unique trong Center; account type immutable sau khi tạo.
- Role chỉ được nhận permission cho phép account type đó; user chỉ được nhận role trùng account type.
- Account type không thể bị thay đổi gián tiếp bằng role assignment; đổi account type cần use case/migration riêng được duyệt.
- Một user có thể nhận nhiều active role cùng Center.
- Effective permission v1 là hợp các permission từ active role; không có explicit deny.
- CenterManager chỉ quản lý role, permission assignment và user-role assignment trong Center của mình.
- CenterManager có authorization.roles.manage_permissions được cấp permission Active/delegable/tương thích cho role Student hoặc Teacher. Khi target là role/user CenterManager, permission mới phải là subset effective permission của actor; luôn cấm tự nâng quyền và làm mất tenant administrator cuối cùng.
- Mọi thay đổi role/permission/assignment phải có authorization audit append-only và làm authorization version cũ mất hiệu lực theo policy.
- Trường JSON authorizationVersion và claim auth_version đều ánh xạ duy nhất tới users.auth_version; không tạo cột/version thứ hai.
- Password reset, thay đổi trạng thái user, replace user-role và thay role-permission ảnh hưởng user phải tăng users.auth_version trong cùng transaction. Access token có version cũ bị từ chối; refresh token của user bị ảnh hưởng phải được revoke theo cùng policy.
- TenantAdminCorePermissionsV1 gồm authorization.permissions.read, authorization.roles.read, authorization.roles.create, authorization.roles.update, authorization.roles.archive, authorization.roles.manage_permissions, authorization.user_roles.read, authorization.user_roles.assign và authorization.audit.read. Sau mọi mutation authorization phải còn ít nhất một User Active có account type CenterManager và effective permission chứa đủ tập này.
- Logout/revoke phải vô hiệu Refresh Token.
- User bị khóa phải mất quyền refresh.

Authorization phải kết hợp Authentication + Permission + Tenant + Resource Ownership/Scope. UI ẩn nút không phải security control.

Trong migration:

- Endpoint chưa migrate tiếp tục dùng legacy policy + tenant + ownership.
- Endpoint đã migrate chỉ dùng permission + tenant + ownership.
- Cấm dùng biểu thức legacy role OR dynamic permission làm đường cho phép chung.

## 10. Quy tắc dữ liệu

- Tên bảng/cột vật lý dùng snake_case; Entity/Property C# dùng PascalCase.
- UUID dùng Guid lưu dạng VARCHAR(36), chữ thường canonical.
- Bảng transaction cường độ cao dùng BIGINT UNSIGNED AUTO_INCREMENT.
- Thời gian lưu UTC bằng DATETIME(6).
- Điểm số dùng DECIMAL, không dùng FLOAT cho giá trị nghiệp vụ.
- JSON chỉ dùng cho payload có cấu trúc thay đổi hoặc calculation breakdown; quan hệ cần join/query phải chuẩn hóa thành bảng.
- Tất cả aggregate mutable có audit fields và soft delete.
- Lịch sử nghiệp vụ như Attempt, Reasoning Analysis và Twin Update History là append-oriented; không hard delete.
- row_version dùng làm optimistic concurrency token tại aggregate có thể bị sửa đồng thời.
- Database constraint và BLL validation cùng tồn tại; không chọn một bỏ một.
- Mọi migration phải có tên diễn đạt nghiệp vụ, có thể áp dụng trên database mới và không sửa migration đã merge.

DATABASE_SCHEMA.md là nguồn duy nhất cho table, column, key, index và delete behavior.

## 11. Knowledge Graph

- Knowledge Graph dùng node hierarchy và edge.
- Edge PrerequisiteOf tạo DAG; self-loop và cycle bị cấm.
- Cycle detection thực hiện ở BLL trước SaveChanges.
- Seed Data cũng phải đi qua cùng cycle validator hoặc một validator tương đương.
- Topic là đơn vị tính Knowledge Twin, Opportunity Gap và biểu đồ.
- Chapter, Skill, Concept hỗ trợ phân loại/truy vết nhưng không bắt buộc có Twin riêng trong MVP.
- Xóa node đã có Attempt/Twin phải bị chặn; dùng soft delete và trạng thái inactive.

## 12. AI governance

### 12.1. Boundary

- BLL chỉ phụ thuộc IAIService.
- Gemini implementation nằm sau adapter.
- AI input chỉ gồm dữ liệu cần thiết cho một Attempt.
- AI output bắt buộc theo JSON Schema được version hóa trong contract.
- Deserialize, schema validation và semantic validation phải hoàn tất trước khi ghi Reasoning Analysis.
- AI không được tự thay đổi schema, Knowledge Graph, correct answer hoặc Teacher-authored content.
- Reasoning Analysis phải qua deterministic Evidence Gate trước khi ảnh hưởng Digital Twin.
- Evidence source, trust level và decision mode là ba chiều độc lập: source = AI/RuleFallback/TeacherOverride; trust = Trusted/Reduced/ReviewOnly; mode = AIWeighted/DeterministicOnly/HumanConfirmed.
- Replay là loại sự kiện/history, không phải evidence source hoặc trust level.
- AI confidence chỉ được xét sau structural validation, semantic validation và kiểm tra contradiction deterministic; confidence cao không được vượt qua anomaly hoặc dữ liệu không nhất quán.
- Correctness, score, time, student confidence và answer changes là observed evidence; chúng không được giả thành AI Reasoning Analysis.
- Gemini không được trực tiếp quyết định mastery, risk, opportunity ranking hoặc recommendation.

### 12.2. Retry và fallback

- Mỗi AI Analysis Job retry tối đa một lần.
- Sau lần thất bại thứ hai, dùng Rule-based fallback.
- Fallback chỉ dựa trên đáp án cuối cùng và dữ liệu deterministic.
- Fallback đặt reasoning_quality = null và needs_teacher_review = true.
- Job phải kết thúc ở Completed, FallbackCompleted hoặc FailedTerminal; không được treo Processing vô hạn.
- Khi API restart, job Processing quá timeout phải được đưa lại Pending theo recovery policy.

### 12.3. Logging

- Ghi provider, model, latency, token usage, status và correlation ID.
- Không tạo bảng log AI riêng.
- Không ghi API key.
- Không ghi password, Refresh Token hoặc JWT.
- Mock Data được sử dụng, nhưng log vẫn phải có khả năng redaction.
- Raw AI response chỉ ghi ở Development khi cấu hình debug bật; mặc định Production-like local demo phải tắt.

### 12.4. Teacher Override

- Không xóa kết quả AI gốc.
- Override phải lưu Teacher, lý do, giá trị sửa và thời gian.
- Effective analysis ưu tiên override.
- Sau override phải deterministic replay toàn bộ Attempt liên quan của Student + Topic.
- Replay, Twin update và History authoritative nằm trong Transaction A. Recommendation replacement là derived state, chỉ chạy best-effort sau khi A commit thành công trong Transaction B riêng, có timeout/logging và generation watermark; lỗi B không được rollback A.

## 13. Evidence Gate và Mastery heuristic v2

Mọi giá trị chuẩn hóa về 0–1 trước khi tính.

Ký hiệu:

- R: reasoning_quality / 100; null nếu fallback.
- C: 1 nếu đúng, 0 nếu sai.
- T: time_quality từ 0 đến 1.
- K: confidence calibration từ 0 đến 1.
- D: difficulty multiplier; 0.85, 0.925, 1.0, 1.075, 1.15 cho difficulty 1–5.
- M: Mastery hiện tại từ 0 đến 100.

Evidence Gate chạy trước mọi thay đổi Knowledge Twin. Gate dùng policy có version, dữ liệu quan sát được và kết quả semantic validation; không gọi AI thêm lần nữa.

| Trust level | Source/mode điển hình | Điều kiện mặc định | ReasoningWeight | Hành động |
|---|---|---|---:|---|
| Trusted | AI/AIWeighted hoặc TeacherOverride/HumanConfirmed | Validation hợp lệ, không contradiction; AI confidence 80–100 hoặc giáo viên xác nhận hợp lệ | 1.00 | Cho phép cập nhật/replay theo policy |
| Reduced | AI/AIWeighted | Validation hợp lệ, không contradiction nghiêm trọng; AI confidence 50–79 | 0.50 | Cập nhật giảm trọng số và gắn cờ theo dõi |
| ReviewOnly | AI/AIWeighted hoặc RuleFallback/DeterministicOnly | Confidence dưới 50, fallback, thiếu evidence, anomaly hoặc contradiction | 0.00 | Không đổi Knowledge Mastery; đưa vào hàng review |

Ngưỡng phải nằm trong configuration có version. Thay đổi ngưỡng cần Change Proposal, regression test và migration dữ liệu nếu ảnh hưởng replay.

Khi có Reasoning Analysis hợp lệ:

~~~text
RawEvidenceTarget = 100 × R × (0.65 + 0.20C + 0.10T + 0.05K)
RawDelta = 0.25 × D × (RawEvidenceTarget - M)
NewMastery = Clamp(M + ReasoningWeight × RawDelta, 0, 100)
~~~

Khi fallback hoặc ReviewOnly:

~~~text
ReasoningWeight = 0
NewMastery = M
~~~

Correctness non-null, thời gian và hành vi vẫn được ghi như evidence quan sát được và có thể cập nhật Behavior Twin; chúng không được giả thành Reasoning Analysis.

Nếu preliminary `isCorrect` là null, điển hình Essay chưa được chấm, Gate bắt buộc trả ReviewOnly với reasoning weight 0 và Knowledge Mastery giữ nguyên. AI có thể phân tích reasoning nhưng không được ép `null → false`, không thay bằng correctness neutral và không tạo quyết định cuối. Chỉ TeacherOverride/HumanConfirmed có effective correctness rõ ràng mới được replay vào Mastery.

Hệ quả bắt buộc:

- reasoning_quality có ảnh hưởng lớn nhất.
- Đúng nhưng reasoning kém chỉ tăng ít.
- Sai nhưng reasoning tốt có thể ghi nhận partial mastery.
- Fallback và evidence ReviewOnly không được thay đổi Knowledge Mastery.
- Mỗi lần cập nhật phải lưu input, output, delta và breakdown trong twin_update_history.
- Mỗi quyết định Gate phải lưu trust level, weight, reason code và policy version.
- Mỗi quyết định Gate phải lưu riêng source type, trust level, decision mode và analysis override version; không dùng một enum để biểu diễn nhiều chiều.
- BLL test phân loại reasoning quality phải bao phủ 0, 39, 40, 59, 60, 79, 80, 100 và null.
- BLL test Evidence Gate theo AI confidence phải bao phủ 0, 49, 50, 79, 80, 100 và null; 49/50 và 79/80 là hai cặp biên bắt buộc.

Phân loại hiển thị:

- 0–39: Poor.
- 40–59: Weak.
- 60–79: Acceptable.
- 80–100: Strong.
- null: AI unavailable/fallback.

## 14. Behavior Twin

Behavior Twin được tính theo Student + Subject:

- avg_time_spent_seconds.
- skip_rate.
- change_answer_rate.
- avg_confidence.
- confidence_calibration.
- attempt_count.

Behavior metrics không được lấn át Reasoning trong Mastery. Chúng phục vụ dashboard, time quality và giải thích.

## 15. Risk Score

Risk Score thuộc Student Subject Goal.

~~~text
PredictedScore = 10 × WeightedAverageTopicMastery / 100
ScoreGap = Clamp((TargetScore - PredictedScore) / 10, 0, 1)
TimePressure = 1 - Clamp(RemainingDays / 180, 0, 1)
RiskScore = Round(100 × ScoreGap × (0.70 + 0.30 × TimePressure), 2)
~~~

Topic Mastery được weighted theo exam_importance. Nếu chưa có evidence, Mastery là 0. Risk Score chỉ là heuristic giải thích được, không được quảng bá như dự báo xác suất thống kê.

## 16. Opportunity Gap

Chỉ kích hoạt sau khi Student có ít nhất 3 Attempt trong Subject. Trước đó dùng Rule-based linear path theo order_index và difficulty.

Candidate Topic phải:

- Active.
- Chưa đạt Mastery 80.
- Có toàn bộ prerequisite đạt ít nhất 60, hoặc không có prerequisite.

~~~text
ExpectedScoreGain = (1 - Mastery/100) × ExamImportance
ProbabilityOfMastery = Clamp(
    0.20 + 0.60 × RecentReasoningAverage + 0.20 × PrerequisiteReadiness,
    0,
    1)
RawOpportunity = ExpectedScoreGain × ProbabilityOfMastery
                 / Max(EstimatedLearningHours, 0.5)
~~~

RawOpportunity được normalize 0–100 trong tập candidate hiện tại. Tie-break:

1. Mastery thấp hơn.
2. exam_importance cao hơn.
3. order_index thấp hơn.
4. topic_id tăng dần để deterministic.

Recommendation phải lưu breakdown đầy đủ và chọn một Question active, chưa làm gần đây, phù hợp difficulty mục tiêu.

## 17. Transaction boundary

Các luồng sau bắt buộc dùng database transaction:

- Tạo Student User + Student profile + Class membership.
- Publish Assignment + materialize Assignment Targets.
- Hoàn tất AI job, Transaction A: Reasoning Analysis + Attempt status + Knowledge Twin + Behavior Twin + Goal/Risk + Twin History; sau commit, Recommendation/Learning Path chạy trong Transaction B riêng.
- Teacher Override, Transaction A: override + deterministic replay + Twin/History; sau commit, Recommendation replacement chạy trong Transaction B riêng.
- Refresh Token rotation.

Không giữ database transaction trong thời gian gọi Gemini. AI call diễn ra ngoài transaction; transaction chỉ mở khi output đã hợp lệ hoặc fallback đã được xác định.

Transaction B của Recommendation là best-effort derived state nhưng không được chạy không giới hạn: caller phải đặt timeout, ghi log khi thất bại và dùng generation watermark theo Center/Student/Subject để chống trigger cũ hoặc trùng. Accept/Dismiss phải dùng Student row-lock và transaction riêng khi caller chưa có transaction.

## 18. Background processing

- API lưu Attempt và AI job trước khi trả 202 Accepted.
- BackgroundService poll job Pending từ database.
- Hosted service phải tạo DI scope riêng cho mỗi batch/job.
- Claim của request không tồn tại trong worker; worker lấy center_id từ job và tạo tenant execution scope có kiểm soát.
- Claim/lease job phải chống hai worker xử lý cùng một job.
- Job phải idempotent: Reasoning Analysis unique theo attempt_id.
- Frontend poll mỗi 3 giây và dừng khi trạng thái terminal.
- UI phải có timeout thân thiện và nút thử tải lại trạng thái; không tự tạo Attempt mới.

## 19. API conventions

- Base path: /api/v1.
- Content-Type: application/json; charset=utf-8.
- ID trong URL không đủ để cấp quyền; luôn kiểm tra tenant/ownership.
- Validation error dùng Problem Details.
- Mọi response lỗi có traceId.
- Timestamp theo ISO 8601 UTC.
- Pagination dùng page, pageSize; pageSize tối đa 100.
- Sort/filter dùng allow-list.
- POST tạo resource trả 201; submit async trả 202.
- DELETE nghiệp vụ là soft delete và trả 204.
- Optimistic update yêu cầu rowVersion ở các resource được chỉ định.
- API_CONTRACTS.md là contract đóng băng; không đổi field hoặc status code âm thầm.

## 20. Frontend conventions

- UI 100% tiếng Việt.
- question_text và reasoning_text chấp nhận tiếng Việt hoặc tiếng Anh.
- AI phản hồi cùng ngôn ngữ của reasoning_text.
- Zustand chỉ giữ auth/session và UI state nhỏ.
- TanStack Query quản lý server state.
- Không nhân bản server data dài hạn vào Zustand.
- Axios interceptor được phép refresh token đúng một lần; tránh retry loop.
- Polling job 3 giây và tự dừng ở trạng thái terminal.
- Dashboard phải có loading, empty, error và stale state.
- Chart phải có text/table fallback để người dùng vẫn đọc được số liệu.
- Không đưa center_id vào form do client kiểm soát.
- Không hiển thị dữ liệu từ Center khác kể cả trong cache key.

## 21. Dashboard đã khóa

### 21.1. Student

- Câu hỏi phải trả lời: “Em yếu ở đâu, nên làm gì tiếp theo và có nguy cơ không đạt mục tiêu môn học không?”
- Header: Target Score và Remaining Days.
- Radar: Topic Mastery.
- Line: Twin Update History.
- Action: Opportunity Gap và Question tiếp theo.

### 21.2. Teacher

- Câu hỏi phải trả lời: “Học sinh nào cần hỗ trợ, lỗi chung là gì và nên giao hoạt động/bài tập nào tiếp theo?”
- Class overview: sĩ số và predicted score trung bình.
- High-risk Students.
- Weak Topics dạng Bar chart.
- Gap Groups và action giao Assignment.

### 21.3. Center Manager

- Câu hỏi phải trả lời: “Quyền trong Center có an toàn không, lớp nào cần chú ý và mức đạt mục tiêu toàn Center ra sao?”
- Tổng Teachers, Students, Classes.
- Mastery trung bình theo Subject.
- High-risk Students theo Class.
- Class ranking theo Mastery và Assignment completion.

Không xây Center AI scoring hoặc Teacher performance scoring.

## 22. Testing constitution

Phạm vi bắt buộc:

- xUnit + Moq.
- Unit Test BLL lõi.
- Mastery calculator.
- Risk calculator.
- Opportunity Gap calculator/ranker.
- AI JSON parser và semantic validator.
- DAG cycle detector.
- Teacher Override replay.
- Tenant ownership guards ở service quan trọng.
- Job state machine/fallback policy.
- Database constraint, migration và query quan trọng trên MySQL thật.
- Dynamic permission, privilege escalation, cross-tenant assignment và last-admin protection.
- Evidence Gate, policy boundary và deterministic replay.
- Frontend capability gate, direct URL denial và trạng thái unauthorized.
- Selected end-to-end security flow cho đường nghiệp vụ quan trọng.

Coverage:

- Nhóm thuật toán BLL lõi tối thiểu 80%.
- Coverage không thay thế test boundary và invariant.

Mỗi test phải deterministic, không gọi Gemini thật, dùng TimeProvider hoặc clock abstraction thay vì thời gian hệ thống trực tiếp và không dùng shared mutable state.

## 23. Git, CI và commit gate

- main là nhánh được bảo vệ bằng Pull Request.
- Mỗi Phase dùng branch riêng.
- Commit nhỏ, mô tả theo Conventional Commits.
- Không commit khi build/test đỏ.
- Không trộn refactor ngoài scope vào task.
- GitHub Actions bắt buộc chạy backend restore/build/test, frontend clean install/build, EF pending-model check và tập MySQL integration test đã chọn.
- Migration đi cùng code sử dụng migration đó.
- Sau mỗi Phase phải chạy local bằng Docker Compose theo MASTER_PLAN.md trước khi merge.

## 24. Docker và cấu hình

Bốn service duy nhất:

- mysql.
- api.
- web.
- adminer.

mysql phải có healthcheck. api chỉ start luồng nghiệp vụ sau khi database healthy. Migration/seed phải có chiến lược rõ ràng, không tạo race khi container restart.

Configuration bắt buộc qua environment:

- ConnectionStrings__Default.
- Jwt__SigningKey.
- Jwt__Issuer.
- Jwt__Audience.
- Gemini__ApiKey.
- Gemini__Model.
- Logging level.

File .env thật không commit; chỉ commit .env.example không chứa secret.

## 25. Definition of Done toàn cục

Một task chỉ Done khi:

- Không vi phạm bộ tài liệu authoritative và requirement ID của task.
- Có human owner, acceptance criteria và bằng chứng review.
- Chỉ sửa file được cho phép.
- Build thành công.
- Test liên quan thành công.
- Migration/seed áp dụng được trên MySQL thật nếu có thay đổi dữ liệu đã duyệt.
- API contract không drift.
- Tenant isolation được kiểm tra.
- Permission, account-type compatibility và resource scope được kiểm tra ở server nếu task có authorization.
- Evidence provenance và policy version được lưu nếu task ảnh hưởng Digital Twin.
- Error/empty/loading state được xử lý nếu có UI.
- Không có secret hoặc log nhạy cảm.
- Có self-review và danh sách file đã đổi.
- Không còn TODO che giấu yêu cầu acceptance.

Một Phase chỉ Done khi:

- Tất cả task Done.
- Docker Compose chạy được từ môi trường sạch.
- Demo checkpoint của Phase chạy thành công.
- Commit/PR độc lập có thể review.
- MASTER_PLAN checklist được cập nhật bởi người thực thi.

## 26. Quy tắc dành cho AI Developer

AI Developer bắt buộc:

- Đọc PROJECT_REQUIREMENTS.md, CONSTITUTION.md và các tài liệu authoritative liên quan trước khi làm.
- Chỉ thực hiện Task ID được giao.
- Ghi human owner, requirement ID, expected HEAD, allow-list và acceptance gate trong prompt.
- Liệt kê assumption trước khi sửa.
- Không tự đổi schema, endpoint, architecture, enum hoặc thuật toán.
- Không thêm package nếu task không cho phép.
- Không sửa file ngoài allow-list.
- Không dùng shortcut làm mất tenant isolation.
- Không bỏ validation/test để làm demo chạy nhanh.
- Báo BLOCKED nếu specification thiếu hoặc mâu thuẫn.
- Khi đề xuất thay đổi, dùng Change Proposal trong PROMPT_TEMPLATES.md và chờ duyệt.
- Dừng và báo trạng thái nếu lặp cùng thao tác, command không tiến triển hoặc quota/tool bị gián đoạn.
- Không stage, commit, push hay nhận quyền tác giả nếu chưa có chỉ dẫn closeout rõ ràng của con người.

AI Developer bị cấm:

- Tự chạy migration phá dữ liệu.
- Hard delete dữ liệu audit.
- Gọi AI trực tiếp từ Controller.
- Đưa business logic vào React component hoặc Controller.
- Trả EF Entity ra API.
- Tạo endpoint không có trong API_CONTRACTS.md.
- Lưu API key trong code/repository.
- Tự ý “cải tiến” sang microservices, Clean Architecture hoặc Event Sourcing.
- Che giấu việc dùng AI, tạo commit giả hoặc chia baseline cũ thành đóng góp mới.

## 27. Change control

Mọi thay đổi frozen decision phải có:

- Change ID.
- Vấn đề và bằng chứng.
- File/contract bị ảnh hưởng.
- Phương án A/B.
- Tác động migration, API, test và timeline.
- Khuyến nghị của Codex.
- Human decision owner và người phê duyệt.

Chỉ sau phê duyệt mới cập nhật specification trước, rồi mới cập nhật source code.

## 28. Course rebaseline và provenance

- Pre-course source-code snapshot là commit `2d768f270e0395bcafcbcab2305ac3617fb5f9ca`; đây là mốc audit code, không phải mặc nhiên là commit import của repository môn học.
- Documentation rebaseline checkpoint là commit `b14f6c4171dc55043a3bb910332061c55ba66a7f`; correction sau checkpoint vẫn thuộc snapshot chuẩn bị trước môn cho tới khi nhóm khóa baseline.
- Repository môn học nhập một snapshot code + tài liệu đã được duyệt dưới một initial-import commit riêng. PROJECT_TRACKING.md phải lưu repository nguồn, hai source checkpoint, full SHA snapshot được nhập, ngày import và SHA initial-import.
- Chỉ commit sau initial-import commit của repository môn học mới được dùng làm bằng chứng đóng góp trong học kỳ.
- Không rewrite lịch sử để biến source cũ thành đóng góp mới của thành viên.
- Yêu cầu giảng viên, stakeholder interview, Figma approval, weekly demo và quyết định nhóm phải có ngày, người xác nhận và liên kết bằng chứng.
- Phần trăm hoàn thành không được tự ước lượng từ số commit; phải dựa trên acceptance criteria đã được xác minh.

## 29. Nguyên tắc migration từ prototype sang v2

- Rebaseline là thay đổi tăng dần, không viết lại toàn hệ thống.
- Specification được chốt trước schema; schema trước API; API trước UI.
- Dynamic RBAC được triển khai theo capability slice, có compatibility window và cutover rõ ràng.
- Không cho phép legacy role OR dynamic permission trở thành đường cấp quyền lâu dài.
- Evidence Gate được đặt trước Twin mutation; source cũ chỉ được chuyển sang sau khi có regression baseline.
- Mỗi migration dữ liệu phải có forward plan, backup/rollback procedure, idempotency check và validation query.
- Chỉ xóa compatibility code khi toàn bộ endpoint trong slice đã migrate, test security xanh và nhóm phê duyệt.
- Thay đổi schema lớn phải thử trên bản sao dữ liệu hoặc fixture đại diện trước khi áp dụng môi trường dùng chung.

## 30. Nguồn kỹ thuật nền

- [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [EF Core Multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy)
- [ASP.NET Core Background Tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Gemini Structured Outputs](https://ai.google.dev/gemini-api/docs/structured-output)
- [TanStack Query Polling](https://tanstack.com/query/v5/docs/framework/react/guides/polling)
- [MySQL 8 CHECK Constraints](https://dev.mysql.com/doc/refman/8.0/en/information-schema-table-constraints-table.html)
- [Docker Compose startup order](https://docs.docker.com/compose/how-tos/startup-order/)
