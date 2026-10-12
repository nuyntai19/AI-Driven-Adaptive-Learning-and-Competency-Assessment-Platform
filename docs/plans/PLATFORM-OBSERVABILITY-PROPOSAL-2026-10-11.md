# Đề xuất giám sát vận hành cho Platform Admin

Ngày rà soát: 11/10/2026. Trạng thái: **IMPLEMENTED / VERIFIED-v1** theo người dùng “oke làm đi”; chỉ trong phạm vi chính thức ở [Implementation SPEC](PLATFORM-OPERATIONS-IMPLEMENTATION-SPEC-2026-10-11.md), với [báo cáo kiểm chứng](../verification/PLATFORM-OPERATIONS-REPORT-2026-10-11.md). Các mục rộng hơn dưới đây vẫn là roadmap, không tự coi đã hoàn thành.

Tài liệu này ghi đề xuất ban đầu cho milestone vận hành từng deferred trong `POST-R09-PLATFORM-OPS.md`. Checkpoint UI Admin/box thông báo trước đó không thêm API/bảng/quyền giám sát; lượt triển khai được duyệt sau đó đã bổ sung ba trang và hạ tầng metadata theo SPEC riêng. Không bao gồm mọi hạng mục của roadmap này.

## 1. Kết luận

Ba chức năng token/chi phí AI, hàng đợi AI và dung lượng lưu trữ đều khả thi. Với 1.000+ học sinh, chúng càng cần thiết, nhưng số học sinh không đủ để kết luận hệ thống đáp ứng tải. Cần đo lượt nộp trong giờ cao điểm, số câu/ảnh, số lần gọi AI trên mỗi job, quota của project, độ trễ provider và năng lực worker/database.

Ưu tiên đề nghị: **sức khỏe + backlog chỉ xem → token/chi phí → lưu trữ**. Không mở rộng Admin sang xem bài làm, ảnh nháp, đề/lời giải, nhận xét AI hoặc điểm riêng.

## 2. Hạ tầng đã có trong code

| Phần | Đã có | Chưa đủ cho dashboard quản trị |
| --- | --- | --- |
| AI backlog | `AIAnalysisJob`: status, retry count, available/started/completed/created time, lease, error code | API aggregate riêng, bộ lọc theo khoảng thời gian, đếm tuổi job/lease hết hạn, lịch sử throughput |
| Thời gian xử lý | `AIProcessingMetrics`: stage duration, queue wait, outcomes, checkpoint hits, batch size | Collector/exporter và lưu series bền vững; phép tìm trong `src` chưa thấy wiring OpenTelemetry/Prometheus/OTLP |
| Token | Gemini adapter đã đọc prompt/output/thinking/cached token vào counter; result trả prompt/output/total; có metadata log | Counter/log không phải sổ chi phí bền vững. Thiếu ledger mỗi provider invocation, model/pool/stage, giá áp dụng và aggregate theo giờ/ngày |
| Quota | `AIProviderQuotaState` chia sẻ trạng thái pool giữa tenant | Không phải số tiền đã thanh toán; cần phân biệt configured limit, quota xác minh từ provider và telemetry thực tế |
| Ảnh nháp | `AttemptAttachment.FileSizeBytes` và storage key | Tổng dung lượng theo phạm vi, file vật lý/orphan/tạm và đối soát |
| Ảnh câu hỏi | `QuestionImage.Data` là byte array trong DB, không có trường size riêng | Aggregate DB dùng độ dài byte của blob, không tải blob vào API Admin; cân nhắc metadata size khi cần |

Nguồn code: `src/EduTwin.DAL/AssessmentAndReasoning/AIAnalysisJob.cs`, `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIProcessingMetrics.cs`, `src/EduTwin.API/AssessmentAndReasoning/AI/GoogleGenAIGenerateContentClient.cs`, `src/EduTwin.DAL/AssessmentAndReasoning/AttemptAttachment.cs`, `src/EduTwin.DAL/CurriculumAndQuestions/QuestionImage.cs`.

## 3. Màn hình và giới hạn đề nghị

### A. Sức khỏe hệ thống và hàng đợi AI — làm trước

- Trạng thái API, DB, worker heartbeat, collector và provider; lần cập nhật gần nhất. Thiếu số liệu hiện “chưa có dữ liệu”, không hiện 0 hay “khỏe”.
- Số job Pending/Processing/Completed/FallbackCompleted/FailedTerminal, tỷ lệ hoàn thành/fallback/lỗi, throughput theo giờ/ngày.
- Job chờ lâu nhất, job chưa đến AvailableAt, Processing có lease hết hạn, nhóm mã lỗi đã làm sạch.
- Tách **queue wait**, **provider call latency** và **end-to-end completion**. Hiển thị p50/p95 cùng trung bình; ghi rõ mẫu/khoảng thời gian. Timestamp hiện tại của job có thể phản ánh lần retry mới nhất, không dựng lịch sử từng lần từ chúng.
- Trang đầu chỉ aggregate. Danh sách chẩn đoán phân trang, chỉ ID kỹ thuật/trạng thái/thời gian/mã lỗi an toàn; không trả `LastErrorMessage` thô hay liên kết học thuật.
- Không có nút chấm lại/bỏ qua Evidence Gate/duyệt điểm. Retry job là scope nghiệp vụ khác, cần thiết kế quyền, chống trùng và audit riêng; không tự cấp cho Platform Admin.

### B. Token và chi phí AI

- Đếm theo **provider invocation**, không theo bài làm hay job. Đọc ảnh độc lập + chấm có thể là hai calls; retry có thể phát sinh thêm calls; microbatch có thể gom nhiều job vào một call.
- Lưu metadata bền vững: call ID duy nhất, provider/project pool alias không chứa secret, requested/returned model, stage, thời gian, latency, outcome, input/output/thinking/cached/total nếu provider trả, chất lượng số liệu và phiên bản giá. Không lưu prompt, answer, image hoặc API key.
- Một counter Gemini hiện chỉ gắn `kind`; cần chuẩn hóa provider/model/stage trước khi so sánh đa provider. Chỉ thêm nhãn có tập giá trị hữu hạn, không dùng student/user/attempt ID làm metric labels.
- `totalTokenCount` là số kiểm tra/tổng của response, **không cộng lại với input/output**. Cached và thinking có quy tắc giá riêng; không tính twice phần cached đã nằm trong prompt. Metadata không có phải null/unknown, không mặc định 0.
- Bảng giá có provider/model/service tier/currency/effective time. Giá ước tính theo giá tại thời điểm gọi; không thay giá mới vào lịch sử cũ. Phân biệt Free, Paid đã xác minh và BillingUnknown.
- Ghi rõ “chi phí ước tính, không phải hóa đơn”; phải đối soát billing provider mới gọi là phí thực thu. Không tự bật billing, tăng quota, đổi model hay thêm key.
- Bộ lọc 24 giờ/7 ngày/30 ngày/custom có giới hạn; biểu đồ input/output/thinking, tổng calls, chi phí theo thời gian và cảnh báo ngân sách. Raw ledger giữ thời hạn ngắn đã duyệt, hourly/daily rollup giữ dài hơn.

[Gemini GenerateContent API](https://ai.google.dev/api/generate-content) cung cấp `usageMetadata`, bao gồm các trường token nói trên. [Bảng giá chính thức](https://ai.google.dev/gemini-api/docs/pricing) phải là nguồn cập nhật giá, không suy tiền từ số học sinh.

### C. Dung lượng lưu trữ

- Tổng attachment bytes/file count theo trung tâm và thời gian, tăng trưởng/ngày, ước tính thời điểm chạm ngưỡng; ảnh câu hỏi trong DB báo riêng với ảnh nháp ở filesystem.
- Phân biệt **logical usage của dữ liệu nghiệp vụ** với **physical usage** (DB/index, temporary/orphan files, volume free space, backup). Không lấy dung lượng Docker VHD làm tổng dung lượng ảnh của người dùng.
- Logical attachment SUM từ metadata; ảnh câu hỏi tính độ dài byte tại DB hoặc duy trì size metadata. Quét/đối soát file vật lý chạy nền, có ngân sách I/O và checkpoint; không đọc toàn bộ file/ảnh cho mỗi lượt mở dashboard.
- Cảnh báo dung lượng, trạng thái backup/lần thử phục hồi là phần vận hành hữu ích bổ sung. Chính sách retention cần được duyệt và giữ chứng cứ học tập; không tự xóa ảnh/bài làm hay backup từ màn hình thống kê.

## 4. Thiết kế cho 1.000+ học sinh

Ví dụ **giả định tải**, không phải benchmark: 1.000 học sinh × 20 câu nộp/ngày = 20.000 lượt câu hỏi/ngày. Không được kết luận bằng 20.000 requests AI: batching, checkpoint, đọc ảnh và retries làm số calls khác đi. Tải dồn trong 15 phút khác hoàn toàn tải trải đều một ngày.

- Tạo kịch bản đo riêng: 1.000 tài khoản, đợt nộp đồng thời, phần trăm ảnh, token distribution, worker throughput và quota đã xác minh. Load test dùng provider giả cho hạ tầng; test provider thật giới hạn số calls/ngân sách, cần duyệt riêng.
- Thu thập không đồng bộ/bounded buffer; lỗi telemetry không chặn submit/chấm. Nếu ghi thiếu, có counter dropped/incomplete và trạng thái số liệu không đầy đủ; không trình bày thành báo cáo chi phí chính xác.
- Dashboard đọc summary/rollup đã index, cache 30–60 giây, request có date bound/page limit/cancellation, polling dừng khi tab ẩn. Không scan tất cả jobs/blobs mỗi giây cho mỗi Admin.
- Quota Gemini áp dụng **theo project, không theo API key**; RPM/TPM/RPD là các giới hạn riêng. Thêm key cùng project không tạo thêm capacity. Cần queue/fairness/backpressure và ngân sách retry, không tăng concurrency mù quáng. [Nguồn quota](https://ai.google.dev/gemini-api/docs/rate-limits).
- Ước tính thời gian thoát backlog phải xét số calls còn lại, RPM/TPM/RPD, latency, concurrency và retry; khi chưa đủ thông tin hiện unknown, không dựng ETA.
- Tránh cardinality explosion: metric labels chỉ provider/model/stage/outcome hữu hạn; per-center rollup được truy vấn bằng DB projection có quyền riêng, không biến mọi user thành chuỗi metric. [OpenTelemetry guidelines](https://opentelemetry.io/docs/specs/otel/metrics/supplementary-guidelines/).
- Acceptance targets về p95 API/queue delay, cache TTL, retention, ngân sách và alert threshold được chốt sau baseline đo; hiện **chưa cam kết chịu 1.000 người dùng đồng thời**.

## 5. Có cần SQL mới không?

- UI readability/chuông/sidebar trong bản sửa này: **không**.
- Aggregate backlog hiện tại: có thể dùng bảng/index đang có, rà query plan trước; lịch sử throughput/heartbeat cần nơi lưu phù hợp.
- Token/chi phí lịch sử đáng tin cậy: cần migration cho metadata ledger, versioned prices và hourly/daily rollup, hoặc kho telemetry tương đương đã vận hành; không dựa riêng vào log/counter trong RAM.
- Storage logical: dùng metadata hiện có trước. Size ảnh câu hỏi, snapshot/daily usage và kết quả đối soát có thể cần migration sau khi chọn phương án; phải backfill bảo toàn dữ liệu, không fetch blobs qua Admin.
- Permission API aggregate mới phải được định nghĩa trước cùng tenant boundary, negative tests và field allowlist. Không dùng việc UI là PlatformAdmin để bỏ mọi query filter rồi trả dữ liệu thô.

## 6. Gate trước khi triển khai

1. Người dùng duyệt milestone A/B/C, ưu tiên và chính sách metadata/privacy.
2. Chốt model/pool/service tier đang sử dụng và giá/billing/quota đã xác minh; không lộ key.
3. Thiết kế API/DB/retention và test quyền/cross-tenant, empty/stale/error states, exporter offline, retry/cost dedup.
4. Đo baseline và kịch bản tải có giới hạn ngân sách; báo cáo kết quả thật, không suy từ số test pass.
5. Chỉ sau đó mới mở dashboard/cảnh báo. Không thay đổi quyền học thuật, điểm, bài làm hay retention bằng milestone quan sát.
