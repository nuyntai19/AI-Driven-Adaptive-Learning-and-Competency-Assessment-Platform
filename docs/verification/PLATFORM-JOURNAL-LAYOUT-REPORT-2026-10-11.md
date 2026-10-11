# Nhật ký thống nhất và bố cục Admin — 11/10/2026

Phạm vi người dùng phê duyệt: gom hai mục Nhật ký hoạt động/Nhật ký kiểm toán thành một mục Nhật ký với hai phần; mở rộng nội dung Admin và giảm khoảng trống cạnh sidebar. Báo cáo này ghi riêng phần tiếp nối sau [bản chỉnh typography/chuông](PLATFORM-UI-REPORT-2026-10-11.md).

## Kết quả

- Sidebar desktop/mobile có một mục **Nhật ký**. Trang có hai tab liên kết **Tổng quan** và **Kiểm toán chi tiết**, chọn theo URL và hỗ trợ Back/Forward.
- Tổng quan tiếp tục dùng nguồn activity; Kiểm toán giữ API riêng. Hai nguồn khác phạm vi, không cộng/gộp bản ghi. Mỗi route giữ permission `platform.activity.read` hoặc `platform.audit.read`, cùng ràng buộc PlatformAdmin. Tab/menu hiện theo quyền; quyền này không cấp thay quyền kia.
- `/quan-tri-nen-tang/hoat-dong` chuyển đến `/quan-tri-nen-tang/nhat-ky/tong-quan`; URL kiểm toán cũ `/quan-tri-nen-tang/nhat-ky` chuyển đến `/quan-tri-nen-tang/nhat-ky/kiem-toan`. Link xem nhật ký từ Trung tâm tiếp tục mở kiểm toán. Chỉ trang con đang chọn được mount/tải dữ liệu.
- Admin sử dụng chiều rộng còn lại sau sidebar320px, gutter16px mobile/24px desktop. Bỏ max-width1280px ở Trung tâm/Kiểm toán; bỏ padding lồng ở Thông báo Admin và Tổng quan nhúng. Vai trò khác giữ bố cục mặc định cũ.
- Bảng kiểm toán dùng cột ổn định, cuộn ngang trong khung ở màn hình nhỏ. Nhãn hành động/tài nguyên bằng tiếng Việt; mã kỹ thuật đầy đủ còn trong Chi tiết. Modal cho phép xuống dòng các mã dài.

## File thay đổi trong phạm vi này

Đường dẫn tương đối từ repository; các file đã có thay đổi ở milestone trước được chỉnh tiếp, không phải toàn bộ nội dung trong Git diff đều thuộc lượt này.

| File | Nội dung |
| --- | --- |
| `web/edutwin-web/src/App.tsx` | Wrapper Nhật ký, route con có guard riêng, redirect URL cũ |
| `web/edutwin-web/src/layouts/PlatformLayout.tsx` | Một mục Nhật ký, breadcrumb, gutter thống nhất |
| `web/edutwin-web/src/pages/PlatformJournalPage.tsx` (mới) | Tiêu đề, tab theo quyền/URL, Outlet |
| `web/edutwin-web/src/utils/platformJournalNavigation.ts` (mới) | Hai đường dẫn và lọc tab theo quyền/tác nhân |
| `web/edutwin-web/src/pages/ActivityPage.tsx` | Chế độ nhúng không lồng padding/heading cấp1 |
| `web/edutwin-web/src/pages/NotificationsPage.tsx` | Admin dùng gutter của layout |
| `web/edutwin-web/src/pages/PlatformCentersPage.tsx` | Bỏ giới hạn chiều rộng |
| `web/edutwin-web/src/pages/PlatformAuditLogsPage.tsx` | Bố cục rộng, bảng/cuộn, nhãn và chi tiết responsive |
| `web/edutwin-web/src/utils/platformPresentation.ts` | Nhãn tiếng Việt và fallback dễ hiểu |
| `web/edutwin-web/tests/platformJournal.test.ts` (mới) | 7 ca hồi quy route/quyền/shell/menu/bố cục |
| `UI_UX_SPEC.md` | Phạm vi được duyệt trước triển khai |
| `PROJECT_TRACKING.md` | Checkpoint thực tế |
| Báo cáo này (mới) | Bằng chứng, file và giới hạn |

## Kiểm thử tự động

- Full frontend: **680 pass, 0 fail, 0 skip**. Ca mới kiểm tra quyền riêng, actor khác, shell React thực, tab chọn theo URL, guard của route mới/cũ (đọc AST), fallback nhãn và bố cục.
- ESLint `--quiet`: đạt, không lỗi. Không tuyên bố loại bỏ warning sẵn có.
- TypeScript/Vite: đạt, **972 modules**. Warning vendor MathLive chunk >600kB vẫn có từ trước.
- Bundle budget: **3/3 pass**. `git diff --check`: đạt; Git chỉ cảnh báo chuẩn hóa CRLF.
- Không sửa backend/schema/API/permission trong phạm vi này; không chạy lại backend/SQL và không lấy kết quả cũ làm bằng chứng mới.

## Kiểm chứng giao diện thật

Skill computer-use được dùng để xem trên Chrome với tài khoản PlatformAdmin do người dùng đăng nhập. Kiểm tra chỉ đọc ở chế độ sáng, zoom100%/DPR1.

- Desktop1920px (client1905px): nội dung bắt đầu x=344, rộng **1537px**, gutter cạnh sidebar **24px**. Bảng client/scroll đều1535px; nút Chi tiết ở cột cuối nằm trong khung. Không tràn ngang trang.
- Sidebar có một mục Nhật ký. Chuyển Tổng quan/Kiểm toán và Back chọn đúng tab. Tổng quan có1 hoạt động, Kiểm toán có6 bản ghi trước/sau cập nhật; khác số lượng do khác phạm vi nguồn.
- Hai URL cũ và link “Xem tất cả nhật ký” từ Trung tâm chuyển đúng phần. Chi tiết bản ghi#58 còn mã hành động, Trace ID, đối tượng và before/after; đã mở/đóng để xác minh.
- 1280px: client/scroll trang1265/1265; bảng895/1100 cuộn trong vùng riêng. Trung tâm rộng897px, bắt đầu x=344. Thông báo không có padding lồng (computed0px), rộng912px khi không có scrollbar.
- 390px: client/scroll375/375; gutter16px; bảng341/1100. 320px: client/scroll305/305; bảng271/1100; hai tab xuống dòng vẫn dùng được. Tổng quan320px rộng273px, không tràn ngang.
- Không có console error được ghi trong phiên kiểm tra. Đã reset viewport về1920px và giữ tab Nhật ký mới để người dùng xem.
- Screenshot local: `C:/Users/ACER/AppData/Local/Temp/edutwin-admin-unified-journal-100-20261011.png` (đã hiển thị trong chat), `C:/Users/ACER/AppData/Local/Temp/edutwin-admin-journal-mobile-390-20261011.png`. Không đưa ảnh/payload thật vào Git.

## Rollout và giới hạn

Build/recreate **riêng web**; image runtime `sha256:ddb0d0cb53f46e561d5f4ae68b391a07b052463b7182849d0c99bbf30d1ab045`. API giữ image `sha256:ec9356ee8b9849f2f275e7b3df268ab642a4ce32a1641e6880e5930267bbb918`; API/MySQL đang running, không restart hoặc đổi volume.

Không gửi thông báo, đổi quyền, sửa trung tâm/bài làm/điểm hoặc tạo dữ liệu thử. Không thêm bảng/migration. Không kiểm lại toàn UI ba tác nhân khác hoặc nghiệm thu production trong lượt này. Observability/token–cost/backlog/storage vẫn là đề xuất riêng. **Chưa stage, commit hoặc push.**
