# Student QA — sửa lỗi và kiểm thử lại 20/10/2026

Môi trường: localhost:3000, API localhost:5000, dữ liệu demo EDUTWIN_A.
Tài khoản kiểm thử: student01, teacher.math và teacher.english khi cần.
Không ghi mật khẩu hoặc nội dung cấu hình bí mật trong báo cáo.

## Bản sửa

1. Nhật ký có thêm nguồn **Nộp bài và phân tích** từ thời điểm đã lưu của câu trả lời, lần nộp toàn bài và bản phân tích. Trước đây chỉ có lịch sử giáo viên duyệt và sự kiện thông báo, nên bài đã nộp không hiện trong nhật ký học thuật. Nguồn **Giáo viên đánh giá** được đặt tên đúng phạm vi. Giữ scope student-own / teacher-owned-class; manager và platform không có dữ liệu học thuật.
2. Sự kiện `AssignmentAiReady` có tên và mô tả tiếng Việt cụ thể; không còn rơi vào nhãn chung “Cập nhật được ghi nhận”.
3. Câu hỏi seed có **chủ đề**, nhưng thiếu **khối lớp**. Đây là nguyên nhân bộ lọc lớp 10 không tìm được câu hỏi; nhận định ban đầu rằng thiếu chủ đề đã được sửa sau khi kiểm tra dữ liệu. Seed mới có khối; backfill chỉ bổ sung khối còn trống cho câu hỏi khớp ID, trung tâm, môn, chủ đề, giáo viên tạo và nguyên văn đề của template. Không ghi đè câu đã phân khối hoặc đề do giáo viên sửa.
4. README lấy mật khẩu demo từ cấu hình lúc seed, không công bố một mật khẩu mặc định sai.
5. Nguy cơ 62% ở student và 0 học sinh nguy cơ cao ở teacher phù hợp ngưỡng 70%. Dashboard teacher giải thích ngưỡng này và sửa phép nhân 100 thừa khi hiển thị phần trăm.

Không thay schema. Dữ liệu local đã được sao lưu trước khi cập nhật.

## Kiểm tra tự động

- Backend: 25/25 kiểm thử liên quan ActivityAndOperations, SeedQuestionGradeBackfill, EduTwinSeedFactory đạt.
- Frontend: build thành công; 18/18 kiểm thử notifications đạt, gồm nhãn nguồn/sự kiện mới.
- `git diff --check`: đạt.

## Kiểm thử trực tiếp

Đang thực hiện vòng hồi quy sau khi cập nhật dịch vụ. Kết quả chi tiết sẽ được cập nhật tại đây.
