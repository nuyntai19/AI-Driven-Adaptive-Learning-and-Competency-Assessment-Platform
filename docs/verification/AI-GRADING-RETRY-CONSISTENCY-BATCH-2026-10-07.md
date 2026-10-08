# Sửa retry, nhất quán lập luận và microbatch AI — 07/10/2026

## Phạm vi

Sửa các vấn đề phát hiện trong lần thử 50 câu có đáp án đúng, sai và gần đúng.
Không chấm lại bài đã lưu, không sửa điểm cũ, không duyệt thay giáo viên.

## Thay đổi

- Giữ cơ chế xoay credential/project còn khả dụng khi lỗi dịch vụ/quota. Pool đang chờ không bị gọi dồn; chờ quota không tiêu hao lượt retry nội dung.
- Phản hồi không hợp lệ được lưu mã lỗi và quy tắc kiểm tra an toàn (JSON, shape, ngôn ngữ, rubric, thang điểm, node scope, nhất quán lập luận...). Không log key, provider body hoặc bài làm.
- Một lượt retry tự động dành riêng câu lỗi, dùng hướng dẫn sửa tương ứng và chạy singleton. Câu đã chấm thành công giữ checkpoint, không gửi lại cả batch. Hint sửa phản hồi được bảo toàn qua lượt chờ quota.
- Thêm `reasoningIssues`: phát biểu của học sinh có lỗi/không thể xác minh và giải thích tiếng Việt. Kết luận Valid/Invalid/Uncertain phải nhất quán với bằng chứng này. Không suy ra đúng lập luận từ đáp án đúng, không trừ điểm vì khác cách giải/câu chữ.
- Đáp án và điểm xác định vẫn độc lập với chất lượng lập luận. Điểm tự luận/rubric của AI chỉ là đề xuất, tổng rubric do server tính; giáo viên vẫn duyệt/chốt.
- Profile `vietnamese-grade-proposal-v4` không dùng lại checkpoint của prompt cũ.
- Feedback API thêm `aiProcessing` gồm trạng thái job, nhóm nguyên nhân an toàn và thời điểm có thể xử lý tiếp. Giao diện phân biệt phản hồi sai cấu trúc/nội dung, timeout, chờ quota/capacity và lỗi đọc ảnh. Chờ giáo viên duyệt sau khi AI đã chấm không hiện lỗi dịch vụ AI.
- Banner chờ tự kết thúc theo trạng thái toàn bài, bao gồm fallback và chờ giáo viên. Giao diện cập nhật một request detail có giới hạn ở chế độ nền, không polling từng câu; ngừng khi đã xong hoặc sau lỗi mạng liên tiếp.
- Batch 3: mặc định cấp 6 job chuẩn bị, tối đa 24 job/trung tâm mỗi đợt, cửa sổ gom 500 ms. Giới hạn request Gemini/quota không tăng. Các ràng buộc phân vùng trung tâm/học sinh/bài tập/ngôn ngữ, ảnh và kích thước input vẫn giữ nguyên.
- Trường hợp bộ đếm retry đã tăng vì ảnh nháp: lỗi AI sau đó vẫn kết thúc bằng fallback đúng trạng thái, không để job mắc kẹt ở Processing.

## Bằng chứng kiểm tra

### Gemini thật, dữ liệu giả lập

Một batch 3 câu, khoảng 8,01 giây:

| Trường hợp | Kết quả |
|---|---|
| Chọn whom đúng nhưng nói whom là chủ ngữ | Correct / Invalid, giữ đề xuất điểm đáp án 10/10 |
| Chọn was studying đúng nhưng nói cứ last night là quá khứ tiếp diễn | Correct / Invalid, giữ đề xuất điểm đáp án 10/10 |
| Định nghĩa conserve energy + ví dụ LED hợp lệ | AI công nhận đúng, nhưng lời giải tiếng Anh đơn thuần bị kiểm tra ngôn ngữ từ chối |

Lượt sửa riêng câu LED, khoảng 4,40 giây: Correct / Valid, đề xuất 10/10,
giữ đáp án tiếng Anh và giải thích bằng tiếng Việt. Không gọi lại hai câu đầu.
Tổng cộng 2 request thật; không gọi dịch vụ cho các bài làm đã lưu.

Phản hồi thô chỉ được giữ trong kết quả **giả lập** của harness opt-in, trong `storage/verification` bị git-ignore. Hệ thống chấm bài thông thường không lưu provider body vào log.

### Pipeline MySQL thực, AI giả lập

Test dùng database tạm do fixture tạo và dọn, không sửa database ứng dụng:

| Test | Request provider | Batch đơn | Bằng chứng / lịch sử Twin | Fallback / trùng dữ liệu |
|---|---:|---:|---:|---:|
| 50 câu | 18 | 1 | 50 / 50 | 0 / 0 |
| 100 câu, có quota wait và phản hồi lỗi cục bộ | 36 | 2 | 100 / 100 | 0 / 0 |

Trình tự cập nhật Twin, ảnh theo đúng câu, điểm đã có, checkpoint và chờ giáo viên đều được kiểm tra.
Thời gian test pipeline giả lập không phải thời gian Gemini chấm 50/100 câu thật.
Benchmark UI trước sửa là 190,12 giây cho 50 câu; chưa chạy lại benchmark UI toàn bài sau sửa.

## Triển khai local

- Không có thay đổi schema database trong lượt sửa này.
- Chỉ cập nhật image/container API và Web, giữ MySQL và các volume.
- Không push tự động; mã nguồn và test nằm trong working tree cùng các thay đổi đã có trước lượt này.

## Kết quả cuối

- Backend: 3.891 passed, 69 test MySQL opt-in skipped trong lượt toàn bộ.
- Hai test pipeline MySQL 50/100 câu được chạy opt-in riêng: 2 passed.
- Frontend: 562 passed; TypeScript/Vite build passed; 3 test bundle budget passed.
- `git diff --check` không có lỗi khoảng trắng.
- API và Web đã build/recreate, container dùng đúng image mới. HTTP Web, bundle và API readiness đều trả 200; MySQL Healthy.
- Runtime xác nhận 6 job chuẩn bị, batch window 500 ms, giới hạn Gemini 2 request đồng thời vẫn giữ nguyên.
- Fingerprint 104 bài làm và 104 phân tích trước/sau giống nhau. Container MySQL và volume MySQL không bị recreate/thay đổi.
- Kết quả AI lịch sử không được viết lại. Để nghiệm thu thời gian thực tế sau sửa, cần một lượt nộp 50 câu mới; chưa có số đo UI mới thay thế mốc 190,12 giây trước sửa.
