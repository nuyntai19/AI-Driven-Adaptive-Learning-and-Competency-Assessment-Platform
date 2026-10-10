# Phạm vi học tập theo lớp và lịch sử áp dụng giáo trình

Hoàn tất triển khai và cập nhật localhost trong lượt làm việc 08–09/10/2026. Chưa commit/push.

**Cập nhật rà soát 09/10:** trạng thái “hoàn tất” ở trên chỉ phản ánh triển khai/kiểm thử kỹ thuật của lượt trước, không phải chấp thuận nghiệp vụ để push. Đã phát hiện migration tự chuyển mọi lớp chưa phân khối sang History và thiếu luồng quản lý/audit tương ứng. Xem `CLASS-LIFECYCLE-AUDIT-2026-10-09.md`; phải thống nhất hướng sửa trước khi push phần này. Các số liệu bảo toàn bài làm bên dưới là kiểm chứng của lượt trước và vẫn có giá trị.

## Kết quả

- Học sinh chọn môn và lớp của mình để **xem**, không có quyền chọn/thay giáo trình. Server xác thực thành viên của lớp; lớp ngoài phạm vi không được đọc.
- `Class.LearningScope` phân biệt Current/History. Lớp đang học cần trạng thái Active và thành viên Active. Lớp kiểm thử cũ được tách sang lịch sử bằng dữ liệu, không suy luận từ tên.
- Dashboard chỉ lấy hợp các chủ đề của giáo trình đã xuất bản đang áp dụng trong lớp được chọn, loại trùng. Không có giáo trình => trạng thái trống có giải thích, không thay bằng toàn bộ đồ thị môn.
- Một lớp có tối đa một giáo trình chính đang áp dụng, có thể có nhiều giáo trình bổ trợ. Giáo viên phụ trách quản lý phần áp dụng tại trang chi tiết giáo trình đã xuất bản. Cấu trúc giáo trình Published vẫn đóng băng.
- Thay/ngừng áp dụng phải có lý do. Mỗi lần áp dụng là một bản ghi mới; bản cũ được kết thúc, không ghi đè hay xóa. Lý do bắt đầu và lý do kết thúc lưu riêng.
- Ngoại lệ khác khối lưu lý do, người duyệt, thời điểm duyệt và ảnh chụp khối của lớp/giáo trình. Actor/thời gian lấy từ server, không nhận người duyệt tùy ý từ client.
- Atlas nhóm theo chương/nhóm, tối đa 6 chủ đề một trang, nhãn dài có giới hạn dòng và tên đầy đủ hỗ trợ truy cập. Đường nối thứ tự trình bày được ghi rõ không phải cạnh tiên quyết.
- Radar tổng hợp năng lực từ chủ đề có bằng chứng, không coi chủ đề chưa đánh giá là bài sai. Chỉ vẽ radar với 3–8 nhóm phù hợp; còn lại dùng thanh tổng hợp.
- Các liên kết tổng quan, hồ sơ Twin, danh sách/chi tiết bài tập giữ `subjectId/classId/history`. Chế độ lịch sử không giả lập “nhiệm vụ hôm nay”.
- Twin là năng lực tích lũy theo môn qua nhiều lớp. Đổi lớp/phạm vi không reset Twin. Chế độ lịch sử lọc giáo trình từng áp dụng, không phải ảnh chụp năng lực tại thời điểm quá khứ.
- Lộ trình dùng ứng dụng giáo trình Current, chấp nhận chính+bổ trợ và loại trùng. Không khôi phục liên kết kế hoạch cũ khi ứng dụng đã kết thúc. Lớp hiện tại chưa có giáo trình được trả trạng thái `NO_APPLIED_CURRICULUM`.
- Huy hiệu bài tập cùng phạm vi với danh sách. Daily streak vẫn tính hoạt động học thật trên toàn hồ sơ, không thay đổi theo lớp đang xem.
- Dữ liệu mẫu mới có lịch sử áp dụng chính tương ứng sáu lớp phân khối; kiểm tra manifest nhận diện thiếu ledger và không tự reseed dữ liệu đã thay đổi.

## Ràng buộc

Migration `20261008162602_AddAcademicClassScopeAndCurriculumApplications` đã áp dụng trên MySQL localhost.

- Khóa ngoại tổ hợp cùng trung tâm cho lớp/giáo trình/người thực hiện; unique generated keys giới hạn một ứng dụng chính và một cặp lớp–giáo trình hiện hành.
- Check constraints cho khối 10–12, metadata ngoại lệ, vai trò áp dụng và cặp thời điểm/người kết thúc.
- Trigger kiểm tra cùng môn, giáo viên phụ trách, trạng thái lớp/giáo trình/tài khoản; bảo toàn lịch sử đã kết thúc và cấm xóa ledger.
- Trigger chặn thay môn của lớp/giáo trình/nút khi đã có liên kết học thuật; chặn đổi khối lớp khi có ứng dụng hiện hành.
- API/BLL kiểm tra quyền và optimistic concurrency; database không thay thế kiểm tra quyền server.
- Liên kết `curriculum_classes` có trước nâng cấp được giữ nguyên và nhập vào ledger khi phù hợp. Các lớp cũ chưa phân khối được đưa vào History; không ép chuyển bài làm sang lớp mới.

## Kiểm thử

| Hạng mục | Kết quả |
| --- | --- |
| Toàn bộ backend | 3.953 đạt, 0 lỗi, 76 bỏ qua theo cấu hình tích hợp; tổng 4.029 |
| Toàn bộ frontend | 590 đạt, 0 lỗi |
| MySQL thật – migration/khối/thành viên | 1 đạt, không gọi AI |
| MySQL thật – lifecycle/unique/actor/tenant/môn/lịch sử | 1 đạt, không gọi AI |
| Docker API/web | Build thành công; API 0 warning, 0 error |
| Health readiness | Healthy; MySQL Healthy |
| `git diff --check` | Đạt; chỉ có cảnh báo chuẩn hóa LF/CRLF ở một số tệp cũ |

TRX nằm trong thư mục kiểm thử bỏ qua bởi Git: `tests/TestResults/academic-scope/academic-full-complete.trx`, `academic-migration-live.trx`, `academic-lifecycle-live.trx`.
Vite còn cảnh báo kích thước chunk MathLive như trước; không phải lỗi build.

## Bảo toàn dữ liệu localhost

Đã tạo bản sao lưu mới, không ghi đè bản sao lưu cũ; file SQL/JSON riêng tư nằm trong `storage/backups` được Git bỏ qua. Không đưa dữ liệu hay bí mật vào báo cáo.

SHA-256 bản sao lưu: `c25b8fa67ba0f86e79987e1c585acb3b1d616f1a025411cd4ccae0edca366754`.

`node scripts/ops/academic_scope_integrity.cjs --verify` sau cập nhật cuối trả:
`protectedHistoryUnchanged=true`, `changedTables=[]`.

18 bảng được đối chiếu toàn bộ cột theo khóa chính:

| Bảng | Số dòng giữ nguyên |
| --- | ---: |
| assignments | 4 |
| assignment_questions | 54 |
| assignment_targets | 20 |
| student_assignment_progress | 20 |
| attempts | 204 |
| reasoning_analyses | 204 |
| attempt_attachments | 2 |
| curriculums | 7 |
| curriculum_nodes | 84 |
| curriculum_classes | 4 |
| student_twins | 4 |
| student_subject_goals | 20 |
| students | 10 |
| users | 18 |
| class_students | 30 |
| knowledge_nodes | 96 |
| knowledge_twins | 14 |
| twin_update_history | 362 |

Trường mới của lớp và bảng ledger mới là thay đổi dự kiến, không nằm trong kiểm tra bất biến này. Sau migration có 6 lớp Current, 4 lớp History và 4 ứng dụng Primary nhập từ liên kết cũ (tính cả hai trung tâm). Không reseed/reset database thật.

## Kiểm tra Chrome bằng student01

- Đang học/Toán tự chọn Lớp Toán 10, không gộp lớp kiểm thử cũ. Hiện chưa có giáo trình Published áp dụng, hiển thị hướng dẫn rõ.
- Xem lịch sử/Toán chọn lớp kiểm thử cũ và giáo trình cũ: Atlas chỉ có Hàm số, Mũ–Logarit, Nguyên hàm; không còn 81 nhãn chồng nhau.
- Thanh tổng hợp nhóm nền tảng cho biết 2/3 chủ đề có bằng chứng, 39,9% theo bằng chứng quan sát.
- Danh sách lịch sử còn Test 1 (2/2), Test 2 (1/1), Test 3 (1/1), đều đã xong/giáo viên duyệt. Link mở chi tiết giữ lớp/lịch sử.
- Đổi sang Tiếng Anh trong phạm vi lịch sử vẫn thấy Test 50 câu, 50/50 câu hoàn thành/đúng, trạng thái giáo viên đã duyệt; không chuyển bài này sang lớp Tiếng Anh 10 mới.
- Mở lại bài 50 câu: tổng điểm cũ 10/10, đã nộp/chỉ đọc, chờ giáo viên duyệt 0 câu. Đi vào từng câu và quay về giữ nguyên lớp lịch sử. Số phân tích AI 48/50 là số liệu lịch sử của lượt cũ; không tự gọi AI để đổi kết quả đó.

Bằng chứng giao diện tại `docs/verification/evidence/academic-scope/`:

- `student01-current-math10.jpg`
- `student01-history-atlas.jpg`
- `student01-history-radar.jpg`
- `student01-history-assignments.jpg`
- `student01-history-english50.jpg`
- `student01-history-english50-score.jpg`

## Bước sử dụng tiếp theo và giới hạn kiểm chứng

Ba giáo trình Toán 10/11/12 thật đã tạo ở lượt trước vẫn là Draft/Private, chưa tự xuất bản hoặc áp dụng. Giáo viên cần duyệt nội dung, xuất bản rồi áp dụng cho lớp đúng khối để Dashboard hiện tại có bản đồ mới. Học sinh không được thực hiện bước này.

Khung áp dụng phía giáo viên đã kiểm tra source, build và lifecycle MySQL/API/BLL; lượt này chưa kiểm tra thao tác lưu trực tiếp bằng phiên Chrome giáo viên vì người dùng đang đăng nhập student01. Không thử submit bài mới hay dùng quota AI trong phần kiểm tra này. Kết quả trên không phải tuyên bố mọi tính năng của hệ thống đều hết lỗi.

Quan sát ngoài phạm vi sửa lần này: thẻ “Khoảng cách cần vượt” của môn Tiếng Anh còn định dạng `+-1.8` khi điểm dự đoán 9.8 đã vượt mục tiêu 8.0. Đây là lỗi nhãn có trước trong phần mục tiêu, không thay đổi điểm lưu; ghi nhận để xử lý riêng, không đổi công thức mục tiêu trong lượt này.
