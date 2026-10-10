# Phạm vi lịch sử lớp học — 2026-10-09

## Kết quả và quy tắc

Đã sửa “Xem lịch sử” không còn lấy lớp học sinh vẫn đang học. Bản đã chạy trên localhost, kiểm tra bằng student01. Chưa commit/push.

- **Đang học:** lớp Active/Current và học sinh là thành viên Active.
- **Xem lịch sử:** lớp đã kết thúc/lưu trữ hoặc học sinh đã rời lớp. Một lớp có thể vẫn hoạt động cho học sinh khác nhưng là lớp đã học đối với học sinh đã rời.
- Không có lớp cũ thì không chọn lớp đang học thay thế; không có giáo trình/chủ đề của lớp đang học lọt vào phần lịch sử.
- Bài làm đã hoàn thành trong lớp vẫn hoạt động vẫn ở “Đang học”, có thể lọc “Đã xong”. Đây không phải chuyển/xóa bài làm cũ.
- Lịch sử giáo trình từng áp dụng và thời điểm kết thúc lớp là hai khái niệm khác nhau. Nhãn giáo trình trong phạm vi lịch sử là “Giáo trình từng áp dụng”. Lượt này không tạo chức năng mới để xem phiên bản giáo trình cũ của lớp đang hoạt động.
- Chỉ xem phạm vi, không tự đổi trạng thái lớp hoặc trạng thái thành viên. Lưu trữ/mở lại lớp vẫn là hành động của quản lý trong luồng đã có.

## Thay đổi kỹ thuật

Tạo một phép chiếu thành viên lớp dùng chung để xác định `IsHistorical` theo vòng đời lớp **và** trạng thái thành viên của riêng học sinh. Reader chọn giáo trình/chủ đề, danh sách bài tập và bộ đếm bài tập cùng dùng quy tắc này. Server từ chối lựa chọn lớp trái chế độ, không chỉ ẩn trên UI.

Giao diện lọc chính xác `isHistorical === history`. Liên kết cũ có `history=true` cùng lớp hiện tại bị server từ chối; khung phạm vi chỉ khi gặp 404 cho lựa chọn lớp mới tải lại danh mục của chính học sinh và sửa URL về lớp lịch sử hợp lệ hoặc bỏ classId nếu không có lớp cũ. Không biến lỗi mạng/401/403 thành kết quả trống.

Giữ tương thích API cũ: khi khách gọi bỏ hẳn tham số history, danh sách/bộ đếm vẫn có hợp đồng all-targets cũ. Giao diện hiện tại luôn gửi chế độ true/false, nên dùng lọc mới. Không thay đổi cách tính daily streak toàn tài khoản.

## Các tệp trong riêng lượt sửa này

8 tệp mã nguồn, 4 tệp test; thêm báo cáo này và 3 ảnh bằng chứng. Các thay đổi từ lượt trước trong worktree được giữ nguyên, không tính thành thay đổi mới của lượt này.

| Tệp | Vai trò |
| --- | --- |
| `src/EduTwin.BLL/Organization/StudentClassScope.cs` | Mới: phép chiếu thành viên lớp và quy tắc phân biệt đang học/đã học dùng chung. |
| `src/EduTwin.BLL/Dashboards/StudentAcademicScopeReader.cs` | Chỉ chọn lớp thuộc đúng chế độ; không lấy lớp hiện tại làm fallback lịch sử; thông báo trống rõ ràng. |
| `src/EduTwin.BLL/Assignments/StudentAssignmentScope.cs` | Dùng quy tắc chung cho kiểm tra lớp và lọc bài tập. |
| `src/EduTwin.BLL/Assignments/ListStudentAssignmentsUseCase.cs` | Lọc cả chế độ lịch sử, không lấy mọi bài tập khi history=true. |
| `src/EduTwin.BLL/Dashboards/GetStudentWorkspaceSummaryUseCase.cs` | Số bài tập khớp phạm vi danh sách, kiểm tra lớp cùng chế độ. |
| `web/edutwin-web/src/utils/studentAcademicScope.ts` | Mới: lọc lớp theo chế độ và chuẩn hóa classId trong URL. |
| `web/edutwin-web/src/components/student/StudentAcademicContext.tsx` | Chọn lớp theo đúng chế độ; xử lý bookmark cũ; nhãn/giải thích lịch sử. |
| `web/edutwin-web/src/pages/StudentAssignmentsPage.tsx` | Thông báo danh sách trống đúng nghĩa, không báo “Tất cả bài tập đã hoàn thành” khi không có bài. |
| `tests/EduTwin.BLL.Tests/Dashboards/AcademicScopeTests.cs` | 3 test mới: không có lớp cũ, lớp lưu trữ, học sinh rời lớp còn hoạt động; fixture lịch sử theo vòng đời lớp hợp lệ. |
| `tests/EduTwin.BLL.Tests/Dashboards/StudentWorkspaceSummaryTests.cs` | Test mới đối chiếu danh sách/bộ đếm, các lớp hiện tại/đã lưu trữ/đã rời, và giữ dữ liệu target cũ. |
| `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs` | Bổ sung kiểm tra lọc thật trước/sau lưu trữ, mở lại lớp, rời lớp; số bài làm không đổi. |
| `web/edutwin-web/tests/studentClassHistory.test.ts` | Mới: 5 test lọc tách rời, không fallback hiện tại, sửa bookmark và thông báo UI. |

Không thêm migration, không sửa dữ liệu mẫu, trạng thái lớp hay phân quyền. Không sửa pipeline AI, điểm, dữ liệu Twin hoặc thuật toán daily streak.

## Kiểm thử

| Kiểm tra | Kết quả |
| --- | --- |
| Backend liên quan | 17 passed, 0 failed. |
| Backend toàn bộ | 3.966 passed, 0 failed, 77 skipped / 4.043 tổng. Không coi các test bị skip là đã chạy. |
| Frontend toàn bộ | 607 passed, 0 failed. |
| MySQL riêng | 11 passed, 0 failed trên database tạm; không gọi AI. |
| TypeScript/Vite | Build thành công; còn cảnh báo kích thước bundle vốn có. |
| Docker API/web | Build và cập nhật localhost thành công; readiness Healthy, MySQL Healthy. |

## Kiểm tra Chrome

1. Đăng nhập student01, chọn môn Toán: “Đang học” vẫn có Lớp Toán 10 và lớp kiểm thử cũ còn hoạt động.
2. Chuyển “Xem lịch sử”: không hiện hai lớp trên; dropdown khóa với “Chưa có lịch sử lớp học”. Tổng quan không dựng biểu đồ của lớp hiện tại thay thế.
3. Mở trực tiếp bookmark lịch sử có classId của Lớp Toán 10: URL được bỏ classId không hợp lệ, giữ history=true; danh sách hiện 0 nhiệm vụ và thông báo lịch sử trống, không còn lỗi tải dai dẳng hoặc thông báo hoàn thành giả.
4. Quay lại “Đang học”, chọn lớp kiểm thử cũ: Test 1, Test 2, Test 3 vẫn hiện đúng, có kết quả GV đã duyệt và liên kết Xem lại.
5. Chuyển lại lịch sử, giữ tab đã kiểm tra mở cho người dùng xem.

Không lưu trữ lớp thật hoặc rút học sinh thật để dựng dữ liệu kiểm thử. Trường hợp lớp đã lưu trữ và học sinh đã rời lớp còn hoạt động được kiểm tra bằng unit test và MySQL tạm.

Ảnh tại `docs/verification/evidence/student-class-history/`:

- `history-empty.jpg`: tổng quan lịch sử không nhận lớp đang học thay thế.
- `history-assignments-empty.jpg`: danh sách lịch sử trống, nhãn đúng.
- `current-existing-submissions.jpg`: ba bài làm cũ còn hiện trong lớp đang hoạt động.

## Bảo toàn dữ liệu

Backup riêng git-ignored: `storage/backups/2026-10-09-before-history-filter.sql` và metadata `.json`. SHA-256 SQL: `bb9d69c20771c9dbe3caceff917980ed3f3032bf91f1e2dc08d233386d2193bf`.

So sánh 19 bảng được bảo vệ sau cập nhật: chỉ `users` thay đổi. Kiểm tra từng cột xác nhận đúng 1 tài khoản chỉ đổi `last_login_at`, `updated_at`, `row_version` do đăng nhập (`onlyLoginMetadataChanged: true`, vẫn đủ 18 tài khoản).

18 bảng học thuật còn lại giữ nguyên dấu vân tay, gồm 204 bài làm, 204 phân tích lập luận, 30 thành viên lớp, 5 bản ghi áp dụng giáo trình và 362 bản ghi lịch sử cập nhật Twin. Không đổi trạng thái lớp, chuyển bài làm, reset điểm/năng lực hoặc xóa lịch sử.
