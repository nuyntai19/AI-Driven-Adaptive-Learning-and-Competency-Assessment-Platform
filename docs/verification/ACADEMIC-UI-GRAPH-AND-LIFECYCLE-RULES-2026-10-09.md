# Giao diện học thuật và điều kiện thao tác hiện tại

Ngày 09/10/2026. Tài liệu đối chiếu trực tiếp code đang làm việc, không suy luận nghiệp vụ từ tên lớp. Lượt này chỉ sửa frontend và bổ sung kiểm thử/tài liệu, không sửa API, database, migration hay chấm AI. Không commit/push.

## Giao diện đã sửa

- Báo cáo lớp của Manager có lề 16/24/32 px theo kích thước màn hình; cả trạng thái tải/lỗi dùng cùng lề. Bảng rộng cuộn trong khung, không đẩy toàn trang.
- Bộ lọc “Thư viện giáo trình” chuyển vào cùng `th-page-container` với tiêu đề và danh sách, không còn dính sidebar. Không thêm padding vào toàn layout để tránh làm các trang đã có lề bị giãn gấp đôi.
- Đồ thị thay CSS scale + vùng cuộn bằng camera SVG/viewBox. `Toàn cảnh` bỏ phạm vi tập trung và vừa tất cả nút vào khung. `Vừa khung` vừa phạm vi đang xem. Có thể thu nhỏ dưới 60%, phóng tới 200%, kéo nền, dùng phím mũi tên hoặc bản đồ định vị.
- Tìm tên/mã có danh sách đầy đủ kết quả (cuộn khi dài). Chọn kết quả hoặc bấm `Nút & liên kết gần` để xem nút, các liên kết trực tiếp và nút cha/con thực tế. Không tạo cạnh giả từ quan hệ cha/con; các cạnh chỉ lấy từ dữ liệu server.
- Bộ chọn “Chương / nhóm” lấy các nút Chapter và quan hệ parentNodeId thực tế, không đoán khối từ mã nút. Có thể xem riêng nội dung cốt lõi/chuyên đề nếu các nhóm đó đã được tạo trong đồ thị.
- Thẻ nút có tên tối đa hai dòng, tên đầy đủ vẫn có trong tooltip và nhãn truy cập. Khi xem toàn cảnh nhiều nút, chữ nhỏ là có chủ ý: dùng tìm kiếm/tập trung để đọc, không cố nhét 80 nhãn lớn chồng nhau.
- Khung chi tiết nằm dưới sơ đồ ở màn hình hẹp, bên phải khi đủ rộng. Các quyền tạo/sửa/xóa và hành động sang câu hỏi/bài tập vẫn ở luồng cũ.
- Chuỗi dài/URL trong mô tả và mã nút xuống dòng trong khung chi tiết; grid dùng cột `minmax(0,1fr)` để không kéo trang rộng hơn màn hình. Toolbar được xuống dòng trên màn hình hẹp.
- Sửa lời xác nhận xóa nút: đây là **xóa mềm**, không phải “Xóa vĩnh viễn”; tắt hoạt động không phải cách vượt ràng buộc xóa.

## Giáo trình

| Thao tác | Được phép khi | Bị chặn / không hỗ trợ |
| --- | --- | --- |
| Sửa tên, mô tả, khối, phạm vi Private/Shared | Giáo viên là chủ sở hữu, có quyền API phù hợp, bản Draft, đúng RowVersion | Published/Archived; giáo viên khác chỉ đọc/dùng Shared Published, không sửa bản gốc |
| Thêm/bớt/đổi thứ tự chủ đề | Draft của chính mình; ID hợp lệ, không trùng, các nút hoạt động, cùng môn/trung tâm | Published/Archived khóa danh sách/cấu trúc |
| Xuất bản | Draft của chính mình, đúng phiên bản; chủ đề và lớp dự kiến hợp lệ | Lớp dự kiến không thuộc giáo viên, khác môn/khối, đã lưu trữ, hoặc đã có giáo trình chính đang áp dụng theo luồng xuất bản |
| Áp dụng/thay/ngừng cho lớp | Published; lớp Active/Current do giáo viên thực hiện phụ trách, cùng môn/trung tâm. Thay/ngừng cần lý do; khác/chưa phân khối cần lý do ngoại lệ | Lớp lịch sử/lưu trữ, lớp người khác, giáo trình Draft/Archived. Học sinh không được thay giáo trình |
| Lưu trữ | Chủ sở hữu, quyền phù hợp, đúng phiên bản, chưa Archived; không còn lớp Active/Current đang áp dụng; lý do 1–500 ký tự | Chặn nếu còn bất kỳ lớp hoạt động sử dụng, kể cả lớp giáo viên khác dùng Shared. Lỗi/nạp phụ thuộc chưa xong thì dialog không cho xác nhận |
| Nhân bản | Giáo viên được đọc bản nguồn; bản của mình hoặc Shared Published | Tạo Draft Private mới, không sửa bản nguồn; chỉ sao chép các nút đang hoạt động, không tự sao chép áp dụng lớp |
| Xóa | Chưa có chức năng/API xóa giáo trình | Không có xóa cứng để bỏ dữ liệu cũ; dùng lưu trữ khi thỏa điều kiện |

**Nếu giáo trình có lớp đang học:** không lưu trữ được. Giáo viên phải chủ động thay sang giáo trình khác hoặc ngừng áp dụng với lý do, hoặc Manager kết thúc/lưu trữ lớp theo luồng lớp học khi thực sự phù hợp. Sau đó mới lưu trữ giáo trình. Không tự động chuyển lớp thành lịch sử.

Nếu chỉ còn lớp đã lưu trữ, lưu trữ giáo trình được phép và kết thúc các lượt áp dụng còn mở, ghi người thực hiện/thời điểm/lý do. Không xóa bài làm, điểm, chủ đề hoặc năng lực.

API lưu trữ hiện chấp nhận cả Draft chưa Archived nếu thỏa điều kiện; UI hiện chỉ đưa nút Lưu trữ cho Published. Đây là khác biệt khả năng API/UI hiện có, không khẳng định UI có nút lưu trữ Draft.

## Đồ thị tri thức

Điều kiện nền: đúng trung tâm, trung tâm hoạt động, vai trò Teacher/CenterManager và quyền từng thao tác; ID/giá trị hợp lệ. Sửa có RowVersion để tránh ghi đè phiên khác. Không có thao tác “lưu trữ toàn bộ đồ thị”; có **tắt hoạt động từng nút** và xóa mềm nút/cạnh.

| Thao tác | Điều kiện / ràng buộc |
| --- | --- |
| Tạo nút | Mã không trùng trong môn/trung tâm, loại và giá trị hợp lệ; nút cha cùng phạm vi, không tạo vòng lặp. Nếu khôi phục mã đã xóa mềm, vẫn phải kiểm tra nội dung lịch sử và chu trình |
| Đổi tên/mô tả/nút cha | Không thuộc giáo trình Published hoặc Archived. Nếu đã thuộc, tạo mã nút mới và giáo trình Draft mới, không ghi đè ý nghĩa lịch sử |
| Đổi thứ tự, trọng số thi, thời lượng | Được phép với giá trị hợp lệ; ghi nhật ký. Đây là tham số đồ thị chung, không phải điểm số hay snapshot bất biến |
| Tắt hoạt động | Chặn nếu nút còn trong giáo trình đang được lớp Active/Current dùng, còn nút con hoạt động, câu hỏi Active hoặc lộ trình Active trong phạm vi học hiện tại |
| Xóa nút | Chỉ xóa mềm khi không còn con, cạnh, giáo trình (kể cả lưu trữ), câu hỏi, liên kết câu hỏi–nút, dữ liệu/lịch sử Twin, lộ trình, khuyến nghị hoặc tham chiếu RootCauseNodeIds. Không gỡ tự động các tham chiếu để ép xóa |
| Tạo/khôi phục cạnh | Hai đầu hoạt động, khác nhau, cùng môn/trung tâm; không trùng cùng loại; trọng số 0–1. Cạnh tiên quyết/thuộc về và quan hệ nút cha không được tạo chu trình. Chặn nếu một trong hai đầu thuộc giáo trình đang được lớp hoạt động sử dụng |
| Sửa cạnh | API sửa trọng số, không đổi hai đầu/loại quan hệ tại chỗ. Nếu trọng số thực sự thay đổi, chặn khi một đầu còn được lớp hoạt động dùng |
| Xóa cạnh | Xóa mềm; chặn khi một đầu còn được lớp hoạt động dùng. Không tự xóa cạnh để cho phép xóa nút |

`RelatedTo`/`CausesErrorIn` không được coi là cạnh tiến thứ hạng DAG như `PrerequisiteOf`/`PartOf`. Chế độ tập trung/tìm kiếm/ẩn nút chỉ lọc **hiển thị**, không đổi dữ liệu, quyền hay các ràng buộc trên.

## Lịch sử và giới hạn cần hiểu đúng

- Điều kiện ở server mới là quyết định cuối. Một nút bấm hiện trên UI không có nghĩa thao tác luôn được phép.
- Lưu trữ giáo trình và thay đổi nút/cạnh hợp lệ có nhật ký người thực hiện, thời điểm, trước/sau. Chưa có giao diện nhật ký học thuật tổng hợp hoặc hệ thống thông báo đa tác nhân; đó vẫn là phần riêng đã hoãn.
- Clone giáo trình vẫn tham chiếu các nút cũ; không tự tạo bản nội dung mới của từng nút.
- Chưa có snapshot/version đầy đủ cho cả đồ thị. Nội dung nút Published/Archived được khóa, nhưng tham số và cạnh chung có thể sửa khi không còn lớp hoạt động phụ thuộc. Không gọi đó là bản chụp đồ thị lịch sử bất biến.
- Lượt này không chỉnh trạng thái lớp, lưu trữ/xóa giáo trình thật, tạo/xóa nút thật hoặc gọi AI để kiểm tra.

## Tệp thay đổi trong riêng lượt này

1. `web/edutwin-web/src/pages/CenterClassReportPage.tsx`
2. `web/edutwin-web/src/pages/teacher/TeacherCurriculumListView.tsx`
3. `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx`
4. `web/edutwin-web/src/components/teacher/KnowledgeGraphCanvas.tsx` (mới)
5. `web/edutwin-web/src/utils/knowledgeGraphViewport.ts` (mới)
6. `web/edutwin-web/tests/knowledgeGraphViewport.test.ts` (mới)
7. Tài liệu này (mới)

Các thay đổi khác đang có trong worktree thuộc những lượt trước, không tính vào danh sách trên.

## Kiểm chứng

- Frontend: 630 passed, 0 failed; bổ sung 12 test về vừa khung 96 nút ở 320/768/1240 px, zoom, pan, lân cận, tên truy cập, lề trang và bộ chọn chương thực tế. Nhãn một từ rất dài cũng được giới hạn hai dòng, tên đầy đủ giữ trong tooltip.
- TypeScript/Vite build thành công; cảnh báo chunk MathLive lớn là cảnh báo hiện có.
- ESLint thành công cho component canvas, helper viewport và trang báo cáo Manager.
- Bundle budget: 3 passed. Không thêm thư viện đồ thị mới.
- Build cuối trên Windows và Docker đều thành công; web cục bộ đã dựng/chạy lại từ mã cuối. API và MySQL không khởi động lại trong lượt này.
- `git diff --check` cho các tệp theo dõi đã sửa trong lượt này thành công. Không chạy lại test backend vì không sửa backend; ma trận nghiệp vụ phía trên được đối chiếu code, bộ kiểm thử ràng buộc backend của lượt trước nằm trong báo cáo `ACADEMIC-LIFECYCLE-DEPENDENCIES-2026-10-09.md`.
- Đã dùng skill computer-use kiểm tra Chrome Teacher Math: bộ lọc thư viện nằm trong lề 32px; mở dialog lưu trữ Toán 10 thấy lớp Toán 10 đang sử dụng và nút Xác nhận bị khóa (không lưu thật).
- Đồ thị Toán thực tế 87 nút/69 cạnh: tất cả 87 nút nằm trong khung, không bị cắt ở toàn cảnh. Kiểm tra 320px, 360px, 768px và kích thước mặc định: không tràn ngang. Chọn nhóm Toán 12 cốt lõi: 19 nút/13 cạnh; tìm Tích phân: 5 nút/3 cạnh. Zoom, phím di chuyển, Vừa khung và Escape hoạt động.
- Ảnh minh chứng trong `docs/verification/evidence/knowledge-graph-redesign/`. Chưa hoàn tất kiểm tra trực tiếp Manager tại thời điểm này (đã yêu cầu đổi phiên đăng nhập).
