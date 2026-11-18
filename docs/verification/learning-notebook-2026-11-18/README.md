# Learning Notebook — kiểm chứng 18/11/2026

## Thay đổi

- Thêm Sổ tay học tập tại `/hoc-tap/so-tay`, có lối vào trên tổng quan và điều hướng desktop/điện thoại.
- Báo cáo bài làm trong cửa sổ 30 ngày của API; nhóm chủ đề cần củng cố; danh sách tham chiếu câu cần xem lại; lọc đúng/sai/chưa kết luận và tìm không dấu.
- Thư viện bookmark theo môn, tìm/lọc chủ đề, mở đáp án đã công bố. Lượt ôn chủ động tối đa 10 thẻ, tự đánh giá nhớ/ôn lại/bỏ qua, thẻ chưa nhớ quay lại cuối lượt, có màn hình kết thúc.
- Tổ chức lại màn hình luyện tập thành ba quyết định rõ ràng. Giữ phạm vi môn/lớp khi đi từ sổ tay; `minutes=10` thật sự chọn 10 phút; đổi phạm vi xóa gợi ý chủ đề cũ.
- Chặn tạo phiên khi chưa xác minh lớp/giáo trình, phiên đang học hoặc đề xuất; giữ guard tại cả nút và mutation. Kiểm tra capability tạo/dừng/nộp riêng; server tiếp tục là nguồn quyết định.
- Bổ sung lỗi tạo/dừng phiên và lỗi tải lịch sử/phiên; cache sổ tay/tổng quan được làm mới khi phiên đóng.
- Tách tải bộ đếm tập trung; giữ nguyên ngân sách bundle thay vì tăng ngưỡng để vượt kiểm tra.

## Kiểm tra tự động

- `npm --prefix web/edutwin-web test`: **941 pass, 0 fail, 0 skip**.
- `npm --prefix web/edutwin-web run build`: TypeScript và Vite production build.
- `node --test web/edutwin-web/tests/bundle/bundleBudget.test.ts`: **4 pass, 0 fail** sau khi tách bộ đếm.
- `git diff --check`: không có lỗi whitespace.
- MathLive vẫn có cảnh báo vendor chunk lớn; không thuộc phần thư viện vừa thêm. Sổ tay được lazy-load thành route riêng.

## Kiểm tra trực tiếp

Fixture riêng `web/edutwin-web/tests/visual/student-notebook.html` render **StudentLayout thật**, trang sổ tay thật và màn hình luyện tập thật. HTTP adapter chỉ tồn tại trong fixture: trả dữ liệu mẫu và từ chối mọi POST/PUT/DELETE. Không gọi backend hoặc ghi dữ liệu học thuật.

Đã quan sát:

1. Tìm `dao ham` lọc đúng câu Đạo hàm; Chưa có kết luận chứa `isCorrect=null`, có nhãn guided.
2. Liên kết từ câu cần xem lại mở màn hình luyện tập đúng môn/lớp/chủ đề, radio 10 phút được chọn. ArrowRight chuyển sang 15 phút.
3. Mô phỏng tạo phiên lỗi hiển thị thông báo, không mất lựa chọn.
4. Có ba câu đã lưu nhưng chỉ hai câu đã có đáp án được vào lượt ôn.
5. Ôn lại cuối lượt chuyển từ Đạo hàm sang Tích phân và chưa cộng tiến độ; Mình đã nhớ tăng tiến độ lên 1; bỏ qua thẻ còn lại kết thúc với 1 đã nhớ + 1 bỏ qua.
6. View dữ liệu trống hiện “—” cho tỷ lệ đúng và không cho bắt đầu 0 thẻ. View lỗi có nút tải lại. View lịch sử dẫn về hồ sơ, không dựng báo cáo hiện hành.
7. Điện thoại: sáu điểm điều hướng, không ghi nhận tràn ngang (scrollWidth 381 ≤ innerWidth 406 ở viewport kiểm tra). Đã quan sát cả sáng/tối.
8. Laptop và màn hình rộng: khối tổng quan, thẻ thống kê, lời gợi ý và tab sổ tay hiển thị theo bố cục tương ứng. Nhãn điều hướng desktop dùng khoảng cách gọn để thêm điểm Sổ tay.

Ảnh: [tổng quan](overview-desktop.jpg), [lượt ôn](recall-desktop.jpg), [điện thoại tối](library-mobile-dark.jpg). Tất cả ảnh có nhãn dữ liệu mẫu.

## Tái hiện và giới hạn

Chạy frontend bằng `npm run dev -- --host 127.0.0.1` trong `web/edutwin-web`, mở `/tests/visual/student-notebook.html` tại cổng Vite trả về. Thêm `?state=error`, `?state=empty` hoặc `?history=true` để kiểm tra các nhánh tương ứng; `?surface=practice` mở bộ chọn phiên.

Đây là kiểm thử frontend với fixture, không phải bằng chứng nộp bài/chấm điểm/tổng hợp dữ liệu trên MySQL thật. Không sửa schema, thuật toán chấm, Evidence Gate hoặc quyền server. Bộ tự đánh giá thẻ ôn chưa đồng bộ bền vững và không lên lịch nhắc ôn qua ngày. Dữ liệu câu xem lại bị giới hạn đúng theo API hiện có. Không chạy lại toàn bộ .NET suite cho đợt chỉnh frontend này.
