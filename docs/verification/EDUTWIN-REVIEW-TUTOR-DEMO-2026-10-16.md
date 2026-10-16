# Xem lại, trợ giảng AI và dữ liệu tự kiểm tra — 16/10/2026

## Sửa theo phản hồi người dùng

- Nút Xem lại của Practice/IndependentCheck không còn bị vô hiệu hóa vì nhiệm vụ đã Completed. Agenda có LatestRunId; GET mission/run đọc lượt đã lưu, không gọi start để tạo lượt mới. Có nút xem lượt đã làm khi nhiệm vụ còn chờ kết quả hoặc có thể làm lại.
- Xem lại giữ nguyên đáp án, lời giải và cả đoạn tự diễn giải của học sinh. Nút số của hoạt động đọc đã nộp có thể mở cách hiểu đã lưu; trước đây nút chỉ mở nếu có question feedback nên Study bị loại.
- Lời giải tiếng Anh trong ảnh phản hồi là dữ liệu Solution có sẵn của ngân hàng, không phải bản dịch hay phản hồi AI mới. UI nay ghi rõ “Lời giải ngân hàng · nguyên văn”, đồng thời thêm khu vực “Trợ giảng AI · tiếng Việt”, nhận xét, lời giải riêng/cách hiểu đúng và điểm cần sửa.
- Endpoint AI dùng provider/model đã cấu hình, cùng quota/telemetry `learning_path`. Phản hồi được kiểm tra JSON và lưu kèm ActivityRun để đọc lại. Timeout/provider lỗi hiển thị Unavailable và cho thử lại; không giả phản hồi AI hay cấp Passed. Không giữ transaction SQL qua cuộc gọi provider.
- Study chấm cách hiểu theo các trạng thái Understood/PartlyUnderstood/Misunderstood/NotEnoughContext, kèm góp ý và cách diễn đạt đúng. Đây là phản hồi học tập, không tự tạo Attempt/evidence/Twin hoặc thay điểm chấm câu hỏi.
- AI độc lập với tốc độ chuyển câu: lời giải tham khảo có ngay, AI tải bổ sung. Xem lại danh sách không tự gọi AI cho mọi câu đóng trong details; chỉ hoạt động đang xem hoặc yêu cầu rõ mới gọi. Cache dùng lại kết quả đã tạo.
- Server chặn nhận xét/lời giải AI của IndependentCheck/TransferChallenge cho đến khi nộp cả bộ. Owner/tenant/curriculum checks áp dụng cả GET review và POST AI.
- Hai hộp thoại bỏ nền tối ngoài, dùng background trong suốt. Header/nút đóng nằm ngoài vùng cuộn nội dung nên luôn nhìn thấy khi kéo xuống.
- Accept proposal chờ agenda/map tải tuyến mới trước khi đóng, tránh chọn nhiệm vụ từ cache của tuyến vừa thay thế.

## Dữ liệu dùng thử được người dùng yêu cầu

Script `scripts/ops/seed_learning_path_demo.cjs` thêm 20 câu/chủ đề Tiếng Anh (Thì, Mệnh đề quan hệ, Từ vựng theo ngữ cảnh) cho EDUTWIN_A và EDUTWIN_B: tổng **120 câu**, có lời giải tiếng Việt và câu mức khó 4. Mọi câu mang nhãn `[DỮ LIỆU DÙNG THỬ]`; mức khó là cấu hình thử, chưa hiệu chuẩn qua pilot.

Chỉ thêm Questions, không thêm Attempts/điểm/evidence. ID dành riêng, không ghi đè nội dung cũ. Đã kiểm tra thêm lại không trùng trên database tạm. Đã sao lưu trước khi thêm local. Snapshot CanStart=false của tuyến cũ không được giữ khóa sau khi ngân hàng được bổ sung: agenda/start kiểm tra lượng câu mới hiện tại, vẫn giữ prerequisite và gate 5 câu độc lập/80%.

## Bằng chứng

- Backend full: **4341 pass, 0 fail, 103 skip** (`scratch/v4-tutor-full-backend.log`). Sau hiệu chỉnh telemetry, 6 ca adapter/tutor chạy lại pass (`scratch/v4-tutor-adapter-tests.log`).
- Frontend: **757 pass**, lint sạch, build local và Docker đạt. Không tính relational tests skip là pass.
- Seed test: đủ 120 câu/6 phạm vi, chạy lại thêm 0, không tạo Attempt (`scratch/v4-seed-demo-test.log`). Seed local thêm đúng 120 câu (`scratch/v4-demo-seed.json`). Baseline users/attempts/evidence/history trước cập nhật: **17/15/15/15**, đã backup.
- Đã gọi provider **Gemini / gemini-3.5-flash-lite thật** trên cách hiểu sai của tài khoản dùng thử. Nhận xét và lời giải tiếng Việt trả Generated/Misunderstood, nêu đúng việc nhầm who/which và khác chủ đề bài học; GET/replay đọc lại kết quả lưu, không gọi lại (`scratch/v4-tutor-provider-probe.json`, `.log`). Tài khoản thử không có Attempts lúc bật provider; sau probe đã tắt provider trong stack thử trước khi chạy các bộ khác để tránh gọi AI hàng loạt.
- HTTP/MySQL **9 nhóm pass**, có đọc lại không thêm Run, cách ly chủ sở hữu và chặn AI trước khi nộp hết tự kiểm tra (`scratch/v4-tutor-api-tests.log`).
- Browser cuối **PASS** trên center B của database thử: preview tự điền, overlay trong suốt, nút đóng còn thấy sau cuộn, Study trả pane nhận xét AI và đoạn hiểu đã nộp, luyện 7 câu với phản hồi riêng, mở lại lượt đã lưu và từng câu, reload/autosave, mobile không tràn (`scratch/v4-tutor-browser.log`). Đã xem ảnh thực `storage/v4-browser-evidence/practice-feedback-desktop.png` với nền ngoài không tối.
- Lượt browser đầu phát hiện cache tuyến cũ khi accept đã được sửa. Lượt kế tiếp cần thêm điều kiện chờ tải nút review trong harness; dùng tài khoản fixture còn mới ở B để chạy lại toàn luồng, không xóa/sửa điểm nhằm đạt test. Các lượt lỗi không được tính pass.
- Đã cập nhật local bằng `docker compose up -d --no-deps api web`, không recreate MySQL hoặc xóa volume, không cần migration mới. Xác nhận image/HTTP/counts lịch sử tại `scratch/v4-local-verification.json`; môi trường thử được dọn sau kiểm chứng. Không commit/push.
