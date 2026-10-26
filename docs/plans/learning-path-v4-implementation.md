# Triển khai lộ trình V4 — cập nhật 26/10/2026

Các thay đổi nằm trong mã nguồn của workspace hiện tại, chưa áp dụng migration vào cơ sở dữ liệu đang dùng.

## Hành vi đã triển khai

- Hợp nhất mục tiêu vào LearningGoal; Digital Twin đọc cùng điểm mục tiêu và ngày đích. Sửa từ Twin làm mất hiệu lực bản xem trước cũ. Không xóa lịch sử.
- Tách kiểm tra đầu vào khỏi loại mục tiêu; dùng 3–5 câu chấm tự động hợp lệ. Mẫu nhỏ thể hiện chưa kết luận/độ tin cậy thấp; 0–2 câu không chặn tuyến học. Câu chấm tay và câu cấu hình lỗi không được phát vào đầu vào tự động.
- Lập lịch theo ngày thực tế và múi giờ, ngày loại trừ, trần phút/ngày, dự phòng ôn; phần vừa ngân sách vẫn học được, phần chưa xếp được ghi rõ. Giữ thứ tự tiên quyết. Buổi bỏ lỡ được lưu lịch sử rồi lên lịch phần còn lại khi đọc agenda.
- Nhịp học thay đổi kích thước bộ luyện và trần số bộ dự kiến mỗi kỹ năng/ngày (1/3/4 cho Gentle/Moderate/Accelerated). Ngân sách hôm nay trừ phần đã học và kỳ thi cố định; không tách bài thi có giờ thành nhiều ngày.
- Kỳ thi lớp có loại, ngày giờ và thời lượng; học sinh không bắt đầu trước giờ. Thành viên mới được cấp quyền cho kỳ thi chung sắp diễn ra; agenda sửa liên kết còn thiếu từ ghi danh cũ. Giáo viên được đổi lịch bài thi Published chưa bắt đầu hoặc đóng/hủy lịch; lộ trình đọc cùng Assignment và hiển thị trong chuỗi chặng. Midterm/Final cùng pipeline, khác nhãn/phạm vi đề giáo viên chọn.
- Ôn thi có cổng kiểm tra chặng theo tiến độ kể cả không có ngày đích. Thi thử tổng hợp trong giai đoạn 14 ngày được xếp gần ngày đích, ưu tiên chừa thời gian rèn lại; không làm cổng. Bài bấm giờ không dừng khi đóng player và tự được tổng kết khi đọc agenda/mở bài sau khi hết giờ. Trượt chặng giữ phần phụ thuộc đóng đến khi rèn lại/thi lại đạt, phần độc lập vẫn học được.
- Thiếu thời gian/phạm vi bị hoãn được giải thích trên giao diện và đưa thông báo qua outbox cho học sinh/giáo viên lớp; khóa khử trùng tránh gửi lại cùng cảnh báo.
- Đầu vào Vững/Cần củng cố/Chưa kết luận được dùng khi tạo tuyến kế tiếp để giảm học cơ bản, ưu tiên và hẹn probe. Câu kiểm tra nền tảng bổ sung thuộc kỹ năng khác nhau và có nhãn. Có thao tác bỏ qua câu, kết quả giữ trạng thái chưa kết luận thay vì kẹt lượt học.
- Luyện/ôn có thời gian chờ câu đã làm/lộ đáp án, mặc định 2 ngày và có thể cấu hình. Câu đã dùng không trở thành câu kiểm tra độc lập mới sau thời gian chờ.
- Đổi ngày đích cập nhật mốc thi chưa bắt đầu; giữ lượt đang làm và kết quả cũ. Cảnh báo tiến độ theo số ngày học tách khỏi kết luận điểm thi.
- Một bộ chọn môn, một điểm vào tạo tuyến; khung rộng và lưới co giãn, trạng thái thao tác được làm mới khi đổi phạm vi.
- Ngừng sinh tuyến legacy từ API và callback; giữ chức năng đọc lịch sử.

## Giới hạn cần biết

- Điểm dự báo dùng quan sát độc lập hợp lệ trong 30 ngày, tối đa 5 câu mỗi kỹ năng và trọng số nội dung. Thiếu tối thiểu 3 câu ở bất kỳ kỹ năng trong phạm vi thì trả chưa đủ dữ liệu. Đây là ước lượng sơ bộ có phiên bản, chưa phải mô hình điểm thi đã hiệu chuẩn; không ghi thẳng thành mastery của Knowledge Twin.
- Lịch được điều chỉnh khi truy cập agenda; chưa có tác vụ nền tự chạy hằng đêm cho mọi học sinh. Thông báo chỉ được tạo khi bước tính lịch thực sự chạy.
- API hỗ trợ ngày loại trừ và múi giờ; hộp thoại hiện cung cấp các ngày trong tuần và tỷ lệ dự phòng, chưa có bộ chọn riêng cho hai trường trên.
- Chưa chạy kiểm thử MySQL/Docker và chưa áp dụng migration trên dữ liệu thực trong môi trường này. Cần sao lưu dữ liệu và kiểm thử migration trên bản sao cơ sở dữ liệu trước khi phát hành.

## Kiểm chứng

Các kiểm thử mới bao phủ mẫu đầu vào nhỏ, lịch/ngân sách/tiên quyết, thi thử nhiều kỹ năng, hết giờ, buổi bỏ lỡ, chặn legacy, dự báo thiếu dữ liệu và xung đột mục tiêu.

- Toàn bộ backend: 4.646 đạt, 106 bỏ qua theo cấu hình, 0 lỗi. Sau sửa cuối, các nhóm bị ảnh hưởng được chạy lại riêng; kết quả chi tiết cập nhật trong [handoff](learning-path-v4-handoff-2026-10-26.md).
- Frontend: 823 đạt, 0 lỗi; build TypeScript/Vite thành công. Vite vẫn cảnh báo chunk MathLive lớn.
- ESLint các file frontend sửa: 0 lỗi, 3 cảnh báo có sẵn (dependency hook và Fast Refresh).
- EF `has-pending-model-changes`: không có thay đổi mô hình chưa được migration bao phủ. Sinh SQL nâng cấp thành công; chưa thực thi SQL vào DB.
- `git diff --check`: không có lỗi khoảng trắng.

Log kiểm chứng nằm trong thư mục `scratch/` của workspace, được Git bỏ qua: `final-tests.log`, `final-recommendations.log`, `final-web-tests.log`, `final-web-build.log`, `final-lint.log`, `model-check.log`, `migration-script.log`. SQL sinh thử: `scratch/learning-path-calendar.sql`.
