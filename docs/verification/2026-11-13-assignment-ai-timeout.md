# Chẩn đoán chấm bài và thời lượng — 13/11/2026

Đối chiếu log container `edutwin-api`, cấu hình runtime đã che bí mật và truy vấn MySQL chỉ đọc. Không thay đổi bài nộp, điểm hoặc API key. Các giờ dưới đây dùng UTC+7.

## Bài được kiểm tra

- Bài `cac`, assignment `d8ee5d0c-4060-4eb4-8ab0-32b66b0d8abc`.
- Attempts 41, 42, 43 cùng nộp lúc 01:01:15 ngày 13/11/2026.
- Thời gian từng câu: 42, 317, 10 giây; tổng 369 giây (00:06:09). Đây là thời gian hoạt động do ứng dụng ghi nhận, không phải đồng hồ thi có tính thẩm quyền.

## AI: timeout và upstream 503

| Giờ | Bằng chứng |
| --- | --- |
| 01:01:17 | Chuẩn bị một batch 3 câu, model `gemini-3.5-flash-lite`, không có ảnh. |
| 01:01:47 | Batch thất bại sau 30086 ms; cả ba attempt nhận `AI_PROVIDER_TIMEOUT`. |
| 01:01:50 | Bắt đầu batch tiếp theo. |
| 01:02:09 | Gemini trả HTTP 503 / `ServerError`, credential index 2; batch được hoãn sau 18756 ms. |
| 01:02:12 | Gọi lại batch. |
| 01:02:42 | Timeout sau 30027 ms. |
| 01:02:42–46 | Ba job chuyển `FallbackCompleted`, retry_count 1, last_error_code `AI_PROVIDER_TIMEOUT`; attempts chuyển `NeedsTeacherReview`. |

Tổng từ lúc nộp đến fallback cuối khoảng 91 giây. Giao diện đang hiển thị đúng trạng thái fallback; không có kết quả phân tích AI thành công cho lượt này. Đáp án sơ bộ được chấm theo quy tắc vẫn được lưu.

Cấu hình runtime: đủ 7 key, global concurrency 2, worker concurrency 4, microbatch 3, cửa sổ gom 150 ms. `GEMINI_QUOTA_POOLS` rỗng nên các key được xếp chung vào `unverified-shared-project`. Chưa xác minh các key có thuộc những Google Cloud project độc lập hay không. Không thấy HTTP 429 trong chuỗi lỗi này; không có bằng chứng để kết luận hết quota hoặc key sai.

`GeminiOptions.Timeout` mặc định 30 giây. Deadline bao trùm toàn bộ một thao tác, gồm xoay credential; không phải 30 giây nhân với 7 key. `ReasoningBatchExecutor` cũng giới hạn cả inspection + grading trong cùng deadline. Timeout lần đầu tiêu thụ một retry; 503 được hoãn theo cơ chế capacity/cooldown; timeout tiếp theo tạo fallback.

Một yêu cầu REST tối thiểu, không có dữ liệu học sinh, bằng key đầu tiên và cùng model đã trả HTTP 200 trong 1,98 giây khi chẩn đoán. Điều này chỉ xác nhận key/model có thể hoạt động tại thời điểm kiểm tra; không xác nhận tác vụ chấm đầy đủ sẽ kịp deadline, cũng không tái hiện tải vào thời điểm xảy ra lỗi.

Tài liệu Google: [quota tính theo project](https://ai.google.dev/gemini-api/docs/rate-limits), [xử lý 503 và retry có backoff](https://ai.google.dev/gemini-api/docs/troubleshooting).

## Kiểm tra sâu thêm về fallback và tác vụ chấm

Ba lời gọi kiểm tra có giới hạn, dùng dữ liệu giả và đúng `ReasoningBatchExecutor`, `GeminiPromptBuilder`, JSON schema, Google SDK của ứng dụng; không ghi database. Deadline thử nghiệm 60 giây, không đổi deadline production.

| Credential index (zero-based) | Số câu | Kết quả |
| --- | --- | --- |
| 1 | 1 | Thành công, parser chấp nhận; 7962 ms; input 2968 token, output 299 token. |
| 1 | 3 | Sau 20867 ms trả `AI_PROVIDER_CAPACITY_WAIT`; phép thử đầu chưa thu HTTP status nên không quy kết chính xác mã HTTP. |
| 2 | 3 | HTTP 503 sau 3193 ms; `AI_PROVIDER_CAPACITY_WAIT`. |

Prompt dữ liệu giả một câu dài 14825 ký tự, schema 4908 ký tự. Batch ba câu dài 17511 ký tự, schema 7845 ký tự. Đây là đo trên dữ liệu giả, không phải kích thước chính xác request học sinh trong sự cố. Tác vụ chấm gửi nhiều hướng dẫn và yêu cầu đầu ra cấu trúc, không tương đương phép thử trả lời `OK`.

Đã tái hiện lỗi upstream 503 với tác vụ chấm đầy đủ trước deadline 60 giây. Có bằng chứng dịch vụ Gemini không sẵn sàng tại thời điểm đó; không có quyền truy cập telemetry nội bộ Google để kết luận chính xác vì sao hai request cũ chưa phản hồi trong 30 giây. Cũng chưa đủ phép đo để kết luận batching là nguyên nhân duy nhất hoặc mọi key đều hỏng.

Phân biệt ba cơ chế trong code:

1. **Đổi credential:** `GoogleGenAIGenerateContentClient.GenerateCoreAsync` có vòng xoay key. Tuy nhiên, khi nhận cancellation do deadline, nó ném lỗi ra ngoài ngay. Deadline là ngân sách chung, không tạo thêm 30 giây cho từng key.
2. **Đổi quota pool khi lỗi tạm thời:** 503 đánh dấu pool hiện tại không sẵn sàng cho lượt gọi đó. Các key khác trong cùng pool bị bỏ qua. Vì runtime chưa khai báo `GEMINI_QUOTA_POOLS`, cả 7 key thuộc một pool mặc định. Việc đổi model/provider không tự diễn ra.
3. **Fallback cuối:** `RuleBasedFallbackBuilder` tạo nhận xét theo chấm sơ bộ, đặt `FeedbackOrigin=RuleBased`, `IsFallback=true`, `NeedsTeacherReview=true`, `ReasoningQuality=null`. Nó không gọi một AI dự phòng. Fallback này đã hoạt động đúng trong sự cố, nhưng không thay thế được phần phân tích lập luận từ AI.

Lần kiểm tra sâu này chỉ bổ sung công cụ đo tạm trong `scratch/` và bằng chứng vào báo cáo; chưa thay đổi logic gọi Gemini, retry hoặc policy quota ở production.

## Thời lượng 746:19:40

Database lưu `started_at = 2026-10-12 15:41:34.884708 UTC`, `completed_at = 2026-11-12 18:01:15.435759 UTC`. Hiệu hai mốc là 746 giờ 19 phút 40 giây. Bài không giới hạn thời gian (`time_limit_minutes = NULL`). Vì mốc bắt đầu được giữ khi mở lại bài, hiệu hai mốc bao gồm cả thời gian rời bài trong một tháng; không phù hợp với nhãn thời gian làm bài.

Đã sửa projection backend cho bài không giới hạn giờ: cộng thời gian đã lưu của latest attempt thuộc từng câu trong bài. Frontend dùng cùng cách khi API cũ thiếu `elapsedSeconds`. Bài thi có giới hạn giờ tiếp tục dùng đồng hồ server. Không sửa mốc bắt đầu/kết thúc trong database.

Đã bổ sung chuyển tiếp `Gemini__Timeout` qua Docker Compose và hướng dẫn ở `.env.example`; mặc định vẫn 30 giây. Tăng deadline là lựa chọn chịu chờ lâu hơn, không khắc phục upstream 503. Không đổi model, quota pool, batch size hoặc `.env` thực tế khi chưa có phép đo so sánh tác vụ chấm.

## Kiểm tra và phạm vi

- Backend: 81/81 test qua (assignment timing, Gemini options/admission/DI, microbatch).
- Frontend: 15/15 test timing qua.
- TypeScript: `tsc -b` qua.
- Các thay đổi ở mã nguồn; chưa rebuild/recreate container đang chạy.
- Lỗi upstream và độ trễ chấm AI chưa được khắc phục triệt để bởi bản sửa thời lượng.
