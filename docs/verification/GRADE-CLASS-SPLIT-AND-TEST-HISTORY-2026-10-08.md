# Tách lớp mẫu theo khối và bảo toàn lịch sử kiểm thử

Ngày kiểm tra: 2026-10-08. Phạm vi dữ liệu đang chạy: localhost, EDUTWIN_A.
Không commit/push trong lượt này. Không xóa, reset hoặc seed lại cơ sở dữ liệu hiện có.

## Kết quả dữ liệu đang chạy

| Môn | Lớp 10 | Lớp 11 | Lớp 12 |
| --- | --- | --- | --- |
| Toán | 2 học sinh | 2 học sinh | 1 học sinh |
| Tiếng Anh | 2 học sinh | 2 học sinh | 1 học sinh |

Các lớp mới dùng ID v2 (`51000000…` và `61000000…`), không tái sử dụng ID lớp cũ.
Không đổi khối, ID, tên hoặc tài khoản học sinh.

Hai lớp gốc được đổi tên thành `Lớp Toán — Dữ liệu kiểm thử cũ` và
`Lớp Tiếng Anh — Dữ liệu kiểm thử cũ`. Chúng vẫn Active, chưa phân khối và giữ nguyên
toàn bộ thành viên cũ. Lý do: còn bài được giao nhưng chưa bắt đầu, nên lưu trữ hoặc
rút thành viên ngay có thể ảnh hưởng quyền làm tiếp. EDUTWIN_A hiện có 8 lớp:
6 lớp theo khối để sử dụng từ nay và 2 lớp giữ lịch sử.

Không chuyển bài tập hay bài làm sang lớp mới. Vì thế báo cáo theo lớp mới có thể
hiện 0 bài đã giao; để xem kết quả kiểm thử trước đó, chọn lớp `Dữ liệu kiểm thử cũ`.
Ba giáo trình Toán 10/11/12 tạo ở lượt trước vẫn Private/Draft, chưa gán lớp.

## Thay đổi mã nguồn

- `EduTwinSeedFactory`: dữ liệu mẫu mới cho cả A và B có 6 lớp theo khối, mỗi học sinh
  được gán vào đúng khối của 2 môn. Khung giáo trình minh họa cũng có khối tương ứng.
  Khung mẫu không được gọi là giáo trình đầy đủ cả năm học.
- `DeterministicSeedIds`: ID v2 tách biệt ID lớp/giáo trình cũ.
- `ManifestEvaluator`: kiểm tra cả GradeLevel; tenant đã sửa dữ liệu vẫn fail closed,
  không tự viết đè bằng dữ liệu mẫu mới.
- `CreateStudentUseCase`: tạo học sinh kèm lớp chỉ nhận lớp Active, đã phân khối,
  cùng khối. Lưu `GradeLevelAtEnrollment`.
- `UpdateStudentUseCase`: không cho đổi khối gây lệch các lớp theo khối đang học;
  bỏ qua quan hệ Removed/lớp Archived và lớp lịch sử chưa phân khối. Không sửa lại
  khối tại thời điểm ghi danh của dữ liệu lịch sử.
- `AddStudentsToClassUseCase`: lớp chưa phân khối không nhận thêm thành viên.
  Ngoại lệ học khác khối ở lớp đã phân khối vẫn được phép qua luồng có lý do/phê duyệt.
- Hai form tạo tài khoản học sinh chỉ hiển thị lớp đúng khối và bỏ lựa chọn cũ nếu
  thay đổi khối; lỗi server được trả về bằng thông báo có hướng xử lý.
- Sửa lỗi MySQL EF có sẵn trong `CreateStudentUseCase`: danh sách Guid trong IN không
  có type mapping. Dùng biểu thức so sánh Guid tường minh, giữ lọc tenant ở server.

`scripts/seed_student_assignments.sql` đã đổi sang lớp Toán 10 v2 và câu hỏi cùng chủ đề;
không reset hàng assignment/progress đã có khi chạy lại. Đây chỉ là fixture dữ liệu mẫu,
không phải công cụ chuyển lịch sử bài làm. Không chạy script SQL này trên dữ liệu hiện tại.

## Áp dụng an toàn và đối chiếu

Backup trước sửa: `storage/backups/2026-10-08-before-grade-class-split.sql` (ignored),
2,628,094 byte; SHA256:
`ebbcf7016538eaf9fc549175c535a3ae4ba38c2ca1f344f949856d3e2cf66f59`.
Không đưa backup hoặc nội dung bài làm lên Git.

`scripts/ops/split_seed_grade_classes_local.cjs` mặc định chỉ đọc kế hoạch;
`--apply` thực hiện sửa cộng thêm trong transaction Serializable, kiểm tra đúng center,
actor, teacher, subject và va chạm ID/tên trước khi ghi. Giữ nguyên membership Removed,
không tự khôi phục. Có audit `DemoGradeClassesSplit`. Đã chạy apply hai lần để kiểm tra
không tạo lớp/membership trùng hoặc tăng lại version lớp cũ khi tên đã đúng.

Fingerprint tính từ toàn bộ cột, theo thứ tự khóa chính; số hàng và SHA256 ở 12 bảng
sau bằng nhau trước/sau sửa và sau khi khởi động API bản cuối:

| Bảng | Số hàng EDUTWIN_A | SHA256 |
| --- | ---: | --- |
| assignments | 4 | 10cee2b35ee483110cf3082589620cdbf6d45ea8d61a31b2775a438196bbc0d9 |
| assignment_questions | 54 | fa88ef2879758a1ad7610caabe1c2b363988d6cb2706e0eaec405f3734b530ac |
| assignment_targets | 20 | a42e562fe8db1b072f6f005efcf79f7bdef284ed25a93aa5104257a717f88ab1 |
| student_assignment_progress | 20 | 9d152e88a932c409dae6efdc76a151d86c0c211a579b17915918d98545af2d43 |
| attempts | 204 | 1cc81800445a027a0cbccd77353197d2949ab868adfcf3f356c7213e56f35126 |
| reasoning_analyses | 204 | 0b2b9ab53c43b89afda02cf297d07e70079249e8cd5ba7064e82564a98eadd10 |
| attempt_attachments | 2 | 8ec22730a7bd34a345c691ebe58c9c7fc380127fb77eea76aadd73fe2c3a4f2a |
| curriculums | 5 | abae70c83112291428ed51bde5c1b5798ac95fbba796750648c3224315eb8497 |
| curriculum_nodes | 78 | 399872288987493211dda9706efd9e370974e23a3a385d48715aa5b15ac72482 |
| curriculum_classes | 2 | 8874416173f97275c8389e692c33cec6f9c7db89ca0ebfccb558561351ece990 |
| student_twins | 4 | a735e3d48f2f1ed47fb04c258f868ab578229e2509970201d33f5ed0a4e1247c |
| student_subject_goals | 10 | 50bfa7fc9bcfbf477c117d6db0aeec10f43d3a2a1cdcebe6b8d6f98a3ba95c20 |

Các cột điểm, trạng thái, teacher override và nội dung bài làm trong các bảng trên
được bao phủ bởi fingerprint. Không yêu cầu chấm AI lại, không tiêu thụ quota AI.

## Kiểm thử

- Backend đầy đủ sau sửa cuối: 3,947 passed, 75 skipped, 0 failed (4,022 tổng).
  Các test MySQL cần opt-in được chạy riêng ở bên dưới.
- Frontend đầy đủ: 584 passed, 0 failed. Vite/TypeScript build thành công;
  cảnh báo chunk MathLive lớn là cảnh báo hiện có.
- MySQL thật trong database tạm riêng: 2 ca passed, 0 failed:
  - `AddStudentsToClass_LiveMySql_TranslatesGuidFiltersAndEnforcesBatchLimits`:
    ngoại lệ khác khối có lý do vẫn lưu snapshot/lý do đúng.
  - `StudentGradeGuards_RealSql_PreserveEnrollmentSnapshot_NoProviderCalls`:
    chặn tạo sai khối trước khi ghi tài khoản, tạo cùng khối vào 2 lớp thành công,
    chặn đổi khối khi còn học và cho đổi sau khi rút lớp; snapshot cũ vẫn giữ nguyên.
- API Release build: 0 warning, 0 error. Docker API/web đã cập nhật; ready health Healthy.
- `git diff --check`: không có lỗi whitespace; chỉ cảnh báo line-ending có sẵn.

Kết quả TRX nằm trong thư mục ignored `tests/TestResults/grade-classes`.

## Đối chiếu Chrome

Dùng kỹ năng computer-use, tài khoản Teacher Math của EDUTWIN_A đang đăng nhập.

- Dropdown có Toán 10/11/12 và lớp dữ liệu cũ.
- Toán 12 có 1 học sinh Khối 12; Toán 10 có 2 học sinh và cả hai Khối 10.
- Lớp Toán dữ liệu cũ vẫn có 5 học sinh, 3 bài được giao; student01 còn 3/3 bài nộp,
  điểm trung bình 10.0. Đây là dữ liệu gốc, không copy/chấm lại vào lớp mới.

Ảnh đối chiếu: `evidence/grade-classes/math-10-dashboard.png` và
`evidence/grade-classes/math-10-students.png`.

Giới hạn chưa thay đổi trong lượt này: header trang quản lý học sinh bị ép hẹp tại
viewport khoảng 943px; danh sách Gap Groups còn dùng toàn bộ chủ đề môn khi các giáo
trình mới chưa được xuất bản/gán lớp. Không tự xuất bản hoặc thay đổi thuật toán đánh
giá năng lực trong tác vụ tách lớp này.
