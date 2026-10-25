import { strict as assert } from "node:assert";
import { test } from "node:test";
import { readFileSync } from "node:fs";
import { notificationAccess, notificationBadge, notificationPollInterval, notificationRoutes, notificationSendAccess, notificationPopoverPosition, notificationDateInput, notificationDateBoundary, notificationContentError } from "../src/utils/notificationHelpers.ts";
import { notificationPreviewStorageKey, notificationPreviewVisible, notificationPreviewWatermark, notificationPreviewActions } from "../src/utils/notificationHelpers.ts";
import { activitySourceLabels, activityTitle, activityDescription, activityStatus, activityTargetLabel } from "../src/utils/activityPresentation.ts";

test("routes isolate four actor workspaces", () => {
  assert.equal(notificationRoutes.Student, "/hoc-tap/thong-bao");
  assert.equal(notificationRoutes.Teacher, "/giao-vien/thong-bao");
  assert.equal(notificationRoutes.CenterManager, "/quan-ly/thong-bao");
  assert.equal(notificationRoutes.PlatformAdmin, "/quan-tri-nen-tang/thong-bao");
});
test("access requires permissions, not account type alone", () => {
  assert.deepEqual(notificationAccess(null), { read: false, update: false, acknowledge: false, reply: false });
  assert.deepEqual(notificationAccess({ accountType: "Teacher", permissions: [] }), { read: false, update: false, acknowledge: false, reply: false });
  assert.equal(notificationAccess({ accountType: "Teacher", permissions: ["notifications.inbox.read_own"] }).read, true);
  assert.equal(notificationAccess({ accountType: "PlatformAdmin", permissions: ["notifications.inbox.read_own"] }).read, false);
  assert.equal(notificationAccess({ accountType: "PlatformAdmin", permissions: ["platform.notifications.read_own"] }).read, true);
});
test("no fake badge for missing, zero, or failed summary", () => {
  assert.equal(notificationBadge(undefined), null);
  assert.equal(notificationBadge(0), null);
  assert.equal(notificationBadge(1), "1");
  assert.equal(notificationBadge(100), "99+");
});
test("polling pauses hidden/offline and auth failure, bounded backoff", () => {
  assert.equal(notificationPollInterval(false), false);
  assert.equal(notificationPollInterval(true, 401), false);
  assert.equal(notificationPollInterval(true, 403), false);
  assert.equal(notificationPollInterval(true), 30_000);
  assert.equal(notificationPollInterval(true, 500, 10, 3000), 123_000);
});
test("requests are cancellable; read-batch only sends visible IDs", () => {
  const api = readFileSync(new URL("../src/api/notificationsApi.ts", import.meta.url), "utf8");
  assert.match(api, /signal/);
  assert.match(api, /deliveryIds: ids/);
  const page = readFileSync(new URL("../src/pages/NotificationsPage.tsx", import.meta.url), "utf8");
  assert.match(page, /filter\(\(item\) => !item.readAt\)/);
  assert.match(page, /Đọc các thông báo đang hiển thị/);
});
test("compose and sent require actor-specific permissions; students never broadcast", () => {
  assert.equal(notificationSendAccess({ accountType: "Student", permissions: ["notifications.messages.send_scoped"] }).send, false);
  assert.equal(notificationSendAccess({ accountType: "Teacher", permissions: [] }).send, false);
  assert.equal(notificationSendAccess({ accountType: "Teacher", permissions: ["notifications.messages.send_scoped"] }).send, true);
  assert.equal(notificationSendAccess({ accountType: "PlatformAdmin", permissions: ["notifications.messages.send_scoped"] }).send, false);
  assert.equal(notificationSendAccess({ accountType: "PlatformAdmin", permissions: ["platform.notifications.send_scoped"] }).send, true);
});
test("popover stays within desktop and narrow viewport bounds", () => {
  for (const width of [320, 355, 768, 1920]) {
    const position = notificationPopoverPosition({ right: width - 50, bottom: 65 }, { width, height: 844 });
    assert.ok(position.left >= 12); assert.ok(position.left + position.width <= width - 12);
    assert.ok(position.top + position.maxHeight <= 844 - 12);
  }
});
test("bell portals outside sticky header and never dims or locks the page", () => {
  const bell = readFileSync(new URL("../src/components/notifications/NotificationBell.tsx", import.meta.url), "utf8");
  assert.match(bell, /createPortal/); assert.match(bell, /document.body/); assert.match(bell, /aria-modal="false"/);
  assert.doesNotMatch(bell, /bg-black|useModalAccessibility|body.style.overflow/);
  assert.match(bell, /pointerdown/); assert.match(bell, /Escape/);
});
test("activity routes and sidebar links exist once per workspace, using distinct permissions", () => {
  const app = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  for (const route of ["/hoc-tap/nhat-ky-hoat-dong", "/giao-vien/nhat-ky-hoat-dong", "/quan-ly/nhat-ky-hoat-dong", "/quan-tri-nen-tang/hoat-dong"])
    assert.equal(app.split(`path="${route}"`).length - 1, 1);
  for (const layout of ["Teacher", "CenterManager"])
    assert.equal(readFileSync(new URL(`../src/layouts/${layout}Layout.tsx`, import.meta.url), "utf8").split('label: "Nhật ký hoạt động"').length - 1, 1);
});
test("notification resource links come from the server and ops retry retains an action ID", () => {
  const page = readFileSync(new URL("../src/pages/NotificationsPage.tsx", import.meta.url), "utf8");
  assert.match(page, /to=\{detail.data.data.relatedLink\}/);
  const ops = readFileSync(new URL("../src/components/notifications/NotificationOperations.tsx", import.meta.url), "utf8");
  assert.match(ops, /clientActionId: actionId/);
  assert.match(ops, /item.state === "DeadLetter" && canRedrive/);
  assert.match(ops, /platform.notifications.redrive/);
});
test("date filters send local-day boundaries as UTC without changing the selected day", () => {
  const from = notificationDateBoundary("2026-10-10")!;
  const until = notificationDateBoundary("2026-10-10", true)!;
  assert.equal(notificationDateInput(from), "2026-10-10");
  assert.equal(notificationDateInput(until), "2026-10-10");
  const startDate = new Date(from); const endDate = new Date(until);
  assert.deepEqual([startDate.getHours(), startDate.getMinutes(), startDate.getSeconds(), startDate.getMilliseconds()], [0, 0, 0, 0]);
  assert.deepEqual([endDate.getHours(), endDate.getMinutes(), endDate.getSeconds(), endDate.getMilliseconds()], [23, 59, 59, 999]);
});
test("date filters clear invalid or missing values", () => {
  for (const input of ["", "invalid", "2026-02-30", "2026-13-10"]) assert.equal(notificationDateBoundary(input), undefined);
  assert.equal(notificationDateInput(), "");
  assert.equal(notificationDateInput("invalid"), "");
});
test("content rejection explains the policy without displaying rejected text", () => {
  const error = { isAxiosError: true, response: { status: 400, data: { errorCode: "NOTIFICATION_CONTENT_RESTRICTED", rejectedText: "do-not-echo" } } };
  assert.match(notificationContentError(error)!, /Nội dung chưa được lưu/);
  assert.doesNotMatch(notificationContentError(error)!, /do-not-echo/);
  for (const other of [null, new Error("unknown"), { isAxiosError: true, response: { data: { errorCode: "VALIDATION_FAILED" } } }]) assert.equal(notificationContentError(other), null);
});

test("clear preview is scoped by account and uses an exact bigint snapshot watermark", () => {
  assert.notEqual(notificationPreviewStorageKey("A", "student01"), notificationPreviewStorageKey("B", "student01"));
  assert.notEqual(notificationPreviewStorageKey("A", "student01"), notificationPreviewStorageKey("A", "teacher01"));
  const mark = notificationPreviewWatermark(["9007199254740993", "8", "7"], null);
  assert.equal(mark, "9007199254740993");
  assert.equal(notificationPreviewVisible("9007199254740992", mark), false);
  assert.equal(notificationPreviewVisible("9007199254740993", mark), false);
  assert.equal(notificationPreviewVisible("9007199254740994", mark), true);
  assert.equal(notificationPreviewWatermark(["3"], mark), mark);
  for (const bad of ["invalid", "-1", "18446744073709551616", "0"]) assert.equal(notificationPreviewVisible("7", bad), true);
  const bell = readFileSync(new URL("../src/components/notifications/NotificationBell.tsx", import.meta.url), "utf8");
  assert.match(bell, /read.mutate\(unreadIds\)/);
  assert.doesNotMatch(bell, /notificationsApi\.(hide|delete)/);
  assert.match(bell, /Xóa tất cả/); assert.match(bell, /Hiện lại/);
});

test("preview actions are consistent across four actors and explain disabled states", () => {
  for (const accountType of ["Student", "Teacher", "CenterManager", "PlatformAdmin"] as const) {
    const prefix = accountType === "PlatformAdmin" ? "platform.notifications" : "notifications.inbox";
    const access = notificationAccess({ accountType, permissions: [`${prefix}.read_own`, `${prefix}.update_own`] });
    const ready = notificationPreviewActions(5, 3, access.update, false);
    assert.equal(ready.readDisabled, false); assert.equal(ready.clearDisabled, false);
    const seen = notificationPreviewActions(5, 0, access.update, false);
    assert.equal(seen.readDisabled, true); assert.equal(seen.clearDisabled, false);
    assert.match(seen.readTitle, /đều đã được xem/);
    const empty = notificationPreviewActions(0, 0, access.update, false);
    assert.equal(empty.readDisabled, true); assert.equal(empty.clearDisabled, true);
    assert.match(empty.hint, /khi có thông báo/);
    const busy = notificationPreviewActions(5, 3, access.update, true);
    assert.equal(busy.readDisabled, true); assert.equal(busy.clearDisabled, true);
    const readOnly = notificationPreviewActions(5, 3, false, false);
    assert.equal(readOnly.readDisabled, true); assert.equal(readOnly.clearDisabled, false);
    assert.match(readOnly.readTitle, /không có quyền/);
  }
  const bell = readFileSync(new URL("../src/components/notifications/NotificationBell.tsx", import.meta.url), "utf8");
  assert.doesNotMatch(bell, /access.update && <button|disabled:opacity-40/);
  assert.match(bell, /Dọn box không xóa hộp thư/);
  for (const layout of ["Student", "Teacher", "CenterManager", "Platform"]) {
    const source = readFileSync(new URL(`../src/layouts/${layout}Layout.tsx`, import.meta.url), "utf8");
    assert.match(source, /<NotificationBell/);
  }
});

test("platform sidebar and larger typography stay permission-scoped without browser zoom", () => {
  const layout = readFileSync(new URL("../src/layouts/PlatformLayout.tsx", import.meta.url), "utf8");
  assert.equal(layout.split('to="/quan-tri-nen-tang/thong-bao"').length - 1, 1);
  assert.match(layout, /hasPermission\(permissions.platformNotificationsReadOwn\)/);
  assert.match(layout, /aria-current=\{isNotificationsActive \? "page" : undefined\}/);
  assert.match(layout, /NotificationIcon className="h-6 w-6 shrink-0"/);
  assert.match(layout, /lg:w-80/); assert.match(layout, /lg:pl-80/);
  assert.match(layout, /bg-white border-r border-slate-200 text-slate-900 dark:bg-slate-900/);
  assert.match(layout, /bg-slate-50 dark:border-slate-800 dark:bg-slate-900\/60/);
  assert.match(layout, /text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300/);
  const css = readFileSync(new URL("../src/layouts/platformDesignSystem.css", import.meta.url), "utf8");
  assert.match(css, /--text-base: 1.25rem/);
  assert.doesNotMatch(css, /(?:^|[;{\s])zoom\s*:|transform\s*:|^\s*(?:html|body|:root)\s*\{/m);
  for (const role of ["Teacher", "CenterManager", "Student"]) {
    const source = readFileSync(new URL(`../src/layouts/${role}Layout.tsx`, import.meta.url), "utf8");
    assert.doesNotMatch(source, /platform-typography|platformDesignSystem/);
  }
});
test("activity labels never fall back to raw technical action or status on primary UI", () => {
  assert.equal(activitySourceLabels.Assessment, "Nộp bài và phân tích");
  assert.equal(activitySourceLabels.TeacherReview, "Giáo viên đánh giá");
  assert.equal(activityTitle("AttemptSubmitted"), "Nộp câu trả lời");
  assert.equal(activityTitle("AssignmentAiReady"), "AI đã xử lý toàn bài");
  assert.match(activityDescription("AIAnalysisCompleted"), /chưa phải xác nhận điểm cuối/);
  assert.equal(activityTitle("NotificationReplyReceived"), "Nhận phản hồi riêng cho thông báo");
  assert.match(activityDescription("NotificationReplyReceived"), /không công khai/);
  assert.equal(activityTitle("NewInternalCode"), "Cập nhật được ghi nhận");
  assert.equal(activityStatus("Approved"), "Đã chốt");
  assert.equal(activityStatus("unknown-private-state"), "Đã cập nhật");
  assert.equal(activityTargetLabel("Attempt"), "Bài làm của học sinh");
  const page = readFileSync(new URL("../src/pages/ActivityPage.tsx", import.meta.url), "utf8");
  assert.match(page, /Thông tin kỹ thuật \(khi cần hỗ trợ\)/);
  assert.match(page, /item.targetLabel \|\| activityTargetLabel/);
  const layout = readFileSync(new URL("../src/layouts/StudentLayout.tsx", import.meta.url), "utf8");
  assert.match(layout, /!isAccountPage && subjects.length/);
  assert.match(layout, /!isAccountPage && <StudentAcademicContext/);
});
test("same-page reply links derive sent view from URL, reset selected message and allow returning to inbox", () => {
  const page = readFileSync(new URL("../src/pages/NotificationsPage.tsx", import.meta.url), "utf8");
  assert.match(page, /const view = params.has\("sent"\) \? "sent" : localView/);
  assert.match(page, /clean.delete\("sent"\)/);
  assert.match(page, /SentInbox key=\{params.get\("sent"\) \?\? "sent-inbox"\}/);
});
