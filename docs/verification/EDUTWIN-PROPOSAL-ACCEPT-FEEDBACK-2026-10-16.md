# Phản hồi khi chọn tuyến học — 16/10/2026

## Phát hiện

Nút chọn tuyến gọi API, nhưng khi có nhiệm vụ đang làm/chờ xử lý, API trả 409. Thông báo lỗi nằm ở đầu vùng cuộn trong khi nút ở cuối bản xem trước, nên người dùng không thấy lỗi và tưởng thao tác không có tác dụng. Kiểm tra chỉ đọc local xác nhận còn các lượt InProgress, Grading và NeedsReview.

## Sửa

- Giữ nhóm nút chọn/điều chỉnh ở footer ngoài vùng cuộn bản xem trước. Lỗi xuất hiện ngay trên các nút.
- API trả `LEARNING_PATH_CHANGE_BLOCKED` và danh sách buổi của chính học sinh trong đúng trung tâm/môn, gồm tên, mã buổi/lượt và trạng thái.
- Giao diện phân biệt đang làm, chờ chấm và chờ giáo viên; có đường mở đúng nhiệm vụ hiện tại khi thuộc engine nhiệm vụ hỗ trợ.
- Trong lúc lưu/cập nhật, có trạng thái đang xử lý và chặn bấm lặp. Khi thành công, làm mới agenda/map, đóng preview và hiện xác nhận trên trang.
- Không bỏ các buổi đang dở/chờ duyệt hoặc tự cấp kết quả để ép chuyển tuyến. Điều kiện chuyển tuyến được giữ, bản nháp và bài làm không bị xóa.

## Kiểm thử bổ sung

Kiểm tra ba trạng thái chặn, giữ tuyến/mục tiêu/bản nháp khi bị chặn, chuyển được sau khi hoàn tất buổi học, phản hồi API có tên/trạng thái và không lộ cho tài khoản khác. Trên trình duyệt, kéo preview xuống cuối rồi chọn tuyến khi đang có bản nháp: lỗi phải còn trong màn hình và mở lại được đúng bản nháp.

Nhật ký chạy đợt này dùng tiền tố `scratch/v05-accept-*`.

- Backend planner/execution: 24/24 đạt, gồm các ca chặn theo trạng thái và chuyển tuyến sau khi hoàn tất hoạt động.
- Frontend: 760/760 đạt, build production thành công. Lint có 0 lỗi và 29 cảnh báo hiện có.
- Khi đồng thời dựng image và khởi tạo database thử, migration thử bị timeout. Đã dọn đúng stack/database sở hữu và chuyển sang dựng lần lượt trước khi tạo lại môi trường kiểm tra. Không thay đổi hoặc xóa database chính để xử lý lỗi môi trường thử.
- HTTP/MySQL trên image cuối: 11 nhóm đạt, gồm chặn chuyển tuyến có danh sách buổi đúng chủ sở hữu và không trả metadata cho tài khoản/trung tâm khác.
- Browser trên image cuối: đạt. Có xác nhận khi chọn tuyến thành công; kéo xuống cuối preview rồi chọn khi có buổi đang dở vẫn thấy lỗi ngay trên nút; nút tiếp tục mở đúng cách hiểu/bản nháp đã lưu. Ảnh kiểm tra: `storage/v4-browser-evidence/proposal-blocked-visible.png`.
- Đã cập nhật local API/web, không có migration mới. Xác nhận image mới, web 200, API bảo vệ 401 khi chưa đăng nhập. Users/attempts/evidence/history trước và sau cập nhật giữ nguyên 17/19/19/19; có sao lưu trước cập nhật.
