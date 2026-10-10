# Báo cáo sửa ràng buộc giáo trình, đồ thị tri thức và báo cáo lớp

Ngày: 09/10/2026. Phạm vi: yêu cầu sửa được người dùng đồng ý sau lần kiểm tra lỗi Manager không mở được báo cáo lớp và các phụ thuộc học thuật.

## Kết quả nghiệp vụ

| Thao tác | Điều kiện và hành vi mới |
| --- | --- |
| Manager mở Báo cáo lớp | Route riêng trong workspace Manager, kiểm tra `organization.classes.read` và `organization.students.read`. Chỉ xem, không mở chức năng giao bài/chấm bài của giáo viên. Lớp đã lưu trữ vẫn xem được. |
| Lưu trữ giáo trình | Chặn nếu còn lớp Active/Current áp dụng, bao gồm lớp của giáo viên khác dùng giáo trình Shared. Chủ sở hữu cần nhập lý do; kiểm tra RowVersion. Không tự ngừng giáo trình của lớp đang học. |
| Giáo trình chỉ còn lớp lưu trữ | Cho phép lưu trữ, kết thúc các lượt áp dụng còn mở với người thực hiện, thời điểm và lý do. Giữ các nút và dữ liệu bài làm/năng lực. |
| Sửa tên, nội dung, nút cha | Chặn khi nút thuộc giáo trình Published/Archived. Tạo mã nút mới và giáo trình nháp mới khi đổi ý nghĩa; không ghi đè lịch sử. Clone giáo trình vẫn dùng lại nút cũ, không tự tạo phiên bản nội dung mới cho từng nút. |
| Sửa thông số đồ thị | Thứ tự, độ quan trọng thi và thời gian học vẫn được sửa, có nhật ký trước/sau. Đây là tham số của đồ thị dùng chung, không phải một bản chụp lịch sử bất biến. |
| Tắt hoạt động nút | Chặn nếu đang được lớp hoạt động dùng, còn con hoạt động, câu hỏi Active hoặc lộ trình Active trong phạm vi học hiện tại. Không dùng tắt hoạt động để vượt ràng buộc xóa. |
| Xóa nút | Giữ kiểm tra con, liên kết, giáo trình, câu hỏi, Twin, lịch sử Twin, lộ trình, khuyến nghị, RootCauseNodeIds. Xóa mềm khi không còn phụ thuộc. |
| Tạo/khôi phục liên kết | Hai đầu phải hoạt động, cùng môn/trung tâm; kiểm tra trùng và DAG. Chặn nếu một đầu nằm trong giáo trình đang được lớp hoạt động dùng. |
| Đổi trọng số/xóa liên kết | Chặn khi các đầu còn được lớp hoạt động dùng. Khi không còn sử dụng trực tiếp bởi lớp đang học, có thể sửa/xóa mềm và ghi nhật ký. |
| Tạo lại mã nút đã xóa mềm | Không được dùng luồng tạo/khôi phục để ghi đè nội dung giáo trình Published/Archived hoặc tạo vòng lặp nút cha. |

Tất cả điều kiện được kiểm tra ở server; checkbox/nút bị khóa không thay thế kiểm tra quyền và phụ thuộc.
Lớp lịch sử được nhận diện bằng trạng thái/phạm vi và lịch sử áp dụng, không suy luận từ tên lớp. Một lượt áp dụng đã kết thúc không được khôi phục ngầm từ liên kết kế hoạch `curriculum_classes` cũ.

## Database, nhật ký và tranh chấp

- Migration `20261009103000_HardenAcademicLifecycleDependencies` chỉ tạo bốn trigger, không phân loại lại lớp và không xóa/cập nhật dữ liệu học tập.
- Chặn ghi trực tiếp để lưu trữ giáo trình còn lớp hoạt động, thay nội dung nút thuộc giáo trình Published/Archived, tắt/xóa nút còn phụ thuộc, hoặc tạo/sửa liên kết vi phạm điều kiện sử dụng.
- Việc phát hiện DAG ở server được tuần tự hóa theo môn học bằng một transaction ngắn và khóa hàng môn học. Kiểm thử hai yêu cầu ngược chiều chạy đồng thời chỉ cho một yêu cầu thành công; yêu cầu còn lại nhận lỗi chu trình.
- Thao tác hợp lệ ghi người thực hiện, thời điểm, dữ liệu trước/sau và lý do vào `authorization_audit_logs` trong cùng transaction lưu dữ liệu. Thao tác bị chặn không sinh nhật ký giả về một thay đổi đã xảy ra.
- Mã định danh nhật ký của đối tượng mới có ID tự tăng sử dụng định danh logic: `subjectId:nodeCode` cho nút, `source:target:relationType` cho liên kết; cập nhật/xóa dùng ID đã tồn tại.
- Lỗi chốt database được chuyển thành lỗi nghiệp vụ 409; giao diện không nhầm mọi lỗi 409 thành “phiên khác đã sửa”. Chỉ hiển thị detail của các mã lỗi nghiệp vụ đã kiểm soát, không hiển thị lỗi nội bộ.

## Kiểm chứng

- Backend toàn bộ: 3.991 passed, 79 skipped (các bộ tích hợp cần môi trường riêng), tổng 4.070; không lỗi. Các test MySQL mới chạy riêng ở dưới, không coi skipped là passed.
- Frontend: 618 passed, không lỗi; bổ sung 6 test bảo vệ route, quyền, chỉ xem, dialog lưu trữ và thông báo phụ thuộc.
- Hai test MySQL mới passed trên mã cuối: trigger và mutation có nhật ký (cả tạo/xóa nút độc lập); cập nhật liên kết/nút cha đồng thời không tạo chu trình. Mỗi test dùng schema tạm riêng rồi dọn schema đó.
- Hồi quy bổ sung: 11 passed, gồm 3 test MySQL về vòng đời lớp/phạm vi/lịch sử áp dụng và 8 test workspace summary.
- Frontend build và backend build thành công. Có cảnh báo kích thước chunk MathLive đã tồn tại, không phải lỗi build.
- ESLint cho ba thành phần mới (báo cáo Manager, dialog lưu trữ, mapper lỗi) thành công.
- Ba kiểm thử bundle budget passed trên build cuối; trang mới được tải theo route, không kéo các chức năng giáo viên vào trang Manager.
- `git diff --check` thành công.
- Migration đã áp dụng vào MySQL cục bộ. API/web đang chạy đúng image mới nhất, MySQL healthy. 19/19 bảng được bảo vệ có số lượng và SHA-256 không đổi; không có dữ liệu học tập mồ côi theo kiểm tra runtime.
- Bộ kiểm thử tập trung giáo trình/đồ thị trên mã cuối: 838 passed.
- HTML build Windows và Docker/Linux khác checksum và tên asset; không coi đây là bằng chứng bản chạy cũ. Container đã dựng lại từ source cuối và xác minh có bundle báo cáo Manager mới.
- Kiểm tra trực tiếp bằng skill computer-use: tab Chrome kết nối đang là student01; đã yêu cầu đăng nhập Center Manager. Chưa ghi nhận kiểm tra UI thành công cho Manager hoặc dialog Teacher ở thời điểm lập báo cáo.

## Sao lưu và bảo toàn dữ liệu

Đã sao lưu trước migration tại thư mục `storage/backups` được gitignore. Không đưa bản dump hay dữ liệu cá nhân vào commit.

- Bản dump: `2026-10-09-before-academic-lifecycle-guards.sql`.
- Dấu vân tay 19 bảng: `2026-10-09-before-academic-lifecycle-guards.json`.
- SHA-256 bản dump: `b25380dd00ea84fb8df400e161c8d0ffee46e655a9f8823fe3ef2402c2e3846e`.
- Trước sửa: 204 attempts, 204 analyses, 362 lịch sử cập nhật Twin, 4 bài tập, 7 giáo trình, 84 liên kết giáo trình–nút, 96 nút, 7 lượt áp dụng giáo trình.
- Sau triển khai: 19/19 bảng có fingerprint không đổi. So sánh riêng dữ liệu `classes` trong dump trước/sau cũng khớp hoàn toàn; không thay trạng thái hoặc thông tin lớp thật.

Không chỉnh trạng thái lớp thật, không lưu trữ giáo trình thật, không làm lại/nộp lại bài để kiểm tra. Không sửa `.env`, key AI, hàng đợi/microbatch/timeout/chấm điểm Gemini trong lượt này. Không thực hiện benchmark 50 câu mới; không khẳng định thời gian chấm tuyệt đối không đổi.

## Giới hạn và các phần không tự mở rộng

- Đây chưa phải hệ thống version/snapshot đầy đủ cho cả đồ thị. Nội dung nút lịch sử được bảo vệ, nhưng thông số và liên kết của đồ thị chung có thể thay đổi sau khi hết phạm vi lớp đang học; nhật ký ghi nhận thay đổi đó.
- Chưa có giao diện nhật ký học thuật tổng hợp hoặc thông báo qua lại giữa các tác nhân; kế hoạch tính năng đó vẫn là công việc riêng đã hoãn.
- Không thay quyền để đưa Manager vào workspace giáo viên. Không sửa các redirect học thuật cũ không liên quan nút Báo cáo lớp này.
- Không cam kết mọi invariant đều được ép ở database: kiểm tra quyền, DAG, câu trả lời/RootCause JSON bất thường vẫn cần server. Trigger bổ sung bảo vệ các thao tác vòng đời cụ thể của lượt này.
- Worktree chứa nhiều sửa đổi từ những lượt trước. Danh sách dưới đây chỉ là 40 tệp chạm trong riêng lượt này, không phải toàn bộ diff so với HEAD. Chưa commit/push.

## Danh sách tệp trong lượt này (40 tệp)

### API và hợp đồng (4)

- `src/EduTwin.API/Controllers/CurriculumsController.cs`
- `src/EduTwin.API/Controllers/KnowledgeNodesController.cs`
- `src/EduTwin.API/Controllers/KnowledgeEdgesController.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/ArchiveCurriculumRequest.cs`

### Nghiệp vụ giáo trình (4)

- `src/EduTwin.BLL/CurriculumAndQuestions/AcademicDependencyGuards.cs` (mới)
- `src/EduTwin.BLL/CurriculumAndQuestions/ArchiveCurriculumResult.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/ArchiveCurriculumUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/CurriculumApplicationUseCase.cs`

### Nghiệp vụ đồ thị (13)

- `src/EduTwin.BLL/KnowledgeGraph/GraphMutationTransaction.cs` (mới)
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeNodeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeNodeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeNodeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeNodeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeNodeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeNodeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeEdgeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeEdgeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeEdgeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeEdgeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeEdgeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeEdgeUseCase.cs`

### Migration và vận hành (2)

- `src/EduTwin.DAL/Persistence/Migrations/20261009103000_HardenAcademicLifecycleDependencies.cs` (mới)
- `scripts/ops/run_lifecycle_checks.cjs`

### Giao diện (11)

- `web/edutwin-web/src/App.tsx`
- `web/edutwin-web/src/api/curriculumApi.ts`
- `web/edutwin-web/src/layouts/CenterManagerLayout.tsx`
- `web/edutwin-web/src/pages/classListHelpers.ts`
- `web/edutwin-web/src/pages/CenterClassReportPage.tsx` (mới)
- `web/edutwin-web/src/components/teacher/CurriculumArchiveDialog.tsx` (mới)
- `web/edutwin-web/src/pages/teacher/TeacherCurriculumListView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherCurriculumEditorView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx`
- `web/edutwin-web/src/utils/academicLifecycleError.ts` (mới)
- `web/edutwin-web/src/types/curriculum.ts`

### Kiểm thử và báo cáo (6)

- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/AcademicLifecycleDependencyTests.cs` (mới)
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/ArchiveCurriculumUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/KnowledgeGraph/CreateKnowledgeEdgeUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs`
- `web/edutwin-web/tests/academicLifecycleDependencies.test.ts` (mới)
- `docs/verification/ACADEMIC-LIFECYCLE-DEPENDENCIES-2026-10-09.md` (mới)
