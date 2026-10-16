# KẾ HOẠCH TRIỂN KHAI LỘ TRÌNH HỌC TẬP EDUTWIN V4.0
## (EDUTWIN-V4-IMPLEMENTATION-PLAN)

> **Task ID:** EDUTWIN-LEARNING-PATH-V4  
> **Requirement IDs:** V4-01 đến V4-38  
> **Repository:** C:\AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform  
> **Branch / HEAD:** shin/studytask @ 46670e4b5bc12d22706ff219283209112a81f26f  
> **Trạng thái:** IN_PROGRESS  

---

### 1. Mục tiêu và phạm vi
Triển khai phân hệ lộ trình học tập cá nhân EduTwin V4.0 hoàn chỉnh theo đặc tả `docs/plans/EDUTWIN-PERSONALIZED-LEARNING-PATH-V4.0.md` và prompt `docs/plans/ANTIGRAVITY-IMPLEMENT-LEARNING-PATH-V4-PROMPT.md`:
- Chuỗi giá trị: Mục tiêu cá nhân (Goal) → Điểm xuất phát từ evidence (Diagnostic/Unknown) → Xem trước tuyến (Path Proposal) → Bản đồ năng lực (Capability Map) → Nhiệm vụ phù hợp thời lượng (Agenda 5/15/30m) → Hoạt động học đa dạng (WorkedExample, FindError, IndependentCheck, TransferChallenge) → Đánh giá một evaluator duy nhất → Điều chỉnh tuyến kế tiếp (Adaptation).
- Đảm bảo toàn bộ 38 tiêu chí nghiệm thu (V4-01 đến V4-38).
- Không phá hủy dữ liệu, bảo toàn dirty baseline đã có trong workspace, giữ ranh giới Legacy vs V4 an toàn.

---

### 2. Các gói công việc (Work Packages)

#### WP1: Nền tảng Execution, Data Schema & Single Evaluator
- **Files:**
  - `src/EduTwin.DAL/Recommendations/`: `LearningGoal.cs`, `LearningContentVersion.cs`, `PathProposal.cs`, `LearningPathRevision.cs`, `ActivityRun.cs`, `IssuedItem.cs`, `PathDecision.cs`.
  - `src/EduTwin.DAL/Persistence/Configurations/`: Entity mappings cho các bảng mới, đảm bảo snake_case, center_id composite FK, concurrency row_version.
  - `src/EduTwin.DAL/Persistence/EduTwinDbContext.cs`: DbSets, model builder configurations, global tenant query filters.
  - `src/EduTwin.Contracts/Recommendations/`: DTOs cho V4 Goal, Proposals, ActivityRun, Submissions, Evaluation, Agenda, Capability Map.
  - `src/EduTwin.BLL/Recommendations/Services/`: `ILearningPathV4Evaluator.cs`, `LearningPathV4Evaluator.cs`, `ILearningSessionExecutionService.cs` updates.
- **Invariants:**
  - Tenant lấy từ `ITenantContext`, kiểm tra ownership toàn chuỗi Goal → Path → Mission → Run → Activity.
  - Một evaluator duy nhất quyết định Outcome; submit chỉ lưu bài và tiến vị trí atomic (không tự gán Passed).
  - Draft & Assistance quản lý trạng thái tương tác an toàn; chặn hỗ trợ/lời giải ở IndependentCheck từ server.
  - Idempotency cho submit/evaluate, ngăn chặn race condition.

#### WP2: Bộ nội dung Pilot chuẩn hóa & Authoring
- **Files:**
  - `src/EduTwin.BLL/Recommendations/Content/`: Pilot content catalog cho chương Phương trình & Bài toán thực tế THPT (8 Skills: `MATH-EQ-01` đến `MATH-EQ-08`).
  - Nội dung có đầy đủ 3 trải nghiệm hoạt động cốt lõi:
    1. WorkedExample (giải thích từng bước, câu hỏi hiểu bài).
    2. FindError (tìm bước giải sai, chọn quan niệm sai lầm misconception, sửa lại cho đúng).
    3. IndependentCheck (bài tập tự luyện độc lập, không lộ lời giải, chấm theo rubric).
    4. TransferChallenge (bài toán thực tế đa bước, kiểm tra năng lực vận dụng).
  - Dịch vụ Versioning, Authoring & Publish cho Teacher với validation rubric & prerequisite.

#### WP3: V4 Planner & Personalization Engine
- **Files:**
  - `src/EduTwin.BLL/Recommendations/Planning/`: `ILearningPathV4Planner.cs`, `LearningPathV4Planner.cs`.
  - Phân tích tính khả thi thời gian (Feasibility Check): cảnh báo khi lịch học không đủ đạt mục tiêu trước deadline (V4-05).
  - Khởi tạo điểm xuất phát từ bằng chứng thật:
    - Hồ sơ chưa có evidence -> gán `Unknown`, không tự suy đoán mastery 0 (V4-02).
    - Học sinh tự nhận giỏi -> yêu cầu kiểm tra probe, không tự Confirmed (V4-03).
    - Đã có bằng chứng vững -> bỏ qua lặp lại, cho phép ôn tập (V4-04).
  - 3 Hồ sơ fixture An, Bình, Chi (V4-01):
    - An: Lỗ hổng phân số -> Bổ trợ FindError phân số trước khi vào phương trình.
    - Bình: Đã vững cơ bản -> Probe vượt cấp -> Nhiệm vụ mô hình hóa bài toán thực tế.
    - Chi: Học sinh mới, ít thời gian -> Tuyến tạm thời + Probe ngắn 10 phút.
  - Tạo PathProposal có thời hạn, cho phép học sinh xem trước và Accept kích hoạt tuyến an toàn (V4-20, V4-31).

#### WP4: Agenda & Trải nghiệm Mission Player
- **Files:**
  - `src/EduTwin.API/Controllers/RecommendationsController.cs`: Endpoints cho Goals, Proposals, Accept, Agenda, Mission Runs, Draft, Assistance, Submissions, Evaluation, Feedback.
  - `web/edutwin-web/src/api/learningPathApi.ts`: Client API methods tương ứng.
  - `web/edutwin-web/src/types/learningPath.ts`: Interfaces & types V4.
  - `web/edutwin-web/src/pages/StudentLearningPathPage.tsx`: Giao diện Mục tiêu, Bản đồ năng lực cá nhân, Xem trước tuyến đề xuất, Lịch học hôm nay (5/15/30m).
  - `web/edutwin-web/src/pages/LearningPlayerPage.tsx` & components: Trình diễn thật 3 loại hoạt động (WorkedExample, FindError, IndependentCheck), thanh điều hướng bàn phím/mobile, xử lý draft sync và server-side assistance guard.

#### WP5: Adaptation, Review & Integration
- **Files:**
  - Phản ứng điều chỉnh tuyến: Sai lặp có căn cứ -> sinh nhiệm vụ bổ trợ lỗi cụ thể; đổi thời gian 30m sang 5m -> đổi khối lượng nhiệm vụ (V4-06, V4-11).
  - Trợ giúp AI có ngữ cảnh và cơ chế fallback khi lỗi/timeout (V4-29).
  - Xử lý Pending Teacher Review và Late Review: Evaluator cập nhật lại mà không nhân đôi evidence (V4-16, V4-17, V4-33).
  - Đảm bảo GET không có side-effect (V4-31).

#### WP6: Kiểm thử tự động & Nghiệm thu
- **Files:**
  - `tests/EduTwin.BLL.Tests/Recommendations/`:
    - `LearningPathV4PlannerTests.cs` (Kiểm thử 3 hồ sơ An/Bình/Chi, feasibility, unknown, probe skip).
    - `LearningPathV4EvaluatorTests.cs` (Kiểm thử single evaluator, rubric, guided vs independent, pending review).
    - `LearningPathV4ExecutionTests.cs` (Draft resume, assistance rejection ở independent check, idempotency, concurrency).
  - Chạy `dotnet test` toàn diện.
  - Chạy `npm run build` kiểm tra frontend.
  - Lập báo cáo `docs/verification/EDUTWIN-V4-IMPLEMENTATION-REPORT.md` đối chiếu đủ 38 AC.
