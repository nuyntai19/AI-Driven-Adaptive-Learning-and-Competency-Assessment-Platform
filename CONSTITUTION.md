# EduTwin — Hiến pháp kỹ thuật

> Phiên bản: 1.0  
> Trạng thái: FROZEN — Baseline cho MVP  
> Ngày khóa: 2026-07-15  
> Chủ sở hữu quyết định: Product Owner  
> Kiến trúc sư kiểm duyệt: Codex  

## 1. Mục đích và hiệu lực

Tài liệu này là nguồn luật kỹ thuật cao nhất của EduTwin. Claude, Gemini và mọi AI Developer phải đọc toàn bộ năm tài liệu theo thứ tự:

1. CONSTITUTION.md
2. DATABASE_SCHEMA.md
3. API_CONTRACTS.md
4. MASTER_PLAN.md
5. PROMPT_TEMPLATES.md

Thứ tự ưu tiên khi có mâu thuẫn:

1. Quyết định mới đã được Product Owner phê duyệt bằng văn bản.
2. CONSTITUTION.md.
3. DATABASE_SCHEMA.md.
4. API_CONTRACTS.md.
5. MASTER_PLAN.md.
6. PROMPT_TEMPLATES.md.
7. Source code hiện tại.

Source code không được dùng để hợp thức hóa một hành vi trái specification. Khi phát hiện mâu thuẫn, AI Developer phải dừng phần bị ảnh hưởng, ghi đề xuất thay đổi và chờ phê duyệt.

## 2. Tuyên ngôn sản phẩm

EduTwin là nền tảng AI-Native, Multi-tenant dành cho trung tâm giáo dục THPT. Hệ thống xây dựng Learning Digital Twin cho từng học sinh từ bài làm, cách trình bày lời giải và hành vi học tập.

Luồng giá trị trung tâm:

~~~text
Giáo viên giao bài
→ Học sinh nộp đáp án và reasoning_text
→ AI trả JSON phân tích tư duy
→ Hệ thống cập nhật Knowledge Twin và Behavior Twin
→ Opportunity Gap chọn Topic và Question tiếp theo
→ Student, Teacher và Center Manager theo dõi Dashboard
~~~

AI không phải chatbot phụ trợ. AI cung cấp Reasoning Analysis; dữ liệu có cấu trúc, thuật toán BLL và Teacher Override mới là nguồn quyết định cuối cùng.

## 3. Phạm vi MVP đã khóa

### 3.1. Có trong MVP

- Multi-tenant B2B SaaS theo mô hình Shared Database, Shared Schema.
- Ba vai trò: Student, Teacher, CenterManager.
- Center Manager đồng thời đảm nhận chức năng Admin trong phạm vi Center.
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
- Integration Test, E2E Test và Frontend automated test trong phạm vi bắt buộc.
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
- Role hợp lệ: Student, Teacher, CenterManager.
- CenterManager tạo Teacher.
- Teacher tạo Student và quản lý Class của mình.
- Student chỉ xem dữ liệu bản thân.
- Teacher chỉ xem Student thuộc Class mình phụ trách.
- CenterManager chỉ xem dữ liệu trong Center của mình.
- Logout/revoke phải vô hiệu Refresh Token.
- User bị khóa phải mất quyền refresh.

Authorization phải kết hợp Role + Resource Ownership; chỉ Role check là chưa đủ.

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
- Replay, Twin update, History và Recommendation replacement nằm trong một transaction.

## 13. Mastery heuristic đã khóa

Mọi giá trị chuẩn hóa về 0–1 trước khi tính.

Ký hiệu:

- R: reasoning_quality / 100; null nếu fallback.
- C: 1 nếu đúng, 0 nếu sai.
- T: time_quality từ 0 đến 1.
- K: confidence calibration từ 0 đến 1.
- D: difficulty multiplier; 0.85, 0.925, 1.0, 1.075, 1.15 cho difficulty 1–5.
- M: Mastery hiện tại từ 0 đến 100.

Khi có Reasoning Analysis:

~~~text
EvidenceTarget = 100 × R × (0.65 + 0.20C + 0.10T + 0.05K)
NewMastery = Clamp(M + 0.25 × D × (EvidenceTarget - M), 0, 100)
~~~

Khi fallback:

~~~text
EvidenceTarget = 100 × (0.20C + 0.05T)
NewMastery = Clamp(M + 0.10 × D × (EvidenceTarget - M), 0, 100)
~~~

Hệ quả bắt buộc:

- reasoning_quality có ảnh hưởng lớn nhất.
- Đúng nhưng reasoning kém chỉ tăng ít.
- Sai nhưng reasoning tốt có thể ghi nhận partial mastery.
- Fallback không được làm Mastery tăng mạnh.
- Mỗi lần cập nhật phải lưu input, output, delta và breakdown trong twin_update_history.
- BLL test phải bao phủ boundary 0, 39, 40, 59, 60, 79, 80, 100 và null.

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
- Hoàn tất AI job: Reasoning Analysis + Attempt status + Knowledge Twin + Behavior Twin + Goal/Risk + Twin History + Recommendation/Learning Path.
- Teacher Override + replay + History + Recommendation replacement.
- Refresh Token rotation.

Không giữ database transaction trong thời gian gọi Gemini. AI call diễn ra ngoài transaction; transaction chỉ mở khi output đã hợp lệ hoặc fallback đã được xác định.

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

- Header: Target Score và Remaining Days.
- Radar: Topic Mastery.
- Line: Twin Update History.
- Action: Opportunity Gap và Question tiếp theo.

### 21.2. Teacher

- Class overview: sĩ số và predicted score trung bình.
- High-risk Students.
- Weak Topics dạng Bar chart.
- Gap Groups và action giao Assignment.

### 21.3. Center Manager

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

Coverage:

- Nhóm thuật toán BLL lõi tối thiểu 80%.
- Coverage không thay thế test boundary và invariant.

Mỗi test phải deterministic, không gọi Gemini thật, không phụ thuộc thời gian hệ thống trực tiếp và không dùng shared mutable state.

## 23. Git, CI và commit gate

- main là nhánh được bảo vệ bằng Pull Request.
- Mỗi Phase dùng branch riêng.
- Commit nhỏ, mô tả theo Conventional Commits.
- Không commit khi build/test đỏ.
- Không trộn refactor ngoài scope vào task.
- GitHub Actions chỉ bắt buộc chạy dotnet restore, dotnet build và dotnet test.
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

- Không vi phạm năm specification.
- Chỉ sửa file được cho phép.
- Build thành công.
- Test liên quan thành công.
- Migration/seed áp dụng được nếu có thay đổi dữ liệu đã duyệt.
- API contract không drift.
- Tenant isolation được kiểm tra.
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

- Đọc đủ năm file trước khi làm.
- Chỉ thực hiện Task ID được giao.
- Liệt kê assumption trước khi sửa.
- Không tự đổi schema, endpoint, architecture, enum hoặc thuật toán.
- Không thêm package nếu task không cho phép.
- Không sửa file ngoài allow-list.
- Không dùng shortcut làm mất tenant isolation.
- Không bỏ validation/test để làm demo chạy nhanh.
- Báo BLOCKED nếu specification thiếu hoặc mâu thuẫn.
- Khi đề xuất thay đổi, dùng Change Proposal trong PROMPT_TEMPLATES.md và chờ duyệt.

AI Developer bị cấm:

- Tự chạy migration phá dữ liệu.
- Hard delete dữ liệu audit.
- Gọi AI trực tiếp từ Controller.
- Đưa business logic vào React component hoặc Controller.
- Trả EF Entity ra API.
- Tạo endpoint không có trong API_CONTRACTS.md.
- Lưu API key trong code/repository.
- Tự ý “cải tiến” sang microservices, Clean Architecture hoặc Event Sourcing.

## 27. Change control

Mọi thay đổi frozen decision phải có:

- Change ID.
- Vấn đề và bằng chứng.
- File/contract bị ảnh hưởng.
- Phương án A/B.
- Tác động migration, API, test và timeline.
- Khuyến nghị của Codex.
- Quyết định của Product Owner.

Chỉ sau phê duyệt mới cập nhật specification trước, rồi mới cập nhật source code.

## 28. Nguồn kỹ thuật nền

- [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [EF Core Multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy)
- [ASP.NET Core Background Tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Gemini Structured Outputs](https://ai.google.dev/gemini-api/docs/structured-output)
- [TanStack Query Polling](https://tanstack.com/query/v5/docs/framework/react/guides/polling)
- [MySQL 8 CHECK Constraints](https://dev.mysql.com/doc/refman/8.0/en/information-schema-table-constraints-table.html)
- [Docker Compose startup order](https://docs.docker.com/compose/how-tos/startup-order/)
