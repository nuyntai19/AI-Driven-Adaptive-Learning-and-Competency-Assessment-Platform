# Bộ chọn chủ đề và câu hỏi hình học 10 — 09/10/2026

## Phạm vi lượt sửa này

Không commit, stage hoặc push. Bảo toàn mọi thay đổi có sẵn trong worktree; không tạo migration mới, không sửa trạng thái lớp/giáo trình hay điểm bài làm cũ. Chỉ cập nhật dịch vụ API/web localhost sau khi build thành công, không khởi tạo lại MySQL.

### Đã sửa

- Thay danh sách chọn chủ đề phẳng bằng bộ chọn có tìm kiếm tên/mã, hỗ trợ tiếng Việt không dấu và chia nhóm theo nút Chương thật. Áp dụng ở ngân hàng câu hỏi và biểu mẫu soạn/chỉnh sửa câu hỏi.
- Chỉ cho chọn nút Topic đang hoạt động; không chọn Chapter/Skill làm chủ đề chính. Giữ ID dạng chuỗi, không suy đoán khối từ tên/mã nút; không tự đổi chủ đề cũ đang được chọn. Escape đóng và trả focus; có nút đóng và đóng khi bấm ngoài.
- Chủ đề trong bảng khảo sát lộ trình học sinh được giới hạn theo lớp/giáo trình hiện tại qua server, không lấy toàn bộ môn khi lớp không hợp lệ. Query cache phân biệt tác nhân, lớp và phạm vi lịch sử. Lựa chọn cũ được lọc theo tập chủ đề hợp lệ trên giao diện, không tự ghi lại dữ liệu khi mở trang.
- Nhãn khoảng cách mục tiêu dùng “Cần thêm…”, “Đã vượt…” hoặc “Đã đạt”, không còn dạng `+-1.8`.
- Bộ chọn câu hỏi khi tạo bài tập phân biệt điểm hiển thị thang 10 với trọng số gốc; đổi nhãn ShortAnswer sang tiếng Việt. Không thay công thức tính điểm hoặc trọng số đã lưu.
- Tổng quan Manager nói rõ “Rủi ro năng lực” dựa trên Digital Twin, khác số học sinh quá hạn và điểm đã chốt của báo cáo lớp. Không đổi thuật toán rủi ro.

## Tệp source/test được chạm trong lượt này (15)

1. `src/EduTwin.API/Controllers/RecommendationsController.cs`
2. `src/EduTwin.BLL/Recommendations/UseCases/GetLearningPathTopicsUseCase.cs`
3. `tests/EduTwin.BLL.Tests/Recommendations/ArchivedLearningScopeTests.cs`
4. `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs`
5. `web/edutwin-web/src/api/learningPathApi.ts`
6. `web/edutwin-web/src/components/teacher/KnowledgeTopicPicker.tsx` (mới)
7. `web/edutwin-web/src/utils/knowledgeTopicOptions.ts` (mới)
8. `web/edutwin-web/src/utils/academicDisplay.ts` (mới)
9. `web/edutwin-web/src/pages/teacher/TeacherQuestionBankView.tsx`
10. `web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx`
11. `web/edutwin-web/src/pages/teacher/TeacherAssignmentEditorView.tsx`
12. `web/edutwin-web/src/pages/StudentLearningPathPage.tsx`
13. `web/edutwin-web/src/pages/StudentDashboardPage.tsx`
14. `web/edutwin-web/src/pages/CenterDashboardPage.tsx`
15. `web/edutwin-web/tests/knowledgeTopicPicker.test.ts` (mới)

Bản báo cáo này là tệp tài liệu bổ sung, không phải thay đổi logic. Danh sách trên là phần chạm trong lượt này, không phải toàn bộ worktree từ các lượt trước.

## Câu hỏi được tạo bằng giao diện Chrome

Đã dùng skill computer-use để soạn, tải ảnh, lưu, kích hoạt và mở lại từng câu. Việc tải ảnh từng bị chặn vì quyền truy cập tệp của tiện ích; đã dừng phần upload và chỉ tiếp tục sau khi người dùng tự bật quyền. Không dùng API ẩn hoặc ghi SQL để tạo câu hỏi.

| ID | Dạng / phạm vi | Nội dung | Đáp án / chấm |
|---|---|---|---|
| 20066 | Essay, Khối 10, Private Teacher Math, Active | Tam giác ABC: AB=6 cm, AC=8 cm, góc A=60°, tính BC; ảnh minh họa | BC=2√13 cm; rubric 4 điểm phương pháp + 4 điểm tính toán + 2 điểm kết luận |
| 20067 | ShortAnswer / NumericRational, Khối 10, Private Teacher Math, Active | Tam giác nội tiếp: BC=10 cm, góc A=30°, tính bán kính ngoại tiếp R; ảnh minh họa | Đáp án số 10; phân tích lập luận riêng |

Cả hai gán chủ đề `MATH10-TRIANGLE-METRICS`, yêu cầu lập luận, có lời giải tiếng Việt, lỗi cần chú ý và hướng dẫn chấp nhận phương pháp tương đương. Hình chỉ minh họa, không dùng tỉ lệ hình để suy đoán số đo.

Đây là hai câu **đề chữ có hình minh họa**, không phải ảnh chụp chứa toàn bộ đề. Chưa tạo/giao bài tập mới hoặc nộp bài bằng tài khoản học sinh; chưa đo thời gian Gemini chấm ảnh thật trong lượt này.

### Nguồn ảnh

- Casey Leung, [Triangle ABC with Sides a b c](https://commons.wikimedia.org/wiki/File:Triangle_ABC_with_Sides_a_b_c.png), CC BY-SA 3.0. Nguồn/tác giả/giấy phép đã ghi trong đề; không chỉnh sửa nội dung hình.
- Régis Lachaume, [Triangle and circumcircle with notations](https://commons.wikimedia.org/wiki/File:Triangle_and_circumcircle_with_notations.png), public domain. Nguồn đã ghi trong đề.

Đề, đáp án và lời giải được soạn riêng, không sao chép nguyên bài tập trong sách.

## Kiểm chứng

- Backend full suite: 3.992 passed, 0 failed, 79 skipped vì không bật cấu hình MySQL integration cho toàn suite.
- Frontend: 636 passed, 0 failed.
- Frontend production build và Docker API/web build: thành công. Backend Release build: 0 warnings, 0 errors. Vite còn cảnh báo kích thước chunk MathLive lớn có sẵn.
- Nhóm kiểm tra workspace/lifecycle: 11/11 passed (8 workspace tests + 3 MySQL integration tests).
- Nhóm academic dependency MySQL: 2/2 passed. Tổng cộng 5 bài kiểm tra tích hợp MySQL thật trên schema test độc lập; không gọi AI, không sửa dữ liệu trung tâm thật.
- Kiểm tra SQL mới chứng minh bảng chủ đề khảo sát không lấy nút ngoài giáo trình, không lấy lớp lạ hoặc phạm vi lịch sử/lớp đã lưu trữ.
- Chrome Teacher Math: tìm “tam giac” ra đúng chủ đề, chọn đúng và lọc ngân hàng; bộ chọn trong trang chỉnh sửa hoạt động. Màn hình 360 px: popup nằm trong viewport (left 41 / right 304), Escape trả focus; đã reset viewport.
- Mở lại hai câu đã lưu: ảnh tải từ server, đáp án, Khối 10, rubric và lập luận còn đầy đủ.
- Chrome Center Manager EDUTWIN_A: mở được báo cáo lớp Toán 10 Active và lớp Toán kiểm thử cũ Archived qua nút “Báo cáo lớp”, không chuyển sang trang không có quyền. Lớp cũ ghi rõ chỉ xem, giữ ba bài của student01 với điểm 10,0; 10,0; 5,5 và trung bình 8,5/10. Lớp Toán 10 có 0 bài, hiện “Chưa đủ dữ liệu đánh giá”, không lấy bài cũ sang lớp mới. Không chỉnh trạng thái lớp, thành viên hoặc quyền trong lần smoke test.
- Dashboard Manager hiện nhãn/giải thích “Rủi ro năng lực theo lớp” đúng theo bản sửa.
- `git diff --check` cho các tệp tracked chạm trong lượt này: exit 0. Kiểm tra toàn worktree với quy tắc CRLF phù hợp còn báo whitespace trong một số tệp đã sửa từ trước (không thuộc phần sửa này); chưa tự format rộng các tệp đó.
- Trạng thái cuối: HEAD vẫn `a9918f4`; 337 tệp dirty khi đếm cả từng tệp untracked, 0 staged. Chênh lệch với số đếm cũ không phải 337 tệp được sửa mới trong lượt này.

### Bằng chứng local (không phải mã nguồn)

- `storage/verification/geometry-question-20066.jpg`
- `storage/verification/geometry-question-20067.jpg`
- `storage/verification/topic-picker-360.jpg`
- `storage/verification/manager-archived-class-report.jpg`
- `storage/verification/manager-active-class-report.jpg`

## Điểm còn cần phân biệt / chưa mở rộng

- Ô “Tìm kiếm nội dung” cũ đang lọc phía client trong trang đã tải; phân trang vẫn dùng tổng của API. Vì vậy có thể thấy “2 / 18” dù chỉ có hai kết quả trên trang. Bộ chọn chủ đề mới khác: lọc toàn bộ phạm vi qua server. Chưa chuyển tìm nội dung sang server trong lượt này; cần làm riêng nếu muốn tìm toàn ngân hàng.
- Giáo trình/đồ thị vẫn chưa có snapshot toàn bộ cấu trúc theo thời điểm; không tuyên bố năng lực trên trang lịch sử là bản chụp điểm trong quá khứ.
- Giao diện nhật ký/thông báo liên tác nhân vẫn thuộc kế hoạch để sau, chưa triển khai thêm.
- Chưa benchmark lại 50 câu hoặc benchmark Gemini multimodal. Lượt này không sửa pipeline/quota AI.
