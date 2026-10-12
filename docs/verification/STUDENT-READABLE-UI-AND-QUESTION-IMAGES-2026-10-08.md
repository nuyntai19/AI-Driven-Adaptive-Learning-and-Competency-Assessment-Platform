# Giao diện học sinh dễ đọc và câu hỏi có ảnh — 08/10/2026

## Phạm vi

Người dùng yêu cầu giao diện học sinh lớn hơn, bỏ nền kem và cho phép giáo viên
dùng ảnh chứa toàn bộ đề. Không thêm import PDF/OCR tự động, không thay quy tắc
AI chấm/giáo viên duyệt, không tự xuất bản câu hỏi hoặc bài tập mới.

## Giao diện học sinh

- Chỉ tăng token chữ/spacing trong `.student-shell`, không zoom toàn trang hoặc
  thay cỡ chữ root của khu vực quản lý/giáo viên.
- Desktop: chữ cơ bản 20px, tiêu đề `text-2xl` 30px; container tối đa 1800px.
  Mobile dùng mức tăng nhẹ hơn. Nền sáng `#F6F8FC`, giữ màu dark mode.
- Menu desktop bắt đầu ở 1440px; dưới ngưỡng này dùng drawer. Selector môn học
  và streak luôn ở hàng ngữ cảnh riêng, tránh chen vào các nút tài khoản.
- Tiêu đề và nhãn môn được phép wrap. Ẩn tagline logo không thiết yếu trên mobile.

## Đề bài bằng ảnh

- Một ảnh/câu: nhận PNG/JPG/WebP; ảnh gốc tối đa 8 MiB/24 triệu pixel.
  Browser chuẩn hóa về PNG nền trắng, cạnh dài tối đa 2048px, tối đa 2 MiB.
- Không nhận SVG/GIF. Server kiểm tra lại signature, chunk/CRC, kích thước,
  dữ liệu giải nén và byte thừa. Không tin MIME do client gửi.
- `QuestionText` có thể trống nếu có ảnh; server lưu nhãn an toàn
  `Đọc đề bài trong ảnh đính kèm.`. Không cho xóa ảnh rồi để lại nhãn này làm đề.
  Đáp án, lời giải, chủ đề/khối và rubric theo luồng cũ vẫn cần hợp lệ.
- Lưu binary trong bảng riêng `question_images`, `Question.HasImage` chỉ là flag.
  Query ngân hàng không tải toàn bộ blob; danh sách có nhãn câu chứa ảnh.
- Tạo câu và ảnh cùng transaction. Sửa/xóa ảnh có kiểm tra RowVersion và không
  cho thay nền tảng đề đã dùng trong attempt hoặc bài tập xuất bản: tạo bản sao.
  Bản sao giữ ảnh nếu thuộc câu của chính giáo viên hoặc học liệu Shared Active
  trong cùng trung tâm.
- API `GET /api/v1/questions/{id}/image` cần đăng nhập, quyền động hợp lệ và scope.
  Giáo viên xem câu của mình/Shared Active/câu trong bài lớp mình; quản lý xem
  trong trung tâm; học sinh chỉ xem câu đã được giao xuất bản, đã làm hoặc thuộc
  lộ trình của mình. Ngoài scope trả 404; không có URL ảnh public hoặc JWT trong URL.
- Response ảnh `private, no-store`, `nosniff`; UI tải blob bằng httpClient có auth,
  revoke object URL và hỗ trợ xem ảnh lớn. Nginx cho body JSON/base64 tới 4 MiB;
  giới hạn ảnh cụ thể vẫn do application kiểm tra.

## AI và hiệu năng

- Worker đọc ảnh đề theo trung tâm/câu, kiểm tra hash; ảnh thiếu/hỏng không bị
  bỏ qua để AI chấm một đề thiếu dữ kiện.
- Thứ tự multimodal: ảnh đề của giáo viên trước, ảnh nháp học sinh sau. Batch
  có `questionImageIndexes`/`studentImageIndexes` riêng cho từng câu.
- Prompt yêu cầu đọc toàn bộ đề trong ảnh, không suy đoán hình học chỉ từ tỷ lệ
  vẽ, không nhận lệnh nhúng trong ảnh. Nếu dữ kiện thiết yếu không đọc được thì
  giải thích rõ bằng tiếng Việt và yêu cầu xem xét, không dựng đáp án sai.
- Có ảnh rõ không tự động làm bài bị chuyển giáo viên. Quy tắc chốt điểm/duyệt
  hiện hữu vẫn giữ nguyên. AI vẫn được gửi đáp án/lập luận/nháp/lời giải/rubric.
- Batch đếm cả ảnh đề và nháp trong budget; không trộn ảnh giữa học sinh/câu.
  Fingerprint checkpoint có hash ảnh đề. Câu chỉ có text giữ fingerprint cũ.
- Không thêm lượt gọi AI/OCR lúc tạo câu; một bài có ảnh có thể tốn token và thời
  gian hơn bài text do đọc ảnh và giới hạn số ảnh/batch. Chưa benchmark AI thật
  cho đề ảnh trong lượt này; không suy diễn kết quả 50 câu text sang đề ảnh.

## Kiểm tra và triển khai

- Frontend: 569 passed, 0 failed; TypeScript/Vite và Docker build thành công.
- Backend: 3934 passed, 0 failed, 74 skipped (integration opt-in); TRX trong
  `tests/TestResults/question-images/` (không publish dữ liệu/log tự động).
- Test MySQL ảnh đề: 1 passed, 0 failed, 0 skipped. Database tạm được tạo/migrate
  và dọn sau test. Kiểm tra tạo ảnh-only, blob/FK thực, own teacher, học sinh đúng
  bài published, draft/không được giao/trung tâm khác không đọc được.
- Unit test: PNG lỗi/corrupt/truncated/byte thừa/quá lớn; tạo ảnh-only; clone ảnh
  private; không đổi ảnh đã có attempt; worker gửi ảnh đề; prompt roles/indexes;
  batch budget; checkpoint hash ảnh và tương thích text-only.
- Cập nhật test hợp đồng public shape và số entity tenant-filter cho bảng mới.
  Đồng thời sửa dữ liệu test CreateAssignment dùng ID từ đồng hồ modulo bị trùng
  ngẫu nhiên; không thay code nghiệp vụ tạo bài tập.
- Backup database local trước migration lưu riêng trong `storage/backups/`;
  không có job AI Pending/Processing trước cập nhật. Migration
  `20261008095834_AddQuestionImages` chỉ thêm flag và bảng mới; không reset volume.
- Runtime API readiness/MySQL Healthy, endpoint ảnh chưa auth trả 401.
- Giao diện Chrome đã xác minh nền mới và tiêu đề desktop 30px/mobile 24px.
  Viewport CSS thực 390/1280/1440/1760px đã được kiểm tra; 1440px có khoảng cách
  44px giữa navigation và utilities, 1760px có khoảng cách 145px. Không có
  overflow ngang ở 390/1440/1760px. Drawer mobile mở/đóng được. Đã khôi phục
  viewport mặc định và lưu screenshot riêng trong `storage/verification/`.
  Luồng upload UI giáo viên
  và chấm ảnh thật cần phiên giáo viên và dữ liệu test riêng; chưa khẳng định
  đã có kết quả Gemini thật cho câu ảnh.

Chưa commit/push. Giữ riêng các thay đổi bộ đếm/streak/vai trò từ lượt trước và
không đưa backup, ảnh chụp có danh tính hay dữ liệu test local lên GitHub.
