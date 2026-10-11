# CP-20261010-NOTIFICATIONS — đặc tả NF-00/NF-01

Ngày: 2026-10-10. Người dùng đã cho phép triển khai. Người thực hiện: Codex; reviewer con người chưa được chỉ định. Đây là amendment có ưu tiên cho phạm vi này so với các mục baseline coi Notification ngoài MVP. Không sửa các kết quả kiểm chứng lịch sử.

## Phạm vi được khóa

Thông báo in-app bền vững trong modular monolith hiện có; không thêm database, package, SignalR/broker, email, push, presence hoặc request AI. NF-01 gồm kho outbox, worker riêng, hộp thư riêng, số chưa đọc và đọc một/đọc tập đang hiển thị; chuông bốn tác nhân. Chưa có gửi chủ động, thu hồi/xác nhận, nhật ký chung, liên tenant hoặc hook nghiệp vụ. Không phát dữ liệu demo cho tài khoản thật, không backfill thông báo từ lịch sử.

## Schema additive (DATABASE_SCHEMA amendment)

- `notification_events`: `event_id varchar(36)` PK; `center_id varchar(36)` FK center, alternate `(center_id,event_id)`; `deduplication_key varchar(160)` unique cùng center; `event_type varchar(64)`; `title varchar(160)`; `body varchar(2000)` plain text; `recipient_user_ids json` snapshot không quá 500 GUID khác nhau; `created_at datetime(6)`, `created_by varchar(36) NULL` composite FK user. Bất biến; DB chặn UPDATE/DELETE. NF-01 chỉ `SystemNotice`, do code nội bộ tạo, không có endpoint gửi tùy ý.
- `notification_dispatches`: PK/FK `(center_id,event_id)`; `state Pending|Processing|Completed|DeadLetter`; `available_at`, `lease_until` nullable; `lease_token varchar(36)` nullable; `attempt_count int`; `delivered_count`, `suppressed_count int`; `last_error_code varchar(64) NULL`; `row_version bigint unsigned` concurrency token. Index state/available_at/center. Queue mutable tách khỏi event bất biến.
- `notification_deliveries`: `delivery_id bigint unsigned` auto PK; `(center_id,event_id,user_id)` unique; composite FK tới event/user (Restrict); `delivered_at datetime(6)`, `read_at datetime(6) NULL`. Index `(center_id,user_id,read_at,delivery_id)`. Nội dung lấy từ event snapshot. Không hard-delete. DB chặn đổi identity/time hoặc bỏ dấu đã đọc.

Model count thực được kiểm kê lúc tạo migration; không cập nhật số bảng baseline bằng suy đoán. Bản đầu không tự purge; retention production cần chủ sở hữu duyệt riêng.

## Quyền / default roles

- `notifications.inbox.read_own`, `notifications.inbox.update_own`: Student/Teacher/CenterManager; default system roles tương ứng, không tự cấp custom role.
- `platform.notifications.read_own`, `platform.notifications.update_own`: chỉ PlatformAdmin, non-delegable; inbox chỉ tenant PLATFORM hiện tại, chưa relay liên trung tâm.
- Giữ nguyên LastTenantAdmin và các quyền học thuật bị cấm của Manager. Backfill chỉ role hệ thống active; không tạo lại assignment đã thu hồi. Middleware kiểm tra tài khoản/tenant/auth version như hiện tại; use case tiếp tục fail closed khi thiếu danh tính.

## API v1 amendment (API_CONTRACTS)

Các route `/api/v1/notifications` dùng quyền own tenant hoặc own platform theo applicability; không nhận center/user/sender từ client. ID bigint ở JSON là chuỗi, ngày UTC. Không cho body field ngoài contract.

| Method/route | Contract |
| --- | --- |
| GET `/notifications/summary` | `data: { unreadCount }`; summary không chứa body hoặc danh sách người nhận |
| GET `/notifications` | `page=1`, `pageSize=20` (1…100), `unreadOnly=false`; `data: NotificationDto[]`, paged meta; sort delivery_id DESC |
| GET `/notifications/{id}` | DTO riêng của người nhận, không đánh dấu đã đọc ngầm |
| POST `/notifications/{id}/read` | idempotent, 204; người khác/khác tenant 404 |
| POST `/notifications/read-batch` | `{ deliveryIds: string[] }` 1…100, unique; chỉ cập nhật tập ID đã hiển thị của chính người nhận; 204 |

DTO: `deliveryId`, `eventType`, `title`, `body`, `deliveredAt`, `readAt`. Không trả recipient list, raw audit, URL tùy ý, private evidence hay snapshot của tác nhân khác. NF-01 chưa có link resource: chỉ mở chi tiết thông báo. Validation 400 `VALIDATION_FAILED`; missing/foreign 404 `RESOURCE_NOT_FOUND`; auth/permission 401/403 theo middleware; DB/service lỗi không giả số 0.

Đọc tập snapshot thay cho “đọc tất cả qua max ID”: auto-increment có thể commit lệch thứ tự, max ID không bảo đảm tập đã thấy. UI ghi rõ “Đọc các thông báo đang hiển thị”, không tuyên bố đọc toàn hộp thư. Đọc tập có ID ngoài scope trả 404 và không cập nhật tập còn lại.

## Worker / vận hành

Mặc định cấu hình an toàn: bật worker, poll 5 giây, tối đa 20 event mỗi vòng, mỗi tenant tối đa một event một vòng (fair), lease 2 phút, tối đa 5 lần claim; retry exponential tối đa 5 phút. Fan-out tối đa 500 là giới hạn kỹ thuật checkpoint này, chưa phải quota gửi sản phẩm NF-03. Claim bằng row-version; commit fan-out + terminal state trong cùng transaction và kiểm tra lease/token/version; lỗi không chứa nội dung. Unique DB chống delivery trùng, lease hết hạn phục hồi qua vòng kế tiếp. Worker không gọi AI. Center/user inactive không được phát; user bị suppress có đếm nhưng không hồi sinh thông báo lỗi thời khi mở lại.

## UI / nghiệm thu

Chuông thật trên bốn layout; popup modal accessible có focus/ESC và 5 thông báo mới nhất; “Xem tất cả” mở hộp thư responsive dùng chung theo route tác nhân. Student không thêm tab main-nav. Teacher/Manager/Platform thêm sidebar Thông báo; các mục Nhật ký bổ sung tại NF-04 (Platform giữ mục hiện có).

Query key center/user; chuyển phiên không dùng cache phiên trước. Summary poll 30 giây + jitter khi visible/online; dừng hidden/offline và 401/403, backoff lỗi; preview/list chỉ fetch khi mở, không poll body. Loading/error khác empty/0; chỉ đánh dấu đọc khi chọn item hoặc xác nhận đọc tập.

Gate: unit scope/validation/idempotence/snapshot; model keys/filter/FK; MySQL isolated migration/atomicity/concurrency/lease/duplicate/restart; frontend tests/build/lint. Không chạy lại bài 50 câu thật hoặc thay kết quả cũ trong NF-01. Không áp dụng migration production trước backup và báo cáo checkpoint. Ngưỡng benchmark NF-02: cùng fixture 50/100 câu, không tăng p95 submission/AI commit quá 10% hoặc 100ms (lấy mức lớn hơn); thời gian Gemini được báo riêng, không quy nguyên nhân từ nhiễu provider.

## Requirement IDs / allow-list

`NF-DATA-01` durable tenant outbox; `NF-SEC-01` own inbox fail-closed; `NF-UI-01` real bell bốn tác nhân; `NF-PERF-01` worker/polling riêng. Trạng thái NF-01: TECHNICAL CHECKPOINT VERIFIED cho gate tự động và triển khai local; UI toàn bộ tác nhân/nhận thông báo có dữ liệu thật còn PENDING, chưa nghiệm thu toàn feature. Bằng chứng tại [báo cáo NF-01](../verification/NOTIFICATIONS-NF01-REPORT-2026-10-10.md). Allow-list: notification module mới ở Contracts/DAL/BLL/API/web/tests, DbContext/config snapshot/migration, catalog/composite policies/DI, bốn layout/App và root specification/tracking/report. Không sửa AI provider/queue/model, điểm/evidence/bài làm hoặc .env.
