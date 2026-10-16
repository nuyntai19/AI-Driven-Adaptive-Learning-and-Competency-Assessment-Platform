# Mở ngay tuyến đề xuất đã điền — 16/10/2026

Theo yêu cầu người dùng: nút Xem tuyến đề xuất phải mở thẳng bản kế hoạch, giải thích phần sai nhiều/cần tiến bộ/nội dung học, thay vì bắt đầu bằng biểu mẫu trống. Học sinh được chỉnh kế hoạch trước khi xác nhận. Không thay đổi tuyến đang học chỉ vì mở preview.

## Hành vi

- Giữ luồng Thiết lập mục tiêu thủ công. Luồng đề xuất tự điền tên mục tiêu, loại mục tiêu, kỹ năng, lịch 15 phút/ngày × 5 ngày/tuần và mốc dự kiến hai tuần có thể chỉnh; tự tạo draft goal/proposal qua API hiện hữu rồi hiện các chặng, số câu, thời lượng và lý do. Không tự accept.
- Đầu preview hiển thị “Bài làm cho thấy”, “Đề xuất cải thiện”, “Phần nên học”. Câu sai được thống kê theo câu khác nhau và lần trả lời gần nhất, không gán nguyên nhân tâm lý hay misconception không có bằng chứng.
- Có câu sai: ưu tiên bộ rèn kỹ năng tương ứng. Chưa có hoặc còn ít bằng chứng, kể cả đúng 3/3: dùng mục tiêu Diagnostic và kiểm tra điểm xuất phát trước, không mặc định học lại nền tảng hay tự xếp nâng cao.
- Nâng cao tự động khi kỹ năng được map xác nhận bằng bằng chứng độc lập, đồng thời có ít nhất 5 câu gần đây và tỷ lệ đúng >=80%. Chỉ nói đủ bằng chứng cho kỹ năng cụ thể, không suy ra toàn môn đã giỏi. Người học vẫn có thể tự chọn Advanced khi chỉnh mục tiêu; không được tự cấp chứng nhận.
- Bản Advanced ghi `difficultyFloor = 4` trong mission snapshot. Planner/agenda/start đều lọc câu thực có độ khó 4–5/5. Với kỹ năng đã Confirmed, không thêm bài đọc cơ bản hoặc checkpoint cơ bản lặp lại. Nếu chưa có câu khó, nhiệm vụ bị khóa và giải thích thiếu nội dung. Không phát câu dễ để thay thế.
- Nhãn thống kê dưới 5 câu đúng được đổi thành “Cần thêm bài làm”, tránh dùng chữ “Đang làm tốt” quá mạnh với dữ liệu ít.

## Kiểm thử

- Frontend 757 test pass, gồm 3 ca mới: 3/3 → Diagnostic đã chọn kỹ năng; sai 4/6 → Foundation có nhận xét và phần học rõ; điểm đúng của luyện có hướng dẫn không tự thành Advanced nếu thiếu Confirmed.
- Backend 34 ca V4/connected pass, gồm hai nhánh Advanced: có câu khó thì thật sự phát đúng câu khó; không có thì báo thiếu và chặn start. Agenda vẫn đề xuất nâng cao cho kỹ năng Confirmed, không vô tình bỏ qua như bài ôn cơ bản.
- Build frontend local, lint và Docker API/web đạt. 8/8 nhóm HTTP/MySQL chạy lại PASS (`scratch/v4-auto-api-tests.log`).
- Browser PASS trên image cuối: bấm đề xuất thấy bản kế hoạch ngay, không có form ban đầu; đủ ba khối quan sát/cải thiện/phần học; trường hợp thiếu bằng chứng dùng Diagnostic; điều chỉnh được kỹ năng có sẵn và giữ các luồng luyện/autosave/mobile trước đó (`scratch/v4-auto-browser.log`, `scratch/v4-browser-results.json`). Đã xem ảnh thực `storage/v4-browser-evidence/auto-proposal-desktop.png`.
- Đã cập nhật local bằng `docker compose up -d --no-deps api web`. Không cần migration mới, không recreate MySQL/volume. Dữ liệu chính được sao lưu trước cập nhật; baseline users/attempts/evidence/history là 17/7/7/7. Kết quả xác nhận image/HTTP/counts sau cập nhật tại `scratch/v4-local-verification.json`.
