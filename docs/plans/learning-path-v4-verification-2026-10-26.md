# Kiểm chứng lại Lộ trình V4 — 26/10/2026

## Kết luận và phạm vi

Code hiện tại build được và các luồng giao diện được kiểm tra dưới đây hoạt động đúng trên Chrome thật, với component/CSS production và response API giả lập có kiểm soát. Không phát hiện lỗi runtime trong 9 ca UI đã qua.

**Không đánh đồng kết quả này với kiểm thử end-to-end trên API/MySQL.** Docker CLI không phản hồi khi chuẩn bị môi trường tạm; preflight chưa tạo owner manifest. Đã dừng riêng tiến trình chuẩn bị, không thay container hoặc dữ liệu chính. Công cụ browser/Windows automation cũng timeout; kiểm tra UI dùng Chrome headless với hồ sơ tạm, không dùng tài khoản hoặc profile trình duyệt chính.

Việc đối chiếu yêu cầu dựa trên chính sách đã ghi trong [handoff](learning-path-v4-handoff-2026-10-26.md) và [revision](learning-path-v4-revision.md). Hai điểm không được hiểu là đã triển khai nguyên văn mọi phương án ban đầu:

- Cổng chặng khóa phần phụ thuộc; không khóa toàn tuyến. Midterm/Final của lớp cùng pipeline chấm/follow-up, khác nhãn/phạm vi đề; Final của lớp không mặc định thành cổng cá nhân.
- Chưa có mô hình hiệu chuẩn để ánh xạ điểm mục tiêu thành mastery riêng cho từng kỹ năng. Điểm mục tiêu tác động ngưỡng mốc thi với sàn 80%; không dùng mục tiêu 5 điểm để hạ ngưỡng thành thạo xuống 50%.

## Kết quả tự động trên code hiện tại

| Kiểm tra | Kết quả |
|---|---|
| Backend toàn bộ, lượt cuối | 4.648 đạt, 106 bỏ qua theo cấu hình, 0 lỗi; 4.754 test tổng cộng |
| Frontend unit/regression | 823 đạt, 0 lỗi |
| Frontend TypeScript/Vite build | Thành công; còn cảnh báo chunk lớn có sẵn |
| API publish Release | Thành công |
| Chrome desktop/mobile component smoke | 9/9 đạt; không có exception chưa xử lý hay request fixture thiếu |
| Kiểm thử API/MySQL tách biệt | Chưa chạy được do Docker không phản hồi; không ghi PASS |

Lượt backend đầu trong lần kiểm tra này có 1 lỗi ở `AttachmentOrphanCleanupWorkerTests`. Chạy riêng lại nhóm đó: 6/6 đạt. Sau đó chạy lại toàn bộ: 4.648 đạt, 0 lỗi. Nguyên nhân lỗi thoáng qua chưa được kết luận; không tự sửa logic dọn tệp ngoài phạm vi Lộ trình.

Bằng chứng backend ở `tests/EduTwin.BLL.Tests/TestResults/reverification-clean.trx`. Kết quả Chrome ở `scratch/learning-path-verify/browser-results.json`; ảnh chụp cùng thư mục. Đây là artifact cục bộ, có thể tái tạo bằng script bên dưới.

## Các ca giao diện đã thao tác trên Chrome

1. Form mục tiêu mở sẵn; ngày đã qua bị browser validation chặn.
2. Đổi nhịp học và thời gian chờ câu; chọn học ngay, xem trước và chấp nhận tuyến. Kiểm tra payload: toàn bộ phạm vi đã chọn, điểm mục tiêu, nhịp và thời gian chờ được gửi đúng.
3. Mốc thi lớp và thi thử nằm thành các chặng riêng. Bài thi đã hủy không có link bắt đầu. Nhiệm vụ phụ thuộc có cổng chưa đạt bị vô hiệu hóa.
4. Trang lộ trình ở viewport 375×812 không tràn ngang.
5. Bỏ qua câu gửi `skipped=true` tới endpoint `/submissions` và đến màn kết quả chưa kết luận.
6. Đồng hồ hết giờ tự gọi đánh giá, hiện màn tổng kết và đóng vùng nhập câu trả lời.
7. Giáo viên sửa thời lượng thi đã công bố, nhận thông báo lưu thành công; request PATCH chỉ chứa lịch/thời lượng và RowVersion, không gửi sửa nội dung/câu hỏi.
8. Màn giáo viên với TeacherThemeScope/CSS thật ở 375×812 không tràn ngang.
9. Giáo viên thiếu quyền update không thấy phần sửa lịch thi.

Đã xem trực tiếp ảnh desktop/mobile của trang học sinh và màn đổi lịch giáo viên. Các ca này kiểm chứng component và thao tác UI, không kiểm chứng vòng đăng nhập, shell ứng dụng đầy đủ, transaction MySQL hay việc dữ liệu thật được ghi bền vững.

## Đối chiếu chức năng còn lại với backend/regression

| Yêu cầu | Bằng chứng/giới hạn |
|---|---|
| Thi thử gần ngày đích, chừa rèn lại, không vượt ngân sách | `Mock_is_scheduled_backwards_and_leaves_time_for_repair`, các test CalendarAndBaseline |
| Cổng không phụ thuộc ngày thi; trượt, rèn và thi lại | `Stage_exam_has_no_deadline_dependency_and_cannot_start_before_study`; `Failed_stage_holds_only_dependants_and_repair_then_retake_opens_them` cho mục tiêu 5 và 9 điểm |
| Tự đóng lượt hết giờ, nhả reserved và không tạo follow-up trùng | `Reading_agenda_closes_an_expired_run_and_releases_unanswered_reservations` + ca UI 6 |
| Ghi danh muộn, lịch thi đổi/hủy và sửa liên kết cũ | `Late_enrollment_sees_live_exam_schedule_and_cancellation` cho Midterm/Final; chưa kiểm chứng transaction MySQL thực tế |
| AssignmentKind/ExamAt và quyền sửa Published | Các test Create/Publish/UpdateAssignmentUseCase mới + ca UI 7/9 |
| Thứ tự ExamImportance và giữ tiên quyết | `Exam_importance_orders_independent_skills_but_preserves_prerequisites` |
| Đổi ngày đích sinh lại mốc, giữ lịch sử | `Deadline_change_regenerates_unstarted_mocks_without_deleting_history` |
| Twin/Lộ trình dùng cùng mục tiêu | `Twin_goal_edits_share_the_canonical_goal_and_invalidate_stale_previews` |
| Tác dụng PreferredPace | `Pace_changes_daily_sets_within_the_same_time_budget` + ca UI 2 |
| Lời khuyên thiếu thời gian, ngày nghỉ, buổi nhiều ngày | `Budget_advice_uses_the_actual_excluded_days`, CalendarAndBaseline/StudyFlow |
| Đầu vào Vững/Cần củng cố/Chưa kết luận đi vào planner | `Placement_baseline_changes_learning_order_and_schedules_inconclusive_rechecks` |
| Hai probe nền tảng thuộc kỹ năng khác nhau và có nhãn | `Prerequisite_probes_cover_different_skills_and_have_visible_labels` |
| Câu đã dùng có thời gian chờ, không biến thành bằng chứng mới | `Used_questions_wait_before_practice_and_never_become_fresh_check_evidence` |
| Bỏ qua câu không làm kẹt đầu vào | `Skipping_an_ungradable_answer_finishes_placement_without_claiming_strength` + ca UI 5 |

Cảnh báo tiến độ thời gian và hoãn phần không vừa ngân sách đã đối chiếu code; không tuyên bố đã có kiểm thử trình duyệt riêng cho mọi tổ hợp trễ lịch, đổi múi giờ và lượng dữ liệu lớn.

## Chạy lại kiểm tra giao diện

Từ `web/edutwin-web`:

```powershell
node tests/smoke/chrome_learning_path_smoke.cjs
```

Script tự mở Vite và Chrome với hồ sơ tạm; dùng production component và TeacherThemeScope. Fixture không cài vào luồng ứng dụng, không dùng server thật, không truy cập dữ liệu học sinh. Test thất bại nếu thiếu Chrome, không mount được component, có exception hay request giả lập chưa được định nghĩa.
