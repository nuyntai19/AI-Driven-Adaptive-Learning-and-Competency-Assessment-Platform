# Lộ trình từ bài làm → luyện tập → điều chỉnh

Yêu cầu đã được người dùng xác nhận ngày 16/10/2026. Triển khai trên working tree hiện có, không reset/commit/push và không tự tạo nội dung học thuật giả.

## Hành vi triển khai

- Đầu trang có khung đề xuất lấy bài làm 30 ngày gần nhất trong đúng môn, giáo trình và tài khoản. Đếm riêng lượt trả lời, bài tập đã tham gia, câu khác nhau đã chấm và điểm trên các câu gần nhất. Không gọi số lượt nộp là số bài kiểm tra hoàn tất.
- Một câu được làm nhiều lần chỉ dùng câu trả lời gần nhất để đếm đúng/sai và ưu tiên kỹ năng; lỗi cũ đã sửa không tiếp tục thổi phồng điểm yếu. Có thể mở các câu liên quan và ngày nộp ngay trong khung.
- Nhận định “cần xem lại” xuất phát từ kết quả câu hỏi. Chỉ hiện misconception cụ thể khi phân tích đã được giáo viên xác nhận, không fallback. Đáp án sai đơn lẻ không được tự biến thành chẩn đoán nguyên nhân.
- Nút Xem tuyến đề xuất mở mục tiêu với kỹ năng cần củng cố được điền sẵn; học sinh điều chỉnh kỹ năng, thời gian và hạn học trước preview/accept. Planner ưu tiên kỹ năng có kết quả chưa tốt, vẫn tuân prerequisite.
- Phần luyện dùng danh sách tối đa 5–8 câu thật tùy lịch/ngân hàng, không còn cap 3. Thứ tự ưu tiên câu từng sai rồi tăng độ khó; giao diện chia khởi động, gỡ vướng/áp dụng, luyện phản xạ. Không bịa ra dạng bài hoặc câu hỏi mới nếu dữ liệu chưa có.
- Khi ngân hàng có ít nhất 10 câu mới, dành 5 câu cho kiểm tra độc lập. Khi ít hơn, vẫn cho luyện từ câu thật và báo thiếu câu mới để xác nhận. Không lặp cùng một câu trong một bộ để đủ số lượng. Nội dung chỉ có 1–4 câu được nêu rõ là bộ chưa đủ.
- Submit luyện trả điểm đúng/sai ban đầu khi canonical grader đã chấm được, đáp án và lời giải được biên soạn, cách tiếp cận và bước tiếp theo. Không chờ gọi AI mới cho học tiếp. Giữ nhãn ban đầu/chờ review để không lẫn với kết quả cuối.
- Phản hồi hiện giữa hai câu, có nút tiếp tục; có danh sách câu, tiến độ và phần xem lại. MC hiện đáp án bằng nhãn/nội dung thay vì ID. Không dùng fallback Passed.
- Tự kiểm tra vẫn tách riêng: không trả lời giải/đúng sai của từng câu trước khi nộp cả bộ. Cần ít nhất 5 câu độc lập qua evidence gate, 80%, không pending review để xác nhận theo policy hiện hữu.
- Evaluator thêm buổi rèn lại sau NeedsSupport, hoặc lịch ôn sau 2 ngày khi Passed. Hoàn thành buổi ôn hẹn lượt sau 7 ngày. Review dùng câu luyện, không tự cấp xác nhận độc lập. Mỗi thay đổi có PathDecision và được hiển thị trong “Tiến bộ và lịch ôn”.
- GET chỉ đọc; follow-up được tạo trong evaluator dưới khóa học sinh và transaction, idempotent theo nguồn run/outcome. Lịch ôn chưa đến ngày bị chặn cả server. Worker legacy tiếp tục không được supersede tuyến đã nhận.

## Giới hạn thực tế

- Ngân hàng hiện hữu quyết định độ đa dạng và độ dài thật. Đây chưa phải bộ sinh câu hỏi/bài học mới hay hệ thống authoring hoàn chỉnh.
- Lịch 2/7 ngày là cấu hình sản phẩm khởi đầu, chưa phải thuật toán ghi nhớ đã hiệu chuẩn qua pilot.
- Phản hồi tức thì dựa trên đáp án/lời giải có sẵn và canonical preliminary grading. Bài cần phân tích sâu/giáo viên vẫn giữ trạng thái chờ, không được chấm giả để đạt tốc độ.
- Bộ luyện và bộ tự kiểm tra hiện là các nhiệm vụ riêng trong cùng chặng; không tuyên bố đã có rubric vận dụng đa kỹ năng đầy đủ.

## Kiểm chứng

- Full backend: **4333 pass, 0 fail, 103 skip**, `scratch/v4-connected-full-backend.log`. Các relational suite tùy chọn skip không được tính là pass.
- Sau các guard cuối: **32 test mục tiêu pass**, `scratch/v4-connected-targeted-final.log`: latest distinct answer/subject scope, preselected weakness, 5–8 real questions, authored feedback immediately and after retry/reload, withheld independent solutions, small-bank shortage, fresh check reservation during repair, scheduled recall/repair idempotency, cancelled mission guard.
- Full frontend: **754 pass**, lint sạch, build local production thành công. `scratch/v4-connected-full-frontend.log`, `scratch/v4-connected-lint.log`, `scratch/v4-connected-web-build.log`.
- Bản sao lưu trước rollout lưu trong `storage/backups` (ignored); counts users/attempts/evidence/history tại thời điểm sao lưu: **17 / 5 / 5 / 5**. Không in nội dung backup hoặc secrets vào log.
- HTTP/MySQL: **8/8 nhóm PASS**, gồm recommendation đọc câu sai thật, danh sách câu riêng biệt và phản hồi tức thì/replay/đọc lại (`scratch/v4-connected-api-acceptance.log`, `scratch/v4-api-acceptance.json`). Harness dùng schema tạm có manifest sở hữu; thêm câu fixture chỉ tại schema tạm.
- Browser **PASS**: đăng nhập thật, URL không có classId, preview/điều chỉnh ngày/kỹ năng, Study autosave/reload, luyện **5 câu khác nhau** trên fixture, đọc phản hồi từng câu và bấm tiếp, kết quả sai mới hiện lên khung đề xuất, mục tiêu đề xuất đã chọn sẵn kỹ năng cần xem lại, mobile không tràn ngang (`scratch/v4-connected-browser.log`, `scratch/v4-browser-results.json`).
- Đã xem ảnh thực `storage/v4-browser-evidence/practice-feedback-desktop.png` và `insights-desktop.png`. Đây là dữ liệu thử có nhãn, không phải điểm của tài khoản chính. Không gọi API provider bên ngoài để tạo kết quả kiểm chứng.
- Build Docker web và API thành công; guard cuối bổ sung chặn nhiệm vụ Skipped chưa bắt đầu được đóng gói riêng trong `scratch/v4-connected-api-release.log`, đã có unit test trong bộ 32. Trên image cuối đã chạy HTTP kiểm tra đọc agenda/insights và gọi URL nhiệm vụ đã hủy: 409, không tạo Run (`scratch/v4-connected-final-smoke.log`).
- Đã cập nhật local bằng `docker compose up -d --no-deps api web`; không recreate MySQL hoặc xóa volume, không cần migration mới. Xác nhận image/HTTP và counts sau cập nhật ghi tại `scratch/v4-local-verification.json`.
