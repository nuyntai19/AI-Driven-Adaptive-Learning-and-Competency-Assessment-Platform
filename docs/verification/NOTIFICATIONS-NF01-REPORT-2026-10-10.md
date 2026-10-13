# Checkpoint thông báo NF-01 — 2026-10-10

Baseline `61ef80a`; người dùng yêu cầu triển khai sau khi xem kế hoạch. Đặc tả khóa tại [NF-01 SPEC](../plans/NOTIFICATIONS-NF01-SPEC.md). **Không commit/push.** Trạng thái: nền tảng đã triển khai local và đạt gate tự động; giao diện trống Teacher Math đã kiểm tra trực tiếp. Chưa nghiệm thu toàn feature hoặc toàn bộ giao diện bốn tác nhân.

Đính chính UI sau phản hồi người dùng: QA trống/ESC phía dưới không bao phủ lỗi nền tối bị giới hạn trong sticky header ở bản NF-01. Lượt tiếp theo thay popup bằng popover portal không overlay và ưu tiên core soạn/gửi theo [NF-03a SPEC](../plans/NOTIFICATIONS-NF03-SEND-SPEC.md). Báo cáo này giữ bằng chứng lịch sử NF-01, không đại diện kết quả UI hiện hành.

## Đã thay đổi

1. Khóa amendment phạm vi/schema/API/quyền/UI trên root specifications và kế hoạch; giữ monolith, không transport/AI mới.
2. Thêm event bất biến, dispatch queue riêng có lease/row-version/retry, delivery unique theo người nhận; SQL giữ receipt/history. Migration `20261010072401_AddNotificationInboxFoundation` additive ba bảng và bốn permission; mô hình EF từ 48 lên 51 entity/table (kiểm kê snapshot), không tạo database ứng dụng khác.
3. Publisher nội bộ chỉ stage cùng unit-of-work, không tự commit; chưa gọi từ các nghiệp vụ. Worker nhận snapshot bounded, chỉ phát tài khoản active cùng trung tâm; fan-out/terminal commit chung và chống trùng/lost lease.
4. API inbox/summary/detail/read/read-batch own scope. Không nhận sender/user/tenant tùy ý, không lộ danh sách người nhận, không trả audit raw hoặc arbitrary link. Read snapshot bảo toàn thông báo đến sau và thời điểm đọc đầu.
5. Chuông dùng chung bốn layout; bỏ chấm đỏ giả học sinh; Teacher/Manager/Platform thêm sidebar Thông báo; inbox responsive dùng chung, focus/ESC, loading/error/empty khác nhau, polling summary 30 giây có pause/backoff. Chỉ có read receipt, không tự chốt điểm hoặc xử lý yêu cầu học thuật.
6. Giữ nguyên quyền học thuật của Manager ở trạng thái cấm. Platform own inbox dùng permission riêng không ủy quyền. Role hệ thống additive, không khôi phục role assignment revoked và không tự grant custom role.

## Kiểm chứng đã có

- Backend build: 0 error/0 warning.
- 30 test foundation/filter/endpoint policy pass; test MySQL skip trong lượt unit rồi chạy riêng.
- **6/6 MySQL isolated pass, 0 skip**, 2 phút 35 giây: transaction rollback, unique/composite FK, concurrent dispatch, lease recovery, immutable event/read receipt, cross-tenant fail-closed, actual background discovery và model không drift.
- Frontend **655 pass/0 fail** (gồm 5 test mới); build TypeScript/Vite pass; bundle budget **3/3 pass**; lint các file sửa 0 error/0 warning.
- Lượt full backend đầu: 4048 pass, 7 fail do các assertion số quyền baseline; đã sửa kỳ vọng 71→75 catalog, 80→88 mappings và system-role counts (+2 mỗi tác nhân). Một lượt chạy lại bị khóa DLL bởi testhost SQL đang chạy trên Windows; đã chờ SQL suite kết thúc và chạy lại tuần tự.
- **Full backend cuối: 4.055 pass, 0 fail, 85 integration skip (tổng 4.140)**; sáu ca notification SQL đã chạy riêng 6/6 pass, không gộp skip thành pass. Các assertion bảo vệ revoked/custom role/LastTenantAdmin và quyền chéo tác nhân giữ nguyên.

## Sao lưu / bảo toàn

Backup local trước migration: `2026-10-10_before_math_repair_4a9e6cba6be747699779935816076ef1.sql` ở thư mục riêng `%TEMP%/EduTwin-backups`, 3.364.827 byte; SHA256 `bfd01bf094eb3c2ee4aab8abb94bacce52ed81e112d106cb9e24707e25702f26`. Tên script backup lịch sử có “math_repair”, không có thao tác sửa/chấm lại toán trong checkpoint này. Backup không vào Git.

Trước triển khai: 42 migration, 212 attempts, 5 behavior twins, 10 assignments; orphan analysis/evidence/history/path/knowledge đều 0; FK checks=1, trigger evidence append-only còn nguyên. Không sửa .env, key, AI queue/model/quota, câu trả lời, điểm hay kết quả Test 4–9.

## Runtime và giao diện sau triển khai local

- Build lại riêng API/web rồi `up -d --no-deps api web`; không tạo lại MySQL hoặc volume. Startup áp dụng migration additive và backfill permission cho role hệ thống theo cơ chế hiện có.
- Sau triển khai: **43 migration, 212 attempts, 5 behavior twins, 10 assignments**. Orphan analysis/evidence/history/path/knowledge đều 0; FK checks=1, trigger evidence append-only=1. Ba bảng mới có **0 event, 0 delivery**, bốn trigger notification hiện diện; không còn schema kiểm thử notification tạm.
- API/web đang chạy đúng image mới: API `sha256:ab2da92e7dfaf4b451dd8dabcf3ea77ce266d240b8aea4dfe1dfe9c9067582b1`, web `sha256:e3bf30afba025be21114d3652fd2fcdb26e540763c8576b61b9b4616afc1f204`. Readiness qua web `/api/v1/health/ready`: HTTP 200, `status=Healthy`, `mysql=Healthy`.
- Phiên Chrome **Teacher Math / EDUTWIN_A**: menu Thông báo hiện đúng; chuông không có badge giả; popup và trang `/giao-vien/thong-bao` trả trạng thái “Chưa có thông báo”, không lỗi quyền. Danh sách bài tập Test 4–9 vẫn hiện.
- Kiểm tra responsive bằng override 390×844; CSS viewport thực 355 px theo mức zoom của trình duyệt. Document width=viewport width=355 px, không tràn ngang; popup nằm từ x≈16 tới x≈339, Escape đóng popup. Đã reset override sau kiểm tra.
- Đây là QA trạng thái trống, không phải E2E nhận/đọc thông báo có dữ liệu thật. Không tạo thông báo demo cho tài khoản thật để làm đẹp bằng chứng.

## Kiểm kê thay đổi — 49 file

49 file text, 0 binary; scan credential không có finding, `.env` vẫn ignored. `git diff --check` đạt (Git chỉ cảnh báo chuẩn hóa LF/CRLF theo cấu hình hiện có). Không thêm package hoặc sửa Docker Compose. Migration Designer và model snapshot chứa mô hình đầy đủ do EF sinh, không đồng nghĩa sửa logic của tất cả các bảng.

| Nhóm | Số file |
| --- | ---: |
| Root specifications/tracking | 7 |
| Kế hoạch, ADR và báo cáo | 4 |
| Script kiểm tra vận hành | 1 |
| API | 4 |
| BLL | 5 |
| Contracts | 1 |
| DAL/migration | 9 |
| Backend tests | 5 |
| Web frontend/tests | 13 |

Danh sách đầy đủ theo đường dẫn từ root repository:

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
docs/verification/NOTIFICATIONS-NF01-REPORT-2026-10-10.md
scripts/ops/run_notification_checks.cjs
src/EduTwin.API/Controllers/NotificationsController.cs
src/EduTwin.API/Notifications/NotificationBackgroundService.cs
src/EduTwin.API/Program.cs
src/EduTwin.API/Security/CompositePermissionPolicies.cs
src/EduTwin.BLL/Notifications/DependencyInjection.cs
src/EduTwin.BLL/Notifications/NotificationDispatcher.cs
src/EduTwin.BLL/Notifications/NotificationInboxService.cs
src/EduTwin.BLL/Notifications/NotificationOutbox.cs
src/EduTwin.BLL/Notifications/NotificationRecipientScope.cs
src/EduTwin.Contracts/Notifications/NotificationContracts.cs
src/EduTwin.DAL/Notifications/NotificationDelivery.cs
src/EduTwin.DAL/Notifications/NotificationDispatch.cs
src/EduTwin.DAL/Notifications/NotificationEvent.cs
src/EduTwin.DAL/Persistence/Configurations/Notifications/NotificationConfigurations.cs
src/EduTwin.DAL/Persistence/EduTwinDbContext.cs
src/EduTwin.DAL/Persistence/Migrations/20261010072401_AddNotificationInboxFoundation.cs
src/EduTwin.DAL/Persistence/Migrations/20261010072401_AddNotificationInboxFoundation.Designer.cs
src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs
src/EduTwin.DAL/Seeding/AuthorizationPermissionCatalog.cs
tests/EduTwin.BLL.Tests/IdentityAndTenancy/AuthorizationBootstrapperTests.cs
tests/EduTwin.BLL.Tests/IdentityAndTenancy/GlobalQueryFilterTests.cs
tests/EduTwin.BLL.Tests/IdentityAndTenancy/ListPermissionsUseCaseTests.cs
tests/EduTwin.BLL.Tests/Notifications/NotificationFoundationTests.cs
tests/EduTwin.BLL.Tests/Notifications/NotificationMySqlTests.cs
web/edutwin-web/src/App.tsx
web/edutwin-web/src/api/notificationsApi.ts
web/edutwin-web/src/auth/permissions.ts
web/edutwin-web/src/components/notifications/NotificationBell.tsx
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

## Chưa làm / không hiểu nhầm

- NF-02 chưa nối sự kiện giao/nộp/AI hoàn tất/review/chốt điểm/lớp/giáo trình. Hộp thư thật vì vậy **trống**, không backfill hoặc thêm demo cho người dùng thật.
- NF-03 chưa có soạn/gửi, preview recipients, đã gửi, thu hồi/đính chính/xác nhận/reply; chưa liên tenant Admin–Manager.
- NF-04 chưa có adapter nhật ký chung/menu mới Teacher/Manager; Platform giữ nhật ký kiểm toán đã có.
- Chưa benchmark 50/100 câu trước–sau; chưa khẳng định tốc độ AI bằng số đo mới. Worker thông báo không gọi Gemini và source AI không bị sửa.
- Đã kiểm tra visual trạng thái trống Teacher Math; Student/Manager/Platform và đổi account/mất mạng/403 trên UI thật còn cần nghiệm thu. Production retention và re-drive dead-letter còn cần gate vận hành, không quảng cáo production-ready.

Trước khi tiếp tục NF-02 cần người dùng xem checkpoint này. Không dùng “đã làm thông báo” để tuyên bố hoàn thành toàn hệ thống gửi chủ động/nhật ký.
