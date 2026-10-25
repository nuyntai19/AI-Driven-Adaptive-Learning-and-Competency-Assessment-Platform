# Lộ trình V4 — bàn giao 26/10/2026

Bản sửa trên commit a6200ec. Chưa build C# (máy sửa không có dotnet); frontend đã qua `tsc -b`. Cần chạy `dotnet build` và `dotnet test` trước khi làm tiếp.

## Đã làm

| Mục | Thay đổi | File |
|---|---|---|
| A1 | Form mục tiêu (điểm muốn đạt, ngày thi, phút/ngày, ngày/tuần) luôn mở. Màn đề xuất báo khi chưa đặt điểm/ngày thi, có nút "Đặt ngay". | GoalAndProposalModal.tsx |
| A2 | Chặn ngày đích đã qua ở backend (theo ngày địa phương) và ở ô date (`min`). | LearningPathV4Planner.cs `CreateOrUpdateGoalAsync`, GoalAndProposalModal.tsx |
| A3 | Không đủ thời gian thì nêu số cụ thể: số phút/ngày cần, ngày đích tối thiểu nếu giữ lịch hiện tại, hoặc bớt phạm vi. | LearningPathCalendar.cs `InsufficientTimeAdvice` |
| B1 | Phạm vi mặc định là toàn bộ kỹ năng của môn, không chỉ chủ đề đầu tiên. Ghi chú kiểm tra đầu vào hiện số phần, số câu, số phút. | GoalAndProposalModal.tsx |
| B2 | Kết quả đầu vào 3 mức: ≥ 80% là Vững (4/5, 3/3), ≤ 40% là Hổng, ở giữa là Chưa kết luận. Bỏ yêu cầu 100% với trắc nghiệm. | LearningAssessmentPolicy.cs `ClassifyPlacement`, LearningPathPlacement.cs, test mới |
| B3 | Đổi chữ "bằng chứng độc lập" sang lời học sinh hiểu được. | LearningPathV4Planner.cs, StudentLearningPathPage.tsx, learningPathRecommendation.ts |
| B4 | Bài kiểm tra chọn câu theo tỷ lệ 2 dễ / 2 vừa / 1 khó, xáo theo học sinh (seed theo studentId + kỹ năng), vẫn hiển thị từ dễ đến khó. | LearningActivityExecutionService.cs `MixedCheck` |
| C1 | Kỹ năng chưa nhập trọng số đề thi được tính bằng trung vị các trọng số đã nhập (hoặc 50), không còn là 1. | LearningPathV4Planner.cs |
| C2 | Pha kiểm tra ngắn (≤ 45 ngày) và thi thử (≤ 14 ngày) áp dụng cho mọi mục tiêu có ngày đích, không chỉ ExamPrep. | LearningPathCalendar.cs, LearningPathExams.cs |

## Chưa làm — cần Codex làm

### 1. Quyết định trước khi code

`docs/plans/learning-path-v4-revision.md:58` ghi kỳ thi không khóa chặng, trong khi spec yêu cầu thi cuối kỳ là cổng qua chặng. Cần chốt một bên và sửa docs cho khớp.

### 2. P0 còn thiếu

- **Xếp lịch ngược từ ngày đích.** `LearningPathCalendar.Allocate` đang xếp tiến từ hôm nay. Thi thử phải đặt gần ngày đích, không phải ngày sớm nhất còn chỗ.
- **Loại chặng Exam riêng:**
  - Thi giữa kỳ mở khi học xong chặng, không phụ thuộc số ngày.
  - Thi cuối kỳ là đề tổng hợp cả chặng, bấm giờ và làm cổng: không đạt thì không mở chặng sau, phải rèn lại rồi thi lại.
  - Hiện `IsProbe = true` nên bài thi thử không chặn gì.
- **Lượt thi bấm giờ hết hạn phải tự đóng** bằng job nền hoặc khi đọc agenda, và nhả các câu đã giữ (`reserved`).
- **Thi của lớp:**
  - Học sinh vào lớp sau khi bài đã công bố cũng phải thấy mốc thi.
  - Giáo viên đổi giờ hoặc hủy bài thi đã công bố thì lộ trình phải cập nhật theo.
  - Midterm và Final cần xử lý khác nhau, hoặc ghi rõ là chỉ khác nhãn.
  - Đưa thi của lớp vào chuỗi chặng thay vì để ở ô riêng.
- **Ngưỡng đạt mốc thi** đang là `TargetScore/10` (`LearningPathV4Evaluator.cs:126`), nên mục tiêu 5 điểm thì 50% là đạt. Cần một ngưỡng sàn.

### 3. P1

- **Gộp mục tiêu Twin và mục tiêu Lộ trình.**
  - Twin dùng `RemainingDays`, Lộ trình dùng `DeadlineDate`.
  - Đổi ngày đích sau khi đã chấp nhận lộ trình thì phải sinh lại mốc thi.
- **Điểm mục tiêu đặt ngưỡng thành thạo theo kỹ năng.** `RequiredCorrectRate = .8m` đang cố định.
- **Cảnh báo "dưới tiến độ" theo thời gian.**
- **PreferredPace** phải thay đổi số buổi trong ngày, không chỉ số câu mỗi bộ.
- (Đã sửa ở commit sau: nhiệm vụ nhiều ngày bị Skip sai; ngày nghỉ không học được.)
- **Khi xếp lại lịch do trễ,** phải cắt phần có ưu tiên thấp.

### 4. Kiểm tra đầu vào

- **Planner dùng `ClassifyPlacement`:**
  - Vững thì cắt phần học có hướng dẫn.
  - Hổng thì đưa lên đầu lộ trình.
  - Chưa kết luận thì sinh probe kiểm tra lại sau vài buổi. Hiện follow-up thoát sớm khi đang ở placement.
- **Câu đã dùng có thời gian chờ** rồi mới được dùng lại, thay vì bị loại vĩnh viễn (`LearningActivityExecutionService.cs` khoảng dòng 116–122).
- **Truy vết tiên quyết:**
  - Phân biệt hổng nền với chưa học.
  - Hổng nền thì chèn nhiệm vụ rèn tiên quyết lên trước.
  - Hai câu tiên quyết được thêm phải có nhãn, và thuộc các kỹ năng khác nhau.
- **Câu trả lời ngắn không chấm được** trả về `ValidationFailed` làm học sinh kẹt. Cần loại khỏi pool hoặc cho bỏ qua.

### 5. Test

Thêm test cho:

- AssignmentKind / ExamAt (validate, công bố, `ReadClassExams`)
- thứ tự theo ExamImportance (fixture đang gán mọi nút = 1)
- cổng mốc thi, thi trượt rồi thi lại
- `InsufficientTimeAdvice`
- chặn ngày đích đã qua
- `MixedCheck` trải độ khó
