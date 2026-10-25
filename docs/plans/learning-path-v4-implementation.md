# Triển khai lộ trình V4 — 25/10/2026

Nhánh làm việc: `shin/studytask`. Các thay đổi nằm trong mã nguồn, chưa áp dụng migration vào cơ sở dữ liệu đang dùng.

## Hành vi đã triển khai

- Hợp nhất mục tiêu vào LearningGoal; Digital Twin đọc cùng điểm mục tiêu và ngày đích. Sửa từ Twin làm mất hiệu lực bản xem trước cũ. Không xóa lịch sử.
- Tách kiểm tra đầu vào khỏi loại mục tiêu; dùng 3–5 câu chấm tự động hợp lệ. Mẫu nhỏ thể hiện chưa kết luận/độ tin cậy thấp; 0–2 câu không chặn tuyến học. Câu chấm tay và câu cấu hình lỗi không được phát vào đầu vào tự động.
- Lập lịch theo ngày thực tế và múi giờ, ngày loại trừ, trần phút/ngày, dự phòng ôn; phần vừa ngân sách vẫn học được, phần chưa xếp được ghi rõ. Giữ thứ tự tiên quyết. Buổi bỏ lỡ được lưu lịch sử rồi lên lịch phần còn lại khi đọc agenda.
- Nhịp học thay đổi kích thước bộ luyện. Ngân sách hôm nay trừ phần đã học và kỳ thi cố định; không tách bài thi có giờ thành nhiều ngày.
- Kỳ thi lớp có loại, ngày giờ và thời lượng; học sinh không bắt đầu trước giờ. Agenda đọc kỳ thi được giao trong lớp hiện tại, không tạo bản sao hay đổi lịch thi.
- Ôn thi chuyển sang kiểm tra chặng ở ngưỡng 45 ngày và thi thử ở ngưỡng 14 ngày; thi thử có đồng hồ, không dừng thời gian khi đóng player. Chấm và rèn lại theo kỹ năng thực sự xuất hiện trong đề, không xác nhận tất cả kỹ năng bằng điểm tổng.
- Thiếu thời gian/phạm vi bị hoãn được giải thích trên giao diện và đưa thông báo qua outbox cho học sinh/giáo viên lớp; khóa khử trùng tránh gửi lại cùng cảnh báo.
- Một bộ chọn môn, một điểm vào tạo tuyến; khung rộng và lưới co giãn, trạng thái thao tác được làm mới khi đổi phạm vi.
- Ngừng sinh tuyến legacy từ API và callback; giữ chức năng đọc lịch sử.

## Giới hạn cần biết

- Điểm dự báo dùng quan sát độc lập hợp lệ trong 30 ngày, tối đa 5 câu mỗi kỹ năng và trọng số nội dung. Thiếu tối thiểu 3 câu ở bất kỳ kỹ năng trong phạm vi thì trả chưa đủ dữ liệu. Đây là ước lượng sơ bộ có phiên bản, chưa phải mô hình điểm thi đã hiệu chuẩn; không ghi thẳng thành mastery của Knowledge Twin.
- Lịch được điều chỉnh khi truy cập agenda; chưa có tác vụ nền tự chạy hằng đêm cho mọi học sinh. Thông báo chỉ được tạo khi bước tính lịch thực sự chạy.
- API hỗ trợ ngày loại trừ và múi giờ; hộp thoại hiện cung cấp các ngày trong tuần và tỷ lệ dự phòng, chưa có bộ chọn riêng cho hai trường trên.
- Chưa chạy kiểm thử MySQL/Docker và chưa áp dụng migration trên dữ liệu thực trong môi trường này. Cần sao lưu dữ liệu và kiểm thử migration trên bản sao cơ sở dữ liệu trước khi phát hành.

## Kiểm chứng

Các kiểm thử mới bao phủ mẫu đầu vào nhỏ, lịch/ngân sách/tiên quyết, thi thử nhiều kỹ năng, hết giờ, buổi bỏ lỡ, chặn legacy, dự báo thiếu dữ liệu và xung đột mục tiêu.

- Toàn bộ backend: 4.613 đạt, 106 bỏ qua theo cấu hình, 0 lỗi. Sau chỉnh sửa cuối về bản xem trước, chạy lại nhóm Recommendations: 237 đạt, 10 bỏ qua, 0 lỗi.
- Frontend: 821 đạt, 0 lỗi; build TypeScript/Vite thành công. Vite vẫn cảnh báo chunk MathLive lớn.
- ESLint các file frontend sửa: 0 lỗi, 3 cảnh báo có sẵn (dependency hook và Fast Refresh).
- EF `has-pending-model-changes`: không có thay đổi mô hình chưa được migration bao phủ. Sinh SQL nâng cấp thành công; chưa thực thi SQL vào DB.
- `git diff --check`: không có lỗi khoảng trắng.

Log kiểm chứng nằm trong thư mục `scratch/` của workspace, được Git bỏ qua: `final-tests.log`, `final-recommendations.log`, `final-web-tests.log`, `final-web-build.log`, `final-lint.log`, `model-check.log`, `migration-script.log`. SQL sinh thử: `scratch/learning-path-calendar.sql`.
