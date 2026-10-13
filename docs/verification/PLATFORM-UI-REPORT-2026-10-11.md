# Chỉnh giao diện Platform Admin và box chuông — 11/10/2026

Phạm vi: theo yêu cầu người dùng, tăng khả năng đọc của Admin ở zoom 100%, chuẩn hóa menu Thông báo và làm rõ hai thao tác trong box ở bốn tác nhân. Bổ sung sidebar theo chế độ sáng/tối sau phản hồi trực tiếp. **Không triển khai observability, không commit/push.**

## Thay đổi

- Sidebar Admin vốn đã có text link Thông báo, nhưng thiếu icon/dấu chọn. Chuẩn hóa Thông báo và Nhật ký hoạt động thành nav item có SVG/active state/aria-current, dùng cùng spacing với các mục khác. Giữ nguyên permission guard và route, không thêm link trùng.
- Typography scoped `.platform-typography`: body 20px; menu/text-sm 17.5px; metadata tối thiểu 15px; các heading tăng khoảng 25%. Sidebar 320px, header 80px; breadcrumb mobile thu gọn/truncate. Không dùng CSS zoom/transform, không thay root font của các vai trò khác.
- Sidebar desktop, mobile drawer, menu, logo labels và khung tài khoản có palette sáng/tối tương ứng; active item vẫn xanh. Không còn sidebar tối cố định khi nội dung sáng.
- Chuông dùng chung `NotificationBell` cho Student/Teacher/Manager/Platform. Đã xem/Xóa tất cả luôn có nhãn/icon/nền/viền, không dùng opacity 40% khiến khó nhận ra. Read-only role thấy nút Đã xem vô hiệu kèm giải thích, không được cấp thêm quyền.
- Khi trống, đang tải hoặc đã xem hết, state có lời giải thích rõ. Đã xem chỉ gửi unread IDs đang hiện. Xóa tất cả vẫn chỉ lưu watermark của box theo center/user trong browser; không gọi hide/delete inbox, không đổi số chưa đọc hay xác nhận nhận. Hiện lại giữ nguyên.
- Portal chuông Admin mang đúng typography scope; không tạo backdrop tối hoặc khóa trang.
- Đề xuất 3 module vận hành được ghi riêng ở [proposal](../plans/PLATFORM-OBSERVABILITY-PROPOSAL-2026-10-11.md), phân biệt hạ tầng có sẵn và phần thiếu, SQL cần/không cần, giới hạn privacy/quota/scale. Đây không phải feature đã triển khai hay cam kết 1.000 người dùng đồng thời.

## File sửa trong lượt này

1. `web/edutwin-web/src/layouts/PlatformLayout.tsx`
2. `web/edutwin-web/src/layouts/platformDesignSystem.css` (mới)
3. `web/edutwin-web/src/components/notifications/NotificationBell.tsx` (đã có từ milestone thông báo, chỉnh tiếp)
4. `web/edutwin-web/src/utils/notificationHelpers.ts` (chỉnh tiếp)
5. `web/edutwin-web/tests/notifications.test.ts` (chỉnh tiếp)
6. `UI_UX_SPEC.md`
7. `docs/plans/PLATFORM-OBSERVABILITY-PROPOSAL-2026-10-11.md` (mới)
8. Báo cáo này; `PROJECT_TRACKING.md` cập nhật checkpoint.

## Kiểm thử tự động

- Frontend cuối sau bổ sung light sidebar: **673 pass, 0 fail, 0 skip**. Hai regression mới bao phủ state chuông/capability ở bốn actors và Admin nav/font/theme scope; test source không thay thế full browser mutation test.
- Lint đầy đủ: 0 error, 26 warning ở file ngoài phạm vi; lượt lint `--quiet` sau sửa theme đạt. Không tự sửa các warning không liên quan.
- TypeScript/Vite source cuối đạt, 970 modules; vẫn có warning vendor MathLive chunk >600kB đã có từ trước. Bundle-budget source cuối **3/3 pass**.
- Không sửa source backend/schema/API/permission/AI provider hay chạy lại backend/SQL trong lượt UI này. Các kết quả backend/SQL của checkpoint trước không được tính là lượt chạy mới.

## Kiểm chứng runtime

- Người dùng xác nhận Chrome zoom 100%; runtime pixel ratio 1. Bản cuối đã recreate **riêng web** với image `ec25556de05ff6e16df198797ae7253308ae7b170f97a15f4546ffd11657cd33`. API giữ nguyên image `ec9356ee8b9849f2f275e7b3df268ab642a4ce32a1641e6880e5930267bbb918`; không restart API/MySQL.
- PlatformAdmin đăng nhập root thật. Desktop menu Thông báo/Nhật ký hoạt động có icon và `aria-current=page` đúng route. Đo DOM: sidebar 320px, main body 20px, nav 17.5px, heading 37.5px.
- Xem bản tối rồi đổi sang sáng: sidebar có computed background `rgb(255,255,255)`; logo/menu/profile/logout sáng đúng palette, active item xanh. Mobile drawer sáng đã mở kiểm tra ở viewport 320px.
- Chuông Admin có Đã xem/Xóa tất cả, font 20px và opacity 1; inbox thật trống nên cả hai vô hiệu đúng quy tắc, hint nói rõ khi nào dùng được. Không gửi dữ liệu giả để bật nút. Popover không overlay tối; Escape đóng.
- Notification/activity kiểm tra viewport 390 và 320; document client/scroll width bằng nhau (375/375 và 305/305 khi có scrollbar). Trang Thông báo wrap nút và tabs; header truncate tiêu đề. Drawer có nhãn/icon và nút đóng. Viewport được reset sau kiểm tra.
- Xem Trung tâm đối tác/Nhật ký kiểm toán ở 1280px: có active menu, typography mới và không tràn ngang trang (1265/1265). Không mở mutation quản trị.
- Bản mới chỉ kiểm browser Admin. Student/Teacher/Manager dùng cùng chuông và có unit/source regression state/capability; **không tuyên bố đã chạy lại full browser của cả ba actors trong lượt này**.

Screenshot tại máy, không đưa vào Git: `C:/Users/ACER/AppData/Local/Temp/edutwin-admin-light-bell-100-20261011.png`, `C:/Users/ACER/AppData/Local/Temp/edutwin-admin-bell-mobile-390-20261011.png`. Đã hiển thị ảnh desktop trong phản hồi cho người dùng.

## Giới hạn

Không gửi thông báo thử mới, không thao tác Đã xem/dọn box trên dữ liệu thật của người khác, không nộp/chấm bài, đổi mật khẩu/quyền/trung tâm hay retry job. Chỉ kiểm tra UI read-only. Backend không đổi; không migration hoặc reset dữ liệu. Chưa nghiệm thu production và chưa đánh giá tải 1.000 học sinh.
