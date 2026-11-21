# Góc tập trung — giờ báo thức và chuông

Yêu cầu được làm rõ: học sinh chọn giờ báo thức cụ thể (HH:mm), không phải nhập thời lượng đếm ngược tùy ý.

## Thay đổi

- Giữ đếm ngược 10/25/45 phút; thêm chế độ Giờ báo thức với bộ chọn giờ/phút theo giờ thiết bị.
- Hiển thị thời điểm sẽ báo; giờ đã qua được đặt cho ngày mai và ghi rõ ngày mai trước khi bắt đầu.
- Phát chuông ngắn bằng Web Audio, không tải file âm thanh; có bật/tắt chuông và Nghe thử chuông để kiểm tra âm thanh.
- Hiện cảnh báo khi đến giờ kể cả lúc panel thu gọn. Không tự mở modal hoặc lấy mất vị trí đang học.
- Lưu giờ báo thức, deadline, chế độ âm thanh và trạng thái đóng cảnh báo trong sessionStorage theo người dùng; tương thích dữ liệu bộ đếm cũ.
- Hủy/đặt lại không phát chuông. Quay lại tab cập nhật theo thời gian thực, không chỉ trừ số lần interval chạy.
- Tab cần còn mở để phát chuông; trường hợp trình duyệt không phát được âm thanh vẫn có cảnh báo trên màn hình. Không triển khai báo thức chạy sau khi đóng trình duyệt.

## Kiểm chứng

- `node --test tests/focusTimer.test.ts`: 4/4, gồm giờ hôm nay, giờ ngày mai, giao năm, thời gian không hợp lệ và khôi phục dữ liệu cũ/mới.
- `npm test`: 968/968.
- TypeScript build và ESLint các file TypeScript thay đổi: đạt.
- `node web/edutwin-web/tests/smoke/chrome_focus_alarm_smoke.cjs`: 5/5 trên Chrome headless với component React thật. Kiểm tra real Web Audio tạo ba nốt chuông; chỉ phát một lần lúc đến giờ; hủy không báo; tắt âm vẫn có cảnh báo; giữ deadline tuyệt đối sau reload; đếm ngược tạm dừng/tiếp tục/đặt lại; màn hình 375px không tràn ngang.
- Đã xem ảnh kiểm thử desktop tối và mobile, sửa màu chữ panel tối để giữ độ tương phản.
- Build Docker và cập nhật riêng service web của QA localhost:3000; không cập nhật API/database.
- QA trực tiếp: đăng nhập student01, mở góc tập trung, chuyển Giờ báo thức, chọn 19:29 ngày 21/11/2026 bằng bộ chọn giờ native, nghe thử, đặt báo thức và thu gọn khi còn 17 giây. Khi đến giờ thật, trang hiện “Đã đến giờ báo thức 19:29. Hãy nghỉ một chút nhé.” ngoài modal. Không thay đổi giờ hệ thống hoặc deadline trong phiên QA thật.

Ảnh và kết quả Chrome component: `scratch/focus-alarm-verify/`.
