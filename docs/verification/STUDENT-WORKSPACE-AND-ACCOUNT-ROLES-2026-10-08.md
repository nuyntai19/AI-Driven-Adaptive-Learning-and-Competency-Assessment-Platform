# Bộ đếm bài tập, chuỗi ngày học và vai trò tài khoản mới — 08/10/2026

## Nguyên nhân và bản sửa

- Thanh điều hướng học sinh từng ghi cố định số bài tập `3` và `Chuỗi 7 ngày`
  trong cả giao diện desktop/mobile. Thay bằng API riêng
  `GET /api/v1/students/me/workspace-summary?subjectId=...`.
- Số bài tập là tổng bài được giao nhìn thấy trong danh sách **Tất cả**, theo môn
  đang chọn, bao gồm bài đã hoàn thành. Không tính bản nháp/bài đã xóa và không
  lấy độ dài của trang đầu làm tổng. Streak dùng toàn bộ môn của chính học sinh.
- Một ngày học được ghi nhận khi có ít nhất một câu đã nộp không bị bỏ qua,
  lấy `Attempt.CreatedAt` theo múi giờ trung tâm, không lấy giờ AI chấm xong hoặc
  giờ đăng nhập. Nhiều câu cùng ngày chỉ tính một ngày. Nếu hôm nay chưa học,
  chuỗi kết thúc hôm qua được giữ đến hết hôm nay; thiếu cả hôm qua thì chuỗi về 0.
- Truy vấn nhóm tối đa 64 ngày địa phương mỗi lượt, trả một số nguyên cho mỗi
  ngày có hoạt động; không tải toàn bộ bài làm lên giao diện. Chuỗi dài tiếp tục
  đọc cửa sổ trước, không bị giới hạn ở 64 ngày. Ranh giới UTC từng ngày xử lý DST.
- Cache riêng theo trung tâm/học sinh/môn. Cập nhật sau khi server nhận bài,
  khi quay lại tab và mỗi phút khi tab đang hoạt động. Loading/lỗi không giả lập
  số bài hay ngày học. Không thêm polling riêng cho từng câu hoặc gọi Gemini.
- Luồng tạo giáo viên/học sinh trước đây gán `User.RoleName` nhưng thiếu
  `UserRoleAssignment` của phân quyền động. Login đọc vai trò động nên tài khoản
  mới có thể đăng nhập thành công nhưng không có quyền mở trang giáo viên.
- Luồng tạo mới nay gán vai trò hệ thống đang hoạt động đúng loại tài khoản và
  đúng trung tâm, cùng lần lưu/transaction với tài khoản và hồ sơ. Có audit
  `UserSystemRoleAssigned`; không sửa ma trận quyền, không khôi phục vai trò đã
  bị thu hồi của người khác. Nếu thiếu vai trò mặc định hợp lệ thì không tạo
  tài khoản không sử dụng được. Không bỏ filter tenant hoặc kiểm tra quyền API.
- Tạo trung tâm/quản lý trung tâm và provision admin đã có luồng bootstrap vai
  trò riêng. Không tự cấp quyền quản lý/admin cho giáo viên hoặc học sinh.

## Khôi phục tài khoản local được người dùng chỉ định

Đã xác minh đúng một giáo viên mới ở trung tâm được báo cáo: tài khoản Active,
không có assignment vai trò và không có audit thay vai trò thủ công. Vai trò
Teacher của trung tâm đã tồn tại với 26 quyền.

Lưu backup đầy đủ local trước khi khôi phục; transaction chỉ thêm một assignment
Teacher và audit `UserSystemRoleRecovered`, tăng AuthVersion/RowVersion và thu hồi
refresh token của đúng tài khoản đó. Kết quả: 1 vai trò, 26 quyền, AuthVersion 2.
Không đổi mật khẩu, bài làm, ma trận quyền hoặc dữ liệu người khác. Script và
backup chỉ ở `storage/`, bị git-ignore; không đưa danh tính/credential vào báo cáo.
Người dùng cần đăng xuất/đăng nhập lại tài khoản bị ảnh hưởng.

## Kiểm tra

- Test đơn vị: ngày trống, 0 bài, nhiều câu cùng ngày, sát nửa đêm UTC+7,
  câu bỏ qua, thời điểm tương lai, học sinh/trung tâm khác, chuỗi bị ngắt,
  chuỗi 70 ngày, DST, bộ đếm theo môn và trạng thái xuất bản.
- Test phân quyền: tài khoản mới có quyền mặc định ngay lập tức, không có quyền
  quản lý/admin; không dùng vai trò trung tâm khác, vai trò thiếu/archived;
  không khôi phục assignment đã bị thu hồi của người khác.
- Test MySQL opt-in dùng database tạm: SQL tổng hợp ngày và bài tập, tạo
  giáo viên/học sinh cùng vai trò/audit, kiểm tra duplicate không tạo assignment thừa.
- Frontend: 565 test passed, 0 failed; TypeScript/Vite build passed.
- Backend toàn bộ: 3.917 passed, 0 failed, 73 skipped (các integration test opt-in).
- Test MySQL nói trên đã chạy lại với binary cuối: 1 passed, 0 failed, 0 skipped.
  Database tạm được dọn sau test; không gọi nhà cung cấp AI.
- Docker API/web đã build và cập nhật bằng đúng image mới. Trước cập nhật,
  hàng đợi không có job Pending/Processing; chỉ recreate API/web, không đụng MySQL.
- Kiểm tra runtime: API readiness `Healthy`, MySQL `Healthy`, web HTTP 200;
  endpoint summary trả HTTP 401 khi không đăng nhập (không mở dữ liệu công khai).
- `git diff --check` passed. Chưa kiểm tra lại giao diện bằng phiên đăng nhập
  của người dùng; cần refresh học sinh và đăng xuất/đăng nhập lại giáo viên.

Không thêm migration/schema và không reset database/volume. Chưa commit/push
trong lượt sửa này.
