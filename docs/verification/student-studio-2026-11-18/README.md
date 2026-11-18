# Learning Studio — bằng chứng kiểm tra 18/11/2026

## Kết quả

- `npm test`: **932 pass, 0 fail, 0 skip**.
- `npm run build`: TypeScript và Vite production build thành công.
- `node --test tests/bundle/bundleBudget.test.ts`: **4 pass, 0 fail**.
- `git diff --check`: sạch.
- Vite còn cảnh báo kích thước vendor MathLive (~825 kB minified). Kiểm tra budget về preload và tách chunk vẫn đạt; chưa tuyên bố đã giải quyết kích thước thư viện.

## Kiểm tra trực tiếp trong trình duyệt

Render component sản phẩm với fixture biệt lập có nhãn **DỮ LIỆU MẪU**. Không đăng nhập tài khoản thật hoặc thay đổi dữ liệu học thuật.

- Bố cục desktop: khối nhiệm vụ + hạn nộp, kế hoạch/ưu tiên/mục tiêu trước biểu đồ; thẻ khám phá và hành trình.
- Bố cục điện thoại: nội dung xếp cột, không ghi nhận tràn ngang trong fixture (scrollWidth ≤ innerWidth).
- Góc tập trung: mở dialog, chọn 10 phút (aria-pressed cập nhật, đồng hồ 10:00), bắt đầu, tạm dừng (24:55 ở lượt 25 phút), thu gọn/mở lại, Escape đóng dialog và trả focus về nút mở.
- Thẻ ôn: nhấn Đối chiếu đáp án hiển thị đáp án và lời giải từ fixture; nút chuyển thành Ẩn đáp án.
- Có ảnh [desktop](desktop.jpg), [điện thoại](mobile.jpg), [bộ đếm điện thoại](focus-mobile.jpg).

Các ảnh dùng header fixture đơn giản; không phải bằng chứng nghiệm thu header tài khoản thật, mọi route hoặc backend/MySQL. Chưa chạy lại .NET suite vì đợt thay đổi không sửa backend. Dark mode giữ token và hỗ trợ trong CSS, chưa có ảnh QA riêng cho mọi trang ở dark mode.

## Tái hiện

Từ `web/edutwin-web`, chạy `npm run dev -- --host 127.0.0.1`, mở `/tests/visual/student-studio.html` tại cổng Vite trả về. Fixture nằm trong `tests/visual/`, ngoài application entry và production bundle. Đây là preview giao diện, các liên kết chỉ để kiểm tra URL đúng ngữ cảnh, không phải backend giả để làm bài.

Báo cáo sản phẩm: [STUDENT-EXPERIENCE-AUDIT](../../STUDENT-EXPERIENCE-AUDIT-2026-11-18.md).
