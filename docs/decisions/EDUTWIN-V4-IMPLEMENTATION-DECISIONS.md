# QUYẾT ĐỊNH THIẾT KẾ VÀ KIẾN TRÚC EDUTWIN V4.0
## (EDUTWIN-V4-IMPLEMENTATION-DECISIONS)

> **Task ID:** EDUTWIN-LEARNING-PATH-V4  
> **Requirement IDs:** V4-01 đến V4-38  
> **Trạng thái:** Các quyết định thiết kế cần đối chiếu bằng chứng triển khai; bản ghi hiệu chỉnh 16/10/2026 bên dưới có ưu tiên khi mô tả hành vi hiện tại. Không phải chứng nhận đạt toàn bộ V4.  

### Hiệu chỉnh sau rà soát — 16/10/2026

### Trải nghiệm học liên kết với bài làm — cùng ngày

Triển khai theo yêu cầu người dùng xác nhận sau rà soát: khung đề xuất từ câu đã làm, mục tiêu điền sẵn có thể chỉnh, chuỗi luyện nhiều câu và phản hồi tức thì từ dữ liệu biên soạn/canonical grader. Không đợi AI mới trả đáp án trong phần luyện. Kết luận kỹ năng vẫn dùng evidence gate; quan sát sai không tự trở thành misconception. Evaluator bổ sung buổi rèn lại hoặc lịch ôn 2/7 ngày có quyết định giải thích, GET không ghi. Chi tiết và bằng chứng: [EDUTWIN-CONNECTED-LEARNING-2026-10-16.md](../verification/EDUTWIN-CONNECTED-LEARNING-2026-10-16.md).

- Planner dùng graph/evidence/nội dung theo đúng môn và lớp; không còn danh mục Toán pilot làm nguồn cho mọi môn. Mỗi chặng là một kỹ năng thật, không phải lịch tuần lặp chủ đề.
- Start nhiệm vụ lưu Run/Activity/IssuedItem thật. Submit câu hỏi qua pipeline hiện hữu; đọc/ví dụ chỉ hoàn thành hoạt động, không sinh điểm hay Attempt giả.
- Policy hiện tại `v4-evidence-grade-scope-2` (23/10/2026): kiểm tra độc lập cần ít nhất 5 câu khác nhau, chưa làm/chưa lộ lời giải, đúng phạm vi khối/giáo trình, tối thiểu 80% và evidence không còn chờ review. Không dùng điểm của hoạt động có hướng dẫn để tự xác nhận kỹ năng. Phiên V4 policy cũ cần tạo lại đề xuất; không được đi vòng qua endpoint luyện legacy.
- Khi tất cả prerequisite chưa đạt đều nằm ngoài phạm vi chương trình, có thể đề xuất kiểm tra độc lập trực tiếp năng lực đích bằng 5 câu mới đúng phạm vi. Đề xuất phải nêu lý do; không tự ghi mastery cho prerequisite ngoài phạm vi, không bỏ qua prerequisite nội bộ, và không mở luyện thông thường trước khi đạt kiểm tra độc lập hợp lệ.
- Preview chỉ lưu draft. Accept idempotent và có khóa học sinh. Chặn thay tuyến khi còn run V4 hoặc chờ chấm/review; phiên legacy đang làm được đánh dấu Abandoned, bảo toàn submissions, có thông báo trong preview và reason code trong quyết định.
- Worker legacy không được supersede tuyến V4. Reevaluation đọc kết quả canonical, không sửa Attempt trực tiếp, không tự ghi Twin.
- Báo cáo kiểm chứng và khoảng trống sản phẩm: [EDUTWIN-V4-CODEX-REVIEW-2026-10-16.md](../verification/EDUTWIN-V4-CODEX-REVIEW-2026-10-16.md). Lịch ôn, nội dung tương tác sâu, authoring và pilot chưa thể coi là hoàn tất.

---

### Quyết định 1: Single Evaluator & Tách rời Submit khỏi Outcome
- **Bối cảnh:** Ở phiên bản cũ, `SubmitAttemptUseCase` kiểm tra tỷ lệ đúng tổng câu và tự cập nhật `sessionRun.Status = Completed` và `sessionRun.Outcome = Passed` ngay lập tức. Điều này vi phạm nguyên tắc đánh giá đa chiều, không phân biệt được hoạt động có hướng dẫn (WorkedExample) với hoạt động kiểm tra độc lập (IndependentCheck), và làm hỏng cơ chế chấm AI/giáo viên.
- **Quyết định:** Submit attempt chỉ thực hiện:
  1. Lưu bài làm (`Attempt`) và tạo job AI nếu cần.
  2. Gắn kết với `ActivityRun` và `SessionRun`.
  3. Cập nhật vị trí hiện tại (`CurrentQuestionId` / tiến trình activity) một cách atomic.
  4. Đánh dấu trạng thái `InProgress` hoặc `Grading` nếu đang chờ chấm.
  5. **Tuyệt đối không tự phong Passed/Outcome**. Chỉ có `ILearningPathV4Evaluator` (chạy qua endpoint `/evaluate` hoặc khi hoàn thành tất cả activity hợp lệ) mới có thẩm quyền đánh giá Outcome.

---

### Quyết định 2: Database là Single Source of Truth cho Execution (PlanJson chỉ là Projection/Snapshot)
- **Bối cảnh:** Hệ thống cũ ghi cả vào `PlanJson` lẫn bảng quan hệ dẫn đến rủi ro đồng bộ dữ liệu và vi phạm tính nhất quán. Ngoài ra, việc đọc `PlanJson` trong GET từng sinh side-effect backfill làm bẩn dữ liệu.
- **Quyết định:**
  - Bảng dữ liệu quan hệ (`LearningGoal`, `LearningPath`, `LearningPathSession`, `LearningPathSessionRun`, `ActivityRun`, `IssuedItem`) là nguồn sự thật duy nhất cho execution.
  - `PlanJson` chỉ đóng vai trò snapshot / projection cho frontend hiển thị cây lộ trình hoặc tương thích backward.
  - Mọi thao tác GET (`GET /learning-path`, `GET /learning-agenda`, `GET /learning-path/sessions/{id}`) đều tuyệt đối không có side-effect ghi DB (tuân thủ V4-31).

---

### Quyết định 3: Mô hình phân biệt Unknown và Năng lực thấp
- **Bối cảnh:** Khi học sinh mới vào chưa có bài làm, hệ thống cũ dễ ngộ nhận mastery = 0% dẫn đến bắt học lại từ đầu toàn bộ. Hoặc khi học sinh tự đánh giá "Giỏi", hệ thống dễ bị đánh lừa tự phong Confirmed.
- **Quyết định:**
  - Kỹ năng chưa có bằng chứng bài làm hợp lệ được phân loại rõ ràng là `Unknown`.
  - Học sinh mới hoặc thiếu bằng chứng sẽ được đề xuất bài kiểm tra định hướng ngắn (Probe/Diagnostic) từ 3–8 câu hoặc tuyến tạm thời có nhãn rõ.
  - Tự nhận năng lực chỉ được dùng làm nguyện vọng, không thay thế bằng chứng; muốn bỏ qua bước cơ bản phải qua bài kiểm tra vượt cấp (Probe) (V4-03).

---

### Quyết định 4: Cơ chế Path Proposal và Xem trước tuyến
- **Bối cảnh:** Khi thay đổi mục tiêu hoặc tạo lộ trình mới, việc ghi đè trực tiếp lên Path đang hoạt động có thể phá hỏng các Run đang diễn ra của học sinh.
- **Quyết định:**
  - Sử dụng thực thể `PathProposal` để lưu bản kế hoạch đề xuất.
  - Học sinh có thể xem trước: điểm bắt đầu, các chặng, thời gian ước tính, phân tích tính khả thi theo thời gian rảnh.
  - Chỉ khi học sinh gọi `POST /path-proposals/{id}/accept`, hệ thống mới kích hoạt và cập nhật tuyến vào `LearningPath` một cách atomic và idempotent.

---

### Quyết định 5: Bảo vệ server-side cho IndependentCheck & Kiểm soát trợ giúp
- **Bối cảnh:** Học sinh có thể gọi API trợ giúp hoặc tra cứu lời giải khi đang làm bài kiểm tra độc lập (IndependentCheck) nếu chỉ ẩn nút trên giao diện frontend.
- **Quyết định:**
  - Server kiểm tra loại hoạt động tại endpoint `/activity-runs/{id}/assistance`.
  - Nếu `ActivityType == IndependentCheck`, server từ chối cấp lời giải / gợi ý chi tiết (HTTP 403 hoặc chuyển mode có ghi nhận), bảo vệ tính khách quan của bằng chứng (V4-28).

---

### Quyết định 6: Chống nhân đôi Evidence khi Late Teacher Review đến sau
- **Bối cảnh:** Khi giáo viên chấm lại hoặc override điểm một attempt sau khi phiên học đã kết thúc, nếu cộng dồn điểm mới vào Digital Twin mà không đối chiếu với kết quả cũ thì mastery sẽ bị sai lệch.
- **Quyết định:**
  - Mỗi bài làm gắn với một định danh bằng chứng duy nhất (`AttemptId`).
  - Khi teacher review cập nhật điểm, evaluator chạy lại với cùng chính sách, thay thế nhận định cũ cho attempt đó thay vì tạo bằng chứng trùng lặp (V4-17, V4-33).
