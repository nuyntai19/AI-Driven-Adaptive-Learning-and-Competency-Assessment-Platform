# Kiểm thử giao diện: bài Tiếng Anh 50 câu, đáp án đúng/sai/gần đúng

## Phạm vi và cách đo

Ngày 07/10/2026, làm và nộp bài qua Chrome trên tài khoản học sinh khác do người dùng đăng nhập. Bài gồm 26 câu trắc nghiệm, 15 câu trả lời ngắn, 9 câu tự luận. Cả 50 câu đều có lập luận chứa tiếng Việt; đáp án Tiếng Anh giữ nguyên ngôn ngữ môn học. Đã kiểm tra dữ liệu nhập trên giao diện trước khi nộp. Không sửa trực tiếp bài làm trong database, không duyệt/chốt điểm giáo viên, không gọi chấm lại sau kết quả fallback.

Thời gian đo bằng mốc backend từ lúc nhận lượt nộp đến lúc lưu phân tích cuối cùng, bao gồm hàng đợi, gọi model, retry/fallback và cập nhật bằng chứng/Twin. Không tính thời gian nhập bài, và không dùng thời điểm mở lại giao diện để giả định độ trễ cập nhật UI. Đây là một lần chạy, không phải số liệu p95 hay cam kết tốc độ.

## Kết quả thời gian

| Mốc | Kết quả |
| --- | --- |
| Nhận bài, giờ UTC+7 | 18:57:34.306 |
| Phân tích đầu tiên | Sau 13.074 giây |
| Lưu phân tích cuối cùng | 19:00:44.422, sau **190.116 giây (3 phút 10 giây)** |
| Tất cả job terminal | Sau 190.128 giây |
| Nhận xét tổng thể sẵn sàng | 19:00:46.546, sau khoảng **192.240 giây** |
| Phân tích thành công từ Gemini | **49/50** |
| Fallback | **1/50**, câu 2 |
| Điểm AI đề xuất | 49 câu; không tự thay thế điểm chính thức |
| Phân tích bị nhân đôi | 0 |

Model chạy thực tế: `gemini-3.5-flash-lite`. Có 32 batch được ghi nhận trong log, tổng 51 lượt xử lý item kể cả lần xử lý lại: 20 batch một câu, 5 batch hai câu, 7 batch ba câu. Thời lượng batch khoảng 8.06–18.03 giây, trung bình 11.86 giây. Cấu hình gom tối đa 3 câu, cửa sổ 150 ms và 4 job slots. Dữ liệu cho thấy batch chưa được lấp đầy tốt; không thể suy ra nguyên nhân chỉ là số key hay quota. Phân tích có checkpoint có thể chờ predecessor trước khi cập nhật bằng chứng/Twin theo đúng thứ tự, nên số kết quả đã commit có thể thấp hơn số câu model đã xử lý.

## Các tình huống tiêu biểu

| Câu | Kịch bản cố ý | Kết quả quan sát |
| --- | --- | --- |
| 1 | Hiểu đúng sustainable nhưng chỉ nêu một trong hai dẫn chứng | Rubric **7/10**: nghĩa 4/4, solar power 0/3, dùng chung dụng cụ 3/3; chờ giáo viên duyệt |
| 2 | Đúng nghĩa conserve và dùng ví dụ LED khác mẫu | Fallback, không có điểm AI đề xuất; có nút chấm lại AI |
| 4 | Đáp án healthy sai, lập luận giải thích đúng rằng cần health | Đáp án 0/10; AI tách được đáp án Incorrect và lập luận Valid |
| 7 | Chọn collected thay vì distributed và hiểu sai nghĩa | Incorrect, Invalid, chất lượng lập luận 20/100 |
| 8 | Đáp án conserve đúng nhưng lập luận sai rằng tắt đèn tạo điện | Giữ điểm đáp án 10/10; lập luận Invalid, 50/100; chuyển giáo viên xem xét mâu thuẫn |
| 10 | Rút gọn mệnh đề quan hệ đúng, diễn đạt khác lời giải mẫu | AI đề xuất 10/10, lập luận Valid, 100/100; chờ giáo viên duyệt tự luận |
| 11 | Ghép câu đúng ý nhưng thiếu dấu phẩy mệnh đề không xác định | AI đề xuất **8/10**, không quy thành 0 điểm toàn bộ |
| 13, 37 | Đáp án Whose/BOUGHT khác kiểu chữ | Được công nhận đúng; điểm quy đổi 10/10 |
| 15 | Đáp án whom đúng, nhưng khẳng định sai rằng whom luôn là chủ ngữ | AI nhận xét sửa lỗi tân ngữ/chủ ngữ, nhưng vẫn ghi **Valid, 90/100**; đây là bất nhất cần xử lý |
| 25, 50 | Gần đúng nhưng sai chính tả writen/incomprehensable | Đáp án theo bộ chấm 0/10; AI chỉ rõ vấn đề và vẫn có phân tích lập luận |
| 30 | Chọn thì đúng nhưng giải thích sai rằng cứ last night là quá khứ tiếp diễn | AI vẫn ghi **Valid, 85/100**; chưa phản ánh rõ lỗi khái quát hóa |
| 39 | Đúng thì hiện tại hoàn thành tiếp diễn nhưng dùng since three years | Incorrect, Invalid; chỉ số lập luận 70/100 |
| 40, 45, 49 | Trình bày đúng bằng lời giải/ví dụ khác mẫu | Được AI đề xuất đủ điểm; native 50/50 được hiển thị nhất quán thành 10/10 |
| 44 | Đáp án/lập luận đúng, làm rất nhanh trong bài test tự động | Chờ giáo viên do `ANOMALY_UNREALISTIC_COMPLETION_TIME`; không phải AI coi đáp án sai |

49/49 nhận xét và lời giải của các phân tích Gemini có nội dung tiếng Việt; cả 50 bài làm đã lưu đều có lập luận. Kiểm tra có dấu tiếng Việt không thay thế đánh giá đầy đủ chất lượng ngôn ngữ.

Giao diện tổng kết: **điểm sơ bộ 5.4/10**, 27 câu được bộ chấm công nhận đúng, 49 câu có phân tích Gemini, 11 câu chờ duyệt. Điểm sơ bộ chưa cộng các điểm tự luận chưa được giáo viên chốt. 11 câu chờ gồm 9 câu tự luận (có câu fallback), câu 8 có mâu thuẫn lập luận và câu 44 có cảnh báo thời gian thực hiện quá ngắn.

## Vấn đề cần xử lý trước khi nghiệm thu

1. **Fallback do hợp đồng phản hồi AI:** câu 2 gặp `AI_RESPONSE_SEMANTIC_INVALID` hai lần trong log và kết thúc bằng fallback. Chưa có đủ thông tin an toàn trong log để biết chính xác trường phản hồi nào sai. Không có bằng chứng trong log đã kiểm tra để quy lỗi này thành hết quota hay mất mạng. Cần lưu mã lý do validation chi tiết nhưng không ghi key/payload nhạy cảm, rồi xử lý retry/repair có giới hạn ở đúng item lỗi.
2. **Bất nhất giữa nhận xét và kết luận lập luận:** câu 15 và 30 chưa được phân loại phù hợp dù câu 15 được nhận xét rõ là sai vai trò ngữ pháp. Phải giữ nguyên điểm đáp án đúng và sửa kết luận/chỉ số bằng chứng về lập luận, tránh cập nhật hồ sơ năng lực từ nhãn Valid mâu thuẫn. Đây là lỗi chất lượng model/contract, không phải lý do trừ điểm đáp án đúng.
3. **Thông báo chờ AI không được dọn:** sau khi 50 job đã terminal, banner vẫn nói AI đang mất nhiều thời gian và sẽ hiển thị kết quả sau. Cần phân biệt đang xử lý, đã có 49 kết quả + 1 fallback và đang chờ giáo viên; không để thông báo cũ gây hiểu lầm.
4. **Tốc độ chưa đạt mục tiêu:** đánh giá lại scheduler/coalescing để tăng tỷ lệ batch đầy trong giới hạn quota thực tế. Giữ cơ chế checkpoint và áp dụng evidence theo thứ tự; không bỏ bảo vệ Twin chỉ để có số đo nhanh hơn. Các thay đổi này cần được kiểm thử riêng, không tự điều chỉnh cấu hình trong lần benchmark.

## Bằng chứng local

Ảnh và bản kết quả 50 câu nằm trong thư mục local ignored `storage/verification/ai-grading-50-mixed-2026-10-07/`, không có key và không được đưa lên Git:

- `summary.jpg`: tổng kết và banner chờ AI bị giữ lại.
- `question-01-partial-rubric.jpg`: điểm từng tiêu chí và đề xuất 7/10.
- `question-15-reasoning-contradiction.jpg`: nhận xét sửa lỗi ngữ pháp nhưng kết luận lập luận vẫn Hợp lệ.
- `results.json`: kết quả chi tiết từng câu, thời gian và metadata batch; không gồm danh tính tài khoản học sinh.

Chưa triển khai sửa các vấn đề mới trong lần kiểm thử này; chưa push, chưa giáo viên chốt điểm và chưa chuyển sang import PDF.
