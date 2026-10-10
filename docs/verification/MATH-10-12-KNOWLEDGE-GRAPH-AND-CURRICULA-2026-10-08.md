# Toán 10–12: đồ thị tri thức và khung giáo trình

## Phạm vi và nguồn

Thực hiện trên giao diện Chrome localhost bằng Teacher Math, trung tâm EDUTWIN_A. Không xóa/sửa ba nút cũ và hai liên kết cũ; không thay giáo trình đang Published; không giao bài, gán lớp hoặc gọi AI.

Nguồn chính: [Chương trình môn Toán của Bộ GD&ĐT, ban hành kèm TT32/2018](https://boiduonghanoi.edu.vn/pluginfile.php/45/mod_folder/content/0/3-CT-Toan.pdf), phần Toán 10 trang 79–89, Toán 11 trang 89–105, Toán 12 trang 105–113. Bản đăng lại trên [cổng trường học TP.HCM](https://thleductho.hcm.edu.vn/van-ban/chuong-trinh-giao-duc-pho-thong-mon-toan-ban-hanh-kem-theo-thong-tu-322018tt-bg/vbctmb/72434/406283) cũng được đối chiếu. [Thông tin sửa đổi năm 2025 của Chính phủ](https://xaydungchinhsach.chinhphu.vn/thong-tu-so-17-2025-tt-bgddt-sua-doi-bo-sung-mot-so-noi-dung-trong-chuong-trinh-giao-duc-pho-thong-119250916145653739.htm) không liệt kê sửa đổi nội dung môn Toán.

Khung này là phân rã chủ đề để theo dõi năng lực, không sao chép một bộ SGK và không phải bộ bài giảng/bài tập đầy đủ. Danh sách ban đầu có lẫn khối: đếm/tổ hợp và Newton số mũ nhỏ thuộc lớp 10; mũ–logarit thuộc lớp 11; xác suất có điều kiện, toàn phần/Bayes thuộc cốt lõi lớp 12. Lý thuyết đồ thị và biến ngẫu nhiên rời rạc là chuyên đề lựa chọn; hàm bậc bốn trùng phương không được thêm làm một mục khảo sát bắt buộc của chương trình mới.

## Cấu trúc đã tạo

- 84 nút mới: 6 Chapter tổ chức, 69 Topic cốt lõi và 9 Topic lựa chọn.
- Giữ lại `MATH-FUNCTIONS`, `MATH-EXP-LOG`, `MATH-ANTIDERIVATIVE`: tổng đồ thị 87 nút, gồm 81 Topic.
- 67 liên kết PrerequisiteOf mới, cộng 2 liên kết cũ = 69; giao diện kiểm định DAG hợp lệ, không có chu trình.
- Các quan hệ tiên quyết mới là thiết kế sư phạm của lần thực hiện này, không phải bảng quan hệ do Bộ GD&ĐT ban hành. Thứ tự giáo trình không mặc nhiên biến thành liên kết tiên quyết.
- Ba giáo trình cốt lõi đã lưu với 25 / 28 / 19 Topic cho khối 10 / 11 / 12, gồm một nút cũ được tái sử dụng trong mỗi khối. Không thêm Chapter vào danh sách học, không trộn chuyên đề lựa chọn.
- Nút mới có mô tả yêu cầu học bằng tiếng Việt và nguồn. 90 phút ôn luyện/Topic và trọng số khởi tạo 3 là dữ liệu vận hành để giáo viên hiệu chỉnh, không phải tỷ trọng đề thi hoặc phân phối tiết chính thức. Chapter dùng 5 phút/0 trọng số vì biểu mẫu hiện yêu cầu thời lượng dương.

## Nội dung cốt lõi theo khối

### Toán 10

Mệnh đề; tập hợp; bất phương trình/hệ hai ẩn; hàm số; hàm bậc hai; dấu tam thức và bất phương trình bậc hai; phương trình quy về bậc hai; quy tắc đếm; hoán vị/chỉnh hợp/tổ hợp; Newton số mũ nhỏ; giá trị lượng giác góc 0–180 độ; giải tam giác; vectơ; tích vô hướng; tọa độ vectơ Oxy; đường thẳng; đường tròn; ba conic; số gần đúng/sai số; dữ liệu bảng/biểu đồ; xu thế trung tâm và phân tán mẫu không ghép nhóm; xác suất cổ điển/biến cố đối; thực hành/trải nghiệm.

### Toán 11

Góc lượng giác; công thức lượng giác; hàm/phương trình lượng giác; dãy số; cấp số cộng/nhân; giới hạn dãy số/hàm số; liên tục; lũy thừa; logarit; hàm mũ–logarit; phương trình/bất phương trình mũ–logarit; khái niệm/quy tắc đạo hàm; đạo hàm cấp hai; quan hệ liên thuộc, song song, phép chiếu và vuông góc không gian; góc/nhị diện; khoảng cách; thể tích chóp/lăng trụ/chóp cụt; xu thế trung tâm mẫu ghép nhóm; biến cố hợp/giao/độc lập; cộng/nhân xác suất; thực hành/trải nghiệm.

### Toán 12

Đơn điệu/cực trị; giá trị lớn nhất/nhỏ nhất; tiệm cận; khảo sát hàm bậc ba và phân thức; ứng dụng đạo hàm; nguyên hàm; tích phân; diện tích/thể tích và ứng dụng tích phân; vectơ không gian; tọa độ Oxyz; mặt phẳng/đường thẳng/mặt cầu; góc và khoảng cách bằng tọa độ; phân tán mẫu ghép nhóm; xác suất có điều kiện; toàn phần/Bayes; thực hành/trải nghiệm.

## Chuyên đề lựa chọn được tách riêng trong đồ thị

| Khối | Các Topic lựa chọn |
| --- | --- |
| 10 | Quy nạp/Newton tổng quát; hệ bậc nhất ba ẩn; conic chuyên sâu và ứng dụng |
| 11 | Biến hình phẳng; vẽ kĩ thuật; lý thuyết đồ thị |
| 12 | Biến ngẫu nhiên rời rạc/nhị thức; tối ưu; tài chính |

## Bảo toàn dữ liệu

Đã sao lưu trước thao tác: `storage/backups/2026-10-08-before-math-curricula.sql` (bản sao trong thư mục ignored từ bản dump ban đầu ở Temp), SHA256 `f22d31e430492745c5c85a22c199ffd8da648db799cd68dc58282d27193be263`.

Các biểu mẫu tạo nút/liên kết đều được xác minh có thông báo thành công sau mỗi lần lưu. Giáo trình được kiểm tra mã chủ đề và thứ tự trước/sau khi lưu. Sau tải lại danh sách, có đúng ba giáo trình mới, đều Private/Draft, 0 lớp áp dụng; giáo trình cũ vẫn Published/3 chủ đề/1 lớp.

| Giáo trình | Mã đã tạo | Topic |
| --- | --- | ---: |
| Toán 10 — Chương trình cốt lõi GDPT 2018 | `0f002e99-13ff-4492-a4c4-f36b4cff71eb` | 25 |
| Toán 11 — Chương trình cốt lõi GDPT 2018 | `316e1734-dba2-4c41-bab6-84c5c92e25ed` | 28 |
| Toán 12 — Chương trình cốt lõi GDPT 2018 | `48acda06-adde-4b9a-91e9-bf0ca1db4d57` | 19 |

Bằng chứng giao diện trong `docs/verification/evidence/math-curricula/`. Không xuất bản/gán lớp hoặc chỉnh sửa code ứng dụng trong lượt tạo dữ liệu này. Không commit/push.
