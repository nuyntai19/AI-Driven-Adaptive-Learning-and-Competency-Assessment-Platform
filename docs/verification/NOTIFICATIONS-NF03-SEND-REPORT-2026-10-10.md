# Thông báo NF-03a và sửa UI — 2026-10-10

Baseline `61ef80a`, nhánh `student/answer`. Người dùng chọn **ưu tiên soạn/gửi thông báo** sau khi báo UI NF-01 khó dùng và lỗi nền popup. [Đặc tả phạm vi](../plans/NOTIFICATIONS-NF03-SEND-SPEC.md). **Không stage/commit/push.** Báo cáo kiểm chứng local, không thay thế nghiệm thu production.

## Chức năng đã bổ sung

- Trung tâm thông báo có Đã nhận / Đã gửi / Soạn thông báo theo quyền. Học sinh chỉ nhận.
- Form chọn kênh, tìm/chọn lớp hoặc tài khoản trong phạm vi cho phép, nhập tiêu đề/nội dung, xem trước số lượng/tên rồi xác nhận gửi.
- Teacher gửi học sinh của lớp Active/Current mình phụ trách (theo lớp hoặc từng học sinh), hoặc Manager cùng trung tâm.
- Manager gửi giáo viên cùng trung tâm hoặc Admin qua kênh hỗ trợ vận hành. Không cấp quyền xem bài làm/điểm.
- Admin gửi Manager của các trung tâm Active được chọn; không mở Admin–Teacher hoặc Teacher–Teacher/chat tự do.
- Đã gửi chỉ chủ gửi xem, hiển thị số người nhận/phân phối/đọc/không còn phù hợp. Đã đọc không đồng nghĩa xác nhận hoặc đã xử lý.
- Nội dung plain text được React escape; không HTML/tệp/URL hành động. Cảnh báo không gửi thông tin học thuật riêng trong thông báo chung/hỗ trợ là hướng dẫn sử dụng, **không** tuyên bố hệ thống tự động khử mọi dữ liệu cá nhân.

## Sửa giao diện

Nguyên nhân dải tối: popup NF-01 nằm trong sticky header có backdrop-filter/containing block, khiến lớp fixed overlay bị giới hạn. Bản mới dùng **popover không modal, portal dưới body, không overlay**, không khóa scroll trang. Click ngoài, Escape, nút đóng và rời focus đóng hộp. Vị trí dựa trên nút chuông và viewport, reset/đóng hợp lý khi resize/scroll.

Inbox trống chỉ còn một trạng thái minh họa rõ ràng, không hai khung trắng lớn. Danh sách có unread marker, tên người gửi, thời gian; chi tiết chỉ xuất hiện khi chọn. Form, cards và padding dùng chung cho theme sáng/tối và màn hình hẹp.

## Ràng buộc và dữ liệu

- Preview DataProtection 5 phút gắn tenant/sender/AuthVersion, fingerprint nội dung/selector và tập người nhận. Token sai/hết hạn/sửa nội dung: 400; tập người nhận đổi khi gửi: 409 yêu cầu preview lại. Token của sender/tenant khác bị chặn.
- ClientMessageId unique theo tenant/sender: double-click, gửi đồng thời hoặc retry cùng nội dung không tạo bản gửi mới. Dùng cùng ID cho nội dung khác trả conflict.
- Message + event + dispatch commit trong một transaction. Gửi trả trạng thái tiếp nhận, không giả là đã phân phối.
- Worker chỉ phát snapshot đã chốt, kiểm tra lại tài khoản active, sender AuthVersion và scope lớp/membership; không thêm người mới ngoài snapshot. Không hồi sinh người đã rời lớp/lớp lưu trữ.
- Relay server explicit: message ở tenant nguồn, envelope/delivery ở tenant người nhận; không gán foreign user vào CreatedBy của tenant đích, không thay tenant hiện tại theo request.
- Thêm một bảng append-only `notification_messages`, hai cột nullable message_id/sender_name trên event, unique/composite FK và hai trigger chặn sửa/xóa bản gửi. Migration `20261010081404_AddNotificationManualSend`.
- Bốn capability send/own-sent mới (tenant và platform riêng). Default Teacher/Manager/Platform có đúng quyền; Student không gửi; platform permission không ủy quyền; không tự grant custom/revoked role.
- Giới hạn kỹ thuật: 160 ký tự tiêu đề, 2000 nội dung, tối đa 500 người nhận; options trả tối đa 500 lựa chọn mỗi kênh. Rate limit send 5/phút trên một API instance là safety budget, không phải distributed quota.
- Không sửa .env, package, Docker Compose, AI model/queue/quota, câu hỏi, bài làm, điểm hoặc Test 4–9.

## Kiểm chứng tự động

- Frontend: 658 pass / 0 fail; TypeScript/Vite build đạt; lint phần notification 0 error/0 warning; bundle budget 3/3 pass.
- Backend full cuối: 4.070 pass / 0 fail / 89 integration skip, tổng 4.159. Không tính các skip là pass SQL.
- Lượt SQL đầu: 6 nền tảng pass, 3 test gửi mới fail do fixture đổi User.RoleName sau khi tạo (RoleName thuộc alternate key). Đã sửa **fixture cô lập** tạo đúng role từ đầu, không đổi role tài khoản thật; bổ sung SQL query học sinh theo lớp/lưu trữ và policy endpoint.
- **MySQL 10/10 pass, 0 fail/0 skip**, 4 phút 1 giây sau sửa fixture: query Teacher–class–student thực, archived revalidation, concurrent ClientMessageId, rollback, append-only, explicit relay/local envelope, own sent/inbox scope, outbox/dispatch/history và EF không model drift.
- API/web Docker build đạt, API 0 error/0 warning. Credential-pattern scan các file thay đổi: 0 finding; .env vẫn ignored; git diff --check đạt (chỉ cảnh báo LF/CRLF của cấu hình Git hiện có).

## Sao lưu / runtime / UI

Backup trước NF-03a: `2026-10-10_before_math_repair_02319d4902314f23a1e423969666c000.sql` tại `%TEMP%/EduTwin-backups`, 3.390.089 byte, SHA256 `e66f656b1decbc96da603ee786e90abc6f05f3b86713987e3cb07c4f562a18ea`. Tên script lịch sử không đồng nghĩa sửa toán; backup không vào Git.

Trước deploy: 43 migration, 212 attempts, 10 assignments, 8 attempt attachments. Đã giữ fingerprint đọc-only cho các bảng học tập để so sánh sau deploy; users có thể đổi AuthVersion do additive system-role backfill nên được tách khỏi bằng chứng học tập.

Sau `docker compose up -d --no-deps api web`: 44 migration, attempts=212/behavior twins=5/assignments=10, orphan analysis/evidence/history/path/knowledge=0, FK checks=1, trigger evidence append-only=1. Notification message/event/delivery=0, notification triggers=6, schema kiểm thử tạm=0. Bốn permission send/own-sent hiện diện, orphan message sender=0. Readiness HTTP 200 Healthy/MySQL Healthy. Không tạo lại MySQL/volume hoặc sửa dữ liệu học tập.

Fingerprint của **17 bảng học tập/giáo trình/graph/progress/history giữ nguyên byte-for-byte**. Bảng users đổi fingerprint, số users vẫn 18; điều này phù hợp cơ chế system-role AuthVersion backfill/refresh phiên (suy luận từ luồng startup/refresh, không có bằng chứng so sánh từng cột users trong lượt này). Không dùng hash users để tuyên bố dữ liệu học tập đổi.

API chạy image `sha256:d56c220e0891166f573f274295ffd897b47397f7b375db00eaa1a5f786a379d9`; web `sha256:3ee0b78c391814d66df6ef3eab1b44ff4ebf5f6113a176b4c5e0f106830930fd`, cả hai khớp latest image đã build. HTML Docker không cùng hash build Windows local (artifact build hai môi trường khác nhau); runtime xác nhận bằng đúng image và Chrome tải được chức năng mới, không coi hai hash khác nhau là bản web cũ.

QA Chrome **Teacher Math / EDUTWIN_A**:

- Phiên tự refresh quyền, có Đã nhận/Đã gửi/Soạn, không yêu cầu lấy mật khẩu hoặc token.
- Form trả đúng ba lớp Toán 10/11/12 đang học; lớp kiểm thử đã lưu trữ không nằm trong lựa chọn. Kênh Học sinh được chọn có năm thành viên hợp lệ; kênh Manager riêng.
- Preview nội dung kiểm thử cho Duy Bảo Trịnh trả đúng **1 người nhận** và nút xác nhận. Sửa nội dung làm mất nút xác nhận cũ; không reuse preview cho nội dung đã sửa.
- Tab Đã gửi truy cập thành công và hiển thị “Bạn chưa gửi thông báo nào”, không lỗi quyền. Cuối QA để biểu mẫu soạn trống, không giữ nút xác nhận có thể gửi nhầm; người dùng có thể tự soạn, bản nháp kiểm thử chưa được lưu.
- Popover là con trực tiếp body, không phủ lớp tối lên header/trang. Escape và click ngoài đóng được. Theme sáng/tối hiển thị đúng, đã trả về theme sáng ban đầu.
- Override 390×844, CSS viewport thực 355×767: document width=341≤355, popover x≈12..343 và y≈65.5..373.1, không tràn viewport; đã reset override. Form xếp một cột.
- Ảnh bằng chứng lưu riêng `%TEMP%/EduTwin-NF03`: bell-light.jpg, bell-dark.jpg, bell-mobile.jpg, compose-preview.jpg, compose-empty.jpg. Chụp toàn trang một lần timeout; ảnh viewport và DOM không lỗi, không lặp thao tác gửi.
- Chưa bấm gửi dữ liệu thật: đã hỏi quyền gửi riêng student01 với nội dung kiểm thử, chưa có câu trả lời tại thời điểm QA này. Send/receive/read đã kiểm chứng bằng SQL và unit cô lập, **không** tuyên bố đã E2E gửi thật bốn tác nhân.

## Chưa hoàn thành

- NF-02: thông báo tự động giao bài, nộp bài, AI terminal, giáo viên chốt điểm và lifecycle khác.
- NF-03b: thu hồi/đính chính, xác nhận nhận (ack), phản hồi có phạm vi.
- NF-04: activity log riêng. NF-05: nghiệm thu production, tải thực, retention/operational policy.
- Email/SMS/browser push, presence/online tracking và chat tự do không nằm trong lượt này.
- Chưa nghiệm thu UI từng tài khoản Manager/Admin/Student của runtime mới. Không dùng synthetic unit test thay thế kiểm chứng trình duyệt.

## Danh sách file thay đổi

61 file text so với baseline, bao gồm NF-01 và NF-03a, không chỉ riêng lượt gửi. Migration Designer/snapshot là EF sinh mô hình đầy đủ. Không stage hoặc push; không thêm binary media vào repo.

```text
API_CONTRACTS.md
CONSTITUTION.md
DATABASE_SCHEMA.md
MASTER_PLAN.md
PROJECT_REQUIREMENTS.md
PROJECT_TRACKING.md
UI_UX_SPEC.md
docs/decisions/ADR-POST-R09-AI-QUEUE-AND-NOTIFICATIONS.md
docs/plans/NOTIFICATIONS-AND-ACTIVITY-LOG-PLAN.md
docs/plans/NOTIFICATIONS-NF01-SPEC.md
docs/plans/NOTIFICATIONS-NF03-SEND-SPEC.md
docs/verification/NOTIFICATIONS-NF01-REPORT-2026-10-10.md
docs/verification/NOTIFICATIONS-NF03-SEND-REPORT-2026-10-10.md
scripts/ops/run_notification_checks.cjs
src/EduTwin.API/Controllers/NotificationMessagesController.cs
src/EduTwin.API/Controllers/NotificationsController.cs
src/EduTwin.API/Notifications/NotificationBackgroundService.cs
src/EduTwin.API/Notifications/NotificationPreviewProtector.cs
src/EduTwin.API/Program.cs
src/EduTwin.API/Security/CompositePermissionPolicies.cs
src/EduTwin.BLL/Notifications/DependencyInjection.cs
src/EduTwin.BLL/Notifications/NotificationAudiencePolicy.cs
src/EduTwin.BLL/Notifications/NotificationDispatcher.cs
src/EduTwin.BLL/Notifications/NotificationInboxService.cs
src/EduTwin.BLL/Notifications/NotificationManualService.cs
src/EduTwin.BLL/Notifications/NotificationOutbox.cs
src/EduTwin.BLL/Notifications/NotificationRecipientScope.cs
src/EduTwin.Contracts/Notifications/NotificationContracts.cs
src/EduTwin.DAL/Notifications/NotificationDelivery.cs
src/EduTwin.DAL/Notifications/NotificationDispatch.cs
src/EduTwin.DAL/Notifications/NotificationEvent.cs
src/EduTwin.DAL/Notifications/NotificationMessage.cs
src/EduTwin.DAL/Persistence/Configurations/Notifications/NotificationConfigurations.cs
src/EduTwin.DAL/Persistence/EduTwinDbContext.cs
src/EduTwin.DAL/Persistence/Migrations/20261010072401_AddNotificationInboxFoundation.Designer.cs
src/EduTwin.DAL/Persistence/Migrations/20261010072401_AddNotificationInboxFoundation.cs
src/EduTwin.DAL/Persistence/Migrations/20261010081404_AddNotificationManualSend.Designer.cs
src/EduTwin.DAL/Persistence/Migrations/20261010081404_AddNotificationManualSend.cs
src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs
src/EduTwin.DAL/Seeding/AuthorizationPermissionCatalog.cs
tests/EduTwin.BLL.Tests/IdentityAndTenancy/AuthorizationBootstrapperTests.cs
tests/EduTwin.BLL.Tests/IdentityAndTenancy/GlobalQueryFilterTests.cs
tests/EduTwin.BLL.Tests/IdentityAndTenancy/ListPermissionsUseCaseTests.cs
tests/EduTwin.BLL.Tests/Notifications/NotificationFoundationTests.cs
tests/EduTwin.BLL.Tests/Notifications/NotificationManualTests.cs
tests/EduTwin.BLL.Tests/Notifications/NotificationMySqlTests.cs
web/edutwin-web/src/App.tsx
web/edutwin-web/src/api/notificationsApi.ts
web/edutwin-web/src/auth/permissions.ts
web/edutwin-web/src/components/notifications/NotificationBell.tsx
web/edutwin-web/src/components/notifications/NotificationComposer.tsx
web/edutwin-web/src/components/notifications/NotificationIcon.tsx
web/edutwin-web/src/components/notifications/useNotifications.ts
web/edutwin-web/src/layouts/CenterManagerLayout.tsx
web/edutwin-web/src/layouts/PlatformLayout.tsx
web/edutwin-web/src/layouts/StudentLayout.tsx
web/edutwin-web/src/layouts/TeacherLayout.tsx
web/edutwin-web/src/pages/NotificationsPage.tsx
web/edutwin-web/src/types/notifications.ts
web/edutwin-web/src/utils/notificationHelpers.ts
web/edutwin-web/tests/notifications.test.ts
```
