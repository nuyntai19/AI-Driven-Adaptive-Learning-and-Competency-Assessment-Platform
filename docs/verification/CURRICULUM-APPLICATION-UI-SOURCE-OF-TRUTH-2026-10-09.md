# Kiểm tra gán giáo trình cho lớp — 2026-10-09

## Kết quả

Đã sửa tình trạng giáo trình Published có hai khung chọn lớp, trong đó khung cũ bị khóa và hiển thị 0 dù khung mới đã lưu một lớp.

Giáo trình **Toán 12 — Chương trình cốt lõi GDPT 2018** vẫn áp dụng vai trò **Chính** cho **Lớp Toán 12**. Không gán lại, chuyển lớp, xuất bản thêm hay sửa dữ liệu giáo trình thật trong lượt kiểm tra này. Bản mới đã chạy trên localhost; chưa commit/push.

## Nguyên nhân và cách sửa

- Khung mới đọc/ghi `class_curriculum_applications`, nhưng DTO danh sách/chi tiết và khung cũ còn đọc `curriculum_classes`. Giáo trình Toán 12 có một cấu hình hiện tại trong bảng mới và không có bản ghi trong bảng cũ.
- Một truy vấn dùng chung phục vụ chi tiết, danh sách và phản hồi lưu trữ: Draft đọc lớp **dự kiến**; Published đọc lớp **đang áp dụng**, chưa kết thúc, Active/Current; Archived không tính là đang áp dụng. Dữ liệu cũ không bị xóa hoặc viết lại.
- Published chỉ có một khung áp dụng. Archived có khung lịch sử chỉ đọc. Draft có khung “Lớp dự kiến áp dụng” và giải thích chưa áp dụng cho học sinh. Chế độ tạo mới yêu cầu lưu bản nháp trước khi chọn lớp dự kiến.
- Mặc định hiện lớp cùng môn, cùng khối, do giáo viên đang đăng nhập phụ trách. Lớp khác khối nằm trong mục ngoại lệ, gán ngoại lệ mới phải nhập lý do. Ngoại lệ chưa phân khối đã tồn tại không bị bỏ âm thầm.
- Lấy đủ các trang lớp thay vì chỉ trang đầu. Khóa cache có trung tâm/tác nhân. Lựa chọn chưa lưu không bị ghi đè khi query tải lại; khi phiên bản server thay đổi phải hủy lựa chọn cũ để xem bản mới.
- Danh sách và chi tiết được làm mới sau lưu. Nhãn thống kê Published đổi thành “Đã xuất bản”, không gọi mọi giáo trình Published là “Đang áp dụng”. Giáo viên dùng giáo trình Shared chỉ thấy số lớp mình phụ trách.

## Phạm vi tệp trong riêng lượt sửa này

Danh sách dưới đây là **8 tệp mã nguồn + 6 tệp test** được tạo/sửa trong lượt gán giáo trình này, không phải toàn bộ thay đổi đang có trong worktree từ những lượt trước. Thêm báo cáo này và 3 ảnh bằng chứng.

| Tệp | Thay đổi của lượt này |
| --- | --- |
| `src/EduTwin.BLL/CurriculumAndQuestions/CurriculumClassScopeQuery.cs` | Mới: quy tắc đọc lớp dự kiến/đang áp dụng, phạm vi chủ lớp, loại trùng. |
| `src/EduTwin.BLL/CurriculumAndQuestions/GetCurriculumUseCase.cs` | Chi tiết dùng nguồn lớp thống nhất. |
| `src/EduTwin.BLL/CurriculumAndQuestions/ListCurriculumsUseCase.cs` | Danh sách dùng cùng nguồn, truy vấn theo lô, không N+1. |
| `src/EduTwin.BLL/CurriculumAndQuestions/ArchiveCurriculumUseCase.cs` | Phản hồi Archived có 0 lớp đang áp dụng, không xóa liên kết lịch sử. |
| `web/edutwin-web/src/components/teacher/CurriculumApplicationPanel.tsx` | Một biểu mẫu áp dụng, phân nhóm khối/ngoại lệ, tải đủ lớp, giữ lựa chọn chưa lưu, hủy và kiểm tra phiên bản. |
| `web/edutwin-web/src/pages/teacher/TeacherCurriculumEditorView.tsx` | Bỏ khung trùng Published; phân biệt nội dung đã khóa, kế hoạch Draft và áp dụng hiện tại. |
| `web/edutwin-web/src/pages/teacher/TeacherCurriculumListView.tsx` | Nhãn lớp dự kiến/đang áp dụng và Published chính xác; cache theo trung tâm/tác nhân. |
| `web/edutwin-web/src/utils/curriculumApplicationClasses.ts` | Mới: hàm thuần lọc lớp, yêu cầu lý do ngoại lệ mới, đếm lớp hiện tại không tính tạm ngừng. |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/CurriculumApplicationProjectionTests.cs` | Mới: chi tiết/danh sách khớp nhau, Draft/Published/Archived, Shared, lớp khác giáo viên và lịch sử không bị viết lại. |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/ListCurriculumsUseCaseTests.cs` | Bổ sung lớp thuộc đúng giáo viên cho fixture liên kết lớp dự kiến. |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/ArchiveCurriculumUseCaseTests.cs` | Xác nhận 0 lớp hiện tại sau lưu trữ nhưng liên kết cũ vẫn còn. |
| `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs` | Bổ sung đối chiếu DTO với ledger sau thay giáo trình, lưu trữ và mở lại lớp trên MySQL thật. |
| `web/edutwin-web/tests/curriculumApplicationUi.test.ts` | Mới: 6 test phân nhóm khối, ngoại lệ, số đếm và cấu trúc/guard biểu mẫu. |
| `web/edutwin-web/tests/academicScopeCharts.test.ts` | Cập nhật kiểm tra source theo guard Draft của khung kế hoạch. |

Không thêm migration, không sửa mô hình database trong lượt này. Không sửa pipeline AI, quota, batch hay thời gian chấm. Không đổi quyền tác nhân trong lượt này.

## Kiểm thử

| Kiểm tra | Kết quả cuối |
| --- | --- |
| Backend toàn bộ (`dotnet test ... --no-build --no-restore`) | 3.962 passed, 0 failed, 77 skipped / tổng 4.039. Các test cần môi trường riêng không được coi là đã chạy. |
| Frontend toàn bộ (`npm test`) | 602 passed, 0 failed. |
| MySQL riêng (`node scripts/ops/run_lifecycle_checks.cjs --test`) | 10 passed, 0 failed; database tạm dùng một lần; không gọi AI. |
| Build backend test project | Thành công, 0 warning / 0 error. |
| TypeScript + Vite production build | Thành công; còn cảnh báo kích thước bundle lớn vốn có. |
| Docker API/web | Build và khởi động lại localhost thành công. |

Lượt MySQL đầu có một lỗi **fixture test**: sau khi Center Manager mở lại lớp, test chưa đổi lại sang Teacher khi gọi API-usecase chỉ dành cho giáo viên. Đã sửa phiên tác nhân trong test; lượt chạy lại cả 10 test đều qua. Không nới kiểm tra quyền.

## Kiểm tra Chrome bằng Teacher Math

1. Toán 12 hiển thị “1 lớp đang áp dụng”; lớp 12 đã được chọn; không còn khung “Lớp Học Áp Dụng (0)”.
2. Mặc định không hiện lớp 10/11. Bật ngoại lệ mới hiện hai lớp này.
3. Thử chọn lớp 11: nút Lưu bị khóa nếu thiếu lý do; nhập lý do kiểm thử thì nút được mở. Không bấm Lưu.
4. Ẩn danh sách ngoại lệ không xóa lựa chọn đang sửa, có thông báo rõ. Bấm “Hủy lựa chọn chưa lưu”, trở lại một lớp 12; nút Lưu khóa vì không có thay đổi.
5. Danh sách giáo trình Toán 12 cũng báo 1 lớp đang áp dụng. Thống kê Published có nhãn “Đã xuất bản”.
6. Mở Draft Toán 11: chỉ có “Lớp dự kiến áp dụng (0)”, chỉ hiện lớp cùng khối 11; không sửa hoặc xuất bản.
7. Trả tab về giáo trình Toán 12 và giữ mở để người dùng xem.

Ảnh bằng chứng trong `docs/verification/evidence/curriculum-application/`:

- `before-published.jpg`: giao diện cũ trước cập nhật.
- `published-default.jpg`: một khung áp dụng, một lớp 12 đang chọn.
- `list-live-count.jpg`: danh sách cũng báo 1 lớp đang áp dụng.

## Bảo toàn dữ liệu

Có backup riêng, git-ignored: `storage/backups/2026-10-09-before-curriculum-ui.sql` và metadata `.json`. SHA-256 bản SQL: `c8e6b976fea8ed9f0bfe44b92f5e3ddfaba86662b5cf286937946c39e6f79cce`.

Đã so sánh dấu vân tay 19 bảng được bảo vệ ngay sau cập nhật localhost: `protectedHistoryUnchanged: true`, `changedTables: []`.

Lượt so sánh cuối sau kiểm tra Chrome: 18 bảng giữ nguyên; chỉ bảng `users` có thay đổi. So sánh từng cột với backup xác nhận đúng **một tài khoản** chỉ đổi `last_login_at`, `updated_at`, `row_version` do đăng nhập; không đổi vai trò, trạng thái, thông tin tài khoản hay mật khẩu (`onlyLoginMetadataChanged: true`, 18 tài khoản giữ đủ).

204 bài làm, 204 phân tích lập luận, 362 bản ghi cập nhật Twin, 5 bản ghi áp dụng giáo trình và mọi cấu hình học thuật được bảo vệ đều giữ nguyên. Cấu hình Toán 12 do người dùng đã lưu được giữ nguyên. Kiểm tra UI không có thao tác lưu thay đổi thật.
