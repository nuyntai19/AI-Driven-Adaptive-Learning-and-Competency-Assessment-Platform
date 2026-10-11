# PLATFORM-OPS-OBS-01 — giám sát vận hành Admin

Ngày: 11/10/2026. Human owner/phê duyệt: người dùng EduTwin trong chat, “oke làm đi”. Expected HEAD: `61ef80a7830772c6aec10d3cd0053e61d313cfe7`. Trạng thái: COMPLETED-v1 / VERIFIED trong phạm vi này; [báo cáo kết quả và giới hạn](../verification/PLATFORM-OPERATIONS-REPORT-2026-10-11.md). Không commit/push.

Đây là milestone mới sau đề xuất PLATFORM-OBSERVABILITY, thay quyết định deferred của Stage I **chỉ với phạm vi dưới đây**. Stage H/MFA vẫn deferred. Không thay luồng chấm hoặc dữ liệu học thuật.

## Phạm vi và giả định

- Ba trang: Vận hành hệ thống, Sử dụng AI, Lưu trữ & sao lưu. Quyền riêng `platform.operations.read`, `platform.ai_usage.read`, `platform.storage.read`, sensitive/non-delegable/chỉ PlatformAdmin của PLATFORM. Service kiểm tra thêm tenant thực của DbContext.
- Hàng đợi: trạng thái hiện tại toàn bộ job trong các center còn tồn tại, lọc center; danh sách 20 job chờ/lỗi mới nhất, không trả AttemptId/StudentId/raw error/correlation chứa nội dung. Tuổi job tính từ CreatedAt; không gọi đó là thời gian provider. Lượt gọi provider có latency đo riêng.
- Ledger mới bắt đầu từ rollout, mỗi lần gọi provider thực tế một CallId UUID; không ghi lượt bị chặn trước provider. Chỉ metadata allow-list provider, model, alias pool, stage, thời gian, token, outcome, price-version, estimated USD. Không prompt/response/image/hash/key/student hoặc điểm. Batch có thể gồm nhiều học sinh; số calls không phải số bài/câu/học sinh.
- Buffer giới hạn 1.000, không chặn chấm; thất bại ghi được thử lại, quá tải ghi dropped từ lúc process bắt đầu. Crash có thể mất phần buffer chưa flush; UI ghi rõ giới hạn này. SQL aggregate có index, cache 30s, cửa sổ 1/7/30 ngày; không tải raw ledger hoặc blob vào Admin.
- Bảng giá cấu hình theo provider/model/pool, effective-from/until, version, URL nguồn, verified Free/Paid; mặc định Unknown. Chỉ USD ở phiên bản này. Cached là thành phần prompt, không cộng kép; thinking được tính riêng với quy tắc được cấu hình; total chỉ để đối chiếu. Giá/cost được chụp trên từng record, không tính lại lịch sử bằng giá mới. Không tự bật billing hoặc gọi AI thật để đo.
- Storage collector 5 phút: aggregate SUM attachment metadata và LENGTH ảnh câu hỏi tại SQL theo center. Snapshot bao gồm logical bytes/count và free/total của volume API (không phải toàn ổ C/D hoặc Docker VHD). Không scan/xóa file hoặc bài làm; physical DB/index/orphans chưa được đo phải ghi rõ.
- Backup manifest chỉ metadata success/failure/time/size/checksum, restore rehearsal có thời gian riêng; script dùng schema UUID cô lập, không restore đè production. API không trả path hoặc file backup. Không tự tạo lịch backup hoặc hứa backup tự động khi chưa có scheduler.
- Cảnh báo backlog > ngưỡng, terminal tăng, worker heartbeat stale, volume ít chỗ, backup cũ: điều kiện kéo dài trước gửi; một thông báo mỗi episode và thông báo phục hồi, qua notification outbox của root. Không tự retry AI, đổi quota hoặc xóa dữ liệu. Mặc định bảo thủ, cấu hình được; không gửi cảnh báo chỉ vì lịch sử test có lỗi cũ.
- Retention chưa được duyệt: không purge ledger/snapshot/audit tự động. Dùng snapshot hiện tại upsert; ledger cần chính sách retention/rollup trước tải dài hạn thực tế.

## Schema bổ sung

`platform_ai_calls`: call_id PK varchar(36); started_at datetime(6); provider varchar(16); model varchar(120); pool_alias varchar(32); stage varchar(32); outcome varchar(32); duration_ms bigint; input/output/thinking/cached/total tokens bigint nullable; price_version varchar(80) nullable; estimated_usd decimal(18,8) nullable. Index started_at/provider/model. Không FK sang học sinh hoặc Attempt.

`platform_ops_snapshots`: snapshot_key PK varchar(80); captured_at datetime(6); payload json (DTO allow-list của phiên bản này, không entity/raw academic). Một snapshot storage hiện tại; heartbeat health trong RAM được ghi rõ theo lần khởi động process, không giả thành lịch sử bền vững.

`platform_ops_alerts`: alert_key PK varchar(80); first_seen_at, last_seen_at, notified_at nullable datetime(6); is_active boolean; episode_id varchar(36); row_version bigint unsigned OCC. Định danh điều kiện hữu hạn.

## API bổ sung

GET `/api/v1/platform/operations?centerId=UUID`: summary queue, 20 metadata job, heartbeat, DB probe, alerts; quyền operations.read.
GET `/api/v1/platform/ai-usage?days=1|7|30`: summary + day/provider/model/stage aggregates, null cost/coverage; quyền ai_usage.read.
GET `/api/v1/platform/storage`: snapshot dung lượng theo center + backup metadata; quyền storage.read.
Response `{data,meta:{timestamp,traceId}}`; lỗi ProblemDetails 400/403. Không endpoint mutation mới.

## Allow-list và gate

Sửa core MD và proposal; thêm feature Platform Operations trong DAL/BLL/Contracts/API, controller, DI, adapter instrumentation và heartbeat worker; migration/model snapshot/catalog; scripts backup metadata; compose mount read-only; ba trang frontend/API/types/helper, routes/menu/permissions; tests và report. Không sửa thuật toán/đề/bài làm, không stage/push.

Gate: unit allow-list/tenant/permissions/price/alert/null; frontend typecheck/tests/lint/build; MySQL thật cho migration và LENGTH/grouping; backup + restore rehearsal trước migration; live UI 100% desktop/mobile/light/dark; báo cáo số đo và giới hạn thật. Chưa cam kết 1.000 người đồng thời khi chưa load-test.

Nguồn token và giá: [Gemini GenerateContent](https://ai.google.dev/api/generate-content), [Gemini pricing](https://ai.google.dev/gemini-api/docs/pricing). Không hard-code giá chưa được chủ tài khoản xác minh.
