# Kiểm thử Test 4: hai câu hình học có ảnh

Ngày kiểm thử: 09/10/2026, múi giờ UTC+7. Thực hiện trực tiếp trên Chrome bằng skill computer-use, trong phiên học sinh student01 (Duy Bảo Trịnh), EDUTWIN_A, Lớp Toán 10.

## Phạm vi

- Bài tập `bf0683d5-79ba-424a-932f-687fe5047722` — Test 4, hai câu `20066` và `20067`.
- Làm và nộp cả hai câu đúng, có lập luận tiếng Việt.
- Câu 1: dùng công cụ Tam giác trong Vẽ nháp, đính kèm ảnh qua giao diện, xác minh ảnh còn xem được sau khi nộp.
- Không duyệt điểm thay giáo viên; không thay đổi cấu hình AI, mã nguồn ứng dụng, giáo trình, lớp hoặc quyền. Không commit/push.

## Kết quả

| Câu | Bài làm | Kết quả AI | Điểm chính thức/trạng thái |
| --- | --- | --- | --- |
| 1, `20066`, attempt `209` | Định lí cosin: BC² = 6² + 8² − 2×6×8×cos60° = 52; BC = 2√13 cm ≈ 7,21 cm | Correct, Valid; chất lượng lập luận 100/100; điểm đề xuất 10/10, rubric 4/4 + 4/4 + 2/2 | NeedsTeacherReview; điểm chính thức chưa chốt |
| 2, `20067`, attempt `210` | Nhập số 10; định lí sin: BC/sinA = 2R, R = 10/(2sin30°) = 10 cm | Correct, Valid; chất lượng lập luận 100/100; điểm đề xuất 10/10 | Bộ chấm theo quy tắc: đúng, 10/10; Completed |

Nhận xét và lời giải AI của cả hai câu bằng tiếng Việt. Cả hai job AI Completed, retry_count = 0, is_fallback = 0, không có last_error_code.

Tổng quan học sinh hiển thị AI đã phân tích 2/2 câu, 1 câu chờ giáo viên duyệt. Điểm sơ bộ 5/10 và số câu đúng 1/2 chỉ phản ánh câu đã xác định điểm chính thức; chưa cộng câu tự luận đang chờ giáo viên. Đây không phải AI kết luận câu 1 sai.

## Thời gian

- Bắt đầu thao tác xác nhận nộp: `2026-10-09T12:23:33.787Z`.
- Server lưu hai attempts: `2026-10-09 12:23:34.840626 UTC`.
- Hai job bắt đầu: khoảng `12:23:37.7325 UTC`.
- Câu 1 hoàn tất: `12:23:45.644621 UTC`.
- Câu 2 hoàn tất: `12:23:46.854850 UTC`.
- Từ server lưu bài đến cả hai job hoàn tất: **12,014224 giây**.
- Từ bắt đầu thao tác xác nhận đến server hoàn tất: khoảng **13,068 giây** (gồm thao tác UI và gửi/lưu bài).
- Lần quan sát UI đầu tiên thấy đã phân tích 2/2: khoảng **20,704 giây** sau bắt đầu xác nhận; không coi đây là thời gian tính toán thuần của AI.
- Log trong cửa sổ kiểm thử ghi **một batch thành công chứa hai câu**, Gemini `gemini-3.5-flash-lite`, thời gian batch **7026,2084 ms**. Log HTTP không đủ để đếm độc lập số request vật lý hoặc lượt điều phối key; không suy diễn số request từ việc không có dòng HTTP.

Không so sánh tuyến tính với bài 50 câu: số lượng, kiểu câu và ảnh đầu vào khác nhau.

## Điểm cần lưu ý

1. Có lỗi trình bày nhỏ: giải thích theo từng tiêu chí rubric đang hiện nguyên `$60^{\\circ}$`, `$BC^2 = 52$` thay vì dựng công thức toán. Không ảnh hưởng tổng điểm trong lượt này. Chưa sửa vì lượt này chỉ kiểm thử.
2. Hai câu có đề văn bản kèm ảnh minh họa. Lượt này xác nhận luồng đính kèm ảnh đề, lưu/xem bản nháp và trả kết quả AI; **chưa chứng minh trường hợp ảnh chứa toàn bộ đề mà không có văn bản**.
3. Một lượt đúng không thay thế kiểm thử đáp án sai/gần đúng, ảnh mờ hoặc cách giải khác. Không có lỗi fallback hay badge sai ở kết quả cuối cùng của lượt này.

## Bằng chứng

- `storage/verification/test4-geometry-essay-result.jpg`: AI đề xuất 10/10, rubric 4–4–2, chờ giáo viên duyệt.
- Kết quả được đối chiếu giữa UI, attempts/reasoning_analyses/ai_analysis_jobs và log batch theo đúng khoảng thời gian; không in khóa API hoặc thông tin xác thực.
