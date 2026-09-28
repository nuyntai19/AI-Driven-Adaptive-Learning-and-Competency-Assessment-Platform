# ADR-POST-R09-AI-QUEUE-AND-NOTIFICATIONS: Architecture Evaluation, Capacity Simulation Models, Queue Migration Options (MySQL Outbox + Broker), and Real-Time Push vs. Bounded Polling

**Status:** PROPOSED (Pending Review & Live Staging Load Test Sign-Off before Implementation)<br />
**Date:** 2026-09-28<br />
**Scope:** AI Analysis Background Processing & Real-Time Client Notification Pipeline<br />
**Author:** EduTwin Architecture Team & Antigravity Assistant<br />
**Baseline Git Freeze SHA:** `4298e4f` (kế thừa `5b5682d`, `9ea4c22`, `3cda5ef`)<br />
**Branch:** `student/answer`

---

## 1. Bối cảnh và Đặt vấn đề (Context & Problem Statement)

Trong hệ sinh thái EduTwin, khi học sinh hoàn thành bài làm và nộp bài (`POST /api/v1/learning/attempts/{attemptId}/submit`), hệ thống kích hoạt chuỗi đánh giá năng lực và phân tích tư duy học thuật bằng AI (**AI Analysis Job**).

Tài liệu rà soát kiến trúc gần đây đã nêu ra hai cảnh báo hạ tầng:
1. **Database-as-a-Queue (Anti-pattern cảnh báo):** Bảng `ai_analysis_jobs` trong MySQL đang được sử dụng như một persistent message queue. Worker quét định kỳ mỗi 3 giây; đồng thời frontend polling liên tục trạng thái phân tích.
2. **Nghi ngờ nghẽn hệ thống:** Cho rằng MySQL chắc chắn sẽ bị nghẽn nghiêm trọng (bottleneck / lock waits) khi có 500–1.000 học sinh cùng nộp bài đồng thời.

### 1.1. Hiện trạng Mã nguồn Thực tế (Measured In-Code State)

> [!IMPORTANT]
> **Trạng thái Code Production hiện tại:** Mã nguồn backend [AIAnalysisJobCandidateDiscovery.cs](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/src/EduTwin.BLL/AssessmentAndReasoning/Jobs/AIAnalysisJobCandidateDiscovery.cs:43) **CHƯA ĐƯỢC THAY ĐỔI**. Toàn bộ các cơ chế Unified Query, Adaptive Backoff và SignalR trong tài liệu này là **đề xuất kiến trúc theo giai đoạn (Proposed Phases)** để review trước khi triển khai.

Kiểm tra trực tiếp mã nguồn xác nhận:
- **Background Worker (`AIAnalysisJobBackgroundService.cs:33`):** Quét bảng `ai_analysis_jobs` theo vòng lặp vô hạn với khoảng nghỉ `PollInterval = TimeSpan.FromSeconds(3)` (`AIAnalysisJobWorkerOptions.cs:8`).
- **N+1 Query theo Center (`AIAnalysisJobCandidateDiscovery.cs:43-82`):**
  - Bước 1: 1 truy vấn `SELECT CenterId FROM Centers WHERE NOT IsDeleted ORDER BY CenterId`.
  - Bước 2: Vòng lặp `foreach (var centerId in centerIds)` tạo thêm **N truy vấn độc lập** vào `AIAnalysisJobs` cho từng trung tâm nhằm tuân thủ cơ chế đa khách thuê (Multi-tenant Query Filter).
  - Hệ quả: Với $N$ trung tâm đang kích hoạt, worker phát sinh $1 + N$ truy vấn mỗi 3 giây ngay cả khi hàng đợi hoàn toàn trống!
- **Frontend Bounded Polling (`LearningPlayerPage.tsx:764` & `polling.ts:24`):**
  - Frontend **không** polling vô hạn.
  - Sử dụng chiến lược backoff tăng dần từ 1s $\to$ 2s $\to$ 3s (tối đa 5s khi lỗi mạng).
  - Giới hạn cứng tối đa **60 lần thử** (`maxAttempts = 60`), tương đương tổng thời gian chờ $\approx 180\text{ giây}$ (3 phút).
- **Cơ chế kiểm soát hiện hữu:**
  - Hàng đợi có batch size tổng tối đa 50 job (`BatchSize = 50`), mỗi center tối đa 25 job (`PerCenterBatchSize = 25`).
  - Hỗ trợ lease thời hạn (`LeaseDuration`), thu hồi lease hết hạn (`RecoverExpiredLease`), và khóa lạc quan OCC (`RowVersion`).

---

## 2. Kết quả Mô hình Năng lực & Mô phỏng Hàng đợi (Capacity Simulation Model)

Đã xây dựng bộ mô hình tính toán và mô phỏng logic hàng đợi tại [AIAnalysisJobQueueBenchmarkTests.cs](file:///d:/AI-Driven%20Adaptive%20Learning%20and%20Competency%20Assessment%20Platform/tests/EduTwin.BLL.Tests/AssessmentAndReasoning/Jobs/AIAnalysisJobQueueBenchmarkTests.cs) (dựa trên EF Core InMemory và các tham số batch chuẩn).

> [!NOTE]
> Bảng bên dưới phản ánh **mô hình phân tích toán học và mô phỏng logic (Analytical & Simulation Model)** dựa trên thuật toán và cấu hình batch hiện hành. Đây không phải là kết quả đo lường I/O phần cứng trên môi trường MySQL sống (cần thực hiện trong staging load test trước khi migration).

### 2.1. Phân tích Truy vấn N+1 theo Số lượng Trung tâm (Measured Formula)

Công thức truy vấn thực tế trong code hiện hành:

$$\text{Số truy vấn mỗi chu kỳ 3s} = 1 + N_{\text{centers}}$$

| Số lượng Trung tâm ($N$) | Truy vấn mỗi chu kỳ (3s) | MySQL QPS lúc nhàn rỗi (Idle) | Số truy vấn rỗng / ngày | Tỷ lệ giảm tải lý thuyết nếu dùng Unified Query |
|---|---|---|---|---|
| **1 Center** | 2 queries | 0.7 QPS | 57,600 queries | -50.0% |
| **10 Centers** | 11 queries | 3.7 QPS | 316,800 queries | -90.9% |
| **50 Centers** | 51 queries | 17.0 QPS | 1,468,800 queries | -98.0% |
| **100 Centers** | 101 queries | 33.7 QPS | 2,908,800 queries | -99.0% |

- **Ý nghĩa:** Khi quy mô tăng lên 50–100 trung tâm giáo dục, vòng lặp discovery tạo ra từ **1.5 đến 3 triệu truy vấn rỗng mỗi ngày**, gây lãng phí kết nối và tài nguyên của database.
- **Giải pháp đề xuất (Phase 1):** Dùng 1 truy vấn duy nhất có `IgnoreQueryFilters()` và `ROW_NUMBER() OVER (PARTITION BY CenterId)` để cố định tải ở mức **1 query/3s** (0.33 QPS).

---

### 2.2. Mô phỏng Hàng đợi theo Kịch bản Tải Nộp bài (Simulated Capacity Model)

Mô hình giả lập quá trình xử lý với worker batch = 50 job, poll interval = 3s:

| Kịch bản Giả lập | Số Trung tâm | Số Batch (50/batch) | Thời gian dọn Queue (Mô phỏng) | Độ trễ p50 (Mô phỏng) | Độ trễ p95 (Mô phỏng) | Lượng Polling SELECT ước tính | Tỷ lệ Timeout giả lập (> 60 lần / 180s) |
|---|---|---|---|---|---|---|---|
| **100 bài nộp** | 1 Center | 2 batches | **13.0s** | 13.0s | 13.0s | ~400 SELECTs | **0%** (Nằm trong ngưỡng 180s) |
| **500 bài nộp** | 5 Centers | 10 batches | **77.0s** (~1.3 ph) | 45.0s | 77.0s | ~8,400 SELECTs | **0%** (Nằm trong ngưỡng 180s) |
| **1.000 bài nộp** | 10 Centers | 20 batches | **157.0s** (~2.6 ph) | 85.0s | 157.0s | ~32,800 SELECTs | **0%** (nếu task hoàn tất trong 100ms) |
| **1.000 bài (Giả định LLM 1s)** | 10 Centers | 20 batches | **> 500s** (~8.3 ph) | ~260.0s | ~480.0s | > 60,000 SELECTs | **~100% học sinh nửa sau bị Timeout** |

> **Phân tích nguy cơ kỹ thuật:**
> 1. Điểm nghẽn không nằm ở thao tác INSERT đơn lẻ, mà nằm ở **thông lượng xử lý (Worker Throughput)** khi gọi các mô hình AI ngoại vi có độ trễ thực tế.
> 2. Khi hàng đợi bị dồn ứ, cơ chế Bounded Polling (giới hạn 60 lần $\approx 180\text{s}$) của frontend sẽ hết hạn trước khi worker xử lý đến bài nộp của học sinh ở cuối hàng đợi, dẫn đến trải nghiệm học sinh bị báo timeout giả dù kết quả sau đó vẫn được ghi vào database.
> 3. Hàng chục ngàn lượt polling đồng thời tạo áp lực đọc lặp vô ích lên MySQL nếu không có kênh thông báo trạng thái đẩy (Push notification).

---

### 2.3. Khảo sát Xung đột Tranh chấp Nhiều Worker (Simulated OCC Collisions)

Khi mô phỏng 2 worker chạy đồng thời tranh chấp cùng 1 job:
- Worker 1 claim thành công $\to$ chuyển `Pending` sang `Processing`.
- Worker 2 cố gắng claim cùng bản ghi $\to$ State Machine phát hiện không còn hợp lệ $\to$ trả về `NotEligible` / OCC conflict.
- **Đánh giá:** Logic State Machine hiện tại bảo toàn dữ liệu an toàn (không bị xử lý đúp), nhưng nếu chạy nhiều worker instance để tăng throughput mà không có cơ chế phân phối hàng đợi hoặc `SKIP LOCKED`, tài nguyên CPU và kết nối sẽ bị lãng phí cho các lần tranh chấp thất bại.

---

## 3. Ma trận So sánh Các Giải pháp Hàng đợi (Architecture Options)

> [!NOTE]
> Các con số thông lượng trong bảng bên dưới là **ước lượng năng lực lý thuyết chuẩn công nghiệp (Industry Rule-of-Thumb Estimates)** nhằm so sánh đặc tính kiến trúc, không phải cam kết benchmark thực địa của EduTwin.

| Tiêu chí Đánh giá | **Phương án A: Giữ & Tối ưu MySQL Queue** | **Phương án B: Transactional Outbox + RabbitMQ** | **Phương án C: Transactional Outbox + Redis Streams** |
|---|---|---|---|
| **Độ an toàn giao dịch (ACID)** | Tuyệt đối (Cùng transaction với `Attempt`) | Tuyệt đối qua bảng Outbox (Zero Dual-Write) | Tuyệt đối qua bảng Outbox (Zero Dual-Write) |
| **Hạ tầng bổ sung** | **Không cần** (Tận dụng MySQL hiện có) | **Cần cụm RabbitMQ** (Erlang VM, AMQP) | **Cần Redis 7+** (In-memory, Streams) |
| **Xử lý N+1 Discovery** | Triệt để bằng Single Window Query | Triệt để (Broker tự dispatch theo consumer) | Triệt để (Consumer groups tự dispatch) |
| **Cơ chế tranh chấp worker** | `FOR UPDATE SKIP LOCKED` (MySQL 8.0+) | Tự nhiên qua Prefetch / Ack của RabbitMQ | Tự nhiên qua `XREADGROUP` / `XACK` |
| **Ước lượng năng lực thông lượng** | Hàng ngàn jobs/phút (giới hạn bởi I/O DB) | Hàng chục ngàn jobs/phút | Hàng chục ngàn jobs/phút |
| **Khả năng Dead-letter & Retry** | Xử lý bằng application logic | Rất mạnh (RabbitMQ DLX, TTL exchange) | Khá (Cần worker quản lý PEL và retry) |
| **Độ phức tạp vận hành (Ops)** | **Rất thấp** (Zero ops mới) | **Trung bình - Cao** (Clustering, disk alarms) | **Trung bình** (RAM capacity, AOF/RDB tuning) |
| **Khuyến nghị thời điểm** | **Giai đoạn hiện tại (Ưu tiên số 1)** | Khi vượt ngưỡng chịu tải MySQL (ước lượng giả định > 10.000 học sinh nộp đồng thời - cần staging validation) | Khi hệ thống đã có Redis cache dùng chung |

---

## 4. Giải pháp Đẩy Trạng thái Giao diện: SignalR vs SSE

Để xóa bỏ hàng chục ngàn truy vấn polling dồn về MySQL khi có tải đồng thời:

```text
[Học sinh nộp bài] 
        │
        ▼ (Transaction duy nhất)
  Lưu Attempt + Tạo Job
        │
        ├───> Trả về HTTP 202 Accepted { attemptId }
        │        │
        │        ▼
        │   Frontend lắng nghe SignalR Hub: `/hubs/learning-analysis`
        │   Channel: `attempt_{attemptId}`
        │   (Kèm Timer Bounded Polling 60 attempts chạy nền làm fallback)
        │
  Worker xử lý AI xong
        │
        ▼
  Cập nhật DB ──> Broadcast SignalR event `AttemptAnalysisCompleted`
                        │
                        ▼
                Trình duyệt nhận sự kiện đẩy gần thời gian thực (cần staging validation)
                Hủy bỏ ngay lập tức timer polling fallback!
```

### So sánh SignalR vs Server-Sent Events (SSE):

> [!NOTE]
> Các chỉ số độ trễ truyền tin và mốc quy mô đồng thời là **ước lượng giả định thiết kế (Design Assumptions)**, cần được thẩm định thực nghiệm (Staging Validation) trong điều kiện mạng trường học thực tế trước khi đưa vào SLA cam kết.

| Đặc tính | **ASP.NET Core SignalR (Khuyến nghị đề xuất)** | **Server-Sent Events (SSE)** |
|---|---|---|
| **Phương thức truyền dẫn** | Tự động nâng cấp WebSocket $\to$ SSE $\to$ Long Polling | Thuần HTTP/1.1 hoặc HTTP/2 stream đơn hướng |
| **Độ trễ truyền tin (Ước lượng)** | Rất thấp (WebSocket duplex - ước lượng lý thuyết, cần staging validation) | Rất thấp (HTTP stream - ước lượng lý thuyết, cần staging validation) |
| **Hỗ trợ Reconnect & Handshake** | Tự động có sẵn trong `@microsoft/signalr` | Tự động reconnect cơ bản của EventSource |
| **Nhóm theo đối tượng (Grouping)** | Tích hợp sẵn `Groups.AddToGroupAsync("attempt_123")` | Phải tự xây dựng multiplexing / channel mapping |
| **Khả năng mở rộng cụm (Scale-out)** | Sẵn có Redis Backplane hoặc Azure SignalR Service | Cần tự phân phối sự kiện qua Pub/Sub |

**Nguyên tắc Bất biến:** Dù dùng SignalR hay SSE, **Bounded Polling (60 attempts) bắt buộc phải được giữ lại làm cơ chế dự phòng (fallback)** nhằm đề phòng mạng trường học chặn kết nối WebSocket/SSE qua proxy hoặc tường lửa.

---

## 5. Lộ trình Triển khai An toàn (Zero Dual-Write Migration Plan)

Tuyệt đối **không chuyển đổi vội vã** hoặc bỏ bảng `ai_analysis_jobs` ngay lập tức. Quá trình di chuyển đề xuất tuân theo 4 giai đoạn có kiểm soát:

### Giai đoạn 1: Tối ưu hóa Ngay tại MySQL (Đề xuất làm trước - Zero Risk)
1. **Xóa bỏ N+1 Discovery trong backend:**
   - Thay thế vòng lặp qua từng Center bằng 1 truy vấn duy nhất có `IgnoreQueryFilters()` và `ROW_NUMBER() OVER (PARTITION BY CenterId ORDER BY EligibleAt) <= @perCenterLimit`.
2. **Adaptive Polling Interval:**
   - Khi không có job: Tăng thời gian nghỉ từ 3s $\to$ 5s $\to$ 10s.
   - Khi phát hiện có job: Giảm ngay xuống 500ms để dọn sạch hàng đợi nhanh nhất.
3. **Cạnh tranh Worker an toàn:**
   - Bổ sung `SKIP LOCKED` cho các truy vấn claim để hỗ trợ chạy 2–4 background workers song song mà không bị nghẽn lock.

### Giai đoạn 2: Tích hợp SignalR Push + Bounded Polling Fallback
1. Tạo `LearningAnalysisHub` tại ASP.NET Core API.
2. Học sinh nộp bài xong sẽ kết nối và subscribe vào group của `attemptId`.
3. Background Worker khi hoàn thành phân tích AI sẽ gửi thông báo qua SignalR.
4. Frontend `LearningPlayerPage` nhận sự kiện đẩy và dừng polling ngay lập tức.
5. Nếu sau 10s không có kết nối WebSocket, frontend kích hoạt Bounded Polling như bình thường.

### Giai đoạn 3: Giới thiệu Transactional Outbox (Chuẩn bị Broker)
1. Tạo bảng `ai_analysis_outbox` trong MySQL cùng transaction với `attempts`.
2. Đảm bảo **Zero Dual-Write**: Không bao giờ gọi broker trực tiếp bên trong transaction của web request.
3. Xây dựng Outbox Publisher relay chuyên dụng.

### Giai đoạn 4: Di chuyển sang RabbitMQ / Redis Streams (Sau khi Review và Load Test Staging)
1. Kích hoạt Consumer đọc từ Message Broker.
2. Chạy cơ chế Shadow Run (Chạy song song so sánh kết quả).
3. Đóng hoàn toàn vòng lặp quét DB định kỳ của Worker cũ.

---

## 6. Kết luận & Quyết định Kiến trúc (Action Items)

1. **Giữ nguyên hàng đợi MySQL hiện tại**, không vội vã cài đặt RabbitMQ/Redis vào thời điểm này nhằm tránh gia tăng chi phí vận hành hạ tầng khi chưa có bằng chứng quá tải thực tế.
2. Trình duyệt kế hoạch tối ưu Giai đoạn 1 (Single Unified Query + Adaptive Backoff) và Giai đoạn 2 (SignalR push notification) để đội ngũ kiến trúc phê duyệt trước khi thực hiện code changes.
3. Khi hệ thống tiến hành kiểm thử tải quy mô lớn trên staging với cơ sở dữ liệu thật, các chỉ số latency và throughput thực tế sẽ được cập nhật bổ sung vào ADR này.
