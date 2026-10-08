# Kiểm thử Chrome: học sinh thứ 4, bài 50 câu — 08/10/2026

## Phạm vi và điều kiện

Người dùng đã đăng nhập tài khoản học sinh thứ 4 và yêu cầu làm/nộp bài
`Test 50 câu` để đo tốc độ sau ba sửa đổi của ngày 07/10. Thao tác bằng Chrome,
không gọi API nộp thay giao diện, không sửa bài/điểm bằng SQL, không duyệt thay
giáo viên, không chấm lại lượt cũ. Không đổi/rebuild/restart code trong khi đo.

- Assignment: `6fdbeede-c20f-46e1-ab78-4721d94e35ff`.
- Student: `d0000000-0000-0000-0001-000000000007`.
- Attempts của lượt này: 159–208, cùng một thời điểm tạo.
- Có đủ 50 đáp án và 50 lập luận được lưu (70–254 ký tự/lập luận).
- Đáp án Tiếng Anh, lập luận nhập bằng tiếng Việt; không đính kèm ảnh nháp.
- Dùng đáp án đúng, sai, lỗi chính tả, biến thể tương đương, ví dụ khác đáp án mẫu,
  và đáp án đúng nhưng lập luận sai; tương tự nhóm tình huống của học sinh thứ 3,
  nhưng không phải bản sao byte-for-byte của bài làm đó.
- Trước khi nộp: không có job Pending/Processing; bộ đếm bảy pool là 86.
- API chạy image `sha256:d56b8bca0c4db45e0b578c05483acd3e84ca2c3e432f3a2059f14ad6a941a39d`.
- Model `gemini-3.5-flash-lite`, 6 worker, batch tối đa 3 câu, cửa sổ 500 ms,
  giới hạn chung 2 request Gemini đã được thực thi bằng SQL.

## Thời gian thực tế

Mốc server đọc từ SQL và ghi theo UTC. Ngày kiểm thử hiển thị cho người dùng là
08/10/2026 theo Asia/Bangkok; 17:18 UTC ngày 07/10 là 00:18 ngày 08/10 tại địa phương.

| Mốc | UTC |
|---|---|
| Bấm xác nhận nộp trên Chrome | 2026-10-07 17:18:25.338 |
| Server lưu 50 bài làm | 2026-10-07 17:18:28.585758 |
| Batch provider cuối cùng trả về | 2026-10-07 17:19:35.296249 |
| Phân tích cuối cùng được lưu | 2026-10-07 17:19:42.342514 |
| Job AI cuối cùng hoàn tất | 2026-10-07 17:19:42.349143 |
| Nhận xét tổng bài được tạo | 2026-10-07 17:19:43.455847 |

- Server nhận bài → đủ 50 job hoàn tất: **73,763385 giây**.
- Server nhận bài → đủ 50 phân tích được lưu: **73,756756 giây**.
- Server nhận bài → có nhận xét tổng bài: **74,870089 giây**.
- Bấm xác nhận → đủ 50 job hoàn tất: **77,011143 giây**.
- Không dùng thời điểm quan sát màn hình làm thời điểm UI đổi trạng thái chính xác.

| Lượt | Thời gian đủ job | Request Gemini | Fallback |
|---|---:|---:|---:|
| Học sinh thứ 3, trước ba sửa đổi | 81,974529 giây | 22 | 0 |
| Học sinh thứ 4, sau ba sửa đổi | 73,763385 giây | 19 | 0 |

Lượt này ngắn hơn **8,211144 giây, khoảng 10,02%**. Chưa có bằng chứng chậm hơn
để quay lại code cũ. Đây là một phép đo thực tế đơn lẻ, không phải controlled A/B,
SLA hoặc kết luận về tải nhiều học sinh. Lượt thứ 3 có một timeout khoảng 30 giây;
lượt này không có timeout và ít sửa phản hồi hơn, nên không quy toàn bộ chênh lệch
tốc độ cho các thay đổi code mới.

## Batch, retry và tính toàn vẹn

- Bộ đếm local 86 → 105, tăng **19 request**, khớp 19 bản ghi batch trong log.
- **16 batch 3 câu, 1 batch 2 câu, 2 singleton sửa phản hồi**.
  17 batch ban đầu xử lý 50 câu; không gửi lại toàn bài.
- 18 batch Succeeded, 1 batch Partial, không Failed/Canceled/Deferred trong log batch.
- Câu 1 và 2 (attempt 208, 207) phản hồi ban đầu vi phạm
  `AI_RESPONSE_SEMANTIC_INVALID / VietnameseExplanation`; mỗi câu sửa riêng một lần,
  rồi thành công. Đây không phải bằng chứng quota/key lỗi.
- 50 job Completed, 50 phân tích không fallback; không còn Pending/Processing.
- 50 bằng chứng và 50 lịch sử Twin, đúng một bản ghi cho mỗi attempt.
- Lịch sử Twin theo history_id tăng tuân thủ thứ tự attempt_id 159–208 khi
  các attempt có cùng created_at; không có bằng chứng trùng hoặc đảo thứ tự.
- Không còn checkpoint; nhận xét tổng bài không stale.
- Trong lần đọc đầu, ledger global có 2 slot active; sau khi hoàn tất là 0.
  Đây là mẫu quan sát tại thời điểm đọc, không phải trace liên tục đo peak.
- 50 cặp nhận xét/lời giải có ký tự tiếng Việt và đã vượt qua validation pipeline.
  Kiểm tra ký tự không phải chứng minh mọi câu văn đều đúng ngôn ngữ hoàn hảo.

## Các tình huống được kiểm tra

Điểm đề xuất hiển thị quy đổi thang 10; chỉ số lập luận dùng thang 100 riêng.

| Câu | Trường hợp | Kết quả |
|---|---|---|
| 1, 2 | Giải thích sustainable/conserve, dùng ví dụ mới | Correct/Valid; đề xuất 10/10; sau retry ngôn ngữ không fallback |
| 2 | Rút sạc và tắt thiết bị chờ thay ví dụ mẫu | Rubric 4+4+2=10/10; nhận xét công nhận hành động hợp lý |
| 8 | Chọn conserve đúng nhưng nói tắt đèn tạo điện | Correct/Invalid; lập luận 40/100; chờ giáo viên xem xét |
| 10 | Rút gọn who live → living, near → in the vicinity of | Correct/Valid; đề xuất 10/10 |
| 15 | Chọn whom đúng nhưng gọi whom là chủ ngữ | Correct/Invalid; lập luận 70/100 |
| 20 | Đảo vai trò nấu ăn và chuông điện thoại | Incorrect/Invalid; rubric đề xuất 0/10 |
| 21 | Have been living since 2020 | Correct/Valid; đề xuất 10/10 |
| 25 | Đáp án writen nhưng lập luận nhớ đúng written | Incorrect/Valid; điểm đáp án 0, lập luận 70/100 |
| 30 | Was studying đúng nhưng nói mọi last night đều tiếp diễn | Correct/Invalid; lập luận 70/100 |
| 39 | Has worked đúng nghĩa nhưng đề yêu cầu tiếp diễn | Incorrect/Invalid; không tự coi là đúng yêu cầu cấu trúc |
| 40, 45, 49 | Ví dụ mới có cùng nghĩa/quy tắc | Correct/Valid; đề xuất 10/10 |

Tổng phân loại: 33 Correct/Valid, 3 Correct/Invalid, 13 Incorrect/Invalid,
1 Incorrect/Valid. Có 12 câu chờ giáo viên: 9 tự luận theo chính sách duyệt điểm,
và 3 câu đáp án đúng nhưng lập luận sai (8, 15, 30). Không có câu nào chưa được AI
phân tích vì quota/fallback.

Chrome hiển thị **AI đã phân tích 50/50**, điểm sơ bộ **5,6/10**, 38/50 đã xác định
điểm và 12 câu chờ duyệt. Không còn banner AI đang xử lý, không còn nút nộp bài;
câu 2 có rubric và lời giải/nhận xét tiếng Việt. Giáo viên chưa chốt điểm cuối bài.

## Dẫn chứng local, không push

Thư mục bị git-ignore: `storage/verification/ai-grading-50-student4-2026-10-08/`.

- `summary.jpg`: tổng quan 50/50 đã phân tích.
- `question-2.jpg`: bài làm, rubric và nhận xét/lời giải câu 2.
- `pending.jpg`: giao diện xác nhận đã lưu bài và đang xử lý.
- `metrics.json`: số đo SQL và log batch đã lọc, không có API key/body SDK.
- `measure.cjs`: công cụ quan sát chỉ đọc, không gọi Gemini hoặc sửa database.

Lượt này không sửa mã production, không rollback, không commit/push.
