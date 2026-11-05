# Kiểm chứng phạm vi đồ thị tri thức theo khối, lớp và môn học

## Luồng giáo viên

- Trang đồ thị không còn hiển thị đồ thị gốc chưa phân khối. Thứ tự chọn là **Khối → Năm học → Lớp phụ trách → Giáo trình**. Đồ thị chỉ nạp sau khi chọn lớp hoặc khi mở trực tiếp phiên bản Draft từ giáo trình.
- Giáo viên chỉ thấy môn có ít nhất một lớp do mình phụ trách. Bộ lọc này áp dụng tại API danh sách và chi tiết môn, đồ thị, giáo trình và ngân hàng câu hỏi; quyền sở hữu/Shared của tài liệu vẫn được kiểm tra tiếp theo.
- Giáo trình Draft có nút **Chỉnh sửa khung tri thức Khối N**. Nút mở phiên bản Draft của giáo viên nếu đã có; nếu không, tạo Draft từ bản Published đang ghim (hoặc bản hiện hành). Sau khi xuất bản đồ thị mới, giáo viên quay lại giáo trình Draft, chọn phiên bản Published mới và lưu. Không sửa topology của Published tại chỗ.
- Khi soạn Draft, danh sách chủ đề ưu tiên nút thuộc khối đang chọn hoặc nút chưa phân loại; nút đã có trong Draft vẫn hiện để rà soát. Nút tạo liên kết mới chỉ lấy hai đầu mút đã nằm trong cùng Draft.
- Danh mục nút/cạnh gốc vẫn tồn tại bên trong cơ sở dữ liệu để soạn phiên bản Draft và bảo toàn lịch sử; không còn là một đồ thị tổng hợp hiển thị trên trang giáo viên.

## Dữ liệu mẫu bổ sung

| Môn, khối | Chủ đề trong phiên bản hiện hành mới | Câu hỏi mới / trung tâm |
| --- | --- | ---: |
| Toán 10 | Hàm số; Hàm số và phương trình bậc hai; Vectơ và tọa độ trong mặt phẳng | 2 |
| Toán 11 | Mũ–Logarit; Hàm số lượng giác; Phương trình lượng giác | 2 |
| Toán 12 | Ứng dụng đạo hàm khảo sát hàm số; Bài toán cực trị và tối ưu; Nguyên hàm | 2 |
| Tiếng Anh 10 | Thì; Mệnh đề quan hệ; Từ vựng theo ngữ cảnh; Cộng đồng và gia đình; Đọc hiểu chủ đề cộng đồng | 2 |
| Tiếng Anh 11 | Ba chủ đề ngôn ngữ chung; Môi trường; Viết đoạn văn về môi trường | 2 |
| Tiếng Anh 12 | Ba chủ đề ngôn ngữ chung; Nghề nghiệp và tương lai; Giao tiếp và phỏng vấn nghề nghiệp | 2 |

Đây là **giáo trình minh họa**, chưa phải phân phối chương trình đủ cả năm. Các mạch chủ đề được chọn tham khảo [chương trình Toán GDTX cấp THPT của Bộ GDĐT](https://moet.gov.vn/content/vanban/Lists/VBDT/Attachments/1582/2-mon-toan-gdtx-cap-thpt-ban-ngay-2432022-sua-theo-gop-y-cua-vu-phap-che.pdf) và [chương trình Tiếng Anh GDTX cấp THPT của Bộ GDĐT](https://moet.gov.vn/content/vanban/Lists/VBDT/Attachments/1582/11-ct-tieng-anh-ngay-29-3-2022.pdf).

Backfill chỉ chạy khi bật seed ở môi trường Development, và chỉ với hai trung tâm demo có ID định sẵn. Nó thêm phiên bản đồ thị và giáo trình Published mới, giữ nguyên topology Published cũ, kết thúc sáu ứng dụng giáo trình mẫu cũ của mỗi trung tâm và tạo sáu ứng dụng mới. Nếu lớp đang dùng một ứng dụng Primary khác do người dùng gán, backfill không thay ứng dụng đó. Câu hỏi cũ và lịch sử áp dụng còn nguyên.

## Kiểm chứng trên database mẫu ngày 23/10/2026

- Mỗi trung tâm có 6 phiên bản đồ thị mẫu mở rộng và 6 phiên bản hiện hành theo môn–khối; mỗi khối chỉ có một bản hiện hành.
- Toán có 3 nút/khối; Tiếng Anh có 5 nút/khối. Không có cạnh hiện hành nào thiếu hai đầu mút trong cùng phiên bản.
- Mỗi trung tâm có 12 câu hỏi mới (2 câu/môn–khối). Không câu mới nào thiếu ánh xạ chủ đề trong phiên bản hiện hành.
- Sáu ứng dụng mẫu cũ/trung tâm đã kết thúc và sáu ứng dụng mới đang hoạt động. Khởi động API lần hai không tạo bản ghi trùng.
- API `teacher.math` chỉ liệt kê môn Toán và trả 404 khi hỏi chi tiết môn/đồ thị Tiếng Anh. Readiness API và MySQL đều Healthy.
- Toàn bộ 4.595 bài kiểm thử .NET đạt (106 bài tích hợp cần môi trường riêng được bỏ qua); 819 bài kiểm thử web đạt. Lint các tệp web vừa sửa đạt. Lint toàn dự án còn một lỗi có sẵn ở `tests/questionImportLatexGuide.test.ts:84` (`no-control-regex`).
