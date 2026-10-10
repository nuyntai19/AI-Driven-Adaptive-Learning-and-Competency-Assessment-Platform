# Khóa thao tác học mới trong lớp đã lưu trữ

Ngày: 2026-10-09. Phạm vi: yêu cầu “oke sửa đi” sau đánh giá các chức năng của lớp lưu trữ.

## Quy tắc áp dụng

- Lớp `Archived`/`History` chỉ được xem lại dữ liệu. Không lấy câu luyện mới, tạo lộ trình, đổi trạng thái buổi học, lưu/nộp bài mới trong lớp đó.
- Kiểm tra server dựa vào trạng thái lớp và quan hệ thành viên trong database. Bỏ `classId` hoặc gửi `history=false` không mở lại quyền luyện tập nếu môn đó chỉ còn lớp lịch sử.
- Nếu học sinh còn lớp đang học khác, lớp đó vẫn hoạt động. Câu luyện/lộ trình phải thuộc giáo trình đã xuất bản đang áp dụng cho lớp được chọn; không dùng lộ trình cache của lớp cũ.
- “Xem lịch sử” luôn chỉ xem trên giao diện. Đồng hồ không chạy tiếp, nút nộp/làm lại không xuất hiện, không tự gọi AI hay tự nộp bài khi mở lịch sử.
- Bài làm, điểm, phân tích, ảnh nháp, bằng chứng năng lực và lịch sử áp dụng giáo trình được giữ lại. Giáo viên vẫn có thể xem/chấm bài đã nộp; tác vụ phân tích cho bài đã nộp không bị hủy.
- Không tự lưu trữ/mở lại lớp thật, không đổi thành viên, không đổi giáo trình, không thêm migration trong lượt này.

Hai quy tắc tương thích được giữ nguyên:

1. Danh sách nhận bài đã xuất bản là snapshot quyền nhận bài. Server không thu hồi quyền của bài đã giao chỉ vì học sinh rời một lớp **vẫn hoạt động**. Đây là hợp đồng đã được kiểm thử; không thay đổi nghiệp vụ này trong lượt sửa lớp lưu trữ. Giao diện lịch sử vẫn chỉ xem.
2. Học sinh chưa từng có quan hệ lớp nào trong môn vẫn có thể luyện độc lập theo quy tắc cũ. Học sinh có quan hệ lớp cũ nhưng không còn lớp đang học không được tự chuyển thành trường hợp luyện độc lập.

Lộ trình hiện lưu theo học sinh–môn, không phải bản chụp lịch sử riêng của từng lớp. Giao diện lịch sử không hiển thị một lộ trình cá nhân hiện tại như thể đó là lộ trình lịch sử của lớp.

## Cách chặn

`StudentLearningScope` xác minh lớp đang học, thành viên, môn và giáo trình áp dụng. Các API lấy câu tiếp theo, tạo lộ trình, cập nhật buổi học và nộp câu luyện đều dùng phạm vi này. Bộ chọn ứng viên không rơi về toàn bộ môn khi chỉ còn lớp lưu trữ. Các API giao/xuất bản bài, bắt đầu bài, lưu nháp và nộp bài của lớp đã có kiểm tra trạng thái lớp; lượt này giữ nguyên các kiểm tra đó và đưa trạng thái chỉ xem vào dữ liệu chi tiết bài tập.

GET lộ trình chi tiết không còn ghi bổ sung `PlanJson` cho lộ trình cũ. Khi có lớp được chọn, cả chủ đề trong danh sách mục lẫn các buổi học đã lưu đều được kiểm tra phạm vi.

Header và các trang học dùng chung cơ chế xử lý bookmark lớp không hợp lệ. Chỉ thử chọn lại lớp sau HTTP 404; không bỏ qua lỗi mạng/quyền. Chờ URL khớp lớp mới rồi mới bật thao tác học.

## Kiểm tra

- Backend toàn bộ: 3.975 đạt, 77 bỏ qua do điều kiện môi trường; tổng 4.052.
- Các ca mới về lớp lưu trữ: 9 đạt, gồm bỏ `classId`, `history=false`, lớp khác còn hoạt động, học sinh khác, câu luyện ngoài giáo trình, lộ trình cache sai chủ đề, ngừng áp dụng giáo trình và GET không ghi dữ liệu.
- Frontend toàn bộ: 612 đạt, 0 lỗi. Có kiểm tra hành vi thực của hàm nộp bài: thoát trước mọi ghi nháp/upload khi phạm vi chỉ xem.
- TypeScript/Vite build: đạt. Cảnh báo chunk MathLive lớn đã có từ trước, không phải lỗi build.
- MySQL thật: 11 đạt, 0 lỗi, database test riêng; không gọi AI provider. Kiểm tra cả đọc bài cũ, nộp câu luyện, đọc/tạo lộ trình, lưu trữ/mở lại và bảo toàn lịch sử áp dụng.
- Docker build API/web: đạt; đã cập nhật hai service local, không khởi động lại MySQL. Readiness: `Healthy`, MySQL `Healthy`.
- Chrome, phiên student01: lộ trình/luyện tập lịch sử hiển thị thông báo chỉ xem. Test 3 giữ điểm 10/10, đáp án, lập luận, lời giải AI/giáo viên và rubric cũ; không có nút nộp, bỏ qua hay làm lại. Chuyển về Lớp Toán 10: khảo sát và nút sinh lộ trình sử dụng được. Không bấm sinh lộ trình, không nộp thêm bài trong kiểm tra UI.

Test MySQL bắt được lỗi ánh xạ `List<Guid>.Contains` của provider hiện dùng. Đã đổi danh sách lớp/giáo trình thành truy vấn con SQL có kiểu rõ ràng, không đưa danh sách GUID client-side vào `IN`.

## Bảo toàn dữ liệu thật

Đã tạo bản sao riêng tư trước lượt sửa tại `storage/backups/2026-10-09-before-archived-learning-lock.sql`; không đưa bản sao/dữ liệu vào Git.

SHA-256: `b68e323e00d1bab579d31f5db657370c577ac129397c887cc72debdb36004fbe`.

Đường cơ sở gồm 19 bảng bảo vệ: 204 lượt làm, 204 phân tích, 2 ảnh đính kèm, 20 dòng tiến độ, 7 dòng lịch sử áp dụng giáo trình. Fingerprint sau triển khai: cả 19 bảng không thay đổi (`changedTables: []`), gồm cả bảng users. Không dùng bản sao của lượt trước để ghi đè các thay đổi lưu trữ/xuất bản/áp dụng giáo trình bạn vừa thực hiện.

Ảnh bằng chứng:

- `docs/verification/evidence/archived-learning-lock/history-learning-path.png`.
- `docs/verification/evidence/archived-learning-lock/history-adaptive-practice.png`.
- `docs/verification/evidence/archived-learning-lock/archived-assignment-detail.png`.
- `docs/verification/evidence/archived-learning-lock/saved-result-readonly.png`.
- `docs/verification/evidence/archived-learning-lock/current-class-learning-path.png`.

## Vấn đề khác ghi nhận, chưa mở rộng sửa trong lượt này

Khảo sát lộ trình của lớp đang học vẫn liệt kê chủ đề toàn môn, kể cả khối khác. Server sinh lộ trình đã lọc theo giáo trình lớp và bỏ chủ đề ngoài phạm vi; cần một lượt sửa riêng để danh sách khảo sát cũng khớp lớp, giúp học sinh không chọn nhầm. Đây không phải đường mở lại quyền học trong lớp lưu trữ.

## Danh sách tệp có thay đổi trong riêng lượt này

Danh sách không phải toàn bộ `git diff` của workspace đang có nhiều lượt sửa chưa commit. Các tệp bắt đầu/lưu nháp/nộp bài tập không có thay đổi ròng từ lượt này.

### Server và hợp đồng dữ liệu (14 tệp)

1. `src/EduTwin.API/Controllers/LearningController.cs`: nhận phạm vi lớp/lịch sử khi lấy câu luyện.
2. `src/EduTwin.API/Controllers/RecommendationsController.cs`: nhận lớp cho lộ trình; trả lỗi nghiệp vụ khi phạm vi chỉ xem.
3. `src/EduTwin.BLL/AssessmentAndReasoning/AttemptSubmissionValidator.cs`: chặn nộp mới trong lịch sử/lớp lưu trữ và câu ngoài giáo trình.
4. `src/EduTwin.BLL/Assignments/GetStudentAssignmentUseCase.cs`: trả lớp, trạng thái và lý do chỉ xem cho bài tập.
5. `src/EduTwin.BLL/Organization/StudentLearningScope.cs` (mới): kiểm tra phạm vi học tập dùng chung.
6. `src/EduTwin.BLL/Recommendations/IRecommendationEngine.cs`: hợp đồng sinh lộ trình theo lớp.
7. `src/EduTwin.BLL/Recommendations/OpportunityCandidateBuilder.cs`: lọc ứng viên theo lớp và chặn fallback toàn môn khi không còn lớp đang học.
8. `src/EduTwin.BLL/Recommendations/RecommendationEngine.cs`: sinh theo phạm vi lớp; không thay đổi luồng điểm/bằng chứng hay lịch sử chấm.
9. `src/EduTwin.BLL/Recommendations/UseCases/GenerateLearningPathUseCase.cs`: chặn sinh lộ trình, kiểm tra cache và GET chỉ đọc.
10. `src/EduTwin.BLL/Recommendations/UseCases/GetNextQuestionUseCase.cs`: chặn lấy câu mới, kiểm tra câu cache.
11. `src/EduTwin.BLL/Recommendations/UseCases/UpdateLearningPathSessionUseCase.cs`: chặn đổi tiến độ buổi học lịch sử/ngoài phạm vi.
12. `src/EduTwin.Contracts/AssessmentAndReasoning/SubmitAttemptRequest.cs`: ngữ cảnh lớp/lịch sử khi nộp câu.
13. `src/EduTwin.Contracts/Assignments/StudentAssignmentDetailDto.cs`: thông tin chỉ xem.
14. `src/EduTwin.Contracts/Recommendations/LearningPathQuestionnaireDto.cs`: ngữ cảnh lớp/lịch sử khi sinh lộ trình/cập nhật buổi học.

### Giao diện (14 tệp)

1. `web/edutwin-web/src/api/dashboardsApi.ts`: tải phạm vi và xử lý bookmark thống nhất.
2. `web/edutwin-web/src/api/learningFeedbackApi.ts`: gửi phạm vi lấy câu luyện.
3. `web/edutwin-web/src/api/learningPathApi.ts`: gửi phạm vi lộ trình và buổi học.
4. `web/edutwin-web/src/components/student/StudentAcademicContext.tsx`: dùng chung cơ chế tải phạm vi.
5. `web/edutwin-web/src/components/student/StudentLearningReadOnlyNotice.tsx` (mới): thông báo chỉ xem và điều hướng về dữ liệu đã lưu/lớp đang học.
6. `web/edutwin-web/src/hooks/useStudentLearningAccess.ts` (mới): trạng thái cho phép học/chỉ xem/đang xác minh.
7. `web/edutwin-web/src/pages/LearningPlayerPage.tsx`: chặn thao tác, tự lưu, đồng hồ, nút nộp/làm lại; giữ xem kết quả cũ.
8. `web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx`: thông báo chỉ xem và CTA xem lại.
9. `web/edutwin-web/src/pages/StudentAssignmentsPage.tsx`: CTA lịch sử chỉ xem lại.
10. `web/edutwin-web/src/pages/StudentLearningPathPage.tsx`: chặn sinh/bắt đầu trong lịch sử, giữ ngữ cảnh khi điều hướng.
11. `web/edutwin-web/src/types/assignments.ts`: đồng bộ hợp đồng chỉ xem.
12. `web/edutwin-web/src/types/learning.ts`: đồng bộ ngữ cảnh nộp câu.
13. `web/edutwin-web/src/types/learningPath.ts`: đồng bộ ngữ cảnh lộ trình.
14. `web/edutwin-web/src/utils/assignmentReviewTiming.ts`: không bắt đầu bài chỉ xem.

### Kiểm thử (5 tệp)

1. `tests/EduTwin.BLL.Tests/Recommendations/ArchivedLearningScopeTests.cs` (mới).
2. `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs`.
3. `web/edutwin-web/tests/archivedLearningLock.test.ts` (mới).
4. `web/edutwin-web/tests/submittedMathReview.test.ts`.
5. `web/edutwin-web/tests/studentClassHistory.test.ts`.

Tổng: 33 tệp code/test trong lượt này, cộng báo cáo này và ảnh xác minh. Không commit/push.
