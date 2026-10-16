# Sửa xem lại và tiếp tục buổi học — 16/10/2026

## Nguyên nhân

- Nút phụ “Xem lượt đã làm” cho phép mở chỉ đọc cả lượt đang dở, kể cả khi chưa nộp câu nào. Truy vấn đánh giá bị tắt nhưng giao diện kiểm tra `isPending`, nên hiển thị chờ vô hạn mặc dù không có yêu cầu đang chạy.
- Bắt đầu buổi khác trong cùng môn bị chặn đúng theo quy tắc một buổi đang làm, nhưng lỗi cũ không cho biết buổi nào và không có đường tiếp tục.

## Thay đổi

- Mỗi thẻ còn một hành động: Bắt đầu / Tiếp tục / Xem lại. Bỏ nút phụ “Xem lượt đã làm”.
- Xem lại đọc câu trả lời và kết quả đã lưu bằng GET, không gọi đánh giá lại hoặc chờ chấm lại. Trạng thái đang tổng kết chỉ xuất hiện khi yêu cầu thực sự đang chạy ở lượt đang học.
- Phản hồi 409 có mã `LEARNING_MISSION_IN_PROGRESS`, tên, mã nhiệm vụ và mã lượt đang dở của chính học sinh trong cùng trung tâm/môn. Giao diện hiển thị tên buổi và nút tiếp tục; mở lại dùng lượt cũ, giữ bản nháp.
- Không thay đổi điểm, bằng chứng hay bài làm để giải quyết xung đột; không thêm bảng hoặc migration.

## Kiểm tra

Kết quả chạy được ghi trong các nhật ký `scratch/v4-resume-*`. Kiểm thử bổ sung gồm xung đột có thông tin buổi đang dở, tiếp tục đúng lượt, xem lại không tạo lượt/đánh giá mới và từ chối truy cập của học sinh khác.

- Backend phần thực thi: 10/10 đạt.
- Frontend: 759/759 đạt; build đạt. Lint không có lỗi, còn 29 cảnh báo trong mã hiện có.
- HTTP/MySQL: 10 nhóm đạt, gồm phản hồi xung đột có cấu trúc và đọc lại không tạo lượt/quyết định mới.
- Trình duyệt desktop/mobile: đạt; tái hiện xung đột khi đang có bản nháp, mở đúng buổi và giữ bản nháp, xem lại câu đã nộp bằng một nút, không có trạng thái kiểm tra vô hạn. Lượt chạy đầu của kịch bản chờ nội dung đang thu gọn; đã sửa kịch bản để chọn câu trước khi kiểm tra và chạy lại thành công bằng tài khoản thử mới.
- Local đã cập nhật API và web; HTTP web 200, API được bảo vệ 401 khi không đăng nhập, cả hai dùng image mới. Số bản ghi users/attempts/evidence/history trước và sau: 17/15/15/15, giữ nguyên.
- Có bản sao lưu local trước khi cập nhật. Dữ liệu thử và các thao tác kiểm thử dùng database riêng có cơ chế kiểm tra quyền sở hữu.
