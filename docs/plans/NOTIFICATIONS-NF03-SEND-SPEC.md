# NF-03a — Soạn/gửi thông báo in-app

2026-10-10: người dùng yêu cầu sửa UI và chọn ưu tiên soạn/gửi trước NF-02. Amendment này bổ sung NF-01; không coi toàn bộ NF-03 đã hoàn tất. Không commit/push.

## Phạm vi

- Teacher → học sinh một/nhiều lớp Active/Current mình phụ trách, hoặc học sinh được chọn trong các lớp đó; Teacher → Manager cùng trung tâm.
- Manager → giáo viên cùng trung tâm; Manager → PlatformAdmin để hỗ trợ vận hành.
- PlatformAdmin → Manager của các trung tâm Active được chọn. Không nhận học thuật; Student không gửi tự do; không Teacher–Teacher hoặc Admin–Teacher.
- Nội dung plain text, tiêu đề 160/nội dung 2000 ký tự, tối đa 500 người nhận kỹ thuật; không attachment/URL hành động/HTML. Gửi liên tenant chỉ bằng relay server explicit; không sửa tenant hiện tại theo input.
- Preview token bảo vệ bằng DataProtection hiện có, hạn 5 phút, gắn sender/tenant/auth-version, fingerprint nội dung/selector và tập người nhận. Send kiểm tra lại scope và từ chối 409 nếu tập đổi; không âm thầm thêm người.
- ClientMessageId GUID unique cùng tenant/sender chống double-click/network retry. Nội dung đã gửi và snapshot người nhận bất biến. Sent chỉ chủ gửi xem; receipt tổng hợp không tạo quyền mở bài làm.
- Rate limit send 5 lần/phút/sender trên một API instance là safety budget, không phải quota sản phẩm hay distributed rate-limit. Fan-out bounded, không gọi AI.
- Chưa có thu hồi/đính chính, ack/reply, retention purge; chưa nối nghiệp vụ NF-02 hoặc activity NF-04.

## Schema

Thêm `notification_messages` append-only, PK message_id, center_id/source sender FK cùng tenant, client_message_id unique(center,sender,client), title/body/sender_name snapshot, audience JSON, fingerprint SHA256, sender_auth_version, created_at. Alternate(center,message) và center FK. Event thêm nullable message_id FK message, nullable sender_name snapshot. Event của relay nằm trong tenant người nhận, CreatedBy=null nếu sender khác tenant; sender của message ở tenant nguồn. Worker revalidate sender active/auth-version và scope người nhận trước fan-out; không hồi sinh người mới ngoài snapshot.

## Quyền / API

`notifications.messages.send_scoped` và `.read_sent_own`: Teacher/Manager. `platform.notifications.send_scoped` và `.read_sent_own`: PlatformAdmin, non-delegable. Backfill system role active hiện có; không auto grant custom/revoked role, không mở quyền học thuật Manager.

Các route dưới `/api/v1/notifications`: GET `/compose-options`; POST `/preview`; POST `/send`; GET `/sent`; GET `/sent/{messageId}`. Options/preview/send require send; sent require read-sent. ID GUID strings, UTC, envelope/ProblemDetails hiện có. Send trả messageId/recipient count/dispatch status; trạng thái “Đã tiếp nhận để gửi” khác “Đã phân phối”. Foreign/missing own sent 404; scope invalid 400/403; scope thay đổi 409, token sai/hết hạn/nội dung sửa 400; throttled 429. Không nhận sender/tenant tùy ý hoặc trả recipient list trong inbox.

## UI / gate

Chuông là popover không modal/không tô tối trang, portal dưới body tránh containing block của header. Click ngoài/Escape đóng và trả focus hợp lý; resize/scroll không để popover lệch/tràn. Inbox empty chỉ một trạng thái rõ ràng, không hai ô trắng; Received/Sent và Compose hiện đúng quyền. Form có loại người nhận, chọn scope, preview số lượng/tên hợp lệ và xác nhận rõ. Không gửi test thật khi chưa có recipient/nội dung được người dùng duyệt.

Gate: scope hai tenant/bốn actor/archived class, preview tamper/stale/replay, idempotent/rollback/unique, worker sender/member revalidation, own sent isolation, UI portal/viewport/theme, build/test/migration model drift. NF-01 gate lịch sử giữ nguyên; SQL backup trước deploy migration additive.
