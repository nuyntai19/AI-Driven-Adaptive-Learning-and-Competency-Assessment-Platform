# EduTwin

> Nền tảng học tập thích ứng, đánh giá năng lực và Learning Digital Twin dành cho trung tâm giáo dục THPT.
>
> Phiên bản tài liệu: 2.1-draft.
> Trạng thái tài liệu: COURSE REBASELINE — đang được nhóm xác minh và phê duyệt.
> Chủ sở hữu: Nhóm EduTwin.

## 1. Mục tiêu

EduTwin kết hợp quản lý trung tâm, nội dung học tập, bài tập, bằng chứng làm bài và phân tích reasoning tùy chọn để giúp:

- học sinh biết chủ đề nào cần cải thiện;
- giáo viên nhận diện lỗ hổng kiến thức và trường hợp cần xem xét;
- quản lý trung tâm kiểm soát người dùng, quyền hạn và tiến độ;
- hệ thống tạo khuyến nghị có thể giải thích và tái lập.

AI không phải lõi vận hành hay nguồn sự thật cuối cùng. Gemini chỉ tạo observation khi khả dụng; submission, chấm sơ bộ deterministic, fallback, dữ liệu hành vi, Evidence Gate và Teacher Override bảo đảm hệ thống vẫn chạy khi Gemini/Internet lỗi. AI không tự chấm điểm cuối hoặc trực tiếp quyết định Mastery/Risk/Recommendation.

## 2. Phạm vi môn học

EduTwin được tiếp tục từ prototype đã phát triển trước học kỳ. Repository môn học phải:

1. nhập nguyên trạng một baseline minh bạch;
2. ghi repository nguồn và full commit SHA;
3. không chia nhỏ hoặc giả mạo lịch sử baseline thành đóng góp của năm thành viên;
4. chỉ tính đóng góp môn học từ commit sau baseline;
5. lưu yêu cầu, phân công, tiến độ, commit và bằng chứng kiểm thử theo tuần.

Baseline prototype đã kiểm tra ngày 2026-09-08:

~~~text
Repository:
nuyntai19/AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform

Branch:
feat/center-organization

Commit:
2d768f270e0395bcafcbcab2305ac3617fb5f9ca

Message:
feat(twin): add mastery v1 calculator
~~~

Ba mốc provenance không được đánh đồng:

- Pre-course source-code snapshot: `2d768f270e0395bcafcbcab2305ac3617fb5f9ca`.
- Documentation rebaseline checkpoint trên repository prototype: `b14f6c4171dc55043a3bb910332061c55ba66a7f`; các correction sau checkpoint này vẫn là công việc chuẩn bị trước khi khóa course baseline.
- Course repository initial-import commit: `TBD`; commit này phải nhập đúng snapshot code + tài liệu đã được nhóm duyệt và ghi lại full SHA nguồn.

Documentation checkpoint không tự trở thành đóng góp môn học. Tên/URL repository môn học và initial-import SHA phải được ghi trong PROJECT_TRACKING.md; chỉ commit sau initial import mới được tính là đóng góp trong học kỳ.

## 3. Công nghệ

### Backend

- .NET 10.
- ASP.NET Core Web API.
- Entity Framework Core 10.
- MySQL 8.x với InnoDB và utf8mb4.
- JWT access token và HttpOnly refresh-token cookie.
- xUnit và Moq.

### Frontend

- React và Vite.
- TypeScript.
- Tailwind CSS.
- Zustand.
- TanStack Query.
- Axios.

### AI và thuật toán

- Gemini qua abstraction IAIService.
- Durable AI job, retry và rule-based fallback.
- Deterministic Evidence Gate.
- Explainable mastery/risk/opportunity calculations.
- Teacher review, override và deterministic replay.
- ML.NET chỉ được thêm sau khi có dataset, label, baseline và metric được phê duyệt.
- Luồng quản trị, nội dung, giao/nộp bài và recommendation fallback không phụ thuộc Gemini.

## 4. Kiến trúc

~~~text
React Web
    |
ASP.NET Core API
    |
Business Logic Layer
    |---- Identity, tenant, permission, ownership
    |---- Organization
    |---- Knowledge Graph
    |---- Curriculum and Question Bank
    |---- Assignment and Learning
    |---- Assessment and Reasoning
    |---- Evidence and Digital Twin
    |---- Recommendation and Dashboard
    |
Entity Framework Core
    |
MySQL
~~~

Chiều dependency backend:

~~~text
EduTwin.API -> EduTwin.BLL -> EduTwin.DAL
EduTwin.API -> EduTwin.Contracts
EduTwin.BLL -> EduTwin.Contracts
~~~

Controller không chứa business rule. DAL không gọi AI. Frontend không phải security boundary.

## 5. Trạng thái source đã xác minh

Tại baseline nêu trên:

- backend build Release: thành công, không warning/error;
- frontend production build: thành công;
- full .NET test suite: 2.853 pass, 3 skip, 0 fail;
- EF Core không có pending model changes;
- migrations tạo 31 bảng, 75 index, 66 foreign key và 59 check constraint;
- target v2 đang đặc tả thêm 7 bảng có mục đích cho Dynamic RBAC/Evidence, nâng target lên 38 bảng sau khi được duyệt và migration;
- ba MySQL integration test bị skip do chưa có kết nối integration database;
- AI job, Gemini retry/fallback và Reasoning Analysis đã tồn tại;
- Mastery Calculator v1 đã tồn tại dưới dạng deterministic pure function;
- Evidence Gate, Twin Orchestrator, Dynamic RBAC và các dashboard cuối chưa hoàn tất.

Tổng test xanh không thay thế kiểm thử MySQL thật. Trước release, ba test relational phải chạy trong CI hoặc môi trường kiểm thử có MySQL.

## 6. Bản đồ tài liệu

Đọc theo thứ tự:

1. PROJECT_REQUIREMENTS.md — SRS, yêu cầu môn học/nghiệp vụ và acceptance.
2. CONSTITUTION.md — invariant kỹ thuật cao nhất.
3. DATABASE_SCHEMA.md — nguồn duy nhất cho table/column/key/relationship.
4. API_CONTRACTS.md — HTTP contract và authorization contract.
5. UI_UX_SPEC.md — sitemap, màn hình, capability và trạng thái UX.
6. MASTER_PLAN.md — roadmap/phụ thuộc thực hiện.
7. PROJECT_TRACKING.md — yêu cầu theo tuần, quyết định, trạng thái và evidence.
8. TEAM_ASSIGNMENT.md — phân công năm thành viên, dependency và review chéo.
9. CODEBASE_CHANGE_PLAN.md — khoảng cách Current → Target và file code dự kiến đổi.
10. PROMPT_TEMPLATES.md — cách Human/Codex/Gemini giao việc, review và closeout.

Năm file kỹ thuật cốt lõi là CONSTITUTION, DATABASE_SCHEMA, API_CONTRACTS, MASTER_PLAN và PROMPT_TEMPLATES. Các file còn lại không nhân bản kỹ thuật: chúng bổ sung SRS/UX/bằng chứng môn học/phân công/migration map mà năm file lõi không nên gánh.

Khi có mâu thuẫn, không tự chọn tài liệu thuận tiện hơn. Thực hiện change-control theo CONSTITUTION.md.

## 7. Chuẩn bị môi trường

Cần có:

- Visual Studio 2022 trở lên hoặc IDE hỗ trợ .NET 10;
- .NET SDK 10;
- Node.js 22 và npm;
- MySQL 8 hoặc Docker Desktop;
- Git.

Sao chép .env.example thành .env và điền giá trị local. Không commit hoặc in nội dung .env vào log, prompt, screenshot hay báo cáo.

Các nhóm cấu hình cần có:

- ConnectionStrings__Default
- Jwt__SigningKey
- Jwt__Issuer
- Jwt__Audience
- Gemini__ApiKey
- Seed__Enabled
- Seed__CenterManagerPassword

## 8. Build và kiểm thử

Tại repository root:

~~~powershell
dotnet tool restore
dotnet restore EduTwin.sln
dotnet build EduTwin.sln --configuration Release --no-restore --maxcpucount:1
dotnet test EduTwin.sln --configuration Release --no-build --maxcpucount:1
~~~

Kiểm tra model:

~~~powershell
$env:ConnectionStrings__Default="Server=dummy;Database=dummy"
dotnet ef migrations has-pending-model-changes --project src/EduTwin.DAL --startup-project src/EduTwin.DAL --context EduTwinDbContext --configuration Release --no-build
~~~

Frontend:

~~~powershell
Set-Location web/edutwin-web
npm ci
npm run build
~~~

## 9. Chạy bằng Docker Compose

Sau khi cấu hình .env:

~~~powershell
docker compose up -d --build
docker compose ps
~~~

Dịch vụ mặc định:

- Web: http://localhost:3000
- API: http://localhost:5000
- Liveness: http://localhost:5000/api/v1/health/live
- Readiness: http://localhost:5000/api/v1/health/ready
- Adminer: http://localhost:8080
- MySQL host port: 3307

Dừng môi trường:

~~~powershell
docker compose down
~~~

## 10. Chạy local để debug

Khởi động MySQL riêng hoặc chỉ service MySQL từ Compose, sau đó cấu hình secret trong terminal/local secret store. Không dán giá trị secret vào tài liệu hoặc log.

Terminal backend:

~~~powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:ASPNETCORE_URLS="http://localhost:5000"
dotnet run --project src/EduTwin.API/EduTwin.API.csproj
~~~

Terminal frontend:

~~~powershell
Set-Location web/edutwin-web
npm run dev
~~~

Vite chạy tại http://localhost:3000 và proxy /api tới http://localhost:5000. Nếu thay port, phải cập nhật launch environment và vite.config.ts có kiểm soát.

## 11. Quy trình đóng góp

Mỗi work package phải có:

- requirement ID;
- human owner;
- exact scope;
- acceptance criteria;
- test evidence;
- commit hoặc pull request;
- review độc lập;
- cập nhật PROJECT_TRACKING.md.

AI có thể phân tích, viết nháp, triển khai và review, nhưng người phụ trách phải đọc diff, hiểu thiết kế, chạy gate và chịu trách nhiệm cho commit. Không gán commit của baseline hoặc output AI cho thành viên không thực sự tham gia.

## 12. Nguyên tắc bảo mật

- Không nhận centerId từ client để quyết định tenant.
- Permission, tenant và ownership đều phải pass.
- Cross-tenant resource trả 404 khi cần chống enumeration.
- CenterManager chỉ quản lý role và user trong center của mình.
- UI ẩn nút không thay thế API authorization.
- Không log access token, refresh token, API key, mật khẩu hoặc reasoning nhạy cảm.
- Không cho AI confidence thấp thay đổi mạnh Digital Twin.

## 13. Tài liệu lịch sử

[EduTwin-Overview.docx](docs/archive/vision-v0/EduTwin-Overview.docx) là vision cũ dùng Next.js/FastAPI/Supabase/PostgreSQL và không còn là implementation authority. Tài liệu này được bảo tồn trong archive để truy vết, không được dùng làm nguồn triển khai hiện tại.

Bản đặc tả DOCX hiện hành được xuất tại [docs/EduTwin-Project-Specification.docx](docs/EduTwin-Project-Specification.docx). DOCX phục vụ đọc/báo cáo; nếu mâu thuẫn, requirement và năm file kỹ thuật cốt lõi nêu trên thắng.
